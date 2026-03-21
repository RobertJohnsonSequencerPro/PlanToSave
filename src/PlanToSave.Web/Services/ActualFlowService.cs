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
                                    ? null
                                    : row.Description.Trim() is { Length: > 300 } d
                                        ? d[..297] + "…"
                                        : row.Description.Trim(),
                CreatedAt     = DateTime.UtcNow
            });
        }

        if (toInsert.Count > 0)
        {
            db.ActualFlows.AddRange(toInsert);
            try
            {
                await db.SaveChangesAsync();
            }
            catch
            {
                // Clear the change tracker so a failed import doesn't poison
                // the shared DbContext for subsequent operations in this circuit.
                db.ChangeTracker.Clear();
                throw;
            }
        }

        return (toInsert.Count, errors);
    }

    public async Task<HashSet<int>> FindPotentialDuplicatesAsync(
        string userId, IReadOnlyList<BulkImportRowDto> candidates)
    {
        if (candidates.Count == 0) return [];

        // Filter existing flows by the dates present in the candidates for efficiency.
        var dates = candidates.Select(r => r.Date).Distinct().ToList();

        var existingSet = (await db.ActualFlows
            .Where(f => f.UserId == userId && dates.Contains(f.Date))
            .Select(f => new { f.Date, f.Amount, f.FromAccountId, f.ToAccountId })
            .ToListAsync())
            .Select(f => (f.Date, f.Amount, f.FromAccountId, f.ToAccountId))
            .ToHashSet();

        if (existingSet.Count == 0) return [];

        var duplicateIndices = new HashSet<int>();
        for (int i = 0; i < candidates.Count; i++)
        {
            var r = candidates[i];
            if (existingSet.Contains((r.Date, r.Amount, r.FromAccountId, r.ToAccountId)))
                duplicateIndices.Add(i);
        }
        return duplicateIndices;
    }

    private static ActualFlowDto ToDto(ActualFlow f) => new(
        f.Id,
        f.FromAccountId, f.FromAccount.Name, f.FromAccount.Type,
        f.ToAccountId,   f.ToAccount.Name,   f.ToAccount.Type,
        f.Amount, f.Date, f.Description, f.CreatedAt);

    // ── Description-based account suggestions ────────────────────────────────

    public async Task<(Dictionary<string, Guid> Deposits, Dictionary<string, Guid> Withdrawals)>
        SuggestCounterAccountsAsync(string userId, IReadOnlyList<string?> descriptions)
    {
        // Load history once — project only what we need
        var history = await db.ActualFlows
            .Where(f => f.UserId == userId && f.Description != null)
            .Select(f => new
            {
                f.Description,
                f.FromAccountId,
                FromType = f.FromAccount.Type,
                f.ToAccountId,
                ToType = f.ToAccount.Type,
            })
            .ToListAsync();

        // Build frequency maps: normalizedKey → (accountId → count)
        var depositCounts     = new Dictionary<string, Dictionary<Guid, int>>();
        var withdrawalCounts  = new Dictionary<string, Dictionary<Guid, int>>();

        foreach (var h in history)
        {
            var key = NormalizeDescription(h.Description);
            if (key.Length == 0) continue;

            // Deposit counter = FromAccount when it is an Income source
            if (h.FromType == AccountType.Income)
                Increment(depositCounts, key, h.FromAccountId);

            // Withdrawal counter = ToAccount when it is a spending category (Expense)
            if (h.ToType == AccountType.Expense)
                Increment(withdrawalCounts, key, h.ToAccountId);
        }

        var depositSuggestions    = new Dictionary<string, Guid>();
        var withdrawalSuggestions = new Dictionary<string, Guid>();

        foreach (var raw in descriptions)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var key = NormalizeDescription(raw);
            if (key.Length == 0) continue;

            if (depositCounts.TryGetValue(key, out var dCounts))
                depositSuggestions[raw] = BestAccount(dCounts);

            if (withdrawalCounts.TryGetValue(key, out var wCounts))
                withdrawalSuggestions[raw] = BestAccount(wCounts);
        }

        return (depositSuggestions, withdrawalSuggestions);
    }

    /// <summary>Strips bank-statement noise to extract the core merchant/payee token(s).</summary>
    internal static string NormalizeDescription(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";

        var s = raw.ToUpperInvariant().Trim();

        // Strip common prefixes that add no merchant identity
        string[] prefixes =
        [
            "POS DEBIT ", "POS CREDIT ", "ACH DEBIT ", "ACH CREDIT ",
            "ONLINE PAYMENT TO ", "ONLINE PAYMENT ", "DIRECT DEPOSIT ",
            "DIRECT DEBIT ", "CHECKCARD ", "PURCHASE ", "RECURRING PAYMENT ",
            "DEBIT CARD PURCHASE ", "EXTERNAL TRANSFER TO ", "EXTERNAL TRANSFER ",
            "BILL PAYMENT TO ", "BILL PAYMENT ",
        ];
        foreach (var p in prefixes)
            if (s.StartsWith(p)) { s = s[p.Length..].TrimStart(); break; }

        // Split and keep only tokens that contain at least one letter
        // (filters out reference numbers, dates like 03/21, phone numbers, etc.)
        var tokens = s
            .Split([' ', '\t', '*', '/'], StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Any(char.IsLetter))
            .Take(3)
            .ToArray();

        return tokens.Length > 0 ? string.Join(" ", tokens) : s[..Math.Min(15, s.Length)];
    }

    private static void Increment(Dictionary<string, Dictionary<Guid, int>> map, string key, Guid accountId)
    {
        if (!map.TryGetValue(key, out var counts))
            map[key] = counts = new Dictionary<Guid, int>();
        counts.TryGetValue(accountId, out var c);
        counts[accountId] = c + 1;
    }

    private static Guid BestAccount(Dictionary<Guid, int> counts) =>
        counts.MaxBy(kv => kv.Value).Key;
}
