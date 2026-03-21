namespace PlanToSave.Domain.Entities;

public class ActivityReview
{
    public Guid Id { get; set; }
    public Guid ActivityPlanId { get; set; }
    public int? Rating { get; set; }           // 1–5
    public string? Reflection { get; set; }
    public decimal? ActualAmount { get; set; }
    public DateTime CreatedAt { get; set; }

    public ActivityPlan ActivityPlan { get; set; } = null!;
}
