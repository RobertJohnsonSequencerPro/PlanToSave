using Microsoft.EntityFrameworkCore;
using PlanToSave.Application.Ideas;
using PlanToSave.Domain.Entities;
using PlanToSave.Domain.Enums;
using PlanToSave.Web.Data;

namespace PlanToSave.Web.Services;

public class IdeaService(ApplicationDbContext db) : IIdeaService
{
    public async Task<List<IdeaDto>> GetIdeasAsync(string userId, IdeaFilterDto? filter = null)
    {
        var query = db.Ideas.Where(i => i.UserId == userId);

        if (filter is not null)
        {
            if (filter.Category.HasValue)
                query = query.Where(i => i.Category == filter.Category.Value);
            if (filter.EnergyLevel.HasValue)
                query = query.Where(i => i.EnergyLevel == filter.EnergyLevel.Value);
            if (filter.CostEstimate.HasValue)
                query = query.Where(i => i.CostEstimate == filter.CostEstimate.Value);
            if (filter.Status.HasValue)
                query = query.Where(i => i.Status == filter.Status.Value);
            if (!string.IsNullOrWhiteSpace(filter.Tag))
                query = query.Where(i => i.Tags != null && i.Tags.Contains(filter.Tag));
        }

        // Load into memory, then sort by status (Backlog→Planned→Done→Skipped by int value)
        var raw = await query.ToListAsync();
        return raw
            .OrderBy(i => (int)i.Status)
            .ThenByDescending(i => i.CreatedAt)
            .Select(i => new IdeaDto(
                i.Id, i.Title, i.Description,
                i.Category, i.EnergyLevel, i.CostEstimate,
                i.EstimatedAmount, i.Tags, i.Status, i.CreatedAt))
            .ToList();
    }

    public async Task<Guid> CreateAsync(string userId, SaveIdeaDto dto)
    {
        var idea = new Idea
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = dto.Title,
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
            Category = dto.Category,
            EnergyLevel = dto.EnergyLevel,
            CostEstimate = dto.CostEstimate,
            EstimatedAmount = dto.EstimatedAmount,
            Tags = string.IsNullOrWhiteSpace(dto.Tags) ? null : dto.Tags.Trim(),
            Status = IdeaStatus.Backlog,
            CreatedAt = DateTime.UtcNow
        };
        db.Ideas.Add(idea);
        await db.SaveChangesAsync();
        return idea.Id;
    }

    public async Task DeleteAsync(string userId, Guid id)
    {
        var idea = await db.Ideas.FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId)
            ?? throw new InvalidOperationException("Idea not found.");
        db.Ideas.Remove(idea);
        await db.SaveChangesAsync();
    }

    public async Task<IdeaDto?> GetRandomBacklogIdeaAsync(string userId, IdeaEnergyLevel? energy = null, IdeaCostEstimate? cost = null)
    {
        var query = db.Ideas.Where(i => i.UserId == userId && i.Status == IdeaStatus.Backlog);
        if (energy.HasValue) query = query.Where(i => i.EnergyLevel == energy.Value);
        if (cost.HasValue)   query = query.Where(i => i.CostEstimate == cost.Value);

        var ids = await query.Select(i => i.Id).ToListAsync();
        if (ids.Count == 0) return null;

        var randomId = ids[Random.Shared.Next(ids.Count)];
        var idea = await db.Ideas.FirstAsync(i => i.Id == randomId);
        return new IdeaDto(idea.Id, idea.Title, idea.Description,
            idea.Category, idea.EnergyLevel, idea.CostEstimate,
            idea.EstimatedAmount, idea.Tags, idea.Status, idea.CreatedAt);
    }
}
