using PlanToSave.Domain.Entities;
using PlanToSave.Domain.Enums;

namespace PlanToSave.Tests.Domain;

public class AccountEntityTests
{
    [Theory]
    [InlineData(AccountType.Checking,   true)]
    [InlineData(AccountType.Savings,    true)]
    [InlineData(AccountType.Credit,     true)]
    [InlineData(AccountType.Investment, true)]
    [InlineData(AccountType.Income,     false)]
    [InlineData(AccountType.Expense,    false)]
    public void IsStockAccount_ReturnsExpectedValue(AccountType type, bool expected)
    {
        var account = new Account { Type = type };

        Assert.Equal(expected, account.IsStockAccount);
    }
}
