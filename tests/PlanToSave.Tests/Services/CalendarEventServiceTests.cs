using PlanToSave.Application.Calendar;
using PlanToSave.Domain.Entities;
using PlanToSave.Tests.Helpers;
using PlanToSave.Web.Services;

namespace PlanToSave.Tests.Services;

public class CalendarEventServiceTests
{
    // ── GetForMonthAsync ──────────────────────────────────────────────

    [Fact]
    public async Task GetForMonthAsync_ReturnsEventsInMonth()
    {
        await using var db = TestDbContextFactory.Create();
        db.CalendarEvents.AddRange(
            MakeEvent("user-1", "January Event",  new DateOnly(2025, 1, 15)),
            MakeEvent("user-1", "February Event", new DateOnly(2025, 2, 10)),
            MakeEvent("user-1", "March Event",    new DateOnly(2025, 3, 5)));
        await db.SaveChangesAsync();

        var svc = new CalendarEventService(db);
        var results = await svc.GetForMonthAsync("user-1", 2025, 2);

        Assert.Single(results);
        Assert.Equal("February Event", results[0].Title);
    }

    [Fact]
    public async Task GetForMonthAsync_IsolatesUserData()
    {
        await using var db = TestDbContextFactory.Create();
        db.CalendarEvents.AddRange(
            MakeEvent("user-1", "My Event",    new DateOnly(2025, 4, 1)),
            MakeEvent("user-2", "Their Event", new DateOnly(2025, 4, 2)));
        await db.SaveChangesAsync();

        var svc = new CalendarEventService(db);
        var results = await svc.GetForMonthAsync("user-1", 2025, 4);

        Assert.Single(results);
        Assert.Equal("My Event", results[0].Title);
    }

    [Fact]
    public async Task GetForMonthAsync_ReturnsEmpty_WhenNoEvents()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new CalendarEventService(db);

        var results = await svc.GetForMonthAsync("user-1", 2025, 6);

        Assert.Empty(results);
    }

    [Fact]
    public async Task GetForMonthAsync_IncludesFirstAndLastDayOfMonth()
    {
        await using var db = TestDbContextFactory.Create();
        db.CalendarEvents.AddRange(
            MakeEvent("user-1", "First", new DateOnly(2025, 5, 1)),
            MakeEvent("user-1", "Last",  new DateOnly(2025, 5, 31)));
        await db.SaveChangesAsync();

        var svc = new CalendarEventService(db);
        var results = await svc.GetForMonthAsync("user-1", 2025, 5);

        Assert.Equal(2, results.Count);
    }

    // ── CreateAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_StoresEventAndReturnsDto()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new CalendarEventService(db);

        var result = await svc.CreateAsync("user-1", new CreateCalendarEventDto
        {
            Title = "  Team Lunch  ",
            Date = new DateOnly(2025, 7, 4),
            Notes = "  Bring snacks  "
        });

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Team Lunch", result.Title);
        Assert.Equal(new DateOnly(2025, 7, 4), result.Date);
        Assert.Equal("Bring snacks", result.Notes);

        var stored = await db.CalendarEvents.FindAsync(result.Id);
        Assert.NotNull(stored);
        Assert.Equal("user-1", stored.UserId);
    }

    [Fact]
    public async Task CreateAsync_NullsBlankNotes()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new CalendarEventService(db);

        var result = await svc.CreateAsync("user-1", new CreateCalendarEventDto
        {
            Title = "Meeting",
            Date = new DateOnly(2025, 8, 1),
            Notes = "   "
        });

        Assert.Null(result.Notes);
    }

    // ── DeleteAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_RemovesEvent()
    {
        await using var db = TestDbContextFactory.Create();
        var ev = MakeEvent("user-1", "To Delete", new DateOnly(2025, 9, 1));
        db.CalendarEvents.Add(ev);
        await db.SaveChangesAsync();

        var svc = new CalendarEventService(db);
        await svc.DeleteAsync("user-1", ev.Id);

        Assert.Null(await db.CalendarEvents.FindAsync(ev.Id));
    }

    [Fact]
    public async Task DeleteAsync_Throws_WhenEventNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new CalendarEventService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.DeleteAsync("user-1", Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteAsync_Throws_WhenWrongUser()
    {
        await using var db = TestDbContextFactory.Create();
        var ev = MakeEvent("user-1", "Mine", new DateOnly(2025, 10, 1));
        db.CalendarEvents.Add(ev);
        await db.SaveChangesAsync();

        var svc = new CalendarEventService(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.DeleteAsync("user-99", ev.Id));
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static CalendarEvent MakeEvent(string userId, string title, DateOnly date) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title,
            Date = date,
            CreatedAt = DateTime.UtcNow
        };
}
