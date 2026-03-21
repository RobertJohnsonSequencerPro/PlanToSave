using Microsoft.EntityFrameworkCore;
using PlanToSave.Application.Plans;
using PlanToSave.Domain.Entities;
using PlanToSave.Domain.Enums;
using PlanToSave.Web.Data;

namespace PlanToSave.Web.Services;

public class MonthlyPlanService(ApplicationDbContext db) : IMonthlyPlanService
{
    public async Task<List<MonthlyPlanSummaryDto>> GetPlansAsync(string userId)
    {
        var plans = await db.MonthlyPlans
            .Include(p => p.PlannedFlows)
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.Year).ThenByDescending(p => p.Month)
            .ToListAsync();

        // Build date ranges for each plan to sum actual flows
        var actuals = await db.ActualFlows
            .Where(f => f.UserId == userId)
            .Select(f => new { f.Date, f.Amount })
            .ToListAsync();

        return plans.Select(p =>
        {
            var monthStart = new DateOnly(p.Year, p.Month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            var actualTotal = actuals
                .Where(f => f.Date >= monthStart && f.Date <= monthEnd)
                .Sum(f => f.Amount);
            return new MonthlyPlanSummaryDto(
                p.Id, p.Year, p.Month, p.Status,
                p.PlannedFlows.Sum(pf => pf.Amount),
                actualTotal,
                p.PlannedFlows.Count);
        }).ToList();
    }

    public async Task<MonthlyPlanDetailDto?> GetPlanDetailAsync(string userId, int year, int month)
    {
        var plan = await db.MonthlyPlans
            .Include(p => p.PlannedFlows)
                .ThenInclude(pf => pf.FromAccount)
            .Include(p => p.PlannedFlows)
                .ThenInclude(pf => pf.ToAccount)
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Year == year && p.Month == month);

        if (plan is null) return null;
        return await BuildDetailDto(userId, plan, year, month);
    }

    public async Task<MonthlyPlanDetailDto> GetOrCreatePlanAsync(string userId, int year, int month)
    {
        var plan = await db.MonthlyPlans
            .Include(p => p.PlannedFlows)
                .ThenInclude(pf => pf.FromAccount)
            .Include(p => p.PlannedFlows)
                .ThenInclude(pf => pf.ToAccount)
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Year == year && p.Month == month);

        if (plan is null)
        {
            plan = new MonthlyPlan
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Year = year,
                Month = month,
                Status = MonthlyPlanStatus.Draft,
                CreatedAt = DateTime.UtcNow
            };
            db.MonthlyPlans.Add(plan);
            await db.SaveChangesAsync();
        }

        return await BuildDetailDto(userId, plan, year, month);
    }

    public async Task AddPlannedFlowAsync(string userId, Guid planId, CreatePlannedFlowDto dto)
    {
        var plan = await db.MonthlyPlans
            .FirstOrDefaultAsync(p => p.Id == planId && p.UserId == userId)
            ?? throw new InvalidOperationException("Plan not found.");

        var fromAccount = await db.Accounts
            .FirstOrDefaultAsync(a => a.Id == dto.FromAccountId && a.UserId == userId)
            ?? throw new InvalidOperationException("From account not found.");

        var toAccount = await db.Accounts
            .FirstOrDefaultAsync(a => a.Id == dto.ToAccountId && a.UserId == userId)
            ?? throw new InvalidOperationException("To account not found.");

        if (fromAccount.Type == AccountType.Expense)
            throw new InvalidOperationException("Expense accounts cannot be the source of a flow.");
        if (toAccount.Type == AccountType.Income)
            throw new InvalidOperationException("Income accounts cannot be the destination of a flow.");
        if (dto.FromAccountId == dto.ToAccountId)
            throw new InvalidOperationException("From and To accounts must be different.");

        var pf = new PlannedFlow
        {
            Id = Guid.NewGuid(),
            MonthlyPlanId = planId,
            FromAccountId = dto.FromAccountId,
            ToAccountId = dto.ToAccountId,
            Amount = dto.Amount,
            Description = dto.Description
        };
        db.PlannedFlows.Add(pf);
        await db.SaveChangesAsync();
    }

    public async Task DeletePlannedFlowAsync(string userId, Guid plannedFlowId)
    {
        var pf = await db.PlannedFlows
            .Include(pf => pf.MonthlyPlan)
            .FirstOrDefaultAsync(pf => pf.Id == plannedFlowId && pf.MonthlyPlan.UserId == userId)
            ?? throw new InvalidOperationException("Planned flow not found.");

        db.PlannedFlows.Remove(pf);
        await db.SaveChangesAsync();
    }

    public async Task SetStatusAsync(string userId, Guid planId, MonthlyPlanStatus status)
    {
        var plan = await db.MonthlyPlans
            .FirstOrDefaultAsync(p => p.Id == planId && p.UserId == userId)
            ?? throw new InvalidOperationException("Plan not found.");

        plan.Status = status;
        await db.SaveChangesAsync();
    }

    public async Task SeedFromTemplatesAsync(string userId, Guid planId)
    {
        var plan = await db.MonthlyPlans
            .Include(p => p.PlannedFlows)
            .FirstOrDefaultAsync(p => p.Id == planId && p.UserId == userId)
            ?? throw new InvalidOperationException("Plan not found.");

        var templates = await db.FlowTemplates
            .Where(t => t.UserId == userId && t.IsActive)
            .ToListAsync();

        var existingPairs = plan.PlannedFlows
            .Select(pf => (pf.FromAccountId, pf.ToAccountId, pf.Description))
            .ToHashSet();

        foreach (var t in templates)
        {
            // Skip if an identical row already exists
            if (existingPairs.Contains((t.FromAccountId, t.ToAccountId, t.Description)))
                continue;

            db.PlannedFlows.Add(new PlannedFlow
            {
                Id = Guid.NewGuid(),
                MonthlyPlanId = planId,
                FromAccountId = t.FromAccountId,
                ToAccountId = t.ToAccountId,
                Amount = t.Amount,
                Description = t.Description,
                TemplateId = t.Id
            });
        }
        await db.SaveChangesAsync();
    }

    // ──────────────────────────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────────────────────────

    private async Task<MonthlyPlanDetailDto> BuildDetailDto(
        string userId, MonthlyPlan plan, int year, int month)
    {
        // Pull all actual flows for this month to compute actuals per planned-flow pair
        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var actuals = await db.ActualFlows
            .Where(f => f.UserId == userId && f.Date >= monthStart && f.Date <= monthEnd)
            .Select(f => new { f.FromAccountId, f.ToAccountId, f.Amount })
            .ToListAsync();

        var plannedFlows = plan.PlannedFlows.Select(pf =>
        {
            var actual = actuals
                .Where(a => a.FromAccountId == pf.FromAccountId && a.ToAccountId == pf.ToAccountId)
                .Sum(a => a.Amount);

            return new PlannedFlowDto(
                pf.Id,
                pf.FromAccountId, pf.FromAccount.Name, pf.FromAccount.Type,
                pf.ToAccountId, pf.ToAccount.Name, pf.ToAccount.Type,
                pf.Amount, actual,
                pf.Description);
        }).ToList();

        return new MonthlyPlanDetailDto(plan.Id, year, month, plan.Status, plannedFlows);
    }
}
