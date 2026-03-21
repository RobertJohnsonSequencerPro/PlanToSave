using Microsoft.EntityFrameworkCore;
using PlanToSave.Application.Flows;
using PlanToSave.Domain.Entities;
using PlanToSave.Domain.Enums;
using PlanToSave.Web.Data;

namespace PlanToSave.Web.Services;

public class ActualFlowService(ApplicationDbContext db) : IActualFlowService
{
    public async Task<List<ActualFlowDto>> GetFlowsAsync(string userId, FlowFilterDto? filter = null)
    {
        var query = db.ActualFlows
            .Include(f => f.FromAccount)
            .Include(f => f.ToAccount)
            .Where(f => f.UserId == userId);

        if (filter?.From is not null)
            query = query.Where(f => f.Date >= filter.From);
        if (filter?.To is not null)
            query = query.Where(f => f.Date <= filter.To);
        if (filter?.AccountId is not null)
            query = query.Where(f => f.FromAccountId == filter.AccountId
                                  || f.ToAccountId == filter.AccountId);

        var flows = await query
            .OrderByDescending(f => f.Date)
            .ThenByDescending(f => f.CreatedAt)
            .ToListAsync();

        return flows.Select(ToDto).ToList();
    }

    public async Task<List<AccountOptionDto>> GetFromAccountOptionsAsync(string userId)
    {
        // Income and Stock accounts can be the source of a flow
        var validTypes = new[] { AccountType.Income, AccountType.Checking, AccountType.Savings,
                                  AccountType.Credit, AccountType.Investment };
        return await db.Accounts
            .Where(a => a.UserId == userId && !a.IsArchived && validTypes.Contains(a.Type))
            .OrderBy(a => a.Type).ThenBy(a => a.Name)
            .Select(a => new AccountOptionDto(a.Id, a.Name, a.Type))
            .ToListAsync();
    }

    public async Task<List<AccountOptionDto>> GetToAccountOptionsAsync(string userId)
    {
        // Expense and Stock accounts can be the destination of a flow
        var validTypes = new[] { AccountType.Expense, AccountType.Checking, AccountType.Savings,
                                  AccountType.Credit, AccountType.Investment };
        return await db.Accounts
            .Where(a => a.UserId == userId && !a.IsArchived && validTypes.Contains(a.Type))
            .OrderBy(a => a.Type).ThenBy(a => a.Name)
            .Select(a => new AccountOptionDto(a.Id, a.Name, a.Type))
            .ToListAsync();
    }

    public async Task<Guid> CreateAsync(string userId, CreateActualFlowDto dto)
    {
        // Verify both accounts belong to the user
        var fromAccount = await db.Accounts
            .FirstOrDefaultAsync(a => a.Id == dto.FromAccountId && a.UserId == userId)
            ?? throw new InvalidOperationException("From account not found.");

        var toAccount = await db.Accounts
            .FirstOrDefaultAsync(a => a.Id == dto.ToAccountId && a.UserId == userId)
            ?? throw new InvalidOperationException("To account not found.");

        // Enforce flow direction rules
        if (fromAccount.Type == AccountType.Expense)
            throw new InvalidOperationException("Expense accounts cannot be the source of a flow.");
        if (toAccount.Type == AccountType.Income)
            throw new InvalidOperationException("Income accounts cannot be the destination of a flow.");
        if (dto.FromAccountId == dto.ToAccountId)
            throw new InvalidOperationException("From and To accounts must be different.");

        var flow = new ActualFlow
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FromAccountId = dto.FromAccountId,
            ToAccountId = dto.ToAccountId,
            Amount = dto.Amount,
            Date = dto.Date,
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        db.ActualFlows.Add(flow);
        await db.SaveChangesAsync();
        return flow.Id;
    }

    public async Task DeleteAsync(Guid id, string userId)
    {
        var flow = await db.ActualFlows
            .FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId)
            ?? throw new InvalidOperationException("Flow not found.");

        db.ActualFlows.Remove(flow);
        await db.SaveChangesAsync();
    }

    public async Task<(int Imported, List<string> Errors)> BulkImportAsync(
        string userId, List<BulkImportRowDto> rows)
    {
        var errors = new List<string>();

        // Validate all referenced accounts in a single query
        var allAccountIds = rows
            .SelectMany(r => new[] { r.FromAccountId, r.ToAccountId })
            .Distinct()
            .ToList();

        var validAccounts = await db.Accounts
            .Where(a => a.UserId == userId && allAccountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a);

        var toInsert = new List<ActualFlow>(rows.Count);

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var rowLabel = $"Row {i + 1} ({row.Date:yyyy-MM-dd}): ";

            if (!validAccounts.TryGetValue(row.FromAccountId, out var fromAcc))
            { errors.Add(rowLabel + "From account not found."); continue; }

            if (!validAccounts.TryGetValue(row.ToAccountId, out var toAcc))
            { errors.Add(rowLabel + "To account not found."); continue; }

            if (fromAcc.Type == AccountType.Expense)
            { errors.Add(rowLabel + "Expense accounts cannot be a flow source."); continue; }

            if (toAcc.Type == AccountType.Income)
            { errors.Add(rowLabel + "Income accounts cannot be a flow destination."); continue; }

            if (row.FromAccountId == row.ToAccountId)
            { errors.Add(rowLabel + "From and To accounts must be different."); continue; }

            toInsert.Add(new ActualFlow
            {
                Id            = Guid.NewGuid(),
                UserId        = userId,
                FromAccountId = row.FromAccountId,
                ToAccountId   = row.ToAccountId,
                Amount        = row.Amount,
                Date          = row.Date,
                Description   = string.IsNullOrWhiteSpace(row.Description)
                                    ? null : row.Description.Trim(),
                CreatedAt     = DateTime.UtcNow
            });
        }

        if (toInsert.Count > 0)
        {
            db.ActualFlows.AddRange(toInsert);
            await db.SaveChangesAsync();
        }

        return (toInsert.Count, errors);
    }

    private static ActualFlowDto ToDto(ActualFlow f) => new(
        f.Id,
        f.FromAccountId, f.FromAccount.Name, f.FromAccount.Type,
        f.ToAccountId,   f.ToAccount.Name,   f.ToAccount.Type,
        f.Amount, f.Date, f.Description, f.CreatedAt);
}
