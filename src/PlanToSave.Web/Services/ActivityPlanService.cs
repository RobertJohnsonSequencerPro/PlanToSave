using Microsoft.EntityFrameworkCore;
using PlanToSave.Application.Activities;
using PlanToSave.Domain.Entities;
using PlanToSave.Domain.Enums;
using PlanToSave.Web.Data;

namespace PlanToSave.Web.Services;

public class ActivityPlanService(ApplicationDbContext db) : IActivityPlanService
{
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

    public async Task<List<ActivityPlanDto>> GetPlansAsync(string userId)
    {
        var plans = await db.ActivityPlans
            .Include(p => p.Idea)
            .Include(p => p.Steps)
            .Where(p => p.UserId == userId && p.Status == ActivityPlanStatus.Upcoming)
            .ToListAsync();

        return plans
            .OrderBy(p => p.PlannedDate.HasValue ? 0 : 1)
            .ThenBy(p => p.PlannedDate)
            .Select(ToDto)
            .ToList();
    }

    public async Task<ActivityPlanDto?> GetPlanAsync(string userId, Guid id)
    {
        var plan = await db.ActivityPlans
            .Include(p => p.Idea)
            .Include(p => p.Steps)
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);

        return plan is null ? null : ToDto(plan);
    }

    public async Task<Guid> CreateAsync(string userId, Guid ideaId, CreateActivityPlanDto dto)
    {
        var idea = await db.Ideas
            .FirstOrDefaultAsync(i => i.Id == ideaId && i.UserId == userId)
            ?? throw new InvalidOperationException("Idea not found.");

        var plan = new ActivityPlan
        {
            Id          = Guid.NewGuid(),
            UserId      = userId,
            IdeaId      = ideaId,
            PlannedDate = dto.PlannedDate,
            Notes       = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
            Status      = ActivityPlanStatus.Upcoming,
            CreatedAt   = DateTime.UtcNow
        };

        idea.Status = IdeaStatus.Planned;

        db.ActivityPlans.Add(plan);
        await db.SaveChangesAsync();
        return plan.Id;
    }

    public async Task DeleteAsync(string userId, Guid id)
    {
        var plan = await db.ActivityPlans
            .Include(p => p.Idea)
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId)
            ?? throw new InvalidOperationException("Plan not found.");

        db.ActivityPlans.Remove(plan);

        // If no other active plans exist for this idea, return it to Backlog
        var hasOtherPlans = await db.ActivityPlans
            .AnyAsync(p => p.IdeaId == plan.IdeaId && p.UserId == userId
                           && p.Id != id && p.Status == ActivityPlanStatus.Upcoming);

        if (!hasOtherPlans && plan.Idea.Status == IdeaStatus.Planned)
            plan.Idea.Status = IdeaStatus.Backlog;

        await db.SaveChangesAsync();
    }

    public async Task<Guid> AddStepAsync(string userId, Guid planId, string title)
    {
        // Verify plan ownership
        _ = await db.ActivityPlans
            .FirstOrDefaultAsync(p => p.Id == planId && p.UserId == userId)
            ?? throw new InvalidOperationException("Plan not found.");

        var maxOrder = await db.ActivitySteps
            .Where(s => s.ActivityPlanId == planId)
            .MaxAsync(s => (int?)s.SortOrder) ?? 0;

        var step = new ActivityStep
        {
            Id             = Guid.NewGuid(),
            ActivityPlanId = planId,
            Title          = title.Trim(),
            SortOrder      = maxOrder + 1,
            IsComplete     = false
        };

        db.ActivitySteps.Add(step);
        await db.SaveChangesAsync();
        return step.Id;
    }

    public async Task ToggleStepAsync(string userId, Guid stepId)
    {
        var step = await db.ActivitySteps
            .Include(s => s.ActivityPlan)
            .FirstOrDefaultAsync(s => s.Id == stepId && s.ActivityPlan.UserId == userId)
            ?? throw new InvalidOperationException("Step not found.");

        step.IsComplete = !step.IsComplete;
        step.CompletedAt = step.IsComplete ? DateTimeOffset.UtcNow : null;
        await db.SaveChangesAsync();
    }

    public async Task DeleteStepAsync(string userId, Guid stepId)
    {
        var step = await db.ActivitySteps
            .Include(s => s.ActivityPlan)
            .FirstOrDefaultAsync(s => s.Id == stepId && s.ActivityPlan.UserId == userId)
            ?? throw new InvalidOperationException("Step not found.");

        db.ActivitySteps.Remove(step);
        await db.SaveChangesAsync();
    }
}
