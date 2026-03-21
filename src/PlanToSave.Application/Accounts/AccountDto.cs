using System.ComponentModel.DataAnnotations;
using PlanToSave.Domain.Enums;

namespace PlanToSave.Application.Accounts;

public record AccountDto(
    Guid Id,
    string Name,
    AccountType Type,
    string? Description,
    decimal OpeningBalance,
    bool IsArchived,
    bool IsStockAccount);

public record AccountBalanceDto(
    Guid Id,
    string Name,
    AccountType Type,
    decimal Balance,
    DateOnly? LastSnapshotDate,
    decimal AccruedInterest,
    decimal? AnnualRatePct);

public record BalanceSnapshotDto(
    Guid Id,
    decimal Amount,
    decimal Variance,
    DateOnly EffectiveDate,
    string? Note,
    DateTime CreatedAt);

public record InterestRuleDto(
    Guid Id,
    decimal AnnualRatePct,
    CompoundingFrequency Frequency,
    DateOnly EffectiveDate,
    DateTime CreatedAt);

public class SetInterestRuleDto
{
    [Range(0.001, 100, ErrorMessage = "Rate must be between 0.001% and 100%")]
    public decimal AnnualRatePct { get; set; }

    public CompoundingFrequency Frequency { get; set; } = CompoundingFrequency.Monthly;

    public DateOnly EffectiveDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
}

public class CreateAccountDto
{
    [Required(ErrorMessage = "Account name is required")]
    [MaxLength(100, ErrorMessage = "Name must be 100 characters or less")]
    public string Name { get; set; } = string.Empty;

    public AccountType Type { get; set; } = AccountType.Checking;

    [MaxLength(500, ErrorMessage = "Description must be 500 characters or less")]
    public string? Description { get; set; }

    [Range(-10_000_000, 10_000_000, ErrorMessage = "Opening balance must be a reasonable amount")]
    public decimal OpeningBalance { get; set; }
}

public class UpdateAccountDto
{
    [Required(ErrorMessage = "Account name is required")]
    [MaxLength(100, ErrorMessage = "Name must be 100 characters or less")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "Description must be 500 characters or less")]
    public string? Description { get; set; }

    [Range(-10_000_000, 10_000_000, ErrorMessage = "Opening balance must be a reasonable amount")]
    public decimal OpeningBalance { get; set; }
}
