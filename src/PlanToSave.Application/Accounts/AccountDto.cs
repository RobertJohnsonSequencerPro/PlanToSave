using System.ComponentModel.DataAnnotations;
using PlanToSave.Domain.Enums;

namespace PlanToSave.Application.Accounts;

public record AccountDto(
    Guid Id,
    string Name,
    AccountType Type,
    string? Description,
    bool IsArchived,
    bool IsStockAccount);

public record AccountBalanceDto(
    Guid Id,
    string Name,
    AccountType Type,
    decimal Balance);

public class CreateAccountDto
{
    [Required(ErrorMessage = "Account name is required")]
    [MaxLength(100, ErrorMessage = "Name must be 100 characters or less")]
    public string Name { get; set; } = string.Empty;

    public AccountType Type { get; set; } = AccountType.Checking;

    [MaxLength(500, ErrorMessage = "Description must be 500 characters or less")]
    public string? Description { get; set; }
}

public class UpdateAccountDto
{
    [Required(ErrorMessage = "Account name is required")]
    [MaxLength(100, ErrorMessage = "Name must be 100 characters or less")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "Description must be 500 characters or less")]
    public string? Description { get; set; }
}
