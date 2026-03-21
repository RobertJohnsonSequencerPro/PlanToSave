using System.ComponentModel.DataAnnotations;

namespace PlanToSave.Application.Calendar;

public record CalendarEventDto(
    Guid Id,
    string Title,
    DateOnly Date,
    string? Notes,
    DateTime CreatedAt);

public class CreateCalendarEventDto
{
    [Required(ErrorMessage = "Title is required.")]
    [MaxLength(200, ErrorMessage = "Title must be 200 characters or fewer.")]
    public string Title { get; set; } = "";

    public DateOnly Date { get; set; }

    [MaxLength(500, ErrorMessage = "Notes must be 500 characters or fewer.")]
    public string? Notes { get; set; }
}

public interface ICalendarEventService
{
    Task<List<CalendarEventDto>> GetForMonthAsync(string userId, int year, int month);
    Task<CalendarEventDto> CreateAsync(string userId, CreateCalendarEventDto dto);
    Task DeleteAsync(string userId, Guid id);
}
