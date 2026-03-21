namespace PlanToSave.Domain.Entities;

public class BalanceSnapshot
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = null!;
    public Guid AccountId { get; set; }
    public decimal Amount { get; set; }
    /// <summary>Amount minus the system-computed balance at EffectiveDate. Positive = bank has more than tracked; negative = bank has less.</summary>
    public decimal Variance { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }

    public Account Account { get; set; } = null!;
}
