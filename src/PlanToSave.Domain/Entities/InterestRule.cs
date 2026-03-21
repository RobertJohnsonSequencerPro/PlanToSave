using PlanToSave.Domain.Enums;

namespace PlanToSave.Domain.Entities;

public class InterestRule
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid AccountId { get; set; }

    /// <summary>Annual interest rate as a percentage, e.g. 4.5 means 4.5% APR.</summary>
    public decimal AnnualRatePct { get; set; }

    public CompoundingFrequency Frequency { get; set; }

    /// <summary>The date from which this rate applies (used as the start date for accrual calculation).</summary>
    public DateOnly EffectiveDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public Account Account { get; set; } = null!;
}
