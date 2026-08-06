using GoIsland.Api.DTOs.Common;
using GoIsland.Api.DTOs.Reviews;

namespace GoIsland.Api.Services.Reviews;

public interface IReviewService
{
    Task<ReviewMutationResult> CreateAsync(int userId, int reservationId, ReviewRequest request);
    Task<ReviewMutationResult> UpdateAsync(int userId, int id, ReviewRequest request);
    Task<ReviewMutationStatus> DeleteAsync(int userId, int id);
    Task<PagedResponse<ReviewResponse>> GetForExperienceAsync(
        int experienceId,
        ReviewListRequest request);
    Task<PagedResponse<ReviewResponse>> GetForHostAsync(int hostId, ReviewListRequest request);
    Task<PagedResponse<ReviewResponse>> GetForAdminAsync(ReviewListRequest request);
    Task<ReviewMutationResult> HideAsync(int adminUserId, int id, string reason);
}
