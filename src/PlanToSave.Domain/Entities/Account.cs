using PlanToSave.Domain.Enums;

namespace PlanToSave.Domain.Entities;

public class Account
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public AccountType Type { get; set; }
    public string? Description { get; set; }
    public decimal OpeningBalance { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; }

    public bool IsStockAccount => Type is AccountType.Checking
        or AccountType.Savings
        or AccountType.Credit
        or AccountType.Investment;
}
