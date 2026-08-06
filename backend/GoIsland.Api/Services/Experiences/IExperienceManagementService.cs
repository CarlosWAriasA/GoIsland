using GoIsland.Api.DTOs.Common;
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
    string? Message = null,
    IReadOnlyDictionary<string, string[]>? Errors = null);

public interface IExperienceManagementService
{
    Task<ExperienceManagementResult> CreateAsync(int hostUserId, CreateExperienceRequest request);
    Task<PagedResponse<HostExperienceResponse>> GetMineAsync(
        int hostUserId,
        ManagedExperienceListRequest request);
    Task<HostExperienceResponse?> GetMineByIdAsync(int hostUserId, int id);
    Task<ExperienceManagementResult> UpdateAsync(int hostUserId, int id, UpdateExperienceRequest request);
    Task<ExperienceManagementResult> DeleteAsync(int hostUserId, int id);
    Task<ExperienceManagementResult> SubmitAsync(int hostUserId, int id);
    Task<PagedResponse<HostExperienceResponse>> GetForAdminAsync(
        ManagedExperienceListRequest request);
    Task<ExperienceManagementResult> ReviewAsync(
        int id,
        int adminUserId,
        ExperienceReviewAction action,
        string? reason);
}
