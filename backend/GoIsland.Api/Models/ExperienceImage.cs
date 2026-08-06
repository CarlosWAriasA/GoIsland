namespace GoIsland.Api.Models;

public class ExperienceImage
{
    public int Id { get; set; }
    public int ExperienceId { get; set; }
    public string Provider { get; set; } = ImageStorageProviders.Cloudinary;
    public string? PublicId { get; set; }
    public string? SecureUrl { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string? Format { get; set; }
    public string AltText { get; set; } = string.Empty;
    public string CreditText { get; set; } = string.Empty;
    public string? CreditUrl { get; set; }
    public string? LicenseName { get; set; }
    public string? LicenseUrl { get; set; }
    public bool IsCover { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Experience Experience { get; set; } = null!;
}

public static class ImageStorageProviders
{
    public const string Local = "Local";
    public const string Cloudinary = "Cloudinary";
    public const string External = "External";
}
