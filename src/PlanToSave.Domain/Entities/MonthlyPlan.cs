using PlanToSave.Domain.Enums;

namespace PlanToSave.Domain.Entities;

public class MonthlyPlan
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = null!;
    public int Year { get; set; }
    public int Month { get; set; }
    public MonthlyPlanStatus Status { get; set; } = MonthlyPlanStatus.Draft;
    public DateTime CreatedAt { get; set; }

    public ICollection<PlannedFlow> PlannedFlows { get; set; } = new List<PlannedFlow>();
}
