using GoIsland.Api.DTOs.Experiences;

namespace GoIsland.Api.Services.Experiences;

public enum ExperienceImageStatus
{
    Success,
    NotFound,
    Forbidden,
    InvalidTransition,
    InvalidFile,
    LimitExceeded
}

public record ExperienceImageResult(
    ExperienceImageStatus Status,
    IReadOnlyCollection<ExperienceImageResponse>? Images = null,
    string? Message = null);

public interface IExperienceImageService
{
    Task<ExperienceImageResult> UploadAsync(
        int hostUserId,
        int experienceId,
        IReadOnlyCollection<IFormFile> files,
        IReadOnlyCollection<string>? altTexts = null,
        int? coverIndex = null);

    Task<ExperienceImageResult> UpdateAsync(
        int hostUserId,
        int experienceId,
        int imageId,
        UpdateExperienceImageRequest request);
    Task<ExperienceImageResult> DeleteAsync(int hostUserId, int experienceId, int imageId);
}
