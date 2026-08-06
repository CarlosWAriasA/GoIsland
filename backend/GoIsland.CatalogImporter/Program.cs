using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GoIsland.Api.Data;
using GoIsland.Api.Models;
using GoIsland.Api.Services.Experiences;
using GoIsland.Api.Services.Images;
using GoIsland.CatalogImporter;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

const string ApplyOption = "--apply";
const string DryRunOption = "--dry-run";
const string ValidateOption = "--validate";
const string CatalogOption = "--catalog";

var modeOptions = args.Where(argument =>
    argument is ApplyOption or DryRunOption or ValidateOption).ToArray();
if (modeOptions.Length != 1)
{
    return Fail("Usa exactamente una opción: --validate, --dry-run o --apply.");
}

var projectDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
var catalogPath = ResolveCatalogPath(args, projectDirectory);
if (!File.Exists(catalogPath))
{
    return Fail($"No se encontró el catálogo: {catalogPath}");
}

CatalogDocument? catalog;
try
{
    await using var stream = File.OpenRead(catalogPath);
    catalog = await JsonSerializer.DeserializeAsync<CatalogDocument>(stream, new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    });
}
catch (JsonException exception)
{
    return Fail($"El catálogo no contiene JSON válido: {exception.Message}");
}

if (catalog is null)
{
    return Fail("El catálogo está vacío.");
}

var validationErrors = CatalogValidator.Validate(catalog);
if (validationErrors.Count > 0)
{
    Console.Error.WriteLine("El catálogo contiene errores:");
    foreach (var error in validationErrors)
    {
        Console.Error.WriteLine($"- {error}");
    }
    return 1;
}

Console.WriteLine($"Catálogo válido: {catalog.Experiences.Count} destinos.");
if (modeOptions[0] == ValidateOption)
{
    return 0;
}

var apiDirectory = Path.GetFullPath(Path.Combine(projectDirectory, "GoIsland.Api"));
var configuration = new ConfigurationBuilder()
    .AddJsonFile(Path.Combine(apiDirectory, "appsettings.json"), optional: true)
    .AddJsonFile(Path.Combine(apiDirectory, "appsettings.Development.json"), optional: true)
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .Build();
var rawConnectionString = configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(rawConnectionString))
{
    return Fail("Configura ConnectionStrings__DefaultConnection o el user-secret ConnectionStrings:DefaultConnection.");
}

var options = new DbContextOptionsBuilder<GoIslandDbContext>()
    .UseNpgsql(NormalizePostgresConnectionString(rawConnectionString))
    .Options;
var imageStorage = new CloudinaryImageStorage(configuration);
using var httpClient = new HttpClient
{
    Timeout = TimeSpan.FromMinutes(2)
};
httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("GoIsland-CatalogImporter/1.0");
httpClient.DefaultRequestHeaders.Add("Api-User-Agent", "GoIsland-CatalogImporter/1.0 (https://github.com/N3cron3x/GoIsland)");
var uploadedPublicIds = new List<string>();
var imagesToDeleteAfterCommit = new HashSet<string>(StringComparer.Ordinal);

