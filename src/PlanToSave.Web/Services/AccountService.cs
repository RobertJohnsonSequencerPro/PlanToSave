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

        // Compute inflows and outflows in two batched queries instead of N+1
        var inflows = await db.ActualFlows
            .Where(f => f.UserId == userId && accountIds.Contains(f.ToAccountId))
            .GroupBy(f => f.ToAccountId)
            .Select(g => new { AccountId = g.Key, Total = g.Sum(f => f.Amount) })
            .ToDictionaryAsync(x => x.AccountId, x => x.Total);

        var outflows = await db.ActualFlows
            .Where(f => f.UserId == userId && accountIds.Contains(f.FromAccountId))
            .GroupBy(f => f.FromAccountId)
            .Select(g => new { AccountId = g.Key, Total = g.Sum(f => f.Amount) })
            .ToDictionaryAsync(x => x.AccountId, x => x.Total);

        return accounts.Select(a => new AccountBalanceDto(
            a.Id,
            a.Name,
            a.Type,
            inflows.GetValueOrDefault(a.Id) - outflows.GetValueOrDefault(a.Id)
        )).ToList();
    }

    private static AccountDto ToDto(Account a) =>
        new(a.Id, a.Name, a.Type, a.Description, a.IsArchived, a.IsStockAccount);
}
