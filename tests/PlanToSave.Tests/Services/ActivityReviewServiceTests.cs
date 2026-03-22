using PlanToSave.Application.Activities;
using PlanToSave.Domain.Entities;
using PlanToSave.Domain.Enums;
using PlanToSave.Tests.Helpers;
using PlanToSave.Web.Services;

namespace PlanToSave.Tests.Services;

public class ActivityReviewServiceTests
{
    // ── GetNeedsReviewAsync ───────────────────────────────────────────

    [Fact]
    public async Task GetNeedsReviewAsync_ReturnsOverduePlans()
    {
        await using var db = TestDbContextFactory.Create();
        var idea = SeedIdea(db, "user-1", "Past Trip");
        await db.SaveChangesAsync();

        // Plan with a date well in the past
        db.ActivityPlans.Add(new ActivityPlan
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            IdeaId = idea.Id,
            Status = ActivityPlanStatus.Upcoming,
            PlannedDate = new DateOnly(2020, 1, 1),
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var svc = new ActivityReviewService(db);
        var results = await svc.GetNeedsReviewAsync("user-1");

        Assert.Single(results);
    }

    [Fact]
    public async Task GetNeedsReviewAsync_ExcludesFuturePlans()
    {
        await using var db = TestDbContextFactory.Create();
        var idea = SeedIdea(db, "user-1", "Future Trip");
        await db.SaveChangesAsync();

        // Plan date is far in the future
        db.ActivityPlans.Add(new ActivityPlan
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            IdeaId = idea.Id,
            Status = ActivityPlanStatus.Upcoming,
            PlannedDate = new DateOnly(2099, 12, 31),
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var svc = new ActivityReviewService(db);
        var results = await svc.GetNeedsReviewAsync("user-1");

        Assert.Empty(results);
    }

    [Fact]
    public async Task GetNeedsReviewAsync_ExcludesNonUpcomingStatuses()
    {
        await using var db = TestDbContextFactory.Create();
        var idea1 = SeedIdea(db, "user-1", "Done Trip");
        var idea2 = SeedIdea(db, "user-1", "Skipped Trip");
        await db.SaveChangesAsync();

        db.ActivityPlans.AddRange(
            new ActivityPlan
            {
                Id = Guid.NewGuid(), UserId = "user-1", IdeaId = idea1.Id,
                Status = ActivityPlanStatus.Done,
                PlannedDate = new DateOnly(2020, 1, 1), CreatedAt = DateTime.UtcNow
            },
            new ActivityPlan
            {
                Id = Guid.NewGuid(), UserId = "user-1", IdeaId = idea2.Id,
                Status = ActivityPlanStatus.Skipped,
                PlannedDate = new DateOnly(2020, 1, 1), CreatedAt = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        var svc = new ActivityReviewService(db);
        var results = await svc.GetNeedsReviewAsync("user-1");

        Assert.Empty(results);
    }

    [Fact]
    public async Task GetNeedsReviewAsync_ExcludesNullPlannedDate()
    {
        await using var db = TestDbContextFactory.Create();
        var idea = SeedIdea(db, "user-1", "No Date Trip");
        await db.SaveChangesAsync();

        db.ActivityPlans.Add(new ActivityPlan
        {
            Id = Guid.NewGuid(), UserId = "user-1", IdeaId = idea.Id,
            Status = ActivityPlanStatus.Upcoming,
            PlannedDate = null,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var svc = new ActivityReviewService(db);
        var results = await svc.GetNeedsReviewAsync("user-1");

        Assert.Empty(results);
    }

    [Fact]
    public async Task GetNeedsReviewAsync_IsolatesUserData()
    {
        await using var db = TestDbContextFactory.Create();
        var idea1 = SeedIdea(db, "user-1", "My Trip");
        var idea2 = SeedIdea(db, "user-2", "Their Trip");
        await db.SaveChangesAsync();

        db.ActivityPlans.AddRange(
            new ActivityPlan
            {
                Id = Guid.NewGuid(), UserId = "user-1", IdeaId = idea1.Id,
                Status = ActivityPlanStatus.Upcoming,
                PlannedDate = new DateOnly(2020, 1, 1), CreatedAt = DateTime.UtcNow
            },
            new ActivityPlan
            {
                Id = Guid.NewGuid(), UserId = "user-2", IdeaId = idea2.Id,
                Status = ActivityPlanStatus.Upcoming,
                PlannedDate = new DateOnly(2020, 1, 1), CreatedAt = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        var svc = new ActivityReviewService(db);
        var results = await svc.GetNeedsReviewAsync("user-1");

        Assert.Single(results);
        Assert.Equal("My Trip", results[0].IdeaTitle);
    }

    // ── SubmitAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task SubmitAsync_MarksPlanDoneAndSavesReview()
    {
        await using var db = TestDbContextFactory.Create();
        var idea = SeedIdea(db, "user-1", "Concert");
        var plan = new ActivityPlan
        {
            Id = Guid.NewGuid(), UserId = "user-1", IdeaId = idea.Id,
            Status = ActivityPlanStatus.Upcoming,
            PlannedDate = new DateOnly(2020, 6, 1),
            CreatedAt = DateTime.UtcNow
        };
        db.ActivityPlans.Add(plan);
        await db.SaveChangesAsync();

        var svc = new ActivityReviewService(db);
        await svc.SubmitAsync("user-1", plan.Id, new SubmitReviewDto
        {
            Rating = 5,
            Reflection = "  Amazing!  ",
            ActualAmount = 120m
        });

        var storedPlan = await db.ActivityPlans.FindAsync(plan.Id);
        Assert.NotNull(storedPlan);
        Assert.Equal(ActivityPlanStatus.Done, storedPlan.Status);
        Assert.NotNull(storedPlan.CompletedDate);

        var review = db.ActivityReviews.Single(r => r.ActivityPlanId == plan.Id);
        Assert.Equal(5, review.Rating);
        Assert.Equal("Amazing!", review.Reflection);
        Assert.Equal(120m, review.ActualAmount);
    }

    [Fact]
    public async Task SubmitAsync_MarksIdeaDone()
    {
        await using var db = TestDbContextFactory.Create();
        var idea = SeedIdea(db, "user-1", "Hike");
        var plan = new ActivityPlan
        {
            Id = Guid.NewGuid(), UserId = "user-1", IdeaId = idea.Id,
            Status = ActivityPlanStatus.Upcoming,
            PlannedDate = new DateOnly(2020, 8, 1),
            CreatedAt = DateTime.UtcNow
        };
        db.ActivityPlans.Add(plan);
        await db.SaveChangesAsync();

        var svc = new ActivityReviewService(db);
        await svc.SubmitAsync("user-1", plan.Id, new SubmitReviewDto());

        var updatedIdea = await db.Ideas.FindAsync(idea.Id);
        Assert.NotNull(updatedIdea);
        Assert.Equal(IdeaStatus.Done, updatedIdea.Status);
    }

    [Fact]
    public async Task SubmitAsync_NullsBlankReflection()
    {
        await using var db = TestDbContextFactory.Create();
        var idea = SeedIdea(db, "user-1", "Concert");
        var plan = new ActivityPlan
        {
            Id = Guid.NewGuid(), UserId = "user-1", IdeaId = idea.Id,
            Status = ActivityPlanStatus.Upcoming,
            PlannedDate = new DateOnly(2020, 5, 1),
            CreatedAt = DateTime.UtcNow
        };
        db.ActivityPlans.Add(plan);
        await db.SaveChangesAsync();

        var svc = new ActivityReviewService(db);
        await svc.SubmitAsync("user-1", plan.Id, new SubmitReviewDto { Reflection = "   " });

        var review = db.ActivityReviews.Single();
        Assert.Null(review.Reflection);
    }

    [Fact]
    public async Task SubmitAsync_Throws_WhenPlanNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new ActivityReviewService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.SubmitAsync("user-1", Guid.NewGuid(), new SubmitReviewDto()));
    }

    // ── SkipAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task SkipAsync_MarksPlanSkipped_AndIdeaBacklog()
    {
        await using var db = TestDbContextFactory.Create();
        var idea = SeedIdea(db, "user-1", "Road Trip", IdeaStatus.Planned);
        var plan = new ActivityPlan
        {
            Id = Guid.NewGuid(), UserId = "user-1", IdeaId = idea.Id,
            Status = ActivityPlanStatus.Upcoming,
            PlannedDate = new DateOnly(2020, 3, 1),
            CreatedAt = DateTime.UtcNow
        };
        db.ActivityPlans.Add(plan);
        await db.SaveChangesAsync();

        var svc = new ActivityReviewService(db);
        await svc.SkipAsync("user-1", plan.Id);

        var storedPlan = await db.ActivityPlans.FindAsync(plan.Id);
        Assert.NotNull(storedPlan);
        Assert.Equal(ActivityPlanStatus.Skipped, storedPlan.Status);

        var updatedIdea = await db.Ideas.FindAsync(idea.Id);
        Assert.NotNull(updatedIdea);
        Assert.Equal(IdeaStatus.Backlog, updatedIdea.Status);
    }

    [Fact]
    public async Task SkipAsync_Throws_WhenPlanNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new ActivityReviewService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.SkipAsync("user-1", Guid.NewGuid()));
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
}
