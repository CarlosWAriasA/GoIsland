using System.ComponentModel.DataAnnotations;
using GoIsland.Api.Models;

namespace GoIsland.Api.DTOs.Experiences;

public abstract class ExperienceRequestBase : IValidatableObject
{
    [Required, StringLength(160, MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;
    [StringLength(300)]
    public string ShortDescription { get; set; } = string.Empty;
    [Required, StringLength(4000, MinimumLength = 10)]
    public string Description { get; set; } = string.Empty;
    [Range(1, 10080)]
    public int? DurationMinutes { get; set; }
    [StringLength(80)]
    public string TimeZoneId { get; set; } = "America/Santo_Domingo";
    [StringLength(1000)]
    public string MeetingPointInstructions { get; set; } = string.Empty;
    [StringLength(1000)]
    public string? PickupInformation { get; set; }
    public string[] WhatIsIncluded { get; set; } = [];
    public string[] WhatIsNotIncluded { get; set; } = [];
    public string[] WhatToBring { get; set; } = [];
    [StringLength(1500)]
    public string GuestRequirements { get; set; } = string.Empty;
    [Range(0, 120)]
    public int? MinimumAge { get; set; }
    [StringLength(40)]
    public string Difficulty { get; set; } = string.Empty;
    [StringLength(1500)]
    public string AccessibilityInformation { get; set; } = string.Empty;
    public string[] Languages { get; set; } = [];
    [StringLength(40)]
    public string CancellationPolicy { get; set; } = string.Empty;
    public string[] Tags { get; set; } = [];
    public List<ExperienceItineraryItemRequest> Itinerary { get; set; } = [];

    [Required, StringLength(160, MinimumLength = 2)]
    public string Location { get; set; } = string.Empty;
    [Range(typeof(decimal), "-90", "90")]
    public decimal? Latitude { get; set; }
    [Range(typeof(decimal), "-180", "180")]
    public decimal? Longitude { get; set; }
    [Required, StringLength(80, MinimumLength = 2)]
    public string Category { get; set; } = string.Empty;
    [Range(typeof(decimal), "0", "99999999.99")]
    public decimal Price { get; set; }
    public int Capacity { get; set; } = 1;
    public bool IsUnlimitedCapacity { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Latitude.HasValue != Longitude.HasValue)
            yield return new("Selecciona un punto completo en el mapa.", [nameof(Latitude), nameof(Longitude)]);
        if (!ExperienceCategories.All.Contains(Category.Trim()))
            yield return new("Selecciona una categoría válida.", [nameof(Category)]);
        if (!IsUnlimitedCapacity && Capacity < 1)
            yield return new("La capacidad debe ser mayor que cero.", [nameof(Capacity)]);
        if (!string.IsNullOrWhiteSpace(Difficulty) && !ExperienceDifficulties.All.Contains(Difficulty))
            yield return new("Selecciona una dificultad válida.", [nameof(Difficulty)]);
        if (!string.IsNullOrWhiteSpace(CancellationPolicy)
            && !CancellationPolicies.All.Contains(CancellationPolicy))
            yield return new("Selecciona una política de cancelación válida.", [nameof(CancellationPolicy)]);

        foreach (var value in WhatIsIncluded.Concat(WhatIsNotIncluded).Concat(WhatToBring)
            .Concat(Languages).Concat(Tags))
        {
            if (!string.IsNullOrWhiteSpace(value) && value.Trim().Length > 120)
                yield return new("Cada elemento debe tener 120 caracteres o menos.");
        }
    }
}

public class CreateExperienceRequest : ExperienceRequestBase
{
}
