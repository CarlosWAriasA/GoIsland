using GoIsland.Api.Data;
using GoIsland.Api.DTOs.Experiences;
using GoIsland.Api.Models;
using GoIsland.Api.Services.Images;
using Microsoft.EntityFrameworkCore;

namespace GoIsland.Api.Services.Experiences;

public class ExperienceImageService : IExperienceImageService
{
    public const int MaximumImages = 10;
    public const long MaximumFileBytes = 5 * 1024 * 1024;
    public const int MaximumDimension = 12_000;
    public const long MaximumPixels = 40_000_000;

    private static readonly IReadOnlyDictionary<string, string> AllowedExtensions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = ".jpg",
            ["image/png"] = ".png",
            ["image/webp"] = ".webp"
        };

    private readonly GoIslandDbContext _context;
    private readonly IImageStorage _storage;
    private readonly ILogger<ExperienceImageService> _logger;

    public ExperienceImageService(
        GoIslandDbContext context,
        IImageStorage storage,
        ILogger<ExperienceImageService> logger)
    {
        _context = context;
        _storage = storage;
        _logger = logger;
    }

    public async Task<ExperienceImageResult> UploadAsync(
        int hostUserId,
        int experienceId,
        IReadOnlyCollection<IFormFile> files,
        IReadOnlyCollection<string>? altTexts = null,
        int? coverIndex = null)
    {
        if (!await IsApprovedHostAsync(hostUserId))
        {
            return new(ExperienceImageStatus.Forbidden);
        }

        var experience = await _context.Experiences
            .Include(item => item.Images)
            .SingleOrDefaultAsync(item => item.Id == experienceId && item.HostId == hostUserId);
        if (experience is null)
        {
            return new(ExperienceImageStatus.NotFound);
        }
        if (experience.ApprovalStatus == ExperienceApprovalStatuses.Suspended)
        {
            return new(ExperienceImageStatus.InvalidTransition);
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

        if (altTexts is not null && altTexts.Count != files.Count)
        {
            return new(
                ExperienceImageStatus.InvalidFile,
                Message: "Describe brevemente cada imagen seleccionada.");
        }

        if (coverIndex is < 0 || coverIndex >= files.Count)
        {
            return new(
                ExperienceImageStatus.InvalidFile,
                Message: "Selecciona una portada válida.");
        }

        var normalizedAltTexts = (altTexts ?? files.Select(_ => string.Empty))
            .Select((altText, index) => string.IsNullOrWhiteSpace(altText)
                ? $"Foto de {experience.Title}"
                : altText.Trim())
            .ToArray();
        if (normalizedAltTexts.Any(altText => altText.Length is < 3 or > 180))
        {
            return new(
                ExperienceImageStatus.InvalidFile,
                Message: "Cada descripción debe tener entre 3 y 180 caracteres.");
        }

        foreach (var file in files)
        {
            var validationMessage = await ValidateAsync(file);
            if (validationMessage is not null)
            {
                return new(ExperienceImageStatus.InvalidFile, Message: validationMessage);
            }
        }

        var nextSortOrder = experience.Images.Count == 0
            ? 0
            : experience.Images.Max(image => image.SortOrder) + 1;
        var uploaded = new List<StoredImage>();
        var fileList = files.ToArray();
        var selectedCoverIndex = coverIndex
            ?? (experience.Images.Any(image => image.IsCover) ? null : 0);

        try
        {
            if (selectedCoverIndex is not null)
            {
                foreach (var currentCover in experience.Images.Where(image => image.IsCover))
                {
                    currentCover.IsCover = false;
                }
            }

            for (var index = 0; index < fileList.Length; index++)
            {
                var file = fileList[index];
                await using var content = file.OpenReadStream();
                var stored = await _storage.UploadAsync(
                    content,
                    file.FileName,
                    experienceId);
                uploaded.Add(stored);
                var pixels = (long)stored.Width * stored.Height;
                if (stored.Width <= 0
                    || stored.Height <= 0
                    || stored.Width > MaximumDimension
                    || stored.Height > MaximumDimension
                    || pixels > MaximumPixels)
                {
                    throw new InvalidOperationException(
                        "La imagen tiene dimensiones demasiado grandes o no se pudo leer.");
                }

                var image = new ExperienceImage
                {
                    Provider = stored.Provider,
                    PublicId = stored.PublicId,
                    SecureUrl = stored.SecureUrl,
                    Width = stored.Width,
                    Height = stored.Height,
                    Format = stored.Format,
                    AltText = normalizedAltTexts[index],
                    IsCover = selectedCoverIndex == index,
                    FileName = Path.GetFileName(stored.PublicId),
                    ContentType = file.ContentType,
                    SortOrder = nextSortOrder++,
                    CreatedAt = DateTime.UtcNow
                };
                experience.Images.Add(image);
            }

            ReturnToDraft(experience);
            await _context.SaveChangesAsync();
            return new(ExperienceImageStatus.Success, ToResponses(experience));
        }
        catch
        {
            foreach (var stored in uploaded)
            {
                await TryCompensateUploadAsync(stored.PublicId);
            }

            throw;
        }
    }

    public async Task<ExperienceImageResult> UpdateAsync(
        int hostUserId,
        int experienceId,
        int imageId,
        UpdateExperienceImageRequest request)
    {
        if (!await IsApprovedHostAsync(hostUserId))
        {
            return new(ExperienceImageStatus.Forbidden);
        }

        var image = await _context.ExperienceImages
            .Include(candidate => candidate.Experience)
            .SingleOrDefaultAsync(candidate =>
                candidate.Id == imageId
                && candidate.ExperienceId == experienceId
                && candidate.Experience.HostId == hostUserId);
        if (image is null)
        {
            return new(ExperienceImageStatus.NotFound);
        }
        if (image.Experience.ApprovalStatus == ExperienceApprovalStatuses.Suspended)
        {
            return new(ExperienceImageStatus.InvalidTransition);
        }

        var normalizedAltText = request.AltText.Trim();
        await using var transaction = _context.Database.CurrentTransaction is null
            ? await _context.Database.BeginTransactionAsync()
            : null;

        image.AltText = normalizedAltText;
        if (request.IsCover && !image.IsCover)
        {
            var currentCovers = await _context.ExperienceImages
                .Where(candidate => candidate.ExperienceId == experienceId && candidate.IsCover)
                .ToArrayAsync();
            foreach (var currentCover in currentCovers)
            {
                currentCover.IsCover = false;
            }

            await _context.SaveChangesAsync();
            image.IsCover = true;
        }
        else if (!request.IsCover && image.IsCover)
        {
            image.IsCover = false;
            await _context.SaveChangesAsync();

            var replacement = await _context.ExperienceImages
                .Where(candidate =>
                    candidate.ExperienceId == experienceId
                    && candidate.Id != imageId)
                .OrderBy(candidate => candidate.SortOrder)
                .FirstOrDefaultAsync();
            if (replacement is not null)
            {
                replacement.IsCover = true;
            }
        }

        ReturnToDraft(image.Experience);
        await _context.SaveChangesAsync();
        if (transaction is not null)
        {
            await transaction.CommitAsync();
        }

        return new(
            ExperienceImageStatus.Success,
            await ToResponsesAsync(experienceId));
    }

    public async Task<ExperienceImageResult> DeleteAsync(
        int hostUserId,
        int experienceId,
        int imageId)
    {
        if (!await IsApprovedHostAsync(hostUserId))
        {
            return new(ExperienceImageStatus.Forbidden);
        }

        var image = await _context.ExperienceImages
            .Include(candidate => candidate.Experience)
            .SingleOrDefaultAsync(candidate =>
                candidate.Id == imageId
                && candidate.ExperienceId == experienceId
                && candidate.Experience.HostId == hostUserId);
        if (image is null)
        {
            return new(ExperienceImageStatus.NotFound);
        }
        if (image.Experience.ApprovalStatus == ExperienceApprovalStatuses.Suspended)
        {
            return new(ExperienceImageStatus.InvalidTransition);
        }

        var externalPublicId = image.Provider == ImageStorageProviders.Cloudinary
            && !string.IsNullOrWhiteSpace(image.PublicId)
            ? image.PublicId
            : null;

        await using var transaction = _context.Database.CurrentTransaction is null
            ? await _context.Database.BeginTransactionAsync()
            : null;
        var wasCover = image.IsCover;
        _context.ExperienceImages.Remove(image);
        ReturnToDraft(image.Experience);
        await _context.SaveChangesAsync();

        if (wasCover)
        {
            var replacement = await _context.ExperienceImages
                .Where(candidate => candidate.ExperienceId == experienceId)
                .OrderBy(candidate => candidate.SortOrder)
                .FirstOrDefaultAsync();
            if (replacement is not null)
            {
                replacement.IsCover = true;
                await _context.SaveChangesAsync();
            }
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync();
        }
        if (externalPublicId is not null)
        {
            try
            {
                await _storage.DeleteAsync(externalPublicId);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception,
                    "La imagen {ImageId} se elimino de la experiencia {ExperienceId}, pero el archivo externo {PublicId} requiere limpieza.",
                    imageId,
                    experienceId,
                    externalPublicId);
            }
        }
        return new(
            ExperienceImageStatus.Success,
            await ToResponsesAsync(experienceId));
    }

    private async Task TryCompensateUploadAsync(string publicId)
    {
        try
        {
            await _storage.DeleteAsync(publicId);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Could not compensate Cloudinary upload {PublicId}.",
                publicId);
        }
    }

    private Task<bool> IsApprovedHostAsync(int userId) =>
        _context.HostProfiles.AnyAsync(profile =>
            profile.UserId == userId
            && profile.VerificationStatus == HostVerificationStatuses.Approved);

    private static void ReturnToDraft(Experience experience)
    {
        experience.IsApproved = false;
        experience.ApprovalStatus = ExperienceApprovalStatuses.Draft;
        experience.RejectionReason = null;
        experience.ReviewedAt = null;
        experience.ReviewedByAdminId = null;
        experience.UpdatedAt = DateTime.UtcNow;
    }

    private async Task<IReadOnlyCollection<ExperienceImageResponse>> ToResponsesAsync(int experienceId)
    {
        return await _context.ExperienceImages.AsNoTracking()
            .Where(candidate => candidate.ExperienceId == experienceId)
            .OrderByDescending(candidate => candidate.IsCover)
            .ThenBy(candidate => candidate.SortOrder)
            .Select(candidate => new ExperienceImageResponse
            {
                Id = candidate.Id,
                SourceUrl = candidate.SecureUrl
                    ?? $"/uploads/experiences/{experienceId}/{candidate.FileName}",
                AltText = candidate.AltText,
                CreditText = candidate.CreditText,
                CreditUrl = candidate.CreditUrl,
                LicenseName = candidate.LicenseName,
                LicenseUrl = candidate.LicenseUrl,
                IsCover = candidate.IsCover,
                SortOrder = candidate.SortOrder
            })
            .ToArrayAsync();
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
        await using (var stream = file.OpenReadStream())
        {
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

            if (!signatureIsValid)
            {
                return "Una de las imágenes no tiene un formato válido.";
            }
        }

        return null;
    }

    private static IReadOnlyCollection<ExperienceImageResponse> ToResponses(Experience experience) =>
        experience.Images
            .OrderByDescending(image => image.IsCover)
            .ThenBy(image => image.SortOrder)
            .Select(image => new ExperienceImageResponse
            {
                Id = image.Id,
                SourceUrl = image.SecureUrl
                    ?? $"/uploads/experiences/{experience.Id}/{image.FileName}",
                AltText = image.AltText,
                CreditText = image.CreditText,
                CreditUrl = image.CreditUrl,
                LicenseName = image.LicenseName,
                LicenseUrl = image.LicenseUrl,
                IsCover = image.IsCover,
                SortOrder = image.SortOrder
            })
            .ToArray();
}
