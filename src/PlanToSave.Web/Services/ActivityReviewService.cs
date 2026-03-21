using Microsoft.EntityFrameworkCore;
using PlanToSave.Application.Activities;
using PlanToSave.Domain.Entities;
using PlanToSave.Domain.Enums;
using PlanToSave.Web.Data;

namespace PlanToSave.Web.Services;

public class ActivityReviewService(ApplicationDbContext db) : IActivityReviewService
{
    // Reuse the same DTO projection as ActivityPlanService
    private static ActivityPlanDto ToDto(ActivityPlan p) => new(
        p.Id,
        p.IdeaId,
        p.Idea.Title,
        p.Idea.Category,
        p.PlannedDate,
        p.Status,
        p.Notes,
        p.CompletedDate,
        p.Steps.Count,
        p.Steps.Count(s => s.IsComplete),
        p.CreatedAt,
        p.Steps
            .OrderBy(s => s.SortOrder)
            .Select(s => new ActivityStepDto(s.Id, s.Title, s.SortOrder, s.IsComplete, s.CompletedAt))
            .ToList());

    public async Task<List<ActivityPlanDto>> GetNeedsReviewAsync(string userId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var plans = await db.ActivityPlans
            .Include(p => p.Idea)
            .Include(p => p.Steps)
            .Where(p => p.UserId == userId
                     && p.Status == ActivityPlanStatus.Upcoming
                     && p.PlannedDate.HasValue
                     && p.PlannedDate < today)
            .OrderBy(p => p.PlannedDate)
            .ToListAsync();

        return plans.Select(ToDto).ToList();
    }

    public async Task SubmitAsync(string userId, Guid planId, SubmitReviewDto dto)
    {
        var plan = await db.ActivityPlans
            .Include(p => p.Idea)
            .FirstOrDefaultAsync(p => p.Id == planId && p.UserId == userId)
            ?? throw new InvalidOperationException("Plan not found.");

        // Save the review
        var review = new ActivityReview
        {
            Id             = Guid.NewGuid(),
            ActivityPlanId = planId,
            Rating         = dto.Rating,
            Reflection     = string.IsNullOrWhiteSpace(dto.Reflection) ? null : dto.Reflection.Trim(),
            ActualAmount   = dto.ActualAmount,
            CreatedAt      = DateTime.UtcNow
        };
        db.ActivityReviews.Add(review);

        // Mark plan Done
        plan.Status        = ActivityPlanStatus.Done;
        plan.CompletedDate = DateOnly.FromDateTime(DateTime.UtcNow);

        // Mark idea Done
        plan.Idea.Status = IdeaStatus.Done;

        await db.SaveChangesAsync();
    }

    public async Task SkipAsync(string userId, Guid planId)
    {
        var plan = await db.ActivityPlans
            .Include(p => p.Idea)
            .FirstOrDefaultAsync(p => p.Id == planId && p.UserId == userId)
            ?? throw new InvalidOperationException("Plan not found.");

        plan.Status = ActivityPlanStatus.Skipped;

        // Return idea to Backlog so it can be rescheduled
        plan.Idea.Status = IdeaStatus.Backlog;

        await db.SaveChangesAsync();
    }
}
