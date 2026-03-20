namespace PlanToSave.Domain.Entities;

public class ActualFlow
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = null!;
    public Guid FromAccountId { get; set; }
    public Guid ToAccountId { get; set; }
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
    public string? Description { get; set; }
    /// <summary>Optionally linked to a planned flow for variance tracking.</summary>
    public Guid? PlannedFlowId { get; set; }
    public DateTime CreatedAt { get; set; }

    public Account FromAccount { get; set; } = null!;
    public Account ToAccount { get; set; } = null!;
    public PlannedFlow? PlannedFlow { get; set; }
}
