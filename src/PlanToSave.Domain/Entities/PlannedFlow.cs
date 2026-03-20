namespace PlanToSave.Domain.Entities;

public class PlannedFlow
{
    public Guid Id { get; set; }
    public Guid MonthlyPlanId { get; set; }
    public Guid FromAccountId { get; set; }
    public Guid ToAccountId { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    /// <summary>Set when this flow was seeded from a recurring template.</summary>
    public Guid? TemplateId { get; set; }
    /// <summary>Set when this flow was generated as a goal contribution.</summary>
    public Guid? GoalId { get; set; }

    public MonthlyPlan MonthlyPlan { get; set; } = null!;
    public Account FromAccount { get; set; } = null!;
    public Account ToAccount { get; set; } = null!;
    public FlowTemplate? Template { get; set; }
    public Goal? Goal { get; set; }
}
