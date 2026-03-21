using System.ComponentModel.DataAnnotations;
using PlanToSave.Domain.Enums;

namespace PlanToSave.Application.Ideas;

public record IdeaDto(
    Guid Id,
    string Title,
    string? Description,
    IdeaCategory Category,
    IdeaEnergyLevel EnergyLevel,
    IdeaCostEstimate CostEstimate,
    decimal? EstimatedAmount,
    string? Tags,
    IdeaStatus Status,
    DateTime CreatedAt);

public class SaveIdeaDto
{
    [Required(ErrorMessage = "Title is required")]
    [MaxLength(200, ErrorMessage = "Title must be 200 characters or less")]
    public string Title { get; set; } = "";

    [MaxLength(1000, ErrorMessage = "Description must be 1000 characters or less")]
    public string? Description { get; set; }

    public IdeaCategory Category { get; set; } = IdeaCategory.Other;
    public IdeaEnergyLevel EnergyLevel { get; set; } = IdeaEnergyLevel.Medium;
    public IdeaCostEstimate CostEstimate { get; set; } = IdeaCostEstimate.Cheap;

    [Range(0, 1_000_000, ErrorMessage = "Estimated amount must be between 0 and 1,000,000")]
    public decimal? EstimatedAmount { get; set; }

    [MaxLength(500, ErrorMessage = "Tags must be 500 characters or less")]
    public string? Tags { get; set; }
}

public class IdeaFilterDto
{
    public IdeaCategory? Category { get; set; }
    public IdeaEnergyLevel? EnergyLevel { get; set; }
    public IdeaCostEstimate? CostEstimate { get; set; }
    public IdeaStatus? Status { get; set; }
    public string? Tag { get; set; }
}

public interface IIdeaService
{
    Task<List<IdeaDto>> GetIdeasAsync(string userId, IdeaFilterDto? filter = null);
    Task<Guid> CreateAsync(string userId, SaveIdeaDto dto);
    Task DeleteAsync(string userId, Guid id);
}
