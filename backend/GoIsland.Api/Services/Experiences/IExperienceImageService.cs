using GoIsland.Api.DTOs.Experiences;

namespace GoIsland.Api.Services.Experiences;

public enum ExperienceImageStatus
{
    Success,
    NotFound,
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
        IReadOnlyCollection<IFormFile> files);

    Task<ExperienceImageResult> DeleteAsync(int hostUserId, int experienceId, int imageId);
    Task CleanupDirectoryAsync(int experienceId);
}
