using PlanToSave.Application.Flows;
using PlanToSave.Domain.Entities;
using PlanToSave.Domain.Enums;
using PlanToSave.Tests.Helpers;
using PlanToSave.Web.Services;

namespace PlanToSave.Tests.Services;

public class ActualFlowServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────────

    private static (Account income, Account checking, Account expense) SeedAccounts(
        PlanToSave.Web.Data.ApplicationDbContext db, string userId)
    {
        var income = new Account
        {
            Id = Guid.NewGuid(), UserId = userId, Name = "Salary",
            Type = AccountType.Income
        };
        var checking = new Account
        {
            Id = Guid.NewGuid(), UserId = userId, Name = "Checking",
            Type = AccountType.Checking
        };
        var expense = new Account
        {
            Id = Guid.NewGuid(), UserId = userId, Name = "Groceries",
            Type = AccountType.Expense
        };
        db.Accounts.AddRange(income, checking, expense);
        return (income, checking, expense);
    }

    private static ActualFlow MakeFlow(string userId, Guid fromId, Guid toId,
        decimal amount, DateOnly date, string? description = null) => new()
    {
        Id            = Guid.NewGuid(),
        UserId        = userId,
        FromAccountId = fromId,
        ToAccountId   = toId,
        Amount        = amount,
        Date          = date,
        Description   = description,
        CreatedAt     = DateTime.UtcNow
    };

    // ── FindPotentialDuplicatesAsync ──────────────────────────────────

    [Fact]
    public async Task FindPotentialDuplicatesAsync_ReturnsEmpty_WhenNoCandidates()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new ActualFlowService(db);

        var result = await svc.FindPotentialDuplicatesAsync("user-1", []);

        Assert.Empty(result);
    }

    [Fact]
    public async Task FindPotentialDuplicatesAsync_ReturnsEmpty_WhenNoExistingFlows()
    {
        await using var db = TestDbContextFactory.Create();
        var (income, checking, _) = SeedAccounts(db, "user-1");
        await db.SaveChangesAsync();

        var svc = new ActualFlowService(db);

        var candidates = new List<BulkImportRowDto>
        {
            new(new DateOnly(2025, 1, 10), 500m, "Payroll", income.Id, checking.Id)
        };

        var result = await svc.FindPotentialDuplicatesAsync("user-1", candidates);

        Assert.Empty(result);
    }

    [Fact]
    public async Task FindPotentialDuplicatesAsync_ReturnsIndex_WhenExactMatch()
    {
        await using var db = TestDbContextFactory.Create();
        var (income, checking, _) = SeedAccounts(db, "user-1");
        var date = new DateOnly(2025, 1, 10);
        db.ActualFlows.Add(MakeFlow("user-1", income.Id, checking.Id, 500m, date, "Payroll"));
        await db.SaveChangesAsync();

        var svc = new ActualFlowService(db);

        var candidates = new List<BulkImportRowDto>
        {
            new(date, 500m, "Payroll", income.Id, checking.Id)
        };

        var result = await svc.FindPotentialDuplicatesAsync("user-1", candidates);

        Assert.Contains(0, result);
    }

    [Fact]
    public async Task FindPotentialDuplicatesAsync_ReturnsCorrectIndex_WhenOnlySecondRowIsDuplicate()
    {
        await using var db = TestDbContextFactory.Create();
        var (income, checking, expense) = SeedAccounts(db, "user-1");
        var date = new DateOnly(2025, 2, 1);
        // Only the withdrawal already exists
        db.ActualFlows.Add(MakeFlow("user-1", checking.Id, expense.Id, 75m, date, "Groceries"));
        await db.SaveChangesAsync();

        var svc = new ActualFlowService(db);

        var candidates = new List<BulkImportRowDto>
        {
            new(date, 500m, "Payroll",   income.Id,   checking.Id),  // new — index 0
            new(date,  75m, "Groceries", checking.Id, expense.Id),   // duplicate — index 1
        };

        var result = await svc.FindPotentialDuplicatesAsync("user-1", candidates);

        Assert.DoesNotContain(0, result);
        Assert.Contains(1, result);
    }

    [Fact]
    public async Task FindPotentialDuplicatesAsync_DoesNotMatchDifferentUser()
    {
        await using var db = TestDbContextFactory.Create();
        var (income, checking, _) = SeedAccounts(db, "user-1");
        // Also seed "user-2" accounts pointing at the same IDs (for realism — though
        // the service filters by userId so they won't collide)
        var date = new DateOnly(2025, 3, 5);
        db.ActualFlows.Add(MakeFlow("user-2", income.Id, checking.Id, 200m, date));
        await db.SaveChangesAsync();

        var svc = new ActualFlowService(db);

        var candidates = new List<BulkImportRowDto>
        {
            new(date, 200m, null, income.Id, checking.Id)
        };

        // Looking up as user-1 — the existing flow belongs to user-2
        var result = await svc.FindPotentialDuplicatesAsync("user-1", candidates);

        Assert.Empty(result);
    }

    [Fact]
    public async Task FindPotentialDuplicatesAsync_DoesNotMatchOnAmountAlone()
    {
        await using var db = TestDbContextFactory.Create();
        var (income, checking, expense) = SeedAccounts(db, "user-1");
        var date = new DateOnly(2025, 4, 1);
        // Existing: income → checking for 100
        db.ActualFlows.Add(MakeFlow("user-1", income.Id, checking.Id, 100m, date));
        await db.SaveChangesAsync();

        var svc = new ActualFlowService(db);

        // Candidate has same date + amount but different accounts (checking → expense)
        var candidates = new List<BulkImportRowDto>
        {
            new(date, 100m, null, checking.Id, expense.Id)
        };

        var result = await svc.FindPotentialDuplicatesAsync("user-1", candidates);

        Assert.Empty(result);
    }

    [Fact]
    public async Task FindPotentialDuplicatesAsync_MatchesMultipleDuplicates()
    {
        await using var db = TestDbContextFactory.Create();
        var (income, checking, expense) = SeedAccounts(db, "user-1");
        var date = new DateOnly(2025, 5, 15);
        db.ActualFlows.Add(MakeFlow("user-1", income.Id,   checking.Id, 1000m, date));
        db.ActualFlows.Add(MakeFlow("user-1", checking.Id, expense.Id,    50m, date));
        await db.SaveChangesAsync();

        var svc = new ActualFlowService(db);

        var candidates = new List<BulkImportRowDto>
        {
            new(date, 1000m, "Salary",   income.Id,   checking.Id),
            new(date,   50m, "Lunch",    checking.Id, expense.Id),
        };

        var result = await svc.FindPotentialDuplicatesAsync("user-1", candidates);

        Assert.Equal(2, result.Count);
        Assert.Contains(0, result);
        Assert.Contains(1, result);
    }
}
