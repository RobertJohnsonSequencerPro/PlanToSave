using PlanToSave.Application.Goals;
using PlanToSave.Domain.Entities;
using PlanToSave.Domain.Enums;
using PlanToSave.Tests.Helpers;
using PlanToSave.Web.Services;

namespace PlanToSave.Tests.Services;

public class GoalServiceTests
{
    // ── CreateAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_StoresGoalAndReturnsNewId()
    {
        await using var db = TestDbContextFactory.Create();
        var (target, source) = SeedAccounts(db, "user-1");
        await db.SaveChangesAsync();

        var svc = new GoalService(db);
        var id = await svc.CreateAsync("user-1", new CreateGoalDto
        {
            Name = "Emergency Fund",
            TargetAccountId = target.Id,
            SourceAccountId = source.Id,
            TargetAmount = 10_000m,
            StartDate = new DateOnly(2024, 1, 1),
            TargetDate = new DateOnly(2025, 1, 1)
        });

        Assert.NotEqual(Guid.Empty, id);
        var stored = await db.Goals.FindAsync(id);
        Assert.NotNull(stored);
        Assert.Equal("Emergency Fund", stored.Name);
        Assert.Equal("user-1", stored.UserId);
        Assert.False(stored.IsComplete);
        Assert.Equal(10_000m, stored.TargetAmount);
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenTargetDateNotAfterStartDate()
    {
        await using var db = TestDbContextFactory.Create();
        var (target, source) = SeedAccounts(db, "user-1");
        await db.SaveChangesAsync();

        var svc = new GoalService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateAsync("user-1", new CreateGoalDto
            {
                Name = "Bad Dates",
                TargetAccountId = target.Id,
                SourceAccountId = source.Id,
                TargetAmount = 500m,
                StartDate = new DateOnly(2024, 6, 1),
                TargetDate = new DateOnly(2024, 6, 1)  // same date — not after
            }));
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenTargetAccountNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var (_, source) = SeedAccounts(db, "user-1");
        await db.SaveChangesAsync();

        var svc = new GoalService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateAsync("user-1", new CreateGoalDto
            {
                Name = "Goal",
                TargetAccountId = Guid.NewGuid(),  // doesn't exist
                SourceAccountId = source.Id,
                TargetAmount = 500m,
                StartDate = new DateOnly(2024, 1, 1),
                TargetDate = new DateOnly(2025, 1, 1)
            }));
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenSourceAccountNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var (target, _) = SeedAccounts(db, "user-1");
        await db.SaveChangesAsync();

