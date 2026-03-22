using PlanToSave.Application.Templates;
using PlanToSave.Domain.Entities;
using PlanToSave.Domain.Enums;
using PlanToSave.Tests.Helpers;
using PlanToSave.Web.Services;

namespace PlanToSave.Tests.Services;

public class FlowTemplateServiceTests
{
    // ── GetTemplatesAsync ─────────────────────────────────────────────

    [Fact]
    public async Task GetTemplatesAsync_ReturnsOnlyUserTemplates()
    {
        await using var db = TestDbContextFactory.Create();
        var (income1, savings1) = SeedAccounts(db, "user-1");
        var (income2, savings2) = SeedAccounts(db, "user-2");
        db.FlowTemplates.AddRange(
            MakeTemplate("user-1", income1.Id, savings1.Id, 100m, "Savings"),
            MakeTemplate("user-2", income2.Id, savings2.Id, 200m, "Other"));
        await db.SaveChangesAsync();

        var svc = new FlowTemplateService(db);
        var results = await svc.GetTemplatesAsync("user-1");

        Assert.Single(results);
        Assert.Equal("Savings", results[0].Description);
    }

    [Fact]
    public async Task GetTemplatesAsync_ReturnsEmpty_WhenNoTemplates()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new FlowTemplateService(db);

        var results = await svc.GetTemplatesAsync("user-1");

