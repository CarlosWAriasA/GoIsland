using GoIsland.Api.DTOs.Reviews;

namespace GoIsland.Api.Services.Reviews;

public interface IReviewService
{
    Task<ReviewMutationResult> CreateAsync(int userId, int reservationId, ReviewRequest request);
    Task<ReviewMutationResult> UpdateAsync(int userId, int id, ReviewRequest request);
    Task<ReviewMutationStatus> DeleteAsync(int userId, int id);
    Task<IReadOnlyCollection<ReviewResponse>> GetForExperienceAsync(int experienceId);
    Task<IReadOnlyCollection<ReviewResponse>> GetForHostAsync(int hostId);
    Task<IReadOnlyCollection<ReviewResponse>> GetForAdminAsync(string? status);
    Task<ReviewMutationResult> HideAsync(int adminUserId, int id, string reason);
}
