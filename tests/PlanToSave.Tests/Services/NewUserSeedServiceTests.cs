using PlanToSave.Domain.Enums;
using PlanToSave.Tests.Helpers;
using PlanToSave.Web.Services;

namespace PlanToSave.Tests.Services;

public class NewUserSeedServiceTests
{
    // ── SeedAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task SeedAsync_CreatesDefaultAccountsForNewUser()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new NewUserSeedService(db);

        await svc.SeedAsync("user-1");

        var accounts = db.Accounts.Where(a => a.UserId == "user-1").ToList();
        Assert.NotEmpty(accounts);

        // At least one account of each major type should be present
        Assert.Contains(accounts, a => a.Type == AccountType.Checking);
        Assert.Contains(accounts, a => a.Type == AccountType.Savings);
        Assert.Contains(accounts, a => a.Type == AccountType.Income);
        Assert.Contains(accounts, a => a.Type == AccountType.Expense);
    }

    [Fact]
    public async Task SeedAsync_CreatesExpectedExpenseAccounts()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new NewUserSeedService(db);

        await svc.SeedAsync("user-1");

        var expenseNames = db.Accounts
            .Where(a => a.UserId == "user-1" && a.Type == AccountType.Expense)
            .Select(a => a.Name)
            .ToHashSet();

        Assert.Contains("Housing",       expenseNames);
        Assert.Contains("Groceries",     expenseNames);
        Assert.Contains("Utilities",     expenseNames);
        Assert.Contains("Transportation",expenseNames);
    }

    [Fact]
    public async Task SeedAsync_IsIdempotent_WhenCalledTwice()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new NewUserSeedService(db);

        await svc.SeedAsync("user-1");
        var countAfterFirst = db.Accounts.Count(a => a.UserId == "user-1");

        await svc.SeedAsync("user-1");   // second call — should be a no-op
        var countAfterSecond = db.Accounts.Count(a => a.UserId == "user-1");

        Assert.Equal(countAfterFirst, countAfterSecond);
    }

    [Fact]
    public async Task SeedAsync_IsolatesUserData()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new NewUserSeedService(db);

        await svc.SeedAsync("user-1");
        await svc.SeedAsync("user-2");

        var user1Count = db.Accounts.Count(a => a.UserId == "user-1");
        var user2Count = db.Accounts.Count(a => a.UserId == "user-2");

        Assert.True(user1Count > 0);
        Assert.True(user2Count > 0);
        // Each user gets their own independent set of accounts
        Assert.Equal(user1Count, user2Count);
    }

    [Fact]
    public async Task SeedAsync_DoesNotSeed_WhenUserAlreadyHasAccounts()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new NewUserSeedService(db);

        // Pre-existing account for user-1
        db.Accounts.Add(new PlanToSave.Domain.Entities.Account
        {
            Id        = Guid.NewGuid(),
            UserId    = "user-1",
            Name      = "Existing Account",
            Type      = AccountType.Checking,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        await svc.SeedAsync("user-1");

        var count = db.Accounts.Count(a => a.UserId == "user-1");
        Assert.Equal(1, count); // only the pre-existing account
    }
}
