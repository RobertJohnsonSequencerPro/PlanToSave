using PlanToSave.Application.Activities;
using PlanToSave.Domain.Entities;
using PlanToSave.Domain.Enums;
using PlanToSave.Tests.Helpers;
using PlanToSave.Web.Services;

namespace PlanToSave.Tests.Services;

public class ActivityPlanServiceTests
{
    // ── CreateAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_StoresPlanAndSetsIdeaToPlanned()
    {
        await using var db = TestDbContextFactory.Create();
        var idea = SeedIdea(db, "user-1", "Hiking Trip");
        await db.SaveChangesAsync();

        var svc = new ActivityPlanService(db);
        var planId = await svc.CreateAsync("user-1", idea.Id, new CreateActivityPlanDto
        {
            PlannedDate = new DateOnly(2024, 8, 15),
            Notes = "  Bring water  "
        });

        Assert.NotEqual(Guid.Empty, planId);

        var stored = await db.ActivityPlans.FindAsync(planId);
        Assert.NotNull(stored);
        Assert.Equal(idea.Id, stored.IdeaId);
        Assert.Equal(new DateOnly(2024, 8, 15), stored.PlannedDate);
        Assert.Equal("Bring water", stored.Notes);
        Assert.Equal(ActivityPlanStatus.Upcoming, stored.Status);

        // Idea status should be updated to Planned
        var updatedIdea = await db.Ideas.FindAsync(idea.Id);
        Assert.NotNull(updatedIdea);
        Assert.Equal(IdeaStatus.Planned, updatedIdea.Status);
    }

