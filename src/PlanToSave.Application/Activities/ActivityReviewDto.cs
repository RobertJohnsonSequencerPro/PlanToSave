using System.ComponentModel.DataAnnotations;
using PlanToSave.Domain.Enums;

namespace PlanToSave.Application.Activities;

public record ActivityReviewDto(
    Guid Id,
    Guid ActivityPlanId,
    Guid IdeaId,
    string IdeaTitle,
    IdeaCategory IdeaCategory,
    DateOnly? PlannedDate,
    int? Rating,
    string? Reflection,
    decimal? ActualAmount,
    DateTime CreatedAt);

/// <summary>Form model for submitting a "Yes, I did it!" review.</summary>
public class SubmitReviewDto
{
    [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
    public int? Rating { get; set; }

    [MaxLength(2000, ErrorMessage = "Reflection must be 2000 characters or less.")]
    public string? Reflection { get; set; }

    [Range(0, 1_000_000, ErrorMessage = "Enter a valid amount.")]
    public decimal? ActualAmount { get; set; }
}

public interface IActivityReviewService
{
    /// <summary>Returns Upcoming plans whose PlannedDate is in the past (overdue).</summary>
    Task<List<ActivityPlanDto>> GetNeedsReviewAsync(string userId);

    /// <summary>Mark a plan Done and record a review.</summary>
    Task SubmitAsync(string userId, Guid planId, SubmitReviewDto dto);

    /// <summary>Mark a plan Skipped — returns idea to Backlog.</summary>
    Task SkipAsync(string userId, Guid planId);
}
