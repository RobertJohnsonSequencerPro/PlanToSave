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

            return new AccountBalanceDto(a.Id, a.Name, a.Type,
                baseBalance + inflows - outflows, cutoff);
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
}