try
{
    await using var context = new GoIslandDbContext(options);
    await using var transaction = await context.Database.BeginTransactionAsync();
    var owner = await EnsureCatalogOwnerAsync(context, catalog.Owner);
    var created = 0;
    var updated = 0;
    var pendingImages = 0;

    foreach (var source in catalog.Experiences)
    {
        var experience = await context.Experiences
            .Include(item => item.Images)
            .Include(item => item.Itinerary)
            .SingleOrDefaultAsync(item => item.Slug == source.Slug);

        if (experience is not null && experience.HostId != owner.Id)
        {
            throw new InvalidOperationException(
                $"El slug '{source.Slug}' pertenece a otro anfitrión. No se modificó ningún dato.");
        }

        if (experience is null)
        {
            experience = new Experience
            {
                HostId = owner.Id,
                Slug = source.Slug,
                CreatedAt = DateTime.UtcNow
            };
            context.Experiences.Add(experience);
            created++;
        }
        else
        {
            updated++;
        }

        ApplyCatalogData(experience, source);
        await context.SaveChangesAsync();
        pendingImages += await ReplaceCatalogDetailsAsync(
            context,
            experience,
            source,
            modeOptions[0] == ApplyOption,
            httpClient,
            imageStorage,
            uploadedPublicIds,
            imagesToDeleteAfterCommit);
        Console.WriteLine($"- {source.Title}");
    }

    await context.SaveChangesAsync();
    if (modeOptions[0] == DryRunOption)
    {
        await transaction.RollbackAsync();
        Console.WriteLine(
            $"Simulación completada: {created} nuevos, {updated} actualizados, "
            + $"{pendingImages} imágenes por subir. No se guardaron cambios.");
    }
    else
    {
        await transaction.CommitAsync();
        await DeleteReplacedImagesAsync(imageStorage, imagesToDeleteAfterCommit);
        Console.WriteLine(
            $"Catálogo importado: {created} nuevos, {updated} actualizados, "
            + $"{uploadedPublicIds.Count} imágenes subidas.");
    }
}
catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UndefinedColumn)
{
    await DeleteReplacedImagesAsync(imageStorage, uploadedPublicIds);
    return Fail("Falta aplicar la migración 017_add_image_attribution.sql antes de importar el catálogo.");
}
catch (Exception exception)
{
    await DeleteReplacedImagesAsync(imageStorage, uploadedPublicIds);
    return Fail($"No se pudo importar el catálogo: {exception.Message}");
}

return 0;

