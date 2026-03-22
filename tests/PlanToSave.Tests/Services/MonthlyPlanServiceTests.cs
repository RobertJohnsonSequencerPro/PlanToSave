using PlanToSave.Application.Plans;
using PlanToSave.Domain.Entities;
using PlanToSave.Domain.Enums;
using PlanToSave.Tests.Helpers;
using PlanToSave.Web.Services;

namespace PlanToSave.Tests.Services;

public class MonthlyPlanServiceTests
{
    // ── GetOrCreatePlanAsync ──────────────────────────────────────────

    [Fact]
    public async Task GetOrCreatePlanAsync_CreatesNewPlan_WhenNoneExists()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new MonthlyPlanService(db);

        var detail = await svc.GetOrCreatePlanAsync("user-1", 2024, 3);

        Assert.NotNull(detail);
        Assert.Equal(2024, detail.Year);
        Assert.Equal(3, detail.Month);
        Assert.Equal(MonthlyPlanStatus.Draft, detail.Status);
        Assert.Empty(detail.PlannedFlows);

        // Verify persisted
        var stored = db.MonthlyPlans.Single(p => p.UserId == "user-1" && p.Year == 2024 && p.Month == 3);
        Assert.NotNull(stored);
    }

    [Fact]
    public async Task GetOrCreatePlanAsync_ReturnsExistingPlan_WhenAlreadyExists()
    {
        await using var db = TestDbContextFactory.Create();
        var existing = new MonthlyPlan
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            Year = 2024,
            Month = 5,
            Status = MonthlyPlanStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        db.MonthlyPlans.Add(existing);
        await db.SaveChangesAsync();

        var svc = new MonthlyPlanService(db);
        var detail = await svc.GetOrCreatePlanAsync("user-1", 2024, 5);

        Assert.Equal(existing.Id, detail.Id);
        Assert.Equal(MonthlyPlanStatus.Active, detail.Status);
        Assert.Single(db.MonthlyPlans.Where(p => p.UserId == "user-1"));
    }

    // ── GetPlanDetailAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetPlanDetailAsync_ReturnsNull_WhenPlanDoesNotExist()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new MonthlyPlanService(db);

        var detail = await svc.GetPlanDetailAsync("user-1", 2025, 1);

        Assert.Null(detail);
    }

    // ── AddPlannedFlowAsync ───────────────────────────────────────────

    [Fact]
    public async Task AddPlannedFlowAsync_AddsFlowToPlan()
    {
        await using var db = TestDbContextFactory.Create();
        var (income, savings, plan) = await SeedPlanWithAccountsAsync(db, "user-1", 2024, 6);

        var svc = new MonthlyPlanService(db);
        await svc.AddPlannedFlowAsync("user-1", plan.Id, new CreatePlannedFlowDto
        {
            FromAccountId = income.Id,
            ToAccountId = savings.Id,
            Amount = 500m,
            Description = "Savings transfer"
        });

        var flows = db.PlannedFlows.Where(f => f.MonthlyPlanId == plan.Id).ToList();
        Assert.Single(flows);
        Assert.Equal(500m, flows[0].Amount);
        Assert.Equal("Savings transfer", flows[0].Description);
    }

    [Fact]
    public async Task AddPlannedFlowAsync_Throws_WhenExpenseAccountIsSource()
    {
        await using var db = TestDbContextFactory.Create();
        var plan = SeedPlan(db, "user-1", 2024, 7);
        var expense = SeedAccount(db, "user-1", "Groceries", AccountType.Expense);
        var savings = SeedAccount(db, "user-1", "Savings",   AccountType.Savings);
        await db.SaveChangesAsync();

        var svc = new MonthlyPlanService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AddPlannedFlowAsync("user-1", plan.Id, new CreatePlannedFlowDto
            {
                FromAccountId = expense.Id,
                ToAccountId = savings.Id,
                Amount = 100m
            }));
    }

    [Fact]
    public async Task AddPlannedFlowAsync_Throws_WhenIncomeAccountIsDestination()
    {
        await using var db = TestDbContextFactory.Create();
        var plan = SeedPlan(db, "user-1", 2024, 8);
        var checking = SeedAccount(db, "user-1", "Checking", AccountType.Checking);
        var income   = SeedAccount(db, "user-1", "Salary",   AccountType.Income);
        await db.SaveChangesAsync();

        var svc = new MonthlyPlanService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AddPlannedFlowAsync("user-1", plan.Id, new CreatePlannedFlowDto
            {
                FromAccountId = checking.Id,
                ToAccountId = income.Id,
                Amount = 100m
            }));
    }

    [Fact]
    public async Task AddPlannedFlowAsync_Throws_WhenFromAndToAreSameAccount()
    {
        await using var db = TestDbContextFactory.Create();
        var plan = SeedPlan(db, "user-1", 2024, 9);
        var savings = SeedAccount(db, "user-1", "Savings", AccountType.Savings);
        await db.SaveChangesAsync();

        var svc = new MonthlyPlanService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AddPlannedFlowAsync("user-1", plan.Id, new CreatePlannedFlowDto
            {
                FromAccountId = savings.Id,
                ToAccountId = savings.Id,
                Amount = 100m
            }));
    }

    [Fact]
    public async Task AddPlannedFlowAsync_Throws_WhenPlanNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var income  = SeedAccount(db, "user-1", "Income",  AccountType.Income);
        var savings = SeedAccount(db, "user-1", "Savings", AccountType.Savings);
        await db.SaveChangesAsync();

        var svc = new MonthlyPlanService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AddPlannedFlowAsync("user-1", Guid.NewGuid(), new CreatePlannedFlowDto
            {
                FromAccountId = income.Id,
                ToAccountId = savings.Id,
                Amount = 100m
            }));
    }

    // ── DeletePlannedFlowAsync ────────────────────────────────────────

    [Fact]
    public async Task DeletePlannedFlowAsync_RemovesFlow()
    {
        await using var db = TestDbContextFactory.Create();
        var (income, savings, plan) = await SeedPlanWithAccountsAsync(db, "user-1", 2024, 10);

        var svc = new MonthlyPlanService(db);
        await svc.AddPlannedFlowAsync("user-1", plan.Id, new CreatePlannedFlowDto
        {
            FromAccountId = income.Id,
            ToAccountId = savings.Id,
            Amount = 200m
        });

        var flow = db.PlannedFlows.Single(f => f.MonthlyPlanId == plan.Id);
        await svc.DeletePlannedFlowAsync("user-1", flow.Id);

        Assert.Empty(db.PlannedFlows.Where(f => f.MonthlyPlanId == plan.Id));
    }

    [Fact]
    public async Task DeletePlannedFlowAsync_Throws_WhenFlowNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new MonthlyPlanService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.DeletePlannedFlowAsync("user-1", Guid.NewGuid()));
    }

    // ── SetStatusAsync ────────────────────────────────────────────────

    [Fact]
    public async Task SetStatusAsync_UpdatesPlanStatus()
    {
        await using var db = TestDbContextFactory.Create();
        var plan = SeedPlan(db, "user-1", 2024, 11);
        await db.SaveChangesAsync();

        var svc = new MonthlyPlanService(db);
        await svc.SetStatusAsync("user-1", plan.Id, MonthlyPlanStatus.Active);

        var stored = await db.MonthlyPlans.FindAsync(plan.Id);
        Assert.NotNull(stored);
        Assert.Equal(MonthlyPlanStatus.Active, stored.Status);
    }

    [Fact]
    public async Task SetStatusAsync_Throws_WhenPlanNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new MonthlyPlanService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.SetStatusAsync("user-1", Guid.NewGuid(), MonthlyPlanStatus.Active));
    }

    // ── SeedFromTemplatesAsync ────────────────────────────────────────

    [Fact]
    public async Task SeedFromTemplatesAsync_AddsFreshFlowsFromActiveTemplates()
    {
        await using var db = TestDbContextFactory.Create();
        var plan    = SeedPlan(db, "user-1", 2024, 12);
        var income  = SeedAccount(db, "user-1", "Income",  AccountType.Income);
        var savings = SeedAccount(db, "user-1", "Savings", AccountType.Savings);

        db.FlowTemplates.Add(new FlowTemplate
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            FromAccountId = income.Id,
            ToAccountId = savings.Id,
            Amount = 400m,
            Description = "Monthly savings",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var svc = new MonthlyPlanService(db);
        await svc.SeedFromTemplatesAsync("user-1", plan.Id);

        var flows = db.PlannedFlows.Where(f => f.MonthlyPlanId == plan.Id).ToList();
        Assert.Single(flows);
        Assert.Equal(400m, flows[0].Amount);
    }

    [Fact]
    public async Task SeedFromTemplatesAsync_SkipsInactiveTemplates()
    {
        await using var db = TestDbContextFactory.Create();
        var plan    = SeedPlan(db, "user-1", 2024, 1);
        var income  = SeedAccount(db, "user-1", "Income",  AccountType.Income);
        var savings = SeedAccount(db, "user-1", "Savings", AccountType.Savings);

        db.FlowTemplates.Add(new FlowTemplate
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            FromAccountId = income.Id,
            ToAccountId = savings.Id,
            Amount = 100m,
            Description = "Inactive template",
            IsActive = false,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var svc = new MonthlyPlanService(db);
        await svc.SeedFromTemplatesAsync("user-1", plan.Id);

        Assert.Empty(db.PlannedFlows.Where(f => f.MonthlyPlanId == plan.Id));
    }

    [Fact]
    public async Task SeedFromTemplatesAsync_SkipsDuplicateFlows()
    {
        await using var db = TestDbContextFactory.Create();
        var income  = SeedAccount(db, "user-1", "Income",  AccountType.Income);
        var savings = SeedAccount(db, "user-1", "Savings", AccountType.Savings);
        var plan    = SeedPlan(db, "user-1", 2024, 2);
        await db.SaveChangesAsync();

        // Pre-populate the plan with a flow matching the template
        db.PlannedFlows.Add(new PlannedFlow
        {
            Id = Guid.NewGuid(),
            MonthlyPlanId = plan.Id,
            FromAccountId = income.Id,
            ToAccountId = savings.Id,
            Amount = 300m,
            Description = "Monthly savings"
        });
        db.FlowTemplates.Add(new FlowTemplate
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            FromAccountId = income.Id,
            ToAccountId = savings.Id,
            Amount = 300m,
            Description = "Monthly savings",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var svc = new MonthlyPlanService(db);
        await svc.SeedFromTemplatesAsync("user-1", plan.Id);

        // Should still be exactly 1 (no duplicate added)
        Assert.Single(db.PlannedFlows.Where(f => f.MonthlyPlanId == plan.Id));
    }

    // ── GetPlansAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetPlansAsync_ReturnsOnlyUserPlans()
    {
        await using var db = TestDbContextFactory.Create();
        db.MonthlyPlans.AddRange(
            SeedPlan(db, "user-1", 2024, 1),
            SeedPlan(db, "user-2", 2024, 2));
        await db.SaveChangesAsync();

        var svc = new MonthlyPlanService(db);
        var plans = await svc.GetPlansAsync("user-1");

        Assert.Single(plans);
        Assert.Equal(1, plans[0].Month);
    }

    [Fact]
    public async Task GetPlansAsync_OrdersByYearDescThenMonthDesc()
    {
        await using var db = TestDbContextFactory.Create();
        db.MonthlyPlans.AddRange(
            SeedPlan(db, "user-1", 2023, 12),
            SeedPlan(db, "user-1", 2024, 1),
            SeedPlan(db, "user-1", 2024, 3));
        await db.SaveChangesAsync();

        var svc = new MonthlyPlanService(db);
        var plans = await svc.GetPlansAsync("user-1");

        Assert.Equal(3, plans.Count);
        Assert.Equal(2024, plans[0].Year);
        Assert.Equal(3, plans[0].Month);
        Assert.Equal(2024, plans[1].Year);
        Assert.Equal(1, plans[1].Month);
        Assert.Equal(2023, plans[2].Year);
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static MonthlyPlan SeedPlan(
        PlanToSave.Web.Data.ApplicationDbContext db,
        string userId, int year, int month)
    {
        var plan = new MonthlyPlan
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Year = year,
            Month = month,
            Status = MonthlyPlanStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };
        db.MonthlyPlans.Add(plan);
        return plan;
    }

    private static Account SeedAccount(
        PlanToSave.Web.Data.ApplicationDbContext db,
        string userId, string name, AccountType type)
    {
        var a = new Account
        {
            Id = Guid.NewGuid(), UserId = userId,
            Name = name, Type = type,
            CreatedAt = DateTime.UtcNow
        };
        db.Accounts.Add(a);
        return a;
    }

    private static async Task<(Account income, Account savings, MonthlyPlan plan)>
        SeedPlanWithAccountsAsync(
            PlanToSave.Web.Data.ApplicationDbContext db,
            string userId, int year, int month)
    {
        var plan    = SeedPlan(db, userId, year, month);
        var income  = SeedAccount(db, userId, "Income",  AccountType.Income);
        var savings = SeedAccount(db, userId, "Savings", AccountType.Savings);
        await db.SaveChangesAsync();
        return (income, savings, plan);
    }

    // ── GenerateGoalScheduleAsync ─────────────────────────────────────

    [Fact]
    public async Task GenerateGoalScheduleAsync_CreatesOneFlowPerMonth()
    {
        await using var db = TestDbContextFactory.Create();
        var income  = SeedAccount(db, "user-1", "Income",  AccountType.Income);
        var savings = SeedAccount(db, "user-1", "Savings", AccountType.Savings);
        var goal = new Goal
        {
            Id = Guid.NewGuid(), UserId = "user-1", Name = "Emergency Fund",
            TargetAccountId = savings.Id, TargetAmount = 10_000m,
            CreatedAt = DateTime.UtcNow
        };
        db.Goals.Add(goal);
        await db.SaveChangesAsync();

        var svc = new MonthlyPlanService(db);
        var count = await svc.GenerateGoalScheduleAsync("user-1", new GenerateGoalScheduleDto
        {
            GoalId = goal.Id,
            FromAccountId = income.Id,
            ToAccountId = savings.Id,
            MonthlyAmount = 500m,
            StartYear = 2025,
            StartMonth = 1,
            EndYear = 2025,
            EndMonth = 3
        });

        Assert.Equal(3, count);
        Assert.Equal(3, db.PlannedFlows.Count());
        Assert.All(db.PlannedFlows, pf => Assert.Equal(500m, pf.Amount));
        Assert.All(db.PlannedFlows, pf => Assert.Equal(goal.Id, pf.GoalId));
    }

    [Fact]
    public async Task GenerateGoalScheduleAsync_CreatesMonthlyPlans_WhenTheyDoNotExist()
    {
        await using var db = TestDbContextFactory.Create();
        var income  = SeedAccount(db, "user-1", "Income",  AccountType.Income);
        var savings = SeedAccount(db, "user-1", "Savings", AccountType.Savings);
        var goal = new Goal
        {
            Id = Guid.NewGuid(), UserId = "user-1", Name = "Vacation Fund",
            TargetAccountId = savings.Id, TargetAmount = 2_000m,
            CreatedAt = DateTime.UtcNow
        };
        db.Goals.Add(goal);
        await db.SaveChangesAsync();

        var svc = new MonthlyPlanService(db);
        await svc.GenerateGoalScheduleAsync("user-1", new GenerateGoalScheduleDto
        {
            GoalId = goal.Id,
            FromAccountId = income.Id,
            ToAccountId = savings.Id,
            MonthlyAmount = 200m,
            StartYear = 2025,
            StartMonth = 6,
            EndYear = 2025,
            EndMonth = 7
        });

        // Two monthly plans must have been auto-created
        Assert.Equal(2, db.MonthlyPlans.Count());
    }

    [Fact]
    public async Task GenerateGoalScheduleAsync_ReplacesExistingFlows_WhenReplaceExistingIsTrue()
    {
        await using var db = TestDbContextFactory.Create();
        var income  = SeedAccount(db, "user-1", "Income",  AccountType.Income);
        var savings = SeedAccount(db, "user-1", "Savings", AccountType.Savings);
        var goal = new Goal
        {
            Id = Guid.NewGuid(), UserId = "user-1", Name = "Savings Goal",
            TargetAccountId = savings.Id, TargetAmount = 5_000m,
            CreatedAt = DateTime.UtcNow
        };
        db.Goals.Add(goal);
        await db.SaveChangesAsync();

        var svc = new MonthlyPlanService(db);
        // First schedule: 3 months
        await svc.GenerateGoalScheduleAsync("user-1", new GenerateGoalScheduleDto
        {
            GoalId = goal.Id,
            FromAccountId = income.Id,
            ToAccountId = savings.Id,
            MonthlyAmount = 100m,
            StartYear = 2025, StartMonth = 1,
            EndYear = 2025,   EndMonth = 3,
            ReplaceExisting = true
        });

        // Re-generate with ReplaceExisting = true: 2 months replacing 3
        await svc.GenerateGoalScheduleAsync("user-1", new GenerateGoalScheduleDto
        {
            GoalId = goal.Id,
            FromAccountId = income.Id,
            ToAccountId = savings.Id,
            MonthlyAmount = 200m,
            StartYear = 2025, StartMonth = 4,
            EndYear = 2025,   EndMonth = 5,
            ReplaceExisting = true
        });

        // Old 3 flows replaced; 2 new flows
        var flows = db.PlannedFlows.Where(pf => pf.GoalId == goal.Id).ToList();
        Assert.Equal(2, flows.Count);
        Assert.All(flows, pf => Assert.Equal(200m, pf.Amount));
    }

    [Fact]
    public async Task GenerateGoalScheduleAsync_Throws_WhenGoalNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var income  = SeedAccount(db, "user-1", "Income",  AccountType.Income);
        var savings = SeedAccount(db, "user-1", "Savings", AccountType.Savings);
        await db.SaveChangesAsync();

        var svc = new MonthlyPlanService(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.GenerateGoalScheduleAsync("user-1", new GenerateGoalScheduleDto
            {
                GoalId = Guid.NewGuid(),
                FromAccountId = income.Id,
                ToAccountId = savings.Id,
                MonthlyAmount = 100m,
                StartYear = 2025, StartMonth = 1,
                EndYear = 2025,   EndMonth = 1
            }));
    }

    // ── GetGoalContributionsForMonthAsync ─────────────────────────────

    [Fact]
    public async Task GetGoalContributionsForMonthAsync_ReturnsContributionsForMonth()
    {
        await using var db = TestDbContextFactory.Create();
        var income  = SeedAccount(db, "user-1", "Income",  AccountType.Income);
        var savings = SeedAccount(db, "user-1", "Savings", AccountType.Savings);
        var goal = new Goal
        {
            Id = Guid.NewGuid(), UserId = "user-1", Name = "Dream Fund",
            TargetAccountId = savings.Id, TargetAmount = 3_000m,
            CreatedAt = DateTime.UtcNow
        };
        db.Goals.Add(goal);
        await db.SaveChangesAsync();

        var svc = new MonthlyPlanService(db);
        await svc.GenerateGoalScheduleAsync("user-1", new GenerateGoalScheduleDto
        {
            GoalId = goal.Id,
            FromAccountId = income.Id,
            ToAccountId = savings.Id,
            MonthlyAmount = 250m,
            StartYear = 2025, StartMonth = 2,
            EndYear = 2025,   EndMonth = 2
        });

        var contributions = await svc.GetGoalContributionsForMonthAsync("user-1", 2025, 2);

        Assert.Single(contributions);
        Assert.Equal(goal.Id, contributions[0].GoalId);
        Assert.Equal(250m, contributions[0].Amount);
    }

    [Fact]
    public async Task GetGoalContributionsForMonthAsync_ReturnsEmpty_ForMonthWithNoGoalFlows()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new MonthlyPlanService(db);

        var contributions = await svc.GetGoalContributionsForMonthAsync("user-1", 2025, 1);

        Assert.Empty(contributions);
    }
}
