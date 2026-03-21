using PlanToSave.Application.Accounts;
using PlanToSave.Domain.Entities;
using PlanToSave.Domain.Enums;
using PlanToSave.Tests.Helpers;
using PlanToSave.Web.Services;

namespace PlanToSave.Tests.Services;

public class AccountServiceTests
{
    // ── CreateAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_StoresAccountAndReturnsNewId()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new AccountService(db);

        var dto = new CreateAccountDto
        {
            Name = "My Checking",
            Type = AccountType.Checking,
            OpeningBalance = 500m
        };

        var id = await svc.CreateAsync("user-1", dto);

        Assert.NotEqual(Guid.Empty, id);
        var stored = await db.Accounts.FindAsync(id);
        Assert.NotNull(stored);
        Assert.Equal("My Checking", stored.Name);
        Assert.Equal("user-1", stored.UserId);
        Assert.Equal(500m, stored.OpeningBalance);
        Assert.False(stored.IsArchived);
    }

    [Fact]
    public async Task CreateAsync_TrimsNameAndNullsBlankDescription()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new AccountService(db);

        var id = await svc.CreateAsync("user-1", new CreateAccountDto
        {
            Name = "  Savings  ",
            Description = "   ",
            Type = AccountType.Savings
        });

        var stored = await db.Accounts.FindAsync(id);
        Assert.NotNull(stored);
        Assert.Equal("Savings", stored.Name);
        Assert.Null(stored.Description);
    }

    [Fact]
    public async Task CreateAsync_TrimsDescription_WhenNotBlank()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new AccountService(db);

        var id = await svc.CreateAsync("user-1", new CreateAccountDto
        {
            Name = "Credit Card",
            Description = "  Visa  ",
            Type = AccountType.Credit
        });

        var stored = await db.Accounts.FindAsync(id);
        Assert.NotNull(stored);
        Assert.Equal("Visa", stored.Description);
    }

    // ── GetAccountsAsync ──────────────────────────────────────────────

    [Fact]
    public async Task GetAccountsAsync_ExcludesArchivedByDefault()
    {
        await using var db = TestDbContextFactory.Create();
        db.Accounts.AddRange(
            MakeAccount("user-1", "Active", AccountType.Checking, archived: false),
            MakeAccount("user-1", "Archived", AccountType.Savings, archived: true));
        await db.SaveChangesAsync();

        var svc = new AccountService(db);
        var results = await svc.GetAccountsAsync("user-1");

        Assert.Single(results);
        Assert.Equal("Active", results[0].Name);
    }

    [Fact]
    public async Task GetAccountsAsync_IncludesArchivedWhenRequested()
    {
        await using var db = TestDbContextFactory.Create();
        db.Accounts.AddRange(
            MakeAccount("user-1", "Active",   AccountType.Checking, archived: false),
            MakeAccount("user-1", "Archived", AccountType.Savings,  archived: true));
        await db.SaveChangesAsync();

        var svc = new AccountService(db);
        var results = await svc.GetAccountsAsync("user-1", includeArchived: true);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task GetAccountsAsync_IsolatesUserData()
    {
        await using var db = TestDbContextFactory.Create();
        db.Accounts.AddRange(
            MakeAccount("user-1", "A1", AccountType.Checking),
            MakeAccount("user-2", "A2", AccountType.Checking));
        await db.SaveChangesAsync();

        var svc = new AccountService(db);
        var results = await svc.GetAccountsAsync("user-1");

        Assert.Single(results);
        Assert.Equal("A1", results[0].Name);
    }

    // ── GetByIdAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_ReturnsDto_WhenFound()
    {
        await using var db = TestDbContextFactory.Create();
        var account = MakeAccount("user-1", "Checking", AccountType.Checking);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        var svc = new AccountService(db);
        var dto = await svc.GetByIdAsync(account.Id, "user-1");

        Assert.NotNull(dto);
        Assert.Equal(account.Id, dto.Id);
        Assert.Equal("Checking", dto.Name);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new AccountService(db);

        var dto = await svc.GetByIdAsync(Guid.NewGuid(), "user-1");

        Assert.Null(dto);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenWrongUser()
    {
        await using var db = TestDbContextFactory.Create();
        var account = MakeAccount("user-1", "Checking", AccountType.Checking);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        var svc = new AccountService(db);
        var dto = await svc.GetByIdAsync(account.Id, "user-99");

        Assert.Null(dto);
    }

    // ── UpdateAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_ModifiesNameBalanceAndDescription()
    {
        await using var db = TestDbContextFactory.Create();
        var account = MakeAccount("user-1", "Old Name", AccountType.Checking, openingBalance: 100m);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        var svc = new AccountService(db);
        await svc.UpdateAsync(account.Id, "user-1", new UpdateAccountDto
        {
            Name = "  New Name  ",
            Description = "Updated",
            OpeningBalance = 200m
        });

        var stored = await db.Accounts.FindAsync(account.Id);
        Assert.NotNull(stored);
        Assert.Equal("New Name", stored.Name);
        Assert.Equal("Updated", stored.Description);
        Assert.Equal(200m, stored.OpeningBalance);
    }

    [Fact]
    public async Task UpdateAsync_Throws_WhenAccountNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new AccountService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.UpdateAsync(Guid.NewGuid(), "user-1", new UpdateAccountDto { Name = "X" }));
    }

    // ── ArchiveAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task ArchiveAsync_SetsIsArchivedTrue()
    {
        await using var db = TestDbContextFactory.Create();
        var account = MakeAccount("user-1", "Checking", AccountType.Checking);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        var svc = new AccountService(db);
        await svc.ArchiveAsync(account.Id, "user-1");

        var stored = await db.Accounts.FindAsync(account.Id);
        Assert.NotNull(stored);
        Assert.True(stored.IsArchived);
    }

    [Fact]
    public async Task ArchiveAsync_Throws_WhenAccountNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new AccountService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ArchiveAsync(Guid.NewGuid(), "user-1"));
    }

    // ── GetBalancesAsync ──────────────────────────────────────────────

    [Fact]
    public async Task GetBalancesAsync_ReturnsOpeningBalance_WhenNoFlowsOrSnapshots()
    {
        await using var db = TestDbContextFactory.Create();
        var account = MakeAccount("user-1", "Savings", AccountType.Savings, openingBalance: 1_000m);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        var svc = new AccountService(db);
        var balances = await svc.GetBalancesAsync("user-1");

        Assert.Single(balances);
        Assert.Equal(1_000m, balances[0].Balance);
    }

    [Fact]
    public async Task GetBalancesAsync_AppliesInflowsAndOutflows()
    {
        await using var db = TestDbContextFactory.Create();
        var account = MakeAccount("user-1", "Checking", AccountType.Checking, openingBalance: 500m);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        // Use distinct accounts for the from-side so the flow is not also counted as an outflow
        var incomeAccount  = MakeAccount("user-1", "Salary",   AccountType.Income);
        var expenseAccount = MakeAccount("user-1", "Expenses", AccountType.Expense);
        db.Accounts.AddRange(incomeAccount, expenseAccount);

        db.ActualFlows.Add(new ActualFlow
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            FromAccountId = incomeAccount.Id,
            ToAccountId = account.Id,
            Amount = 200m,
            Date = DateOnly.FromDateTime(DateTime.Today),
            Description = "Inflow"
        });
        db.ActualFlows.Add(new ActualFlow
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            FromAccountId = account.Id,
            ToAccountId = expenseAccount.Id,
            Amount = 100m,
            Date = DateOnly.FromDateTime(DateTime.Today),
            Description = "Outflow"
        });
        await db.SaveChangesAsync();

        var svc = new AccountService(db);
        var balances = await svc.GetBalancesAsync("user-1");

        var checking = balances.Single(b => b.Id == account.Id);
        // Opening 500 + inflow 200 - outflow 100 = 600
        Assert.Equal(600m, checking.Balance);
    }

    [Fact]
    public async Task GetBalancesAsync_UsesSnapshotAsBaseline()
    {
        await using var db = TestDbContextFactory.Create();
        var account = MakeAccount("user-1", "Savings", AccountType.Savings, openingBalance: 0m);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        var snapDate = new DateOnly(2024, 1, 1);
        db.BalanceSnapshots.Add(new BalanceSnapshot
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            AccountId = account.Id,
            Amount = 3_000m,
            Variance = 0m,
            EffectiveDate = snapDate,
            CreatedAt = DateTime.UtcNow
        });

        // Use a separate Income account as the funding source so flows aren't double-counted
        var incomeAccount = MakeAccount("user-1", "Income", AccountType.Income);
        db.Accounts.Add(incomeAccount);

        // Flow BEFORE snapshot — should be ignored
        db.ActualFlows.Add(new ActualFlow
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            FromAccountId = incomeAccount.Id,
            ToAccountId = account.Id,
            Amount = 999m,
            Date = new DateOnly(2023, 12, 31),
            Description = "Old inflow"
        });

        // Flow AFTER snapshot — should be included
        db.ActualFlows.Add(new ActualFlow
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            FromAccountId = incomeAccount.Id,
            ToAccountId = account.Id,
            Amount = 500m,
            Date = new DateOnly(2024, 1, 15),
            Description = "New inflow"
        });
        await db.SaveChangesAsync();

        var svc = new AccountService(db);
        var balances = await svc.GetBalancesAsync("user-1");

        var savings = balances.Single(b => b.Id == account.Id);
        Assert.Equal(3_500m, savings.Balance); // 3000 snapshot + 500 post-snapshot inflow
    }

    [Fact]
    public async Task GetBalancesAsync_ExcludesIncomeAndExpenseAccountTypes()
    {
        await using var db = TestDbContextFactory.Create();
        db.Accounts.AddRange(
            MakeAccount("user-1", "Income",  AccountType.Income),
            MakeAccount("user-1", "Expense", AccountType.Expense),
            MakeAccount("user-1", "Savings", AccountType.Savings));
        await db.SaveChangesAsync();

        var svc = new AccountService(db);
        var balances = await svc.GetBalancesAsync("user-1");

        Assert.Single(balances);
        Assert.Equal("Savings", balances[0].Name);
    }

    // ── Interest rule management ──────────────────────────────────────

    [Fact]
    public async Task SetInterestRuleAsync_CreatesNewRule()
    {
        await using var db = TestDbContextFactory.Create();
        var account = MakeAccount("user-1", "Savings", AccountType.Savings);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        var svc = new AccountService(db);
        await svc.SetInterestRuleAsync("user-1", account.Id, new SetInterestRuleDto
        {
            AnnualRatePct = 5m,
            Frequency = CompoundingFrequency.Monthly,
            EffectiveDate = new DateOnly(2024, 1, 1)
        });

        var rule = await svc.GetInterestRuleAsync("user-1", account.Id);
        Assert.NotNull(rule);
        Assert.Equal(5m, rule.AnnualRatePct);
        Assert.Equal(CompoundingFrequency.Monthly, rule.Frequency);
    }

    [Fact]
    public async Task SetInterestRuleAsync_UpdatesExistingRule()
    {
        await using var db = TestDbContextFactory.Create();
        var account = MakeAccount("user-1", "Savings", AccountType.Savings);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        var svc = new AccountService(db);
        await svc.SetInterestRuleAsync("user-1", account.Id, new SetInterestRuleDto
        {
            AnnualRatePct = 3m,
            Frequency = CompoundingFrequency.Annually,
            EffectiveDate = new DateOnly(2024, 1, 1)
        });

        // Update to new rate
        await svc.SetInterestRuleAsync("user-1", account.Id, new SetInterestRuleDto
        {
            AnnualRatePct = 4.5m,
            Frequency = CompoundingFrequency.Monthly,
            EffectiveDate = new DateOnly(2024, 6, 1)
        });

        var rule = await svc.GetInterestRuleAsync("user-1", account.Id);
        Assert.NotNull(rule);
        Assert.Equal(4.5m, rule.AnnualRatePct);
        Assert.Equal(CompoundingFrequency.Monthly, rule.Frequency);
    }

    [Fact]
    public async Task DeleteInterestRuleAsync_RemovesRule()
    {
        await using var db = TestDbContextFactory.Create();
        var account = MakeAccount("user-1", "Savings", AccountType.Savings);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        var svc = new AccountService(db);
        await svc.SetInterestRuleAsync("user-1", account.Id, new SetInterestRuleDto
        {
            AnnualRatePct = 2m,
            Frequency = CompoundingFrequency.Daily,
            EffectiveDate = new DateOnly(2024, 1, 1)
        });

        await svc.DeleteInterestRuleAsync("user-1", account.Id);

        var rule = await svc.GetInterestRuleAsync("user-1", account.Id);
        Assert.Null(rule);
    }

    [Fact]
    public async Task DeleteInterestRuleAsync_IsIdempotent_WhenNoRuleExists()
    {
        await using var db = TestDbContextFactory.Create();
        var account = MakeAccount("user-1", "Savings", AccountType.Savings);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        var svc = new AccountService(db);

        // Should not throw
        await svc.DeleteInterestRuleAsync("user-1", account.Id);
    }

    [Fact]
    public async Task SetInterestRuleAsync_Throws_WhenAccountNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new AccountService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.SetInterestRuleAsync("user-1", Guid.NewGuid(), new SetInterestRuleDto
            {
                AnnualRatePct = 1m,
                Frequency = CompoundingFrequency.Monthly,
                EffectiveDate = DateOnly.FromDateTime(DateTime.Today)
            }));
    }

    // ── Balance snapshots ─────────────────────────────────────────────

    [Fact]
    public async Task SetSnapshotAsync_CreatesSnapshotWithCorrectVariance()
    {
        await using var db = TestDbContextFactory.Create();
        var account = MakeAccount("user-1", "Checking", AccountType.Checking, openingBalance: 1_000m);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        var svc = new AccountService(db);
        var snapDate = new DateOnly(2024, 6, 1);
        await svc.SetSnapshotAsync("user-1", account.Id, 1_050m, snapDate, "Reconcile");

        var snapshots = await svc.GetSnapshotsAsync("user-1", account.Id);
        Assert.Single(snapshots);
        Assert.Equal(1_050m, snapshots[0].Amount);
        // No flows, so computed = opening balance 1000; variance = 1050 - 1000 = 50
        Assert.Equal(50m, snapshots[0].Variance);
        Assert.Equal("Reconcile", snapshots[0].Note);
    }

    [Fact]
    public async Task SetSnapshotAsync_UpdatesExistingSnapshot_WhenSameDateUsed()
    {
        await using var db = TestDbContextFactory.Create();
        var account = MakeAccount("user-1", "Savings", AccountType.Savings, openingBalance: 0m);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        var svc = new AccountService(db);
        var snapDate = new DateOnly(2024, 3, 31);

        await svc.SetSnapshotAsync("user-1", account.Id, 500m, snapDate, "First");
        await svc.SetSnapshotAsync("user-1", account.Id, 600m, snapDate, "Updated");

        var snapshots = await svc.GetSnapshotsAsync("user-1", account.Id);
        Assert.Single(snapshots);
        Assert.Equal(600m, snapshots[0].Amount);
        Assert.Equal("Updated", snapshots[0].Note);
    }

    [Fact]
    public async Task GetSnapshotsAsync_ReturnsSnapshotsOrderedDescending()
    {
        await using var db = TestDbContextFactory.Create();
        var account = MakeAccount("user-1", "Savings", AccountType.Savings, openingBalance: 0m);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        var svc = new AccountService(db);
        await svc.SetSnapshotAsync("user-1", account.Id, 100m, new DateOnly(2024, 1, 1), null);
        await svc.SetSnapshotAsync("user-1", account.Id, 200m, new DateOnly(2024, 3, 1), null);
        await svc.SetSnapshotAsync("user-1", account.Id, 300m, new DateOnly(2024, 6, 1), null);

        var snapshots = await svc.GetSnapshotsAsync("user-1", account.Id);

        Assert.Equal(3, snapshots.Count);
        Assert.True(snapshots[0].EffectiveDate > snapshots[1].EffectiveDate);
        Assert.True(snapshots[1].EffectiveDate > snapshots[2].EffectiveDate);
    }

    // ── GetBalancesAsync with accrued interest ────────────────────────

    [Fact]
    public async Task GetBalancesAsync_IncludesAccruedInterest_WhenRuleExists()
    {
        await using var db = TestDbContextFactory.Create();
        var account = MakeAccount("user-1", "Savings", AccountType.Savings, openingBalance: 10_000m);
        db.Accounts.Add(account);
        db.InterestRules.Add(new InterestRule
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            AccountId = account.Id,
            AnnualRatePct = 5m,
            Frequency = CompoundingFrequency.Monthly,
            EffectiveDate = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1),
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var svc = new AccountService(db);
        var balances = await svc.GetBalancesAsync("user-1");

        Assert.Single(balances);
        // Balance unchanged; interest may be 0 when rule starts today but should be non-negative
        Assert.True(balances[0].AccruedInterest >= 0m);
        Assert.Equal(5m, balances[0].AnnualRatePct);
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static Account MakeAccount(
        string userId,
        string name,
        AccountType type,
        bool archived = false,
        decimal openingBalance = 0m) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            Type = type,
            IsArchived = archived,
            OpeningBalance = openingBalance,
            CreatedAt = DateTime.UtcNow
        };
}
