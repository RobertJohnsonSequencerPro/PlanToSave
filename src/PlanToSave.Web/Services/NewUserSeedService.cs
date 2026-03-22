using Microsoft.EntityFrameworkCore;
using PlanToSave.Application.Accounts;
using PlanToSave.Domain.Entities;
using PlanToSave.Domain.Enums;
using PlanToSave.Web.Data;

namespace PlanToSave.Web.Services;

/// <summary>
/// Seeds a sensible starter set of accounts for a brand-new user.
/// </summary>
public class NewUserSeedService(ApplicationDbContext db) : INewUserSeedService
{
    // (type, name, description)
    private static readonly (AccountType Type, string Name, string? Description)[] DefaultAccounts =
    [
        // ── Stock accounts ────────────────────────────────────────────────────────
        (AccountType.Checking,   "Primary Checking",    "Main day-to-day checking account"),
        (AccountType.Savings,    "Emergency Fund",      "3-6 months of living expenses"),

        // ── Income (flow) accounts ────────────────────────────────────────────────
        (AccountType.Income,     "Salary",              "Regular employment income"),
        (AccountType.Income,     "Other Income",        "Freelance, side income, or other earnings"),

        // ── Expense (flow) accounts ───────────────────────────────────────────────
        (AccountType.Expense,    "Housing",             "Rent or mortgage payment"),
        (AccountType.Expense,    "Groceries",           "Supermarket and household supplies"),
        (AccountType.Expense,    "Utilities",           "Electricity, gas, water, internet"),
        (AccountType.Expense,    "Transportation",      "Car payment, fuel, public transit"),
        (AccountType.Expense,    "Dining & Takeout",    "Restaurants, cafés, and delivery"),
        (AccountType.Expense,    "Healthcare",          "Insurance, prescriptions, and medical visits"),
        (AccountType.Expense,    "Personal Care",       "Haircuts, toiletries, and personal items"),
        (AccountType.Expense,    "Entertainment",       "Movies, hobbies, and leisure activities"),
        (AccountType.Expense,    "Clothing",            "Apparel and accessories"),
        (AccountType.Expense,    "Subscriptions",       "Streaming, software, and recurring services"),
    ];

    /// <inheritdoc />
    public async Task SeedAsync(string userId)
    {
        var hasAccounts = await db.Accounts
            .AnyAsync(a => a.UserId == userId);

        if (hasAccounts)
            return;

        var now = DateTime.UtcNow;
        foreach (var (type, name, description) in DefaultAccounts)
        {
            db.Accounts.Add(new Account
            {
                Id          = Guid.NewGuid(),
                UserId      = userId,
                Name        = name,
                Type        = type,
                Description = description,
                IsArchived  = false,
                CreatedAt   = now,
            });
        }

        await db.SaveChangesAsync();
    }
}