        var svc = new GoalService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateAsync("user-1", new CreateGoalDto
            {
                Name = "Goal",
                TargetAccountId = target.Id,
                SourceAccountId = Guid.NewGuid(),  // doesn't exist
                TargetAmount = 500m,
                StartDate = new DateOnly(2024, 1, 1),
                TargetDate = new DateOnly(2025, 1, 1)
            }));
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenIdeaIdProvidedButNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var (target, source) = SeedAccounts(db, "user-1");
        await db.SaveChangesAsync();

        var svc = new GoalService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateAsync("user-1", new CreateGoalDto
            {
                Name = "Goal",
                TargetAccountId = target.Id,
                SourceAccountId = source.Id,
                TargetAmount = 500m,
                StartDate = new DateOnly(2024, 1, 1),
                TargetDate = new DateOnly(2025, 1, 1),
                IdeaId = Guid.NewGuid()  // doesn't exist
            }));
    }

    [Fact]
    public async Task CreateAsync_IgnoresEmptyGuidIdeaId()
    {
        await using var db = TestDbContextFactory.Create();
        var (target, source) = SeedAccounts(db, "user-1");
        await db.SaveChangesAsync();

        var svc = new GoalService(db);

        // Guid.Empty should be treated as "no idea" — no exception
        var id = await svc.CreateAsync("user-1", new CreateGoalDto
        {
            Name = "Goal Without Idea",
            TargetAccountId = target.Id,
            SourceAccountId = source.Id,
            TargetAmount = 500m,
            StartDate = new DateOnly(2024, 1, 1),
            TargetDate = new DateOnly(2025, 1, 1),
            IdeaId = Guid.Empty
        });

        var stored = await db.Goals.FindAsync(id);
        Assert.NotNull(stored);
        Assert.Null(stored.IdeaId);
    }

    // ── MarkCompleteAsync ─────────────────────────────────────────────

    [Fact]
    public async Task MarkCompleteAsync_TogglesIsComplete()
    {
        await using var db = TestDbContextFactory.Create();
        var (target, source) = SeedAccounts(db, "user-1");
        var goal = MakeGoal("user-1", "Goal", target.Id, source.Id, isComplete: false);
        db.Goals.Add(goal);
        await db.SaveChangesAsync();

        var svc = new GoalService(db);

        await svc.MarkCompleteAsync("user-1", goal.Id);
        var afterFirst = await db.Goals.FindAsync(goal.Id);
        Assert.NotNull(afterFirst);
        Assert.True(afterFirst.IsComplete);

        await svc.MarkCompleteAsync("user-1", goal.Id);
        var afterSecond = await db.Goals.FindAsync(goal.Id);
        Assert.NotNull(afterSecond);
        Assert.False(afterSecond.IsComplete);
    }

    [Fact]
    public async Task MarkCompleteAsync_Throws_WhenGoalNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new GoalService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.MarkCompleteAsync("user-1", Guid.NewGuid()));
    }

    // ── DeleteAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_RemovesGoal()
    {
        await using var db = TestDbContextFactory.Create();
        var (target, source) = SeedAccounts(db, "user-1");
        var goal = MakeGoal("user-1", "Goal To Delete", target.Id, source.Id);
        db.Goals.Add(goal);
        await db.SaveChangesAsync();

        var svc = new GoalService(db);
        await svc.DeleteAsync("user-1", goal.Id);

        Assert.Null(await db.Goals.FindAsync(goal.Id));
    }

    [Fact]
    public async Task DeleteAsync_Throws_WhenGoalNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new GoalService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.DeleteAsync("user-1", Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteAsync_Throws_WhenWrongUser()
    {
        await using var db = TestDbContextFactory.Create();
        var (target, source) = SeedAccounts(db, "user-1");
        var goal = MakeGoal("user-1", "Goal", target.Id, source.Id);
        db.Goals.Add(goal);
        await db.SaveChangesAsync();

        var svc = new GoalService(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.DeleteAsync("user-99", goal.Id));
    }

    // ── GetGoalsAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetGoalsAsync_ReturnsOnlyUserGoals()
    {
        await using var db = TestDbContextFactory.Create();
        var (t1, s1) = SeedAccounts(db, "user-1");
        var (t2, s2) = SeedAccounts(db, "user-2");
        db.Goals.AddRange(
            MakeGoal("user-1", "User1 Goal", t1.Id, s1.Id),
            MakeGoal("user-2", "User2 Goal", t2.Id, s2.Id));
        await db.SaveChangesAsync();

        var svc = new GoalService(db);
        var results = await svc.GetGoalsAsync("user-1");

        Assert.Single(results);
        Assert.Equal("User1 Goal", results[0].Name);
    }

    [Fact]
    public async Task GetGoalsAsync_IncludesSavedAmount_FromActualFlows()
    {
        await using var db = TestDbContextFactory.Create();
        var (target, source) = SeedAccounts(db, "user-1");
        db.Goals.Add(MakeGoal("user-1", "Savings Goal", target.Id, source.Id));

        // Record two flows into the target account
        db.ActualFlows.AddRange(
            new ActualFlow
            {
                Id = Guid.NewGuid(), UserId = "user-1",
                ToAccountId = target.Id, FromAccountId = source.Id,
                Amount = 300m, Date = DateOnly.FromDateTime(DateTime.Today),
                Description = "Contribution 1"
            },
            new ActualFlow
            {
                Id = Guid.NewGuid(), UserId = "user-1",
                ToAccountId = target.Id, FromAccountId = source.Id,
                Amount = 200m, Date = DateOnly.FromDateTime(DateTime.Today),
                Description = "Contribution 2"
            });
        await db.SaveChangesAsync();

        var svc = new GoalService(db);
        var results = await svc.GetGoalsAsync("user-1");

        Assert.Single(results);
        Assert.Equal(500m, results[0].SavedAmount);
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static (Account target, Account source) SeedAccounts(
        PlanToSave.Web.Data.ApplicationDbContext db, string userId)
    {
        var target = new Account
        {
            Id = Guid.NewGuid(), UserId = userId,
            Name = "Savings", Type = AccountType.Savings,
            CreatedAt = DateTime.UtcNow
        };
        var source = new Account
        {
            Id = Guid.NewGuid(), UserId = userId,
            Name = "Checking", Type = AccountType.Checking,
            CreatedAt = DateTime.UtcNow
        };
        db.Accounts.AddRange(target, source);
        return (target, source);
    }

    private static Goal MakeGoal(
        string userId, string name,
        Guid targetAccountId, Guid sourceAccountId,
        bool isComplete = false) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            TargetAccountId = targetAccountId,
            SourceAccountId = sourceAccountId,
            TargetAmount = 1_000m,
            StartDate = new DateOnly(2024, 1, 1),
            TargetDate = new DateOnly(2025, 1, 1),
            IsComplete = isComplete,
            CreatedAt = DateTime.UtcNow
        };
}