        Assert.Empty(results);
    }

    // ── CreateAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_StoresTemplateAsActive()
    {
        await using var db = TestDbContextFactory.Create();
        var (income, savings) = SeedAccounts(db, "user-1");
        await db.SaveChangesAsync();

        var svc = new FlowTemplateService(db);
        await svc.CreateAsync("user-1", new CreateFlowTemplateDto
        {
            FromAccountId = income.Id,
            ToAccountId = savings.Id,
            Amount = 300m,
            Description = "Monthly savings"
        });

        var template = db.FlowTemplates.Single();
        Assert.Equal("user-1", template.UserId);
        Assert.Equal(300m, template.Amount);
        Assert.True(template.IsActive);
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenFromAccountNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var (_, savings) = SeedAccounts(db, "user-1");
        await db.SaveChangesAsync();

        var svc = new FlowTemplateService(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateAsync("user-1", new CreateFlowTemplateDto
            {
                FromAccountId = Guid.NewGuid(),
                ToAccountId = savings.Id,
                Amount = 100m
            }));
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenToAccountNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var (income, _) = SeedAccounts(db, "user-1");
        await db.SaveChangesAsync();

        var svc = new FlowTemplateService(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateAsync("user-1", new CreateFlowTemplateDto
            {
                FromAccountId = income.Id,
                ToAccountId = Guid.NewGuid(),
                Amount = 100m
            }));
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenExpenseIsSource()
    {
        await using var db = TestDbContextFactory.Create();
        var expense = MakeAccount("user-1", "Groceries", AccountType.Expense);
        var savings = MakeAccount("user-1", "Savings", AccountType.Savings);
        db.Accounts.AddRange(expense, savings);
        await db.SaveChangesAsync();

        var svc = new FlowTemplateService(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateAsync("user-1", new CreateFlowTemplateDto
            {
                FromAccountId = expense.Id,
                ToAccountId = savings.Id,
                Amount = 100m
            }));
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenIncomeIsDestination()
    {
        await using var db = TestDbContextFactory.Create();
        var income = MakeAccount("user-1", "Salary", AccountType.Income);
        var checking = MakeAccount("user-1", "Checking", AccountType.Checking);
        db.Accounts.AddRange(income, checking);
        await db.SaveChangesAsync();

        var svc = new FlowTemplateService(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateAsync("user-1", new CreateFlowTemplateDto
            {
                FromAccountId = checking.Id,
                ToAccountId = income.Id,
                Amount = 100m
            }));
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenFromAndToAreSame()
    {
        await using var db = TestDbContextFactory.Create();
        var (income, savings) = SeedAccounts(db, "user-1");
        await db.SaveChangesAsync();

        var svc = new FlowTemplateService(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateAsync("user-1", new CreateFlowTemplateDto
            {
                FromAccountId = savings.Id,
                ToAccountId = savings.Id,
                Amount = 100m
            }));
    }

    // ── ToggleActiveAsync ─────────────────────────────────────────────

    [Fact]
    public async Task ToggleActiveAsync_TogglesIsActive()
    {
        await using var db = TestDbContextFactory.Create();
        var (income, savings) = SeedAccounts(db, "user-1");
        var template = MakeTemplate("user-1", income.Id, savings.Id, 100m, "Test");
        db.FlowTemplates.Add(template);
        await db.SaveChangesAsync();

        var svc = new FlowTemplateService(db);

        // Toggle OFF
        await svc.ToggleActiveAsync("user-1", template.Id);
        var afterFirst = await db.FlowTemplates.FindAsync(template.Id);
        Assert.NotNull(afterFirst);
        Assert.False(afterFirst.IsActive);

        // Toggle ON
        await svc.ToggleActiveAsync("user-1", template.Id);
        var afterSecond = await db.FlowTemplates.FindAsync(template.Id);
        Assert.NotNull(afterSecond);
        Assert.True(afterSecond.IsActive);
    }

    [Fact]
    public async Task ToggleActiveAsync_Throws_WhenTemplateNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new FlowTemplateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ToggleActiveAsync("user-1", Guid.NewGuid()));
    }

    [Fact]
    public async Task ToggleActiveAsync_Throws_WhenWrongUser()
    {
        await using var db = TestDbContextFactory.Create();
        var (income, savings) = SeedAccounts(db, "user-1");
        var template = MakeTemplate("user-1", income.Id, savings.Id, 100m, "Test");
        db.FlowTemplates.Add(template);
        await db.SaveChangesAsync();

        var svc = new FlowTemplateService(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ToggleActiveAsync("user-99", template.Id));
    }

    // ── DeleteAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_RemovesTemplate()
    {
        await using var db = TestDbContextFactory.Create();
        var (income, savings) = SeedAccounts(db, "user-1");
        var template = MakeTemplate("user-1", income.Id, savings.Id, 50m, "Delete Me");
        db.FlowTemplates.Add(template);
        await db.SaveChangesAsync();

        var svc = new FlowTemplateService(db);
        await svc.DeleteAsync("user-1", template.Id);

        Assert.Null(await db.FlowTemplates.FindAsync(template.Id));
    }

    [Fact]
    public async Task DeleteAsync_Throws_WhenTemplateNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new FlowTemplateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.DeleteAsync("user-1", Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteAsync_Throws_WhenWrongUser()
    {
        await using var db = TestDbContextFactory.Create();
        var (income, savings) = SeedAccounts(db, "user-1");
        var template = MakeTemplate("user-1", income.Id, savings.Id, 50m, "Mine");
        db.FlowTemplates.Add(template);
        await db.SaveChangesAsync();

        var svc = new FlowTemplateService(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.DeleteAsync("user-99", template.Id));
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static (Account income, Account savings) SeedAccounts(
        PlanToSave.Web.Data.ApplicationDbContext db, string userId)
    {
        var income = MakeAccount(userId, "Salary", AccountType.Income);
        var savings = MakeAccount(userId, "Savings", AccountType.Savings);
        db.Accounts.AddRange(income, savings);
        return (income, savings);
    }

    private static Account MakeAccount(string userId, string name, AccountType type) =>
        new()
        {
            Id = Guid.NewGuid(), UserId = userId,
            Name = name, Type = type,
            CreatedAt = DateTime.UtcNow
        };

    private static FlowTemplate MakeTemplate(
        string userId, Guid fromId, Guid toId, decimal amount, string? description) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FromAccountId = fromId,
            ToAccountId = toId,
            Amount = amount,
            Description = description,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
}
