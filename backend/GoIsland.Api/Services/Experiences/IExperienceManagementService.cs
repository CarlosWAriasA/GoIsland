using GoIsland.Api.DTOs.Experiences;

namespace GoIsland.Api.Services.Experiences;

public enum ExperienceManagementStatus
{
    Success,
    NotFound,
    Forbidden,
    Conflict,
    InvalidTransition,
    Incomplete,
    ReasonRequired
}

public enum ExperienceReviewAction
{
    Approve,
    Reject,
    Suspend
}

public record ExperienceManagementResult(
    ExperienceManagementStatus Status,
    HostExperienceResponse? Experience = null,
    string? Message = null);

public interface IExperienceManagementService
{
    Task<ExperienceManagementResult> CreateAsync(int hostUserId, CreateExperienceRequest request);
    Task<IReadOnlyCollection<HostExperienceResponse>> GetMineAsync(int hostUserId);
    Task<HostExperienceResponse?> GetMineByIdAsync(int hostUserId, int id);
    Task<ExperienceManagementResult> UpdateAsync(int hostUserId, int id, UpdateExperienceRequest request);
    Task<ExperienceManagementResult> DeleteAsync(int hostUserId, int id);
    Task<ExperienceManagementResult> SubmitAsync(int hostUserId, int id);
    Task<IReadOnlyCollection<HostExperienceResponse>> GetForAdminAsync(string? status);
    Task<ExperienceManagementResult> ReviewAsync(
        int id,
        int adminUserId,
        ExperienceReviewAction action,
        string? reason);
}
