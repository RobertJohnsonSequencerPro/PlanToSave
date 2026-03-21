using Microsoft.EntityFrameworkCore;
using PlanToSave.Application.Accounts;
using PlanToSave.Domain.Entities;
using PlanToSave.Domain.Enums;
using PlanToSave.Web.Data;

namespace PlanToSave.Web.Services;

public class AccountService(ApplicationDbContext db) : IAccountService
{
    public async Task<List<AccountDto>> GetAccountsAsync(string userId, bool includeArchived = false)
    {
        var query = db.Accounts.Where(a => a.UserId == userId);
        if (!includeArchived)
            query = query.Where(a => !a.IsArchived);

        var accounts = await query
            .OrderBy(a => a.Type)
            .ThenBy(a => a.Name)
            .ToListAsync();

        return accounts.Select(ToDto).ToList();
    }

    public async Task<AccountDto?> GetByIdAsync(Guid id, string userId)
    {
        var account = await db.Accounts
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
        return account is null ? null : ToDto(account);
    }

    public async Task<Guid> CreateAsync(string userId, CreateAccountDto dto)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = dto.Name.Trim(),
            Type = dto.Type,
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
            OpeningBalance = dto.OpeningBalance,
            IsArchived = false,
            CreatedAt = DateTime.UtcNow
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        return account.Id;
    }

    public async Task UpdateAsync(Guid id, string userId, UpdateAccountDto dto)
    {
        var account = await db.Accounts
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId)
            ?? throw new InvalidOperationException("Account not found.");

        account.Name = dto.Name.Trim();
        account.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        account.OpeningBalance = dto.OpeningBalance;
        await db.SaveChangesAsync();
    }

    public async Task ArchiveAsync(Guid id, string userId)
    {
        var account = await db.Accounts
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId)
            ?? throw new InvalidOperationException("Account not found.");

        account.IsArchived = true;
        await db.SaveChangesAsync();
    }

    public async Task<List<AccountBalanceDto>> GetBalancesAsync(string userId)
    {
        var accounts = await db.Accounts
            .Where(a => a.UserId == userId && !a.IsArchived &&
                        (a.Type == AccountType.Checking || a.Type == AccountType.Savings ||
                         a.Type == AccountType.Credit   || a.Type == AccountType.Investment))
            .OrderBy(a => a.Type)
            .ThenBy(a => a.Name)
            .ToListAsync();

        if (accounts.Count == 0) return [];

        var accountIds = accounts.Select(a => a.Id).ToList();

        // Latest snapshot per account (load all, process in memory — avoids N+1)
        var allSnapshots = await db.BalanceSnapshots
            .Where(s => s.UserId == userId && accountIds.Contains(s.AccountId))
            .Select(s => new { s.AccountId, s.Amount, s.EffectiveDate })
            .ToListAsync();

        var latestSnapshot = allSnapshots
            .GroupBy(s => s.AccountId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(s => s.EffectiveDate).First());

        // All flows for these accounts (one query, process in memory per account+cutoff)
        var allFlows = await db.ActualFlows
            .Where(f => f.UserId == userId &&
                        (accountIds.Contains(f.ToAccountId) || accountIds.Contains(f.FromAccountId)))
            .Select(f => new { f.FromAccountId, f.ToAccountId, f.Amount, f.Date })
            .ToListAsync();

        // Interest rules (one rule per account)
        var interestRules = await db.InterestRules
            .Where(r => r.UserId == userId && accountIds.Contains(r.AccountId))
            .ToDictionaryAsync(r => r.AccountId);

        var today = DateOnly.FromDateTime(DateTime.Today);

        return accounts.Select(a =>
        {
            var snap = latestSnapshot.GetValueOrDefault(a.Id);
            var cutoff = snap?.EffectiveDate;           // flows strictly AFTER this date count
            var baseBalance = snap?.Amount ?? a.OpeningBalance;

            var inflows  = allFlows
                .Where(f => f.ToAccountId   == a.Id && (cutoff is null || f.Date > cutoff))
                .Sum(f => f.Amount);
            var outflows = allFlows
                .Where(f => f.FromAccountId == a.Id && (cutoff is null || f.Date > cutoff))
                .Sum(f => f.Amount);

            var balance = baseBalance + inflows - outflows;

            // Accrued interest since the rule's effective date (or the baseline cutoff, whichever is later)
            decimal accruedInterest = 0m;
            decimal? annualRatePct = null;
            if (interestRules.TryGetValue(a.Id, out var rule))
            {
                annualRatePct = rule.AnnualRatePct;
                var accrualFrom = rule.EffectiveDate;
                if (cutoff.HasValue && cutoff.Value > accrualFrom)
                    accrualFrom = cutoff.Value;
                accruedInterest = ComputeAccruedInterest(balance, rule.AnnualRatePct, rule.Frequency, accrualFrom, today);
            }

            return new AccountBalanceDto(a.Id, a.Name, a.Type, balance, cutoff, accruedInterest, annualRatePct);
        }).ToList();
    }

    public async Task SetSnapshotAsync(string userId, Guid accountId, decimal amount,
        DateOnly effectiveDate, string? note)
    {
        var account = await db.Accounts
            .FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId)
            ?? throw new InvalidOperationException("Account not found.");

        // Replace any existing snapshot on the exact same date (idempotent reconcile)
        var existing = await db.BalanceSnapshots
            .FirstOrDefaultAsync(s => s.AccountId == accountId
                                   && s.UserId == userId
                                   && s.EffectiveDate == effectiveDate);
        if (existing is not null)
        {
            existing.Amount = amount;
            existing.Note = note;
        }
        else
        {
            db.BalanceSnapshots.Add(new Domain.Entities.BalanceSnapshot
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                AccountId = accountId,
                Amount = amount,
                EffectiveDate = effectiveDate,
                Note = note,
                CreatedAt = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();
    }

    public async Task<List<BalanceSnapshotDto>> GetSnapshotsAsync(string userId, Guid accountId)
    {
        return await db.BalanceSnapshots
            .Where(s => s.UserId == userId && s.AccountId == accountId)
            .OrderByDescending(s => s.EffectiveDate)
            .Select(s => new BalanceSnapshotDto(s.Id, s.Amount, s.EffectiveDate, s.Note, s.CreatedAt))
            .ToListAsync();
    }

    private static AccountDto ToDto(Account a) =>
        new(a.Id, a.Name, a.Type, a.Description, a.OpeningBalance, a.IsArchived, a.IsStockAccount);

    public async Task<InterestRuleDto?> GetInterestRuleAsync(string userId, Guid accountId)
    {
        var rule = await db.InterestRules
            .FirstOrDefaultAsync(r => r.UserId == userId && r.AccountId == accountId);
        return rule is null ? null
            : new InterestRuleDto(rule.Id, rule.AnnualRatePct, rule.Frequency, rule.EffectiveDate, rule.CreatedAt);
    }

    public async Task SetInterestRuleAsync(string userId, Guid accountId, SetInterestRuleDto dto)
    {
        _ = await db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId)
            ?? throw new InvalidOperationException("Account not found.");

        var existing = await db.InterestRules
            .FirstOrDefaultAsync(r => r.UserId == userId && r.AccountId == accountId);

        if (existing is not null)
        {
            existing.AnnualRatePct = dto.AnnualRatePct;
            existing.Frequency = dto.Frequency;
            existing.EffectiveDate = dto.EffectiveDate;
        }
        else
        {
            db.InterestRules.Add(new InterestRule
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                AccountId = accountId,
                AnnualRatePct = dto.AnnualRatePct,
                Frequency = dto.Frequency,
                EffectiveDate = dto.EffectiveDate,
                CreatedAt = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();
    }

    public async Task DeleteInterestRuleAsync(string userId, Guid accountId)
    {
        var rule = await db.InterestRules
            .FirstOrDefaultAsync(r => r.UserId == userId && r.AccountId == accountId);
        if (rule is not null)
        {
            db.InterestRules.Remove(rule);
            await db.SaveChangesAsync();
        }
    }

    private static decimal ComputeAccruedInterest(
        decimal balance, decimal annualRatePct, CompoundingFrequency frequency,
        DateOnly fromDate, DateOnly toDate)
    {
        var days = toDate.DayNumber - fromDate.DayNumber;
        if (days <= 0 || balance == 0 || annualRatePct == 0) return 0m;

        var r = (double)(annualRatePct / 100m);
        var t = days / 365.0;
        var n = frequency switch
        {
            CompoundingFrequency.Daily    => 365.0,
            CompoundingFrequency.Monthly  => 12.0,
            CompoundingFrequency.Annually => 1.0,
            _                             => 12.0
        };
        var factor = Math.Pow(1.0 + r / n, n * t) - 1.0;
        return Math.Round((decimal)factor * balance, 2);
    }
}
