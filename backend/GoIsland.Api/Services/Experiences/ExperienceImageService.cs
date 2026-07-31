using GoIsland.Api.Data;
using GoIsland.Api.DTOs.Experiences;
using GoIsland.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GoIsland.Api.Services.Experiences;

public class ExperienceImageService : IExperienceImageService
{
    public const int MaximumImages = 10;
    public const long MaximumFileBytes = 5 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, string> AllowedExtensions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = ".jpg",
            ["image/png"] = ".png",
            ["image/webp"] = ".webp"
        };

    private readonly GoIslandDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public ExperienceImageService(GoIslandDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    public async Task<ExperienceImageResult> UploadAsync(
        int hostUserId,
        int experienceId,
        IReadOnlyCollection<IFormFile> files)
    {
        var experience = await _context.Experiences
            .Include(item => item.Images)
            .SingleOrDefaultAsync(item => item.Id == experienceId && item.HostId == hostUserId);
        if (experience is null)
        {
            return new(ExperienceImageStatus.NotFound);
        }

        if (files.Count == 0)
        {
            return new(
                ExperienceImageStatus.InvalidFile,
                Message: "Selecciona al menos una imagen.");
        }

        if (experience.Images.Count + files.Count > MaximumImages)
        {
            return new(
                ExperienceImageStatus.LimitExceeded,
                Message: $"Cada experiencia admite un máximo de {MaximumImages} imágenes.");
        }

        foreach (var file in files)
        {
            var validationMessage = await ValidateAsync(file);
            if (validationMessage is not null)
            {
                return new(ExperienceImageStatus.InvalidFile, Message: validationMessage);
            }
        }

        var directory = GetExperienceDirectory(experienceId);
        Directory.CreateDirectory(directory);
        var nextSortOrder = experience.Images.Count == 0
            ? 0
            : experience.Images.Max(image => image.SortOrder) + 1;
        var writtenPaths = new List<string>();

        try
        {
            foreach (var file in files)
            {
                var fileName = $"{Guid.NewGuid():N}{AllowedExtensions[file.ContentType]}";
                var targetPath = Path.Combine(directory, fileName);
                await using (var target = new FileStream(
                    targetPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    useAsync: true))
                {
                    await file.CopyToAsync(target);
                }

                writtenPaths.Add(targetPath);
                experience.Images.Add(new ExperienceImage
                {
                    FileName = fileName,
                    ContentType = file.ContentType,
                    SortOrder = nextSortOrder++,
                    CreatedAt = DateTime.UtcNow
                });
            }

            experience.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return new(ExperienceImageStatus.Success, ToResponses(experience));
        }
        catch
        {
            foreach (var path in writtenPaths)
            {
                if (File.Exists(path)) File.Delete(path);
            }

            throw;
        }
    }

    public async Task<ExperienceImageResult> DeleteAsync(
        int hostUserId,
        int experienceId,
        int imageId)
    {
        var image = await (from candidate in _context.ExperienceImages
                           join experience in _context.Experiences
                               on candidate.ExperienceId equals experience.Id
                           where candidate.Id == imageId
                               && candidate.ExperienceId == experienceId
                               && experience.HostId == hostUserId
                           select candidate)
            .SingleOrDefaultAsync();
        if (image is null)
        {
            return new(ExperienceImageStatus.NotFound);
        }

        _context.ExperienceImages.Remove(image);
        await _context.SaveChangesAsync();

        var path = Path.Combine(GetExperienceDirectory(experienceId), image.FileName);
        if (File.Exists(path)) File.Delete(path);

        var remaining = await _context.ExperienceImages.AsNoTracking()
            .Where(candidate => candidate.ExperienceId == experienceId)
            .OrderBy(candidate => candidate.SortOrder)
            .Select(candidate => new ExperienceImageResponse
            {
                Id = candidate.Id,
                Url = $"/uploads/experiences/{experienceId}/{candidate.FileName}",
                SortOrder = candidate.SortOrder
            })
            .ToArrayAsync();
        return new(ExperienceImageStatus.Success, remaining);
    }

    public Task CleanupDirectoryAsync(int experienceId)
    {
        var directory = GetExperienceDirectory(experienceId);
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }

        return Task.CompletedTask;
    }

    private string GetExperienceDirectory(int experienceId)
    {
        var webRoot = _environment.WebRootPath
            ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        return Path.Combine(webRoot, "uploads", "experiences", experienceId.ToString());
    }

    private static async Task<string?> ValidateAsync(IFormFile file)
    {
        if (file.Length == 0)
        {
            return "Una de las imágenes está vacía.";
        }

        if (file.Length > MaximumFileBytes)
        {
            return "Cada imagen debe pesar 5 MB o menos.";
        }

        if (!AllowedExtensions.ContainsKey(file.ContentType))
        {
            return "Solo se admiten imágenes JPG, PNG o WebP.";
        }

        var header = new byte[12];
        await using var stream = file.OpenReadStream();
        var bytesRead = await stream.ReadAsync(header);
        var signatureIsValid = file.ContentType.ToLowerInvariant() switch
        {
            "image/jpeg" => bytesRead >= 3
                && header[0] == 0xFF
                && header[1] == 0xD8
                && header[2] == 0xFF,
            "image/png" => bytesRead >= 8
                && header.AsSpan(0, 8).SequenceEqual(
                    new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            "image/webp" => bytesRead >= 12
                && header.AsSpan(0, 4).SequenceEqual("RIFF"u8)
                && header.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            _ => false
        };

        return signatureIsValid ? null : "Una de las imágenes no tiene un formato válido.";
    }

    private static IReadOnlyCollection<ExperienceImageResponse> ToResponses(Experience experience) =>
        experience.Images
            .OrderBy(image => image.SortOrder)
            .Select(image => new ExperienceImageResponse
            {
                Id = image.Id,
                Url = $"/uploads/experiences/{experience.Id}/{image.FileName}",
                SortOrder = image.SortOrder
            })
            .ToArray();
}
