using System.ComponentModel.DataAnnotations;

namespace PlanToSave.Application.Goals;

public record GoalDto(
    Guid Id,
    string Name,
    string? Description,
    Guid TargetAccountId,
    string TargetAccountName,
    Guid SourceAccountId,
    string SourceAccountName,
    decimal TargetAmount,
    decimal SavedAmount,
    DateOnly StartDate,
    DateOnly TargetDate,
    bool IsComplete,
    Guid? IdeaId,
    string? IdeaTitle,
    DateTime CreatedAt);

public class CreateGoalDto
{
    [Required(ErrorMessage = "Name is required")]
    [MaxLength(200, ErrorMessage = "Name must be 200 characters or less")]
    public string Name { get; set; } = "";

    [MaxLength(500, ErrorMessage = "Description must be 500 characters or less")]
    public string? Description { get; set; }

    public Guid TargetAccountId { get; set; }
    public Guid SourceAccountId { get; set; }

    [Range(0.01, 10_000_000, ErrorMessage = "Target amount must be greater than zero")]
    public decimal TargetAmount { get; set; }

    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public DateOnly TargetDate { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddYears(1));

    /// <summary>Optional — link this goal to an idea in the backlog.</summary>
    public Guid? IdeaId { get; set; }
}

public interface IGoalService
{
    Task<List<GoalDto>> GetGoalsAsync(string userId);
    Task<Guid> CreateAsync(string userId, CreateGoalDto dto);
    Task MarkCompleteAsync(string userId, Guid goalId);
    Task DeleteAsync(string userId, Guid goalId);
}
