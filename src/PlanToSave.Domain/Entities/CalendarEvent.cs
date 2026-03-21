namespace PlanToSave.Domain.Entities;

public class CalendarEvent
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = null!;
    public string Title { get; set; } = null!;
    public DateOnly Date { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}
