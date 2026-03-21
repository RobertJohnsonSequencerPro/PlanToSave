namespace PlanToSave.Application.Flows;

public interface IActualFlowService
{
    /// <summary>Returns flows for the user, newest first, with optional date/account filters.</summary>
    Task<List<ActualFlowDto>> GetFlowsAsync(string userId, FlowFilterDto? filter = null);

    /// <summary>Returns accounts the user can select as From (Income + Stock types).</summary>
    Task<List<AccountOptionDto>> GetFromAccountOptionsAsync(string userId);

    /// <summary>Returns accounts the user can select as To (Expense + Stock types).</summary>
    Task<List<AccountOptionDto>> GetToAccountOptionsAsync(string userId);

    Task<Guid> CreateAsync(string userId, CreateActualFlowDto dto);

    Task DeleteAsync(Guid id, string userId);

    /// <summary>
    /// Inserts multiple pre-validated flows in a single transaction.
    /// Returns the count of successfully inserted rows and any per-row errors.
    /// </summary>
    Task<(int Imported, List<string> Errors)> BulkImportAsync(
        string userId, List<BulkImportRowDto> rows);
}
