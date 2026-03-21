using System.ComponentModel.DataAnnotations;
using PlanToSave.Domain.Enums;

namespace PlanToSave.Application.Templates;

public record FlowTemplateDto(
    Guid Id,
    Guid FromAccountId,
    string FromAccountName,
    AccountType FromAccountType,
    Guid ToAccountId,
    string ToAccountName,
    AccountType ToAccountType,
    decimal Amount,
    string Description,
    bool IsActive,
    DateTime CreatedAt);

public class CreateFlowTemplateDto
{
    public Guid FromAccountId { get; set; }
    public Guid ToAccountId { get; set; }

    [Range(0.01, 10_000_000, ErrorMessage = "Amount must be greater than zero")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "Description is required")]
    [MaxLength(300, ErrorMessage = "Description must be 300 characters or less")]
    public string Description { get; set; } = "";
}

public interface IFlowTemplateService
{
    Task<List<FlowTemplateDto>> GetTemplatesAsync(string userId);
    Task CreateAsync(string userId, CreateFlowTemplateDto dto);
    Task ToggleActiveAsync(string userId, Guid templateId);
    Task DeleteAsync(string userId, Guid templateId);
}
