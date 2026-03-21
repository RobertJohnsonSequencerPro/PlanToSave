using System.ComponentModel.DataAnnotations;
using PlanToSave.Domain.Enums;

namespace PlanToSave.Application.Activities;

public record ActivityStepDto(
    Guid Id,
    string Title,
    int SortOrder,
    bool IsComplete,
    DateTimeOffset? CompletedAt);

public record ActivityPlanDto(
    Guid Id,
    Guid IdeaId,
    string IdeaTitle,
    IdeaCategory IdeaCategory,
    DateOnly? PlannedDate,
    ActivityPlanStatus Status,
    string? Notes,
    DateOnly? CompletedDate,
    int TotalSteps,
    int CompletedSteps,
    DateTime CreatedAt,
    List<ActivityStepDto> Steps);

public class CreateActivityPlanDto
{
    public DateOnly? PlannedDate { get; set; }

    [MaxLength(1000, ErrorMessage = "Notes must be 1000 characters or less")]
    public string? Notes { get; set; }
}

public interface IActivityPlanService
{
    Task<List<ActivityPlanDto>> GetPlansAsync(string userId);
    Task<ActivityPlanDto?> GetPlanAsync(string userId, Guid id);
    Task<Guid> CreateAsync(string userId, Guid ideaId, CreateActivityPlanDto dto);
    Task DeleteAsync(string userId, Guid id);
    Task<Guid> AddStepAsync(string userId, Guid planId, string title);
    Task ToggleStepAsync(string userId, Guid stepId);
    Task DeleteStepAsync(string userId, Guid stepId);
}
