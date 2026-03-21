using System.ComponentModel.DataAnnotations;
using PlanToSave.Domain.Enums;

namespace PlanToSave.Application.Plans;

public record MonthlyPlanSummaryDto(
    Guid Id,
    int Year,
    int Month,
    MonthlyPlanStatus Status,
    decimal TotalPlanned,
    decimal TotalActual,
    int PlannedFlowCount);

public record PlannedFlowDto(
    Guid Id,
    Guid FromAccountId,
    string FromAccountName,
    AccountType FromAccountType,
    Guid ToAccountId,
    string ToAccountName,
    AccountType ToAccountType,
    decimal PlannedAmount,
    decimal ActualAmount,
    string? Description,
    Guid? ActivityPlanId = null,
    string? ActivityTitle = null);

public record MonthlyPlanDetailDto(
    Guid Id,
    int Year,
    int Month,
    MonthlyPlanStatus Status,
    List<PlannedFlowDto> PlannedFlows);

public class CreatePlannedFlowDto
{
    public Guid FromAccountId { get; set; }
    public Guid ToAccountId { get; set; }

    [Range(0.01, 10_000_000, ErrorMessage = "Amount must be greater than zero")]
    public decimal Amount { get; set; }

    [MaxLength(300, ErrorMessage = "Description must be 300 characters or less")]
    public string? Description { get; set; }

    /// <summary>When set, tags this planned flow as a goal contribution.</summary>
    public Guid? GoalId { get; set; }
}

/// <summary>
/// Parameters for generating a sequence of monthly goal-contribution planned flows.
/// </summary>
public class GenerateGoalScheduleDto
{
    public Guid GoalId { get; set; }
    public Guid FromAccountId { get; set; }
    public Guid ToAccountId { get; set; }

    [Range(0.01, 10_000_000, ErrorMessage = "Monthly amount must be greater than zero")]
    public decimal MonthlyAmount { get; set; }

    public int StartYear { get; set; }
    public int StartMonth { get; set; }
    public int EndYear { get; set; }
    public int EndMonth { get; set; }

    [MaxLength(300)]
    public string? Description { get; set; }

    /// <summary>
    /// When true, existing planned flows already tagged to this goal are removed
    /// before inserting the new schedule.
    /// </summary>
    public bool ReplaceExisting { get; set; } = true;
}

/// <summary>
/// A goal-tagged planned flow, used to surface goal contributions on the calendar.
/// </summary>
public record GoalContributionDto(
    Guid PlannedFlowId,
    Guid GoalId,
    string GoalName,
    decimal Amount,
    string? Description,
    string FromAccountName,
    string ToAccountName);
