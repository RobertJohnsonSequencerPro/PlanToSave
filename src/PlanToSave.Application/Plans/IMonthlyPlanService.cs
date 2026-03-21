using PlanToSave.Domain.Enums;

namespace PlanToSave.Application.Plans;

public interface IMonthlyPlanService
{
    Task<List<MonthlyPlanSummaryDto>> GetPlansAsync(string userId);
    Task<MonthlyPlanDetailDto?> GetPlanDetailAsync(string userId, int year, int month);
    Task<MonthlyPlanDetailDto> GetOrCreatePlanAsync(string userId, int year, int month);
    Task AddPlannedFlowAsync(string userId, Guid planId, CreatePlannedFlowDto dto);
    Task DeletePlannedFlowAsync(string userId, Guid plannedFlowId);
    Task SetStatusAsync(string userId, Guid planId, MonthlyPlanStatus status);
    Task SeedFromTemplatesAsync(string userId, Guid planId);
}
