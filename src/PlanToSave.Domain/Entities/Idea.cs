using PlanToSave.Domain.Enums;

namespace PlanToSave.Domain.Entities;

public class Idea
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public IdeaCategory Category { get; set; }
    public IdeaEnergyLevel EnergyLevel { get; set; }
    public IdeaCostEstimate CostEstimate { get; set; }
    public decimal? EstimatedAmount { get; set; }
    public string? Tags { get; set; }
    public IdeaStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<ActivityPlan> ActivityPlans { get; set; } = new List<ActivityPlan>();
}
