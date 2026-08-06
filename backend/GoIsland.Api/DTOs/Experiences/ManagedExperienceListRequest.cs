using System.ComponentModel.DataAnnotations;
using GoIsland.Api.DTOs.Common;
using GoIsland.Api.Models;

namespace GoIsland.Api.DTOs.Experiences;

public sealed class ManagedExperienceListRequest : PaginationRequest, IValidatableObject
{
    [StringLength(160, ErrorMessage = "La búsqueda no puede exceder 160 caracteres.")]
    public string? Query { get; set; }

    [StringLength(40, ErrorMessage = "El estado seleccionado no es válido.")]
    public string? Status { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(Status)
            && !ExperienceApprovalStatuses.All.Contains(Status))
        {
            yield return new ValidationResult(
                "El estado seleccionado no es válido.",
                [nameof(Status)]);
        }
    }
}
