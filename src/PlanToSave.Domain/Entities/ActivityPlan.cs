using PlanToSave.Domain.Enums;

namespace PlanToSave.Domain.Entities;

public class ActivityPlan
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = null!;
    public Guid IdeaId { get; set; }
    public DateOnly? PlannedDate { get; set; }
    public ActivityPlanStatus Status { get; set; }
    public string? Notes { get; set; }
    public DateOnly? CompletedDate { get; set; }
    public DateTime CreatedAt { get; set; }

    public Idea Idea { get; set; } = null!;
    public ICollection<ActivityStep> Steps { get; set; } = new List<ActivityStep>();
}
