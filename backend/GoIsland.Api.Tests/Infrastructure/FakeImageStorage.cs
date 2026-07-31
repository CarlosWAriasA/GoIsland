using GoIsland.Api.Services.Images;

namespace GoIsland.Api.Tests.Infrastructure;

public sealed class FakeImageStorage : IImageStorage
{
    public Task<StoredImage> UploadAsync(
        Stream content,
        string fileName,
        int experienceId,
        CancellationToken cancellationToken = default)
    {
        var publicId = $"tests/experiences/{experienceId}/{Guid.NewGuid():N}";
        return Task.FromResult(new StoredImage(
            "Cloudinary",
            publicId,
            $"https://res.cloudinary.com/test/image/upload/{publicId}.jpg",
            1200,
            800,
            "jpg"));
    }

    public Task DeleteAsync(string publicId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
