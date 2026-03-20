namespace PlanToSave.Domain.Enums;

public enum AccountType
{
    /// <summary>Money flowing in from the outside world. No balance — period totals only.</summary>
    Income,

    /// <summary>Money flowing out to the outside world. No balance — period totals only.</summary>
    Expense,

    Checking,
    Savings,
    Credit,
    Investment
}
