using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using GoIsland.Api.Models;

namespace GoIsland.Api.Services.Images;

public sealed class CloudinaryImageStorage : IImageStorage
{
    private readonly Cloudinary? _cloudinary;

    public CloudinaryImageStorage(IConfiguration configuration)
    {
        var cloudName = configuration["Cloudinary:CloudName"]?.Trim();
        var apiKey = configuration["Cloudinary:ApiKey"]?.Trim();
        var apiSecret = configuration["Cloudinary:ApiSecret"]?.Trim();
        if (string.IsNullOrWhiteSpace(cloudName)
            || string.IsNullOrWhiteSpace(apiKey)
            || string.IsNullOrWhiteSpace(apiSecret))
        {
            return;
        }

        _cloudinary = new Cloudinary(new Account(cloudName, apiKey, apiSecret));
        _cloudinary.Api.Secure = true;
    }

    public async Task<StoredImage> UploadAsync(
        Stream content,
        string fileName,
        int experienceId,
        CancellationToken cancellationToken = default)
    {
        var cloudinary = GetConfiguredClient();
        var parameters = new ImageUploadParams
        {
            File = new FileDescription(fileName, content),
            Folder = $"goisland/experiences/{experienceId}",
            UniqueFilename = true,
            Overwrite = false
        };

        var result = await cloudinary.UploadAsync(parameters, cancellationToken);
        if (result.Error is not null
            || string.IsNullOrWhiteSpace(result.PublicId)
            || result.SecureUrl is null)
        {
            throw new InvalidOperationException(
                $"Cloudinary no pudo guardar la imagen: {result.Error?.Message ?? "respuesta incompleta"}.");
        }

        return new StoredImage(
            ImageStorageProviders.Cloudinary,
            result.PublicId,
            result.SecureUrl.AbsoluteUri,
            result.Width,
            result.Height,
            result.Format);
    }

    public async Task DeleteAsync(string publicId, CancellationToken cancellationToken = default)
    {
        var result = await GetConfiguredClient().DestroyAsync(new DeletionParams(publicId)
        {
            Invalidate = true,
            ResourceType = ResourceType.Image
        });

        if (result.Error is not null
            || (result.Result != "ok" && result.Result != "not found"))
        {
            throw new InvalidOperationException(
                $"Cloudinary no pudo eliminar la imagen: {result.Error?.Message ?? result.Result}.");
        }
    }

    private Cloudinary GetConfiguredClient()
    {
        return _cloudinary ?? throw new InvalidOperationException(
            "Cloudinary:CloudName, Cloudinary:ApiKey y Cloudinary:ApiSecret deben configurarse "
            + "para almacenar imágenes.");
    }
}
