using System.Text.Json.Serialization;

namespace GoIsland.Api.DTOs.Experiences;

public class ExperienceImageResponse
{
    public int Id { get; set; }
    [JsonIgnore]
    public string SourceUrl { get; set; } = string.Empty;
    public string Url => ImageDeliveryUrls.ForDetail(SourceUrl);
    public string CardUrl => ImageDeliveryUrls.ForCard(SourceUrl);
    public string ThumbnailUrl => ImageDeliveryUrls.ForThumbnail(SourceUrl);
    public string AltText { get; set; } = string.Empty;
    public string CreditText { get; set; } = string.Empty;
    public string? CreditUrl { get; set; }
    public string? LicenseName { get; set; }
    public string? LicenseUrl { get; set; }
    public bool IsCover { get; set; }
    public int SortOrder { get; set; }
}

public static class ImageDeliveryUrls
{
    public static string ForCard(string sourceUrl) =>
        AddCloudinaryTransformation(sourceUrl, "f_auto,q_auto,c_fill,w_720,h_480");

    public static string ForDetail(string sourceUrl) =>
        AddCloudinaryTransformation(sourceUrl, "f_auto,q_auto,c_limit,w_1600,h_1200");

    public static string ForThumbnail(string sourceUrl) =>
        AddCloudinaryTransformation(sourceUrl, "f_auto,q_auto,c_fill,w_240,h_180");

    private static string AddCloudinaryTransformation(string sourceUrl, string transformation)
    {
        const string uploadSegment = "/image/upload/";
        var position = sourceUrl.IndexOf(uploadSegment, StringComparison.OrdinalIgnoreCase);
        if (position < 0)
        {
            return sourceUrl;
        }

        var insertionPoint = position + uploadSegment.Length;
        return sourceUrl.Insert(insertionPoint, $"{transformation}/");
    }
}
