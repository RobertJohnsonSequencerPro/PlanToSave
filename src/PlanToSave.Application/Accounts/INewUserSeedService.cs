namespace PlanToSave.Application.Accounts;

/// <summary>
/// Seeds a sensible set of starter accounts for a brand-new user so that the app
/// is immediately useful without requiring manual account setup.
/// </summary>
public interface INewUserSeedService
{
    /// <summary>
    /// Creates the default accounts for <paramref name="userId"/> if that user has
    /// no accounts yet.  Safe to call multiple times — it is a no-op when accounts
    /// already exist.
    /// </summary>
    Task SeedAsync(string userId);
}
