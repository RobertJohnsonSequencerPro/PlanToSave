using PlanToSave.Application.Ideas;
using PlanToSave.Domain.Entities;
using PlanToSave.Domain.Enums;
using PlanToSave.Tests.Helpers;
using PlanToSave.Web.Services;

namespace PlanToSave.Tests.Services;

public class IdeaServiceTests
{
    // ── CreateAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_StoresIdeaWithBacklogStatus()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new IdeaService(db);

        var id = await svc.CreateAsync("user-1", new SaveIdeaDto
        {
            Title = "Hike the Appalachian Trail",
            Category = IdeaCategory.Travel,
            EnergyLevel = IdeaEnergyLevel.High,
            CostEstimate = IdeaCostEstimate.Moderate,
            EstimatedAmount = 2_500m
        });

        Assert.NotEqual(Guid.Empty, id);
        var stored = await db.Ideas.FindAsync(id);
        Assert.NotNull(stored);
        Assert.Equal("Hike the Appalachian Trail", stored.Title);
        Assert.Equal(IdeaStatus.Backlog, stored.Status);
        Assert.Equal("user-1", stored.UserId);
        Assert.Equal(2_500m, stored.EstimatedAmount);
    }

    [Fact]
    public async Task CreateAsync_NullsBlankDescriptionAndTags()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new IdeaService(db);

        var id = await svc.CreateAsync("user-1", new SaveIdeaDto
        {
            Title = "Road Trip",
            Description = "   ",
            Tags = "  "
        });

        var stored = await db.Ideas.FindAsync(id);
        Assert.NotNull(stored);
        Assert.Null(stored.Description);
        Assert.Null(stored.Tags);
    }

    [Fact]
    public async Task CreateAsync_TrimsTags_WhenNotBlank()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new IdeaService(db);

        var id = await svc.CreateAsync("user-1", new SaveIdeaDto
        {
            Title = "Concert",
            Tags = "  music, fun  "
        });

        var stored = await db.Ideas.FindAsync(id);
        Assert.NotNull(stored);
        Assert.Equal("music, fun", stored.Tags);
    }

    // ── GetIdeasAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetIdeasAsync_ReturnsOnlyUserIdeas()
    {
        await using var db = TestDbContextFactory.Create();
        db.Ideas.AddRange(
            MakeIdea("user-1", "Idea A"),
            MakeIdea("user-2", "Idea B"));
        await db.SaveChangesAsync();

        var svc = new IdeaService(db);
        var results = await svc.GetIdeasAsync("user-1");

        Assert.Single(results);
        Assert.Equal("Idea A", results[0].Title);
    }

    [Fact]
    public async Task GetIdeasAsync_OrdersByStatusThenCreatedAtDescending()
    {
        await using var db = TestDbContextFactory.Create();
        var now = DateTime.UtcNow;
        db.Ideas.AddRange(
            MakeIdea("user-1", "Done Idea",    IdeaStatus.Done,    createdAt: now.AddDays(-3)),
            MakeIdea("user-1", "Backlog New",  IdeaStatus.Backlog, createdAt: now.AddDays(-1)),
            MakeIdea("user-1", "Backlog Old",  IdeaStatus.Backlog, createdAt: now.AddDays(-5)),
            MakeIdea("user-1", "Planned Idea", IdeaStatus.Planned, createdAt: now.AddDays(-2)));
        await db.SaveChangesAsync();

        var svc = new IdeaService(db);
        var results = await svc.GetIdeasAsync("user-1");

        Assert.Equal(4, results.Count);
        // Backlog (0) comes first
        Assert.Equal(IdeaStatus.Backlog, results[0].Status);
        Assert.Equal(IdeaStatus.Backlog, results[1].Status);
        // Within Backlog: newest first
        Assert.Equal("Backlog New", results[0].Title);
        Assert.Equal("Backlog Old", results[1].Title);
        // Then Planned (1) then Done (2)
        Assert.Equal(IdeaStatus.Planned, results[2].Status);
        Assert.Equal(IdeaStatus.Done, results[3].Status);
    }

    [Fact]
    public async Task GetIdeasAsync_FiltersByCategory()
    {
        await using var db = TestDbContextFactory.Create();
        db.Ideas.AddRange(
            MakeIdea("user-1", "Adventure",  category: IdeaCategory.Travel),
            MakeIdea("user-1", "Learning",   category: IdeaCategory.Learning));
        await db.SaveChangesAsync();

        var svc = new IdeaService(db);
        var results = await svc.GetIdeasAsync("user-1", new IdeaFilterDto
        {
            Category = IdeaCategory.Travel
        });

        Assert.Single(results);
        Assert.Equal("Adventure", results[0].Title);    }

    [Fact]
    public async Task GetIdeasAsync_FiltersByEnergyLevel()
    {
        await using var db = TestDbContextFactory.Create();
        db.Ideas.AddRange(
            MakeIdea("user-1", "High Energy", energy: IdeaEnergyLevel.High),
            MakeIdea("user-1", "Low Energy",  energy: IdeaEnergyLevel.Low));
        await db.SaveChangesAsync();

        var svc = new IdeaService(db);
        var results = await svc.GetIdeasAsync("user-1", new IdeaFilterDto
        {
            EnergyLevel = IdeaEnergyLevel.High
        });

        Assert.Single(results);
        Assert.Equal("High Energy", results[0].Title);
    }

    [Fact]
    public async Task GetIdeasAsync_FiltersByCostEstimate()
    {
        await using var db = TestDbContextFactory.Create();
        db.Ideas.AddRange(
            MakeIdea("user-1", "Cheap",     cost: IdeaCostEstimate.Cheap),
            MakeIdea("user-1", "Expensive", cost: IdeaCostEstimate.Expensive));
        await db.SaveChangesAsync();

        var svc = new IdeaService(db);
        var results = await svc.GetIdeasAsync("user-1", new IdeaFilterDto
        {
            CostEstimate = IdeaCostEstimate.Expensive
        });

        Assert.Single(results);
        Assert.Equal("Expensive", results[0].Title);
    }

    [Fact]
    public async Task GetIdeasAsync_FiltersByStatus()
    {
        await using var db = TestDbContextFactory.Create();
        db.Ideas.AddRange(
            MakeIdea("user-1", "Backlog", status: IdeaStatus.Backlog),
            MakeIdea("user-1", "Done",    status: IdeaStatus.Done));
        await db.SaveChangesAsync();

        var svc = new IdeaService(db);
        var results = await svc.GetIdeasAsync("user-1", new IdeaFilterDto
        {
            Status = IdeaStatus.Done
        });

        Assert.Single(results);
        Assert.Equal("Done", results[0].Title);
    }

    [Fact]
    public async Task GetIdeasAsync_FiltersByTag()
    {
        await using var db = TestDbContextFactory.Create();
        db.Ideas.AddRange(
            MakeIdea("user-1", "Tagged",   tags: "outdoor, fun"),
            MakeIdea("user-1", "NoTag",    tags: null));
        await db.SaveChangesAsync();

        var svc = new IdeaService(db);
        var results = await svc.GetIdeasAsync("user-1", new IdeaFilterDto
        {
            Tag = "outdoor"
        });

        Assert.Single(results);
        Assert.Equal("Tagged", results[0].Title);
    }

    // ── DeleteAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_RemovesIdea()
    {
        await using var db = TestDbContextFactory.Create();
        var idea = MakeIdea("user-1", "To Delete");
        db.Ideas.Add(idea);
        await db.SaveChangesAsync();

        var svc = new IdeaService(db);
        await svc.DeleteAsync("user-1", idea.Id);

        Assert.Null(await db.Ideas.FindAsync(idea.Id));
    }

    [Fact]
    public async Task DeleteAsync_Throws_WhenIdeaNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new IdeaService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.DeleteAsync("user-1", Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteAsync_Throws_WhenWrongUser()
    {
        await using var db = TestDbContextFactory.Create();
        var idea = MakeIdea("user-1", "Someone Else's");
        db.Ideas.Add(idea);
        await db.SaveChangesAsync();

        var svc = new IdeaService(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.DeleteAsync("user-99", idea.Id));
    }

    // ── GetRandomBacklogIdeaAsync ─────────────────────────────────────

    [Fact]
    public async Task GetRandomBacklogIdeaAsync_ReturnsNull_WhenNoBacklogIdeas()
    {
        await using var db = TestDbContextFactory.Create();
        db.Ideas.Add(MakeIdea("user-1", "Done Idea", status: IdeaStatus.Done));
        await db.SaveChangesAsync();

        var svc = new IdeaService(db);
        var result = await svc.GetRandomBacklogIdeaAsync("user-1");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetRandomBacklogIdeaAsync_ReturnsBacklogIdea()
    {
        await using var db = TestDbContextFactory.Create();
        db.Ideas.Add(MakeIdea("user-1", "Backlog Idea", status: IdeaStatus.Backlog));
        await db.SaveChangesAsync();

        var svc = new IdeaService(db);
        var result = await svc.GetRandomBacklogIdeaAsync("user-1");

        Assert.NotNull(result);
        Assert.Equal(IdeaStatus.Backlog, result.Status);
    }

    [Fact]
    public async Task GetRandomBacklogIdeaAsync_RespectsEnergyFilter()
    {
        await using var db = TestDbContextFactory.Create();
        db.Ideas.AddRange(
            MakeIdea("user-1", "High Energy", status: IdeaStatus.Backlog, energy: IdeaEnergyLevel.High),
            MakeIdea("user-1", "Low Energy",  status: IdeaStatus.Backlog, energy: IdeaEnergyLevel.Low));
        await db.SaveChangesAsync();

        var svc = new IdeaService(db);
        var result = await svc.GetRandomBacklogIdeaAsync("user-1", energy: IdeaEnergyLevel.Low);

        Assert.NotNull(result);
        Assert.Equal(IdeaEnergyLevel.Low, result.EnergyLevel);
    }

    [Fact]
    public async Task GetRandomBacklogIdeaAsync_RespectsCostFilter()
    {
        await using var db = TestDbContextFactory.Create();
        db.Ideas.AddRange(
            MakeIdea("user-1", "Cheap",     status: IdeaStatus.Backlog, cost: IdeaCostEstimate.Cheap),
            MakeIdea("user-1", "Expensive", status: IdeaStatus.Backlog, cost: IdeaCostEstimate.Expensive));
        await db.SaveChangesAsync();

        var svc = new IdeaService(db);
        var result = await svc.GetRandomBacklogIdeaAsync("user-1", cost: IdeaCostEstimate.Cheap);

        Assert.NotNull(result);
        Assert.Equal(IdeaCostEstimate.Cheap, result.CostEstimate);
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static Idea MakeIdea(
        string userId,
        string title,
        IdeaStatus status = IdeaStatus.Backlog,
        IdeaCategory category = IdeaCategory.Other,
        IdeaEnergyLevel energy = IdeaEnergyLevel.Medium,
        IdeaCostEstimate cost = IdeaCostEstimate.Cheap,
        string? tags = null,
        DateTime? createdAt = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title,
            Category = category,
            EnergyLevel = energy,
            CostEstimate = cost,
            Status = status,
            Tags = tags,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };
}
