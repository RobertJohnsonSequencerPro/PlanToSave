using Microsoft.EntityFrameworkCore;
using PlanToSave.Application.Flows;
using PlanToSave.Application.Templates;
using PlanToSave.Domain.Entities;
using PlanToSave.Domain.Enums;
using PlanToSave.Web.Data;

namespace PlanToSave.Web.Services;

public class FlowTemplateService(ApplicationDbContext db) : IFlowTemplateService
{
    public async Task<List<FlowTemplateDto>> GetTemplatesAsync(string userId)
    {
        return await db.FlowTemplates
            .Include(t => t.FromAccount)
            .Include(t => t.ToAccount)
            .Where(t => t.UserId == userId)
            .OrderBy(t => t.FromAccount.Name)
            .ThenBy(t => t.Description)
            .Select(t => new FlowTemplateDto(
                t.Id,
                t.FromAccountId, t.FromAccount.Name, t.FromAccount.Type,
                t.ToAccountId, t.ToAccount.Name, t.ToAccount.Type,
                t.Amount, t.Description, t.IsActive, t.CreatedAt))
            .ToListAsync();
    }

    public async Task CreateAsync(string userId, CreateFlowTemplateDto dto)
    {
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

        db.FlowTemplates.Add(new FlowTemplate
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FromAccountId = dto.FromAccountId,
            ToAccountId = dto.ToAccountId,
            Amount = dto.Amount,
            Description = dto.Description,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    public async Task ToggleActiveAsync(string userId, Guid templateId)
    {
        var template = await db.FlowTemplates
            .FirstOrDefaultAsync(t => t.Id == templateId && t.UserId == userId)
            ?? throw new InvalidOperationException("Template not found.");

        template.IsActive = !template.IsActive;
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(string userId, Guid templateId)
    {
        var template = await db.FlowTemplates
            .FirstOrDefaultAsync(t => t.Id == templateId && t.UserId == userId)
            ?? throw new InvalidOperationException("Template not found.");

        db.FlowTemplates.Remove(template);
        await db.SaveChangesAsync();
    }
}
