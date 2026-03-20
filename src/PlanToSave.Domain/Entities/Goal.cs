namespace PlanToSave.Domain.Entities;

public class Goal
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    /// <summary>The stock account this goal is funding (e.g., a Savings account).</summary>
    public Guid TargetAccountId { get; set; }
    public decimal TargetAmount { get; set; }
    public DateOnly TargetDate { get; set; }
    public DateOnly StartDate { get; set; }
    /// <summary>The account contributions are drawn from each month.</summary>
    public Guid SourceAccountId { get; set; }
    public bool IsComplete { get; set; }
    public DateTime CreatedAt { get; set; }

    public Account TargetAccount { get; set; } = null!;
    public Account SourceAccount { get; set; } = null!;
    public ICollection<PlannedFlow> ContributionFlows { get; set; } = new List<PlannedFlow>();
}
