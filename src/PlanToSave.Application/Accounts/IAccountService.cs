namespace PlanToSave.Application.Accounts;

public interface IAccountService
{
    Task<List<AccountDto>> GetAccountsAsync(string userId, bool includeArchived = false);
    Task<AccountDto?> GetByIdAsync(Guid id, string userId);
    Task<Guid> CreateAsync(string userId, CreateAccountDto dto);
    Task UpdateAsync(Guid id, string userId, UpdateAccountDto dto);
    Task ArchiveAsync(Guid id, string userId);
    Task<List<AccountBalanceDto>> GetBalancesAsync(string userId);
    Task SetSnapshotAsync(string userId, Guid accountId, decimal amount, DateOnly effectiveDate, string? note);
    Task<List<BalanceSnapshotDto>> GetSnapshotsAsync(string userId, Guid accountId);
    Task<InterestRuleDto?> GetInterestRuleAsync(string userId, Guid accountId);
    Task SetInterestRuleAsync(string userId, Guid accountId, SetInterestRuleDto dto);
    Task DeleteInterestRuleAsync(string userId, Guid accountId);

    /// <summary>
    /// Returns monthly balance data points for a stock account:
    /// end-of-month actual balances for the past <paramref name="pastMonths"/> months,
    /// the current balance for today, and projected end-of-month balances for the
    /// next <paramref name="futureMonths"/> months based on planned flows.
    /// </summary>
    Task<AccountForecastDto?> GetAccountForecastAsync(
        string userId, Guid accountId, int pastMonths = 6, int futureMonths = 6);

    /// <summary>
    /// Returns all actual and planned transactions that touch a stock account,
    /// ordered by date. Planned (future) transactions are flagged with IsProjected = true.
    /// </summary>
    Task<List<AccountTransactionDto>> GetAccountTransactionsAsync(
        string userId, Guid accountId);

    /// <summary>
    /// Returns <c>null</c> if <paramref name="name"/> is available for the user,
    /// or the next available name with a numeric suffix (e.g. "Name 2") if the name is already taken.
    /// Only active (non-archived) accounts are considered.
    /// </summary>
    Task<string?> SuggestUniqueNameAsync(string userId, string name);
}