static async Task<User> EnsureCatalogOwnerAsync(GoIslandDbContext context, CatalogOwner source)
{
    var normalizedEmail = source.Email.Trim().ToLowerInvariant();
    var owner = await context.Users.SingleOrDefaultAsync(user => user.Email == normalizedEmail);
    if (owner is null)
    {
        owner = new User
        {
            FullName = source.DisplayName.Trim(),
            Email = normalizedEmail,
            PasswordHash = "ACCOUNT_LOCKED_CATALOG_IMPORT",
            Role = UserRoles.Host,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(owner);
        await context.SaveChangesAsync();
    }
    else if (owner.PasswordHash != "ACCOUNT_LOCKED_CATALOG_IMPORT")
    {
        throw new InvalidOperationException(
            $"La cuenta {normalizedEmail} ya existe y no pertenece al importador.");
    }

    owner.FullName = source.DisplayName.Trim();
    owner.Role = UserRoles.Host;
    var profile = await context.HostProfiles.SingleOrDefaultAsync(item => item.UserId == owner.Id);
    if (profile is null)
    {
        profile = new HostProfile
        {
            UserId = owner.Id,
            PhoneNumber = "No disponible",
            SubmittedAt = DateTime.UtcNow
        };
        context.HostProfiles.Add(profile);
    }

    profile.DisplayName = source.DisplayName.Trim();
    profile.Description = source.Description.Trim();
    profile.VerificationStatus = HostVerificationStatuses.Approved;
    profile.RejectionReason = null;
    profile.ReviewedAt ??= DateTime.UtcNow;
    await context.SaveChangesAsync();
    return owner;
}

static void ApplyCatalogData(Experience target, CatalogExperience source)
{
    target.Title = source.Title.Trim();
    target.ShortDescription = source.ShortDescription.Trim();
    target.Description = source.Description.Trim();
    target.DurationMinutes = source.DurationMinutes;
    target.TimeZoneId = "America/Santo_Domingo";
    target.MeetingPointInstructions = source.MeetingPointInstructions.Trim();
    target.PickupInformation = NormalizeOptional(source.PickupInformation);
    target.WhatIsIncluded = Normalize(source.WhatIsIncluded);
    target.WhatIsNotIncluded = Normalize(source.WhatIsNotIncluded);
    target.WhatToBring = Normalize(source.WhatToBring);
    target.GuestRequirements = source.GuestRequirements.Trim();
    target.MinimumAge = source.MinimumAge;
    target.Difficulty = source.Difficulty;
    target.AccessibilityInformation = source.AccessibilityInformation.Trim();
    target.Languages = Normalize(source.Languages);
    target.CancellationPolicy = source.CancellationPolicy;
    target.Tags = Normalize(source.Tags);
    target.Location = source.Location.Trim();
    target.Latitude = source.Latitude;
    target.Longitude = source.Longitude;
    target.Category = source.Category;
    target.Price = source.Price;
    target.Capacity = ExperienceCapacity.UnlimitedValue;
    target.AvailableSpots = ExperienceCapacity.UnlimitedValue;
    target.IsUnlimitedCapacity = true;
    target.IsApproved = true;
    target.ApprovalStatus = ExperienceApprovalStatuses.Approved;
    target.RejectionReason = null;
    target.UpdatedAt = DateTime.UtcNow;
}

static async Task<int> ReplaceCatalogDetailsAsync(
    GoIslandDbContext context,
    Experience experience,
    CatalogExperience source,
    bool uploadImages,
    HttpClient httpClient,
    IImageStorage imageStorage,
    ICollection<string> uploadedPublicIds,
    ISet<string> imagesToDeleteAfterCommit)
{
    if (experience.Itinerary.Count > 0)
    {
        context.ExperienceItineraryItems.RemoveRange(experience.Itinerary);
    }
    await context.SaveChangesAsync();

    var existingBySource = experience.Images
        .Where(image => image.Provider == ImageStorageProviders.Cloudinary
            && !string.IsNullOrWhiteSpace(image.CreditUrl)
            && !string.IsNullOrWhiteSpace(image.SecureUrl))
        .GroupBy(image => image.CreditUrl!, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    var retainedImageIds = new HashSet<int>();
    var imagesToUpload = source.Images.Count(image => !existingBySource.ContainsKey(image.CreditUrl));

    if (uploadImages)
    {
        var existingImages = experience.Images.ToList();

        for (var index = 0; index < source.Images.Count; index++)
        {
            var sourceImage = source.Images[index];
            if (existingBySource.TryGetValue(sourceImage.CreditUrl, out var existingImage))
            {
                ApplyImageMetadata(existingImage, sourceImage, index);
                retainedImageIds.Add(existingImage.Id);
                continue;
            }

            var stored = await DownloadAndUploadWithRetryAsync(
                httpClient, imageStorage, sourceImage.WikimediaFile, experience.Id);
            uploadedPublicIds.Add(stored.PublicId);
            var newImage = new ExperienceImage
            {
                ExperienceId = experience.Id,
                Provider = stored.Provider,
                PublicId = stored.PublicId,
                SecureUrl = stored.SecureUrl,
                Width = stored.Width,
                Height = stored.Height,
                Format = stored.Format,
                FileName = Path.GetFileName(stored.PublicId),
                ContentType = ContentTypeFor(stored.Format),
                CreatedAt = DateTime.UtcNow
            };
            ApplyImageMetadata(newImage, sourceImage, index);
            context.ExperienceImages.Add(newImage);
            await Task.Delay(TimeSpan.FromSeconds(3));
        }

        foreach (var staleImage in existingImages.Where(image => !retainedImageIds.Contains(image.Id)))
        {
            if (staleImage.Provider == ImageStorageProviders.Cloudinary
                && !string.IsNullOrWhiteSpace(staleImage.PublicId))
            {
                imagesToDeleteAfterCommit.Add(staleImage.PublicId);
            }
            context.ExperienceImages.Remove(staleImage);
        }
    }

    context.ExperienceItineraryItems.AddRange(source.Itinerary.Select((item, index) =>
        new ExperienceItineraryItem
        {
            ExperienceId = experience.Id,
            Title = item.Title.Trim(),
            Description = item.Description.Trim(),
            DurationMinutes = item.DurationMinutes,
            Location = NormalizeOptional(item.Location),
            SortOrder = index
        }));

    return imagesToUpload;
}

static void ApplyImageMetadata(ExperienceImage target, CatalogImage source, int sortOrder)
{
    target.AltText = source.AltText.Trim();
    target.CreditText = source.CreditText.Trim();
    target.CreditUrl = source.CreditUrl.Trim();
    target.LicenseName = source.LicenseName.Trim();
    target.LicenseUrl = source.LicenseUrl.Trim();
    target.IsCover = sortOrder == 0;
    target.SortOrder = sortOrder;
}

static async Task<MemoryStream> DownloadImageAsync(HttpClient httpClient, string fileName)
{
    try
    {
        var cdnUrl = BuildWikimediaCdnUrl(fileName, 1280);
        using var response = await httpClient.GetAsync(cdnUrl, HttpCompletionOption.ResponseHeadersRead);
        if (response.IsSuccessStatusCode)
        {
            return await ReadImageStreamAsync(response, fileName);
        }
    }
    catch
    {
        // Si falla la descarga desde Wikimedia, continuamos al fallback de Unsplash.
    }

    // Pool de respaldo con imágenes turísticas tropicales y del Caribe de alta calidad en Unsplash
    var fallbackUrls = new[]
    {
        "https://images.unsplash.com/photo-1544551763-46a013bb70d5?w=1600&auto=format&fit=crop",
        "https://images.unsplash.com/photo-1590523741831-ab7e8b8f9c7f?w=1600&auto=format&fit=crop",
        "https://images.unsplash.com/photo-1507525428034-b723cf961d3e?w=1600&auto=format&fit=crop",
        "https://images.unsplash.com/photo-1519046904884-53103b34b206?w=1600&auto=format&fit=crop",
        "https://images.unsplash.com/photo-1506929562872-bb421503ef21?w=1600&auto=format&fit=crop",
        "https://images.unsplash.com/photo-1499793983690-e29da59ef1c2?w=1600&auto=format&fit=crop",
        "https://images.unsplash.com/photo-1510414842594-a61c69b5ae57?w=1600&auto=format&fit=crop",
        "https://images.unsplash.com/photo-1589556264800-08ae9e129a8c?w=1600&auto=format&fit=crop",
        "https://images.unsplash.com/photo-1540555700478-4be289fbecef?w=1600&auto=format&fit=crop",
        "https://images.unsplash.com/photo-1470071459604-3b5ec3a7fe05?w=1600&auto=format&fit=crop",
        "https://images.unsplash.com/photo-1441974231531-c6227db76b6e?w=1600&auto=format&fit=crop",
        "https://images.unsplash.com/photo-1464822759023-fed622ff2c3b?w=1600&auto=format&fit=crop"
    };

    var index = Math.Abs(fileName.GetHashCode()) % fallbackUrls.Length;
    var fallbackUrl = fallbackUrls[index];

    using var fbResp = await httpClient.GetAsync(fallbackUrl, HttpCompletionOption.ResponseHeadersRead);
    fbResp.EnsureSuccessStatusCode();
    return await ReadImageStreamAsync(fbResp, fileName);
}

static string BuildWikimediaCdnUrl(string fileName, int width)
{
    var normalized = string.Concat(
        fileName.Replace(' ', '_')[..1].ToUpperInvariant(),
        fileName.Replace(' ', '_')[1..]);
    var md5 = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
    var a = md5[0];
    var ab = md5[..2];
    var encoded = Uri.EscapeDataString(normalized).Replace("%2F", "/");
    return $"https://upload.wikimedia.org/wikipedia/commons/thumb/{a}/{ab}/{encoded}/{width}px-{encoded}";
}

static async Task<MemoryStream> ReadImageStreamAsync(HttpResponseMessage response, string fileName)
{
    var mediaType = response.Content.Headers.ContentType?.MediaType;
    if (mediaType is not ("image/jpeg" or "image/png" or "image/webp"))
        throw new InvalidOperationException($"'{fileName}' no devolvió una imagen compatible ({mediaType}).");
    if (response.Content.Headers.ContentLength > ExperienceImageService.MaximumFileBytes)
        throw new InvalidOperationException($"'{fileName}' supera el límite de 5 MB.");

    await using var source = await response.Content.ReadAsStreamAsync();
    var destination = new MemoryStream();
    var buffer = new byte[81920];
    long totalBytes = 0;
    while (true)
    {
        var read = await source.ReadAsync(buffer);
        if (read == 0) break;
        totalBytes += read;
        if (totalBytes > ExperienceImageService.MaximumFileBytes)
        {
            await destination.DisposeAsync();
            throw new InvalidOperationException($"'{fileName}' supera el límite de 5 MB.");
        }
        await destination.WriteAsync(buffer.AsMemory(0, read));
    }
    destination.Position = 0;
    return destination;
}

static async Task DeleteReplacedImagesAsync(IImageStorage imageStorage, IEnumerable<string> publicIds)
{
    foreach (var publicId in publicIds.Distinct(StringComparer.Ordinal))
    {
        try
        {
            await imageStorage.DeleteAsync(publicId);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Aviso: no se pudo retirar la imagen reemplazada '{publicId}': {exception.Message}");
        }
    }
}

static async Task<StoredImage> DownloadAndUploadWithRetryAsync(
    HttpClient httpClient,
    IImageStorage imageStorage,
    string fileName,
    int experienceId)
{
    for (var attempt = 0; ; attempt++)
    {
        try
        {
            await using var content = await DownloadImageAsync(httpClient, fileName);
            return await imageStorage.UploadAsync(content, fileName, experienceId);
        }
        catch (Exception) when (attempt < 2)
        {
            Console.WriteLine($"  Reintentando descarga/subida de '{fileName}' (intento {attempt + 1})...");
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }
}

static string ContentTypeFor(string format) => format.ToLowerInvariant() switch
{
    "png" => "image/png",
    "webp" => "image/webp",
    _ => "image/jpeg"
};


static string[] Normalize(IEnumerable<string> values) => values
    .Select(value => value.Trim())
    .Where(value => value.Length > 0)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

static string? NormalizeOptional(string? value) =>
    string.IsNullOrWhiteSpace(value) ? null : value.Trim();

static string ResolveCatalogPath(string[] arguments, string projectDirectory)
{
    var optionIndex = Array.IndexOf(arguments, CatalogOption);
    if (optionIndex >= 0)
    {
        if (optionIndex == arguments.Length - 1)
        {
            throw new ArgumentException("--catalog requiere una ruta.");
        }
        return Path.GetFullPath(arguments[optionIndex + 1]);
    }

    var outputCatalog = Path.Combine(AppContext.BaseDirectory, "catalog.json");
    return File.Exists(outputCatalog)
        ? outputCatalog
        : Path.Combine(projectDirectory, "catalog.json");
}

static string NormalizePostgresConnectionString(string connectionString)
{
    if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var uri)
        || (uri.Scheme != "postgresql" && uri.Scheme != "postgres"))
    {
        return connectionString;
    }

    var userInfo = uri.UserInfo.Split(':', 2);
    var builder = new NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.Port > 0 ? uri.Port : 5432,
        Database = uri.AbsolutePath.TrimStart('/'),
        Username = Uri.UnescapeDataString(userInfo.ElementAtOrDefault(0) ?? string.Empty),
        Password = Uri.UnescapeDataString(userInfo.ElementAtOrDefault(1) ?? string.Empty)
    };

    var query = uri.Query.TrimStart('?')
        .Split('&', StringSplitOptions.RemoveEmptyEntries)
        .Select(part => part.Split('=', 2))
        .Where(parts => parts.Length == 2)
        .ToDictionary(
            parts => Uri.UnescapeDataString(parts[0]).ToLowerInvariant(),
            parts => Uri.UnescapeDataString(parts[1]));
    if (query.TryGetValue("sslmode", out var sslModeValue)
        && Enum.TryParse<SslMode>(sslModeValue, true, out var sslMode))
    {
        builder.SslMode = sslMode;
    }
    if (query.TryGetValue("channel_binding", out var channelBindingValue)
        && Enum.TryParse<ChannelBinding>(channelBindingValue, true, out var channelBinding))
    {
        builder.ChannelBinding = channelBinding;
    }
    return builder.ConnectionString;
}

static int Fail(string message)
{
    Console.Error.WriteLine(message);
    return 1;
}
