using System.Text.RegularExpressions;
using GoIsland.Api.Models;

namespace GoIsland.CatalogImporter;

public sealed class CatalogDocument
{
    public CatalogOwner Owner { get; set; } = new();
    public List<CatalogExperience> Experiences { get; set; } = [];
}

public sealed class CatalogOwner
{
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class CatalogExperience
{
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int? DurationMinutes { get; set; }
    public string MeetingPointInstructions { get; set; } = string.Empty;
    public string? PickupInformation { get; set; }
    public string[] WhatIsIncluded { get; set; } = [];
    public string[] WhatIsNotIncluded { get; set; } = [];
    public string[] WhatToBring { get; set; } = [];
    public string GuestRequirements { get; set; } = string.Empty;
    public int? MinimumAge { get; set; }
    public string Difficulty { get; set; } = ExperienceDifficulties.Easy;
    public string AccessibilityInformation { get; set; } = string.Empty;
    public string[] Languages { get; set; } = ["Español"];
    public string CancellationPolicy { get; set; } = string.Empty;
    public string[] Tags { get; set; } = [];
    public string Location { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public List<CatalogItineraryItem> Itinerary { get; set; } = [];
    public List<CatalogImage> Images { get; set; } = [];
}

public sealed class CatalogItineraryItem
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public string? Location { get; set; }
}

public sealed class CatalogImage
{
    public string WikimediaFile { get; set; } = string.Empty;
    public string AltText { get; set; } = string.Empty;
    public string CreditText { get; set; } = string.Empty;
    public string CreditUrl { get; set; } = string.Empty;
    public string LicenseName { get; set; } = string.Empty;
    public string LicenseUrl { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
}

public static partial class CatalogValidator
{
    public static IReadOnlyCollection<string> Validate(CatalogDocument catalog)
    {
        var errors = new List<string>();
        Required(errors, "owner.email", catalog.Owner.Email, 254);
        Required(errors, "owner.displayName", catalog.Owner.DisplayName, 120);
        Required(errors, "owner.description", catalog.Owner.Description, 1000);

        if (catalog.Experiences.Count == 0)
        {
            errors.Add("El catálogo debe contener al menos un destino.");
        }

        var duplicateSlugs = catalog.Experiences
            .GroupBy(item => item.Slug, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);
        errors.AddRange(duplicateSlugs.Select(slug => $"Slug duplicado: {slug}."));

        foreach (var item in catalog.Experiences)
        {
            var prefix = string.IsNullOrWhiteSpace(item.Slug) ? "experience" : item.Slug;
            Required(errors, $"{prefix}.slug", item.Slug, 180);
            if (!SlugPattern().IsMatch(item.Slug))
            {
                errors.Add($"{prefix}.slug solo puede contener letras minúsculas, números y guiones.");
            }

            Required(errors, $"{prefix}.title", item.Title, 160);
            Required(errors, $"{prefix}.shortDescription", item.ShortDescription, 300);
            Required(errors, $"{prefix}.description", item.Description, 4000);
            Required(errors, $"{prefix}.meetingPointInstructions", item.MeetingPointInstructions, 1000);
            Required(errors, $"{prefix}.guestRequirements", item.GuestRequirements, 1500);
            Required(errors, $"{prefix}.accessibilityInformation", item.AccessibilityInformation, 1500);
            Required(errors, $"{prefix}.location", item.Location, 160);

            if (!ExperienceCategories.All.Contains(item.Category))
            {
                errors.Add($"{prefix}.category no es una categoría válida.");
            }
            if (!ExperienceDifficulties.All.Contains(item.Difficulty))
            {
                errors.Add($"{prefix}.difficulty no es una dificultad válida.");
            }
            if (!string.IsNullOrWhiteSpace(item.CancellationPolicy)
                && !CancellationPolicies.All.Contains(item.CancellationPolicy))
            {
                errors.Add($"{prefix}.cancellationPolicy no es una política válida.");
            }
            if (item.Latitude is < -90 or > 90 || item.Longitude is < -180 or > 180)
            {
                errors.Add($"{prefix} contiene coordenadas fuera de rango.");
            }
            if (item.Price < 0)
            {
                errors.Add($"{prefix}.price no puede ser negativo.");
            }
            if (item.DurationMinutes is <= 0 or > 1440)
            {
                errors.Add($"{prefix}.durationMinutes debe estar entre 1 y 1440.");
            }
            if (item.MinimumAge is < 0 or > 120)
            {
                errors.Add($"{prefix}.minimumAge debe estar entre 0 y 120.");
            }

            for (var index = 0; index < item.Itinerary.Count; index++)
            {
                var itinerary = item.Itinerary[index];
                Required(errors, $"{prefix}.itinerary[{index}].title", itinerary.Title, 120);
                Required(errors, $"{prefix}.itinerary[{index}].description", itinerary.Description, 800);
                if (itinerary.DurationMinutes is < 1 or > 1440)
                {
                    errors.Add($"{prefix}.itinerary[{index}].durationMinutes debe estar entre 1 y 1440.");
                }
            }

            if (item.Images.Count is < 3 or > 10)
            {
                errors.Add($"{prefix}.images debe contener entre 3 y 10 imágenes.");
            }

            var duplicateImages = item.Images
                .GroupBy(image => image.CreditUrl, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key);
            errors.AddRange(duplicateImages.Select(url => $"{prefix}.images contiene una fuente duplicada: {url}."));

            for (var index = 0; index < item.Images.Count; index++)
            {
                var image = item.Images[index];
                var imagePrefix = $"{prefix}.images[{index}]";
                Required(errors, $"{imagePrefix}.wikimediaFile", image.WikimediaFile, 500);
                Required(errors, $"{imagePrefix}.altText", image.AltText, 180);
                Required(errors, $"{imagePrefix}.creditText", image.CreditText, 240);
                RequiredHttpsUrl(errors, $"{imagePrefix}.creditUrl", image.CreditUrl);
                Required(errors, $"{imagePrefix}.licenseName", image.LicenseName, 80);
                RequiredHttpsUrl(errors, $"{imagePrefix}.licenseUrl", image.LicenseUrl);
                if (image.Width <= 0 || image.Height <= 0)
                {
                    errors.Add($"{imagePrefix} debe incluir dimensiones positivas.");
                }
            }
        }

        return errors;
    }

    private static void Required(List<string> errors, string field, string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{field} es obligatorio.");
        }
        else if (value.Length > maximumLength)
        {
            errors.Add($"{field} excede {maximumLength} caracteres.");
        }
    }

    private static void RequiredHttpsUrl(List<string> errors, string field, string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            errors.Add($"{field} debe ser una URL HTTPS válida.");
        }
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();
}
