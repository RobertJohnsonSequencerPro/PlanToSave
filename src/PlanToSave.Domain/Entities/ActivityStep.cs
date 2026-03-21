namespace PlanToSave.Domain.Entities;

public class ActivityStep
{
    public Guid Id { get; set; }
    public Guid ActivityPlanId { get; set; }
    public string Title { get; set; } = null!;
    public int SortOrder { get; set; }
    public bool IsComplete { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public ActivityPlan ActivityPlan { get; set; } = null!;
}
