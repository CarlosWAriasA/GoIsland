using GoIsland.Api.DTOs.Hosts;

namespace GoIsland.Api.Services.Hosts;

public enum HostOperationStatus
{
    Success,
    NotFound,
    Forbidden,
    Conflict,
    InvalidTransition,
    ReasonRequired
}

public enum HostReviewAction
{
    Approve,
    Reject,
    Suspend
}

public record HostOperationResult(HostOperationStatus Status, HostProfileResponse? Profile = null);

public interface IHostService
{
    Task<HostOperationResult> ApplyAsync(int userId, HostApplicationRequest request);
    Task<HostProfileResponse?> GetMineAsync(int userId);
    Task<HostOperationResult> UpdateMineAsync(int userId, UpdateHostProfileRequest request);
    Task<IReadOnlyCollection<HostProfileResponse>> GetForAdminAsync(string? status);
    Task<HostProfileResponse?> GetByIdForAdminAsync(int id);
    Task<HostOperationResult> ReviewAsync(
        int id,
        int adminUserId,
        HostReviewAction action,
        string? reason);
}
