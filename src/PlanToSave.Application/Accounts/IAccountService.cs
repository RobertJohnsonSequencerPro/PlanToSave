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
}
