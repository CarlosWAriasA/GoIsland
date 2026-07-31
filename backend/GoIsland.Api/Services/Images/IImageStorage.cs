namespace GoIsland.Api.Services.Images;

public record StoredImage(
    string Provider,
    string PublicId,
    string SecureUrl,
    int Width,
    int Height,
    string Format);

public interface IImageStorage
{
    Task<StoredImage> UploadAsync(
        Stream content,
        string fileName,
        int experienceId,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string publicId, CancellationToken cancellationToken = default);
}
