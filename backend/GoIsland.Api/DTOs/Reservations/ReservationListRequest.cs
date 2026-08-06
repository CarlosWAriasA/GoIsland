using System.ComponentModel.DataAnnotations;
using GoIsland.Api.DTOs.Common;
using GoIsland.Api.Models;

namespace GoIsland.Api.DTOs.Reservations;

public sealed class ReservationListRequest : PaginationRequest, IValidatableObject
{
    [StringLength(160, ErrorMessage = "La búsqueda no puede exceder 160 caracteres.")]
    public string? Query { get; set; }

    [StringLength(40, ErrorMessage = "El estado seleccionado no es válido.")]
    public string? Status { get; set; }

    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(Status)
            && !ReservationStatuses.All.Contains(Status))
        {
            yield return new ValidationResult(
                "El estado seleccionado no es válido.",
                [nameof(Status)]);
        }

        if (From.HasValue && To.HasValue && To <= From)
        {
            yield return new ValidationResult(
                "La fecha final debe ser posterior a la inicial.",
                [nameof(From), nameof(To)]);
        }
    }
}