    [Fact]
    public async Task CreateAsync_NullsBlankNotes()
    {
        await using var db = TestDbContextFactory.Create();
        var idea = SeedIdea(db, "user-1", "Trip");
        await db.SaveChangesAsync();

        var svc = new ActivityPlanService(db);
        var planId = await svc.CreateAsync("user-1", idea.Id, new CreateActivityPlanDto
        {
            Notes = "   "
        });

        var stored = await db.ActivityPlans.FindAsync(planId);
        Assert.NotNull(stored);
        Assert.Null(stored.Notes);
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenIdeaNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new ActivityPlanService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateAsync("user-1", Guid.NewGuid(), new CreateActivityPlanDto()));
    }

    // ── GetPlansAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetPlansAsync_ReturnsOnlyUpcomingPlansForUser()
    {
        await using var db = TestDbContextFactory.Create();
        var idea = SeedIdea(db, "user-1", "Idea");
        var ideaOther = SeedIdea(db, "user-2", "Other");
        await db.SaveChangesAsync();

        var svc = new ActivityPlanService(db);
        await svc.CreateAsync("user-1", idea.Id, new CreateActivityPlanDto
        {
            PlannedDate = new DateOnly(2024, 9, 1)
        });
        await svc.CreateAsync("user-2", ideaOther.Id, new CreateActivityPlanDto
        {
            PlannedDate = new DateOnly(2024, 9, 2)
        });

        var results = await svc.GetPlansAsync("user-1");

        Assert.Single(results);
        Assert.Equal("Idea", results[0].IdeaTitle);
    }

    // ── GetPlanAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetPlanAsync_ReturnsPlan_WhenFound()
    {
        await using var db = TestDbContextFactory.Create();
        var idea = SeedIdea(db, "user-1", "Concert");
        await db.SaveChangesAsync();

        var svc = new ActivityPlanService(db);
        var planId = await svc.CreateAsync("user-1", idea.Id, new CreateActivityPlanDto());

        var dto = await svc.GetPlanAsync("user-1", planId);

        Assert.NotNull(dto);
        Assert.Equal(planId, dto.Id);
    }

    [Fact]
    public async Task GetPlanAsync_ReturnsNull_WhenNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new ActivityPlanService(db);

        var dto = await svc.GetPlanAsync("user-1", Guid.NewGuid());

        Assert.Null(dto);
    }

    // ── GetPlansForMonthAsync ─────────────────────────────────────────

    [Fact]
    public async Task GetPlansForMonthAsync_ReturnsPlansInSpecifiedMonth()
    {
        await using var db = TestDbContextFactory.Create();
        var idea1 = SeedIdea(db, "user-1", "August Trip");
        var idea2 = SeedIdea(db, "user-1", "September Trip");
        await db.SaveChangesAsync();

        var svc = new ActivityPlanService(db);
        await svc.CreateAsync("user-1", idea1.Id, new CreateActivityPlanDto
        {
            PlannedDate = new DateOnly(2024, 8, 10)
        });
        await svc.CreateAsync("user-1", idea2.Id, new CreateActivityPlanDto
        {
            PlannedDate = new DateOnly(2024, 9, 5)
        });

        var august = await svc.GetPlansForMonthAsync("user-1", 2024, 8);

        Assert.Single(august);
        Assert.Equal("August Trip", august[0].IdeaTitle);
    }

    // ── DeleteAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_RemovesPlan()
    {
        await using var db = TestDbContextFactory.Create();
        var idea = SeedIdea(db, "user-1", "Trip");
        await db.SaveChangesAsync();

        var svc = new ActivityPlanService(db);
        var planId = await svc.CreateAsync("user-1", idea.Id, new CreateActivityPlanDto());

        await svc.DeleteAsync("user-1", planId);

        Assert.Null(await db.ActivityPlans.FindAsync(planId));
    }

    [Fact]
    public async Task DeleteAsync_Reverts_IdeaToBacklog_WhenNoOtherUpcomingPlans()
    {
        await using var db = TestDbContextFactory.Create();
        var idea = SeedIdea(db, "user-1", "Trip");
        await db.SaveChangesAsync();

        var svc = new ActivityPlanService(db);
        var planId = await svc.CreateAsync("user-1", idea.Id, new CreateActivityPlanDto());

        // At this point idea.Status == Planned
        await svc.DeleteAsync("user-1", planId);

        var updatedIdea = await db.Ideas.FindAsync(idea.Id);
        Assert.NotNull(updatedIdea);
        Assert.Equal(IdeaStatus.Backlog, updatedIdea.Status);
    }

    [Fact]
    public async Task DeleteAsync_KeepsIdeaAsPlanned_WhenOtherUpcomingPlansExist()
    {
        await using var db = TestDbContextFactory.Create();
        var idea = SeedIdea(db, "user-1", "Trip");
        await db.SaveChangesAsync();

        var svc = new ActivityPlanService(db);
        var planId1 = await svc.CreateAsync("user-1", idea.Id, new CreateActivityPlanDto
        {
            PlannedDate = new DateOnly(2024, 7, 1)
        });
        await svc.CreateAsync("user-1", idea.Id, new CreateActivityPlanDto
        {
            PlannedDate = new DateOnly(2024, 8, 1)
        });

        await svc.DeleteAsync("user-1", planId1);

        var updatedIdea = await db.Ideas.FindAsync(idea.Id);
        Assert.NotNull(updatedIdea);
        Assert.Equal(IdeaStatus.Planned, updatedIdea.Status);
    }

    [Fact]
    public async Task DeleteAsync_Throws_WhenPlanNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new ActivityPlanService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.DeleteAsync("user-1", Guid.NewGuid()));
    }

    // ── Step management ───────────────────────────────────────────────

    [Fact]
    public async Task AddStepAsync_CreatesStep_WithIncrementingOrder()
    {
        await using var db = TestDbContextFactory.Create();
        var idea = SeedIdea(db, "user-1", "Trip");
        await db.SaveChangesAsync();

        var svc = new ActivityPlanService(db);
        var planId = await svc.CreateAsync("user-1", idea.Id, new CreateActivityPlanDto());

        var step1Id = await svc.AddStepAsync("user-1", planId, "  Pack bags  ");
        var step2Id = await svc.AddStepAsync("user-1", planId, "Book hotel");

        var step1 = await db.ActivitySteps.FindAsync(step1Id);
        var step2 = await db.ActivitySteps.FindAsync(step2Id);

        Assert.NotNull(step1);
        Assert.NotNull(step2);
        Assert.Equal("Pack bags", step1.Title);   // trimmed
        Assert.Equal(1, step1.SortOrder);
        Assert.Equal(2, step2.SortOrder);
        Assert.False(step1.IsComplete);
    }

    [Fact]
    public async Task AddStepAsync_Throws_WhenPlanNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new ActivityPlanService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AddStepAsync("user-1", Guid.NewGuid(), "Some step"));
    }

    [Fact]
    public async Task ToggleStepAsync_TogglesIsComplete_AndSetsCompletedAt()
    {
        await using var db = TestDbContextFactory.Create();
        var idea = SeedIdea(db, "user-1", "Trip");
        await db.SaveChangesAsync();

        var svc = new ActivityPlanService(db);
        var planId = await svc.CreateAsync("user-1", idea.Id, new CreateActivityPlanDto());
        var stepId = await svc.AddStepAsync("user-1", planId, "Reserve tickets");

        // Toggle ON
        await svc.ToggleStepAsync("user-1", stepId);
        var afterOn = await db.ActivitySteps.FindAsync(stepId);
        Assert.NotNull(afterOn);
        Assert.True(afterOn.IsComplete);
        Assert.NotNull(afterOn.CompletedAt);

        // Toggle OFF
        await svc.ToggleStepAsync("user-1", stepId);
        var afterOff = await db.ActivitySteps.FindAsync(stepId);
        Assert.NotNull(afterOff);
        Assert.False(afterOff.IsComplete);
        Assert.Null(afterOff.CompletedAt);
    }

    [Fact]
    public async Task ToggleStepAsync_Throws_WhenStepNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new ActivityPlanService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ToggleStepAsync("user-1", Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteStepAsync_RemovesStep()
    {
        await using var db = TestDbContextFactory.Create();
        var idea = SeedIdea(db, "user-1", "Trip");
        await db.SaveChangesAsync();

        var svc = new ActivityPlanService(db);
        var planId = await svc.CreateAsync("user-1", idea.Id, new CreateActivityPlanDto());
        var stepId = await svc.AddStepAsync("user-1", planId, "Step to delete");

        await svc.DeleteStepAsync("user-1", stepId);

        Assert.Null(await db.ActivitySteps.FindAsync(stepId));
    }

    [Fact]
    public async Task DeleteStepAsync_Throws_WhenStepNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new ActivityPlanService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.DeleteStepAsync("user-1", Guid.NewGuid()));
    }

    // ── LinkBudgetAsync ───────────────────────────────────────────────

    [Fact]
    public async Task LinkBudgetAsync_SetsPlannedFlowId()
    {
        await using var db = TestDbContextFactory.Create();
        var idea    = SeedIdea(db, "user-1", "Trip");
        var income  = SeedAccount(db, "user-1", "Income",  AccountType.Income);
        var savings = SeedAccount(db, "user-1", "Savings", AccountType.Savings);
        var monthlyPlan = new MonthlyPlan
        {
            Id = Guid.NewGuid(), UserId = "user-1",
            Year = 2024, Month = 8, Status = MonthlyPlanStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };
        db.MonthlyPlans.Add(monthlyPlan);
        var pf = new PlannedFlow
        {
            Id = Guid.NewGuid(),
            MonthlyPlanId = monthlyPlan.Id,
            FromAccountId = income.Id,
            ToAccountId = savings.Id,
            Amount = 500m
        };
        db.PlannedFlows.Add(pf);
        await db.SaveChangesAsync();

        var svc = new ActivityPlanService(db);
        var planId = await svc.CreateAsync("user-1", idea.Id, new CreateActivityPlanDto());

        await svc.LinkBudgetAsync("user-1", planId, pf.Id);

        var stored = await db.ActivityPlans.FindAsync(planId);
        Assert.NotNull(stored);
        Assert.Equal(pf.Id, stored.PlannedFlowId);
    }

    [Fact]
    public async Task LinkBudgetAsync_UnlinksWhenNullPassed()
    {
        await using var db = TestDbContextFactory.Create();
        var idea    = SeedIdea(db, "user-1", "Trip");
        var income  = SeedAccount(db, "user-1", "Income",  AccountType.Income);
        var savings = SeedAccount(db, "user-1", "Savings", AccountType.Savings);
        var monthlyPlan = new MonthlyPlan
        {
            Id = Guid.NewGuid(), UserId = "user-1",
            Year = 2024, Month = 9, Status = MonthlyPlanStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };
        db.MonthlyPlans.Add(monthlyPlan);
        var pf = new PlannedFlow
        {
            Id = Guid.NewGuid(),
            MonthlyPlanId = monthlyPlan.Id,
            FromAccountId = income.Id,
            ToAccountId = savings.Id,
            Amount = 500m
        };
        db.PlannedFlows.Add(pf);
        await db.SaveChangesAsync();

        var svc = new ActivityPlanService(db);
        var planId = await svc.CreateAsync("user-1", idea.Id, new CreateActivityPlanDto());
        await svc.LinkBudgetAsync("user-1", planId, pf.Id);

        // Now unlink
        await svc.LinkBudgetAsync("user-1", planId, null);

        var stored = await db.ActivityPlans.FindAsync(planId);
        Assert.NotNull(stored);
        Assert.Null(stored.PlannedFlowId);
    }

    [Fact]
    public async Task LinkBudgetAsync_Throws_WhenPlanNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new ActivityPlanService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.LinkBudgetAsync("user-1", Guid.NewGuid(), null));
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static Idea SeedIdea(
        PlanToSave.Web.Data.ApplicationDbContext db,
        string userId, string title,
        IdeaStatus status = IdeaStatus.Backlog)
    {
        var idea = new Idea
        {
            Id = Guid.NewGuid(), UserId = userId,
            Title = title, Category = IdeaCategory.Other,
            EnergyLevel = IdeaEnergyLevel.Medium,
            CostEstimate = IdeaCostEstimate.Cheap,
            Status = status, CreatedAt = DateTime.UtcNow
        };
        db.Ideas.Add(idea);
        return idea;
    }

    private static Account SeedAccount(
        PlanToSave.Web.Data.ApplicationDbContext db,
        string userId, string name, AccountType type)
    {
        var a = new Account
        {
            Id = Guid.NewGuid(), UserId = userId,
            Name = name, Type = type, CreatedAt = DateTime.UtcNow
        };
        db.Accounts.Add(a);
        return a;
    }
}
