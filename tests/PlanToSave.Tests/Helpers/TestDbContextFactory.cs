using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PlanToSave.Web.Data;

namespace PlanToSave.Tests.Helpers;

/// <summary>
/// Creates an isolated, in-memory <see cref="ApplicationDbContext"/> for each test.
/// A unique database name per call prevents tests from sharing state.
/// </summary>
internal static class TestDbContextFactory
{
    public static ApplicationDbContext Create()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    /// <summary>
    /// Seeds a minimal <see cref="ApplicationUser"/> row so that FK-referencing entities
    /// can be written without triggering referential-integrity errors in providers that
    /// enforce them.  The in-memory provider does not enforce FKs, but this keeps tests
    /// realistic and forward-compatible.
    /// </summary>
    public static async Task<ApplicationUser> SeedUserAsync(ApplicationDbContext db, string userId = "user-1")
    {
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = $"{userId}@test.com",
            NormalizedUserName = $"{userId}@TEST.COM",
            Email = $"{userId}@test.com",
            NormalizedEmail = $"{userId}@TEST.COM",
            SecurityStamp = Guid.NewGuid().ToString()
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }
}
