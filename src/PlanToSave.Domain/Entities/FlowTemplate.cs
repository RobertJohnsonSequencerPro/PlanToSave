namespace PlanToSave.Domain.Entities;

public class FlowTemplate
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = null!;
    public Guid FromAccountId { get; set; }
    public Guid ToAccountId { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public Account FromAccount { get; set; } = null!;
    public Account ToAccount { get; set; } = null!;
}
