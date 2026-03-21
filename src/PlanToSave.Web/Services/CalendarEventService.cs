using Microsoft.EntityFrameworkCore;
using PlanToSave.Application.Calendar;
using PlanToSave.Domain.Entities;
using PlanToSave.Web.Data;

namespace PlanToSave.Web.Services;

public class CalendarEventService(ApplicationDbContext db) : ICalendarEventService
{
    private static CalendarEventDto ToDto(CalendarEvent e) =>
        new(e.Id, e.Title, e.Date, e.Notes, e.CreatedAt);

    public async Task<List<CalendarEventDto>> GetForMonthAsync(string userId, int year, int month)
    {
        var start = new DateOnly(year, month, 1);
        var end   = start.AddMonths(1).AddDays(-1);
        return await db.CalendarEvents
            .Where(e => e.UserId == userId && e.Date >= start && e.Date <= end)
            .OrderBy(e => e.Date)
            .Select(e => new CalendarEventDto(e.Id, e.Title, e.Date, e.Notes, e.CreatedAt))
            .ToListAsync();
    }

    public async Task<CalendarEventDto> CreateAsync(string userId, CreateCalendarEventDto dto)
    {
        var ev = new CalendarEvent
        {
            Id        = Guid.NewGuid(),
            UserId    = userId,
            Title     = dto.Title.Trim(),
            Date      = dto.Date,
            Notes     = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        db.CalendarEvents.Add(ev);
        await db.SaveChangesAsync();
        return ToDto(ev);
    }

    public async Task DeleteAsync(string userId, Guid id)
    {
        var ev = await db.CalendarEvents
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId)
            ?? throw new InvalidOperationException("Event not found.");
        db.CalendarEvents.Remove(ev);
        await db.SaveChangesAsync();
    }
}
