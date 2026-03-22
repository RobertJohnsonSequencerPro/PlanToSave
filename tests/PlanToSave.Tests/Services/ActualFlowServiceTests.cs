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

    // ── CreateAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_StoresFlowAndReturnsId()
    {
        await using var db = TestDbContextFactory.Create();
        var (income, checking, _) = SeedAccounts(db, "user-1");
        await db.SaveChangesAsync();

        var svc = new ActualFlowService(db);
        var id = await svc.CreateAsync("user-1", new CreateActualFlowDto
        {
            FromAccountId = income.Id,
            ToAccountId = checking.Id,
            Amount = 1_000m,
            Date = new DateOnly(2025, 1, 15),
            Description = "  Payroll  "
        });

        Assert.NotEqual(Guid.Empty, id);
        var stored = await db.ActualFlows.FindAsync(id);
        Assert.NotNull(stored);
        Assert.Equal(1_000m, stored.Amount);
        Assert.Equal("Payroll", stored.Description);
        Assert.Equal("user-1", stored.UserId);
    }

    [Fact]
    public async Task CreateAsync_NullsBlankDescription()
    {
        await using var db = TestDbContextFactory.Create();
        var (income, checking, _) = SeedAccounts(db, "user-1");
        await db.SaveChangesAsync();

        var svc = new ActualFlowService(db);
        var id = await svc.CreateAsync("user-1", new CreateActualFlowDto
        {
            FromAccountId = income.Id,
            ToAccountId = checking.Id,
            Amount = 100m,
            Date = DateOnly.FromDateTime(DateTime.Today),
            Description = "   "
        });

        var stored = await db.ActualFlows.FindAsync(id);
        Assert.NotNull(stored);
        Assert.Null(stored.Description);
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenExpenseIsSource()
    {
        await using var db = TestDbContextFactory.Create();
        var (_, checking, expense) = SeedAccounts(db, "user-1");
        await db.SaveChangesAsync();

        var svc = new ActualFlowService(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateAsync("user-1", new CreateActualFlowDto
            {
                FromAccountId = expense.Id,
                ToAccountId = checking.Id,
                Amount = 50m,
                Date = DateOnly.FromDateTime(DateTime.Today)
            }));
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenIncomeIsDestination()
    {
        await using var db = TestDbContextFactory.Create();
        var (income, checking, _) = SeedAccounts(db, "user-1");
        await db.SaveChangesAsync();

        var svc = new ActualFlowService(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateAsync("user-1", new CreateActualFlowDto
            {
                FromAccountId = checking.Id,
                ToAccountId = income.Id,
                Amount = 50m,
                Date = DateOnly.FromDateTime(DateTime.Today)
            }));
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenFromAndToAreSame()
    {
        await using var db = TestDbContextFactory.Create();
        var (_, checking, _) = SeedAccounts(db, "user-1");
        await db.SaveChangesAsync();

        var svc = new ActualFlowService(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateAsync("user-1", new CreateActualFlowDto
            {
                FromAccountId = checking.Id,
                ToAccountId = checking.Id,
                Amount = 50m,
                Date = DateOnly.FromDateTime(DateTime.Today)
            }));
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenFromAccountNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var (_, checking, _) = SeedAccounts(db, "user-1");
        await db.SaveChangesAsync();

        var svc = new ActualFlowService(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateAsync("user-1", new CreateActualFlowDto
            {
                FromAccountId = Guid.NewGuid(),
                ToAccountId = checking.Id,
                Amount = 50m,
                Date = DateOnly.FromDateTime(DateTime.Today)
            }));
    }

    // ── DeleteAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_RemovesFlow()
    {
        await using var db = TestDbContextFactory.Create();
        var (income, checking, _) = SeedAccounts(db, "user-1");
        var date = new DateOnly(2025, 2, 1);
        db.ActualFlows.Add(MakeFlow("user-1", income.Id, checking.Id, 500m, date));
        await db.SaveChangesAsync();

        var flow = db.ActualFlows.Single();
        var svc = new ActualFlowService(db);
        await svc.DeleteAsync(flow.Id, "user-1");

        Assert.Empty(db.ActualFlows);
    }

    [Fact]
    public async Task DeleteAsync_Throws_WhenNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new ActualFlowService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.DeleteAsync(Guid.NewGuid(), "user-1"));
    }

    [Fact]
    public async Task DeleteAsync_Throws_WhenWrongUser()
    {
        await using var db = TestDbContextFactory.Create();
        var (income, checking, _) = SeedAccounts(db, "user-1");
        db.ActualFlows.Add(MakeFlow("user-1", income.Id, checking.Id, 100m, DateOnly.FromDateTime(DateTime.Today)));
        await db.SaveChangesAsync();

        var flow = db.ActualFlows.Single();
        var svc = new ActualFlowService(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.DeleteAsync(flow.Id, "user-99"));
    }

    // ── GetFlowsAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetFlowsAsync_ReturnsOnlyUserFlows()
    {
        await using var db = TestDbContextFactory.Create();
        var (income1, checking1, _) = SeedAccounts(db, "user-1");
        var (income2, checking2, _) = SeedAccounts(db, "user-2");
        db.ActualFlows.AddRange(
            MakeFlow("user-1", income1.Id, checking1.Id, 100m, new DateOnly(2025, 1, 1)),
            MakeFlow("user-2", income2.Id, checking2.Id, 200m, new DateOnly(2025, 1, 2)));
        await db.SaveChangesAsync();

        var svc = new ActualFlowService(db);
        var results = await svc.GetFlowsAsync("user-1");

        Assert.Single(results);
        Assert.Equal(100m, results[0].Amount);
    }

    [Fact]
    public async Task GetFlowsAsync_ReturnsEmpty_WhenNoFlows()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new ActualFlowService(db);

        var results = await svc.GetFlowsAsync("user-1");

        Assert.Empty(results);
    }

    [Fact]
    public async Task GetFlowsAsync_FiltersByDateRange()
    {
        await using var db = TestDbContextFactory.Create();
        var (income, checking, _) = SeedAccounts(db, "user-1");
        db.ActualFlows.AddRange(
            MakeFlow("user-1", income.Id, checking.Id, 100m, new DateOnly(2025, 1, 1)),
            MakeFlow("user-1", income.Id, checking.Id, 200m, new DateOnly(2025, 2, 1)),
            MakeFlow("user-1", income.Id, checking.Id, 300m, new DateOnly(2025, 3, 1)));
        await db.SaveChangesAsync();

        var svc = new ActualFlowService(db);
        var results = await svc.GetFlowsAsync("user-1", new FlowFilterDto(
            From: new DateOnly(2025, 1, 15),
            To: new DateOnly(2025, 2, 28),
            AccountId: null));

        Assert.Single(results);
        Assert.Equal(200m, results[0].Amount);
    }

    [Fact]
    public async Task GetFlowsAsync_FiltersByAccountId()
    {
        await using var db = TestDbContextFactory.Create();
        var (income, checking, expense) = SeedAccounts(db, "user-1");
        db.ActualFlows.AddRange(
            MakeFlow("user-1", income.Id, checking.Id, 100m, new DateOnly(2025, 1, 1)),
            MakeFlow("user-1", checking.Id, expense.Id, 50m, new DateOnly(2025, 1, 2)));
        await db.SaveChangesAsync();

        var svc = new ActualFlowService(db);
        var results = await svc.GetFlowsAsync("user-1", new FlowFilterDto(
            From: null,
            To: null,
            AccountId: expense.Id));

        Assert.Single(results);
        Assert.Equal(50m, results[0].Amount);
    }

    // ── BulkImportAsync ───────────────────────────────────────────────

    [Fact]
    public async Task BulkImportAsync_ImportsValidRows()
    {
        await using var db = TestDbContextFactory.Create();
        var (income, checking, _) = SeedAccounts(db, "user-1");
        await db.SaveChangesAsync();

        var svc = new ActualFlowService(db);
        var rows = new List<BulkImportRowDto>
        {
            new(new DateOnly(2025, 1, 10), 500m, "Payroll", income.Id, checking.Id),
            new(new DateOnly(2025, 1, 15), 100m, "Transfer", income.Id, checking.Id)
        };

        var (imported, errors) = await svc.BulkImportAsync("user-1", rows);

        Assert.Equal(2, imported);
        Assert.Empty(errors);
        Assert.Equal(2, db.ActualFlows.Count());
    }

    [Fact]
    public async Task BulkImportAsync_SkipsRowsWithInvalidAccounts()
    {
        await using var db = TestDbContextFactory.Create();
        var (income, checking, _) = SeedAccounts(db, "user-1");
        await db.SaveChangesAsync();

        var svc = new ActualFlowService(db);
        var rows = new List<BulkImportRowDto>
        {
            new(new DateOnly(2025, 1, 10), 500m, "Good", income.Id, checking.Id),
            new(new DateOnly(2025, 1, 11), 100m, "Bad", Guid.NewGuid(), checking.Id)
        };

        var (imported, errors) = await svc.BulkImportAsync("user-1", rows);

        Assert.Equal(1, imported);
        Assert.Single(errors);
    }

    [Fact]
    public async Task BulkImportAsync_SkipsExpenseAsSource()
    {
        await using var db = TestDbContextFactory.Create();
        var (_, checking, expense) = SeedAccounts(db, "user-1");
        await db.SaveChangesAsync();

        var svc = new ActualFlowService(db);
        var rows = new List<BulkImportRowDto>
        {
            new(new DateOnly(2025, 1, 1), 10m, "Bad", expense.Id, checking.Id)
        };

        var (imported, errors) = await svc.BulkImportAsync("user-1", rows);

        Assert.Equal(0, imported);
        Assert.Single(errors);
    }

    [Fact]
    public async Task BulkImportAsync_TruncatesLongDescriptions()
    {
        await using var db = TestDbContextFactory.Create();
        var (income, checking, _) = SeedAccounts(db, "user-1");
        await db.SaveChangesAsync();

        var longDesc = new string('A', 350);
        var svc = new ActualFlowService(db);
        var rows = new List<BulkImportRowDto>
        {
            new(new DateOnly(2025, 1, 1), 100m, longDesc, income.Id, checking.Id)
        };

        var (imported, _) = await svc.BulkImportAsync("user-1", rows);

        Assert.Equal(1, imported);
        var stored = db.ActualFlows.Single();
        Assert.NotNull(stored.Description);
        Assert.True(stored.Description!.Length <= 300);
    }

    // ── SuggestCounterAccountsAsync ───────────────────────────────────

    [Fact]
    public async Task SuggestCounterAccountsAsync_ReturnsEmpty_WhenNoHistory()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new ActualFlowService(db);

        var (deposits, withdrawals) = await svc.SuggestCounterAccountsAsync("user-1", ["Groceries"]);

        Assert.Empty(deposits);
        Assert.Empty(withdrawals);
    }

    [Fact]
    public async Task SuggestCounterAccountsAsync_SuggestsFrequentIncomeSource()
    {
        await using var db = TestDbContextFactory.Create();
        var (income, checking, _) = SeedAccounts(db, "user-1");
        // Two historical flows with the same description from an Income account
        db.ActualFlows.AddRange(
            MakeFlow("user-1", income.Id, checking.Id, 1000m, new DateOnly(2025, 1, 1), "DIRECT DEPOSIT PAYROLL"),
            MakeFlow("user-1", income.Id, checking.Id, 1000m, new DateOnly(2025, 2, 1), "DIRECT DEPOSIT PAYROLL"));
        await db.SaveChangesAsync();

        var svc = new ActualFlowService(db);
        var (deposits, _) = await svc.SuggestCounterAccountsAsync("user-1", ["DIRECT DEPOSIT PAYROLL"]);

        Assert.True(deposits.ContainsKey("DIRECT DEPOSIT PAYROLL"));
        Assert.Equal(income.Id, deposits["DIRECT DEPOSIT PAYROLL"]);
    }

    [Fact]
    public async Task SuggestCounterAccountsAsync_SuggestsFrequentExpenseDestination()
    {
        await using var db = TestDbContextFactory.Create();
        var (_, checking, expense) = SeedAccounts(db, "user-1");
        // Two historical flows to the same Expense account
        db.ActualFlows.AddRange(
            MakeFlow("user-1", checking.Id, expense.Id, 50m, new DateOnly(2025, 1, 5), "Groceries"),
            MakeFlow("user-1", checking.Id, expense.Id, 60m, new DateOnly(2025, 2, 5), "Groceries"));
        await db.SaveChangesAsync();

        var svc = new ActualFlowService(db);
        var (_, withdrawals) = await svc.SuggestCounterAccountsAsync("user-1", ["Groceries"]);

        Assert.True(withdrawals.ContainsKey("Groceries"));
        Assert.Equal(expense.Id, withdrawals["Groceries"]);
    }
}
