using System.ComponentModel.DataAnnotations;
using GoIsland.Api.DTOs.Common;
using GoIsland.Api.Models;

namespace GoIsland.Api.DTOs.Experiences;

public class SearchExperiencesRequest : PaginationRequest, IValidatableObject
{
    [StringLength(160, ErrorMessage = "La búsqueda no puede exceder 160 caracteres.")]
    public string? Query { get; set; }

    [StringLength(160, ErrorMessage = "La ubicacion no puede exceder 160 caracteres.")]
    public string? Location { get; set; }

    [StringLength(80, ErrorMessage = "La categoria no puede exceder 80 caracteres.")]
    public string? Category { get; set; }

    [Range(typeof(decimal), "0", "99999999.99", ErrorMessage = "El precio minimo debe ser mayor o igual a cero.")]
    public decimal? MinPrice { get; set; }

    [Range(typeof(decimal), "0", "99999999.99", ErrorMessage = "El precio maximo debe ser mayor o igual a cero.")]
    public decimal? MaxPrice { get; set; }

    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    [Range(1, 100000, ErrorMessage = "La cantidad debe ser mayor que cero.")]
    public int? Quantity { get; set; }

    [StringLength(80, ErrorMessage = "El idioma no puede exceder 80 caracteres.")]
    public string? Language { get; set; }

    [StringLength(40, ErrorMessage = "La dificultad no puede exceder 40 caracteres.")]
    public string? Difficulty { get; set; }

    public bool? Accessible { get; set; }

    [StringLength(20, ErrorMessage = "El orden seleccionado no es válido.")]
    public string? Sort { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MinPrice.HasValue && MaxPrice.HasValue && MinPrice > MaxPrice)
        {
            yield return new ValidationResult(
                "El precio mínimo no puede superar al máximo.",
                [nameof(MinPrice), nameof(MaxPrice)]);
        }

        if (From.HasValue && To.HasValue && To <= From)
        {
            yield return new ValidationResult(
                "La fecha final debe ser posterior a la inicial.",
                [nameof(From), nameof(To)]);
        }

        if (!string.IsNullOrWhiteSpace(Difficulty)
            && !ExperienceDifficulties.All.Contains(Difficulty))
        {
            yield return new ValidationResult(
                "La dificultad seleccionada no es válida.",
                [nameof(Difficulty)]);
        }

        if (!string.IsNullOrWhiteSpace(Sort)
            && !ExperienceSortOptions.All.Contains(Sort))
        {
            yield return new ValidationResult(
                "El orden seleccionado no es válido.",
                [nameof(Sort)]);
        }
    }
}

public static class ExperienceSortOptions
{
    public const string Relevance = "relevance";
    public const string Newest = "newest";
    public const string PriceAscending = "priceAsc";
    public const string PriceDescending = "priceDesc";
    public const string Rating = "rating";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Relevance,
        Newest,
        PriceAscending,
        PriceDescending,
        Rating
    };
}
