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
}
