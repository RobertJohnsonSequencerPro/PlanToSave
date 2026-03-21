using System.ComponentModel.DataAnnotations;
using PlanToSave.Domain.Enums;

namespace PlanToSave.Application.Flows;

public record ActualFlowDto(
    Guid Id,
    Guid FromAccountId,
    string FromAccountName,
    AccountType FromAccountType,
    Guid ToAccountId,
    string ToAccountName,
    AccountType ToAccountType,
    decimal Amount,
    DateOnly Date,
    string? Description,
    DateTime CreatedAt);

public record AccountOptionDto(Guid Id, string Name, AccountType Type);

public class CreateActualFlowDto
{
    public Guid FromAccountId { get; set; }
    public Guid ToAccountId { get; set; }

    [Range(0.01, 10_000_000, ErrorMessage = "Amount must be greater than zero")]
    public decimal Amount { get; set; }

    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [MaxLength(300, ErrorMessage = "Description must be 300 characters or less")]
    public string? Description { get; set; }
}

public record FlowFilterDto(
    DateOnly? From,
    DateOnly? To,
    Guid? AccountId);
