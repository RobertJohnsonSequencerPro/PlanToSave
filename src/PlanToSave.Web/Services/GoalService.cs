using Microsoft.EntityFrameworkCore;
using PlanToSave.Application.Goals;
using PlanToSave.Domain.Entities;
using PlanToSave.Web.Data;

namespace PlanToSave.Web.Services;

public class GoalService(ApplicationDbContext db) : IGoalService
{
    public async Task<List<GoalDto>> GetGoalsAsync(string userId)
    {
        var goals = await db.Goals
            .Include(g => g.TargetAccount)
            .Include(g => g.SourceAccount)
            .Include(g => g.Idea)
            .Where(g => g.UserId == userId)
            .OrderBy(g => g.IsComplete)
            .ThenBy(g => g.TargetDate)
            .ToListAsync();

        // Compute saved amount: sum of actual flows INTO the target account since StartDate
        var goalIds = goals.Select(g => g.TargetAccountId).Distinct().ToList();
        var flowTotals = await db.ActualFlows
            .Where(f => f.UserId == userId && goalIds.Contains(f.ToAccountId))
            .GroupBy(f => f.ToAccountId)
            .Select(g => new { AccountId = g.Key, Total = g.Sum(f => f.Amount) })
            .ToListAsync();

        var totalByAccount = flowTotals.ToDictionary(x => x.AccountId, x => x.Total);

        return goals.Select(g => new GoalDto(
            g.Id,
            g.Name,
            g.Description,
            g.TargetAccountId, g.TargetAccount.Name,
            g.SourceAccountId, g.SourceAccount.Name,
            g.TargetAmount,
            totalByAccount.GetValueOrDefault(g.TargetAccountId, 0m),
            g.StartDate,
            g.TargetDate,
            g.IsComplete,
            g.IdeaId,
            g.Idea?.Title,
            g.CreatedAt)).ToList();
    }

    public async Task<Guid> CreateAsync(string userId, CreateGoalDto dto)
    {
        var targetAccount = await db.Accounts
            .FirstOrDefaultAsync(a => a.Id == dto.TargetAccountId && a.UserId == userId)
            ?? throw new InvalidOperationException("Target account not found.");

        var sourceAccount = await db.Accounts
            .FirstOrDefaultAsync(a => a.Id == dto.SourceAccountId && a.UserId == userId)
            ?? throw new InvalidOperationException("Source account not found.");

        if (dto.TargetDate <= dto.StartDate)
            throw new InvalidOperationException("Target date must be after start date.");

        if (dto.IdeaId.HasValue && dto.IdeaId.Value != Guid.Empty)
        {
            var idea = await db.Ideas.AnyAsync(i => i.Id == dto.IdeaId.Value && i.UserId == userId);
            if (!idea) throw new InvalidOperationException("Idea not found.");
        }

        var goal = new Goal
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = dto.Name,
            Description = dto.Description,
            TargetAccountId = dto.TargetAccountId,
            SourceAccountId = dto.SourceAccountId,
            TargetAmount = dto.TargetAmount,
            StartDate = dto.StartDate,
            TargetDate = dto.TargetDate,
            IdeaId = dto.IdeaId == Guid.Empty ? null : dto.IdeaId,
            IsComplete = false,
            CreatedAt = DateTime.UtcNow
        };
        db.Goals.Add(goal);
        await db.SaveChangesAsync();
        return goal.Id;
    }

    public async Task MarkCompleteAsync(string userId, Guid goalId)
    {
        var goal = await db.Goals
            .FirstOrDefaultAsync(g => g.Id == goalId && g.UserId == userId)
            ?? throw new InvalidOperationException("Goal not found.");

        goal.IsComplete = !goal.IsComplete;
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(string userId, Guid goalId)
    {
        var goal = await db.Goals
            .FirstOrDefaultAsync(g => g.Id == goalId && g.UserId == userId)
            ?? throw new InvalidOperationException("Goal not found.");

        db.Goals.Remove(goal);
        await db.SaveChangesAsync();
    }
}
