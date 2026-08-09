using System.ComponentModel.DataAnnotations;
using GoIsland.Api.DTOs.Common;
using GoIsland.Api.Models;

namespace GoIsland.Api.DTOs.Reviews;

public sealed class ReviewListRequest : PaginationRequest, IValidatableObject
{
    [StringLength(160, ErrorMessage = "La búsqueda no puede exceder 160 caracteres.")]
    public string? Query { get; set; }

    [StringLength(40, ErrorMessage = "El estado seleccionado no es válido.")]
    public string? Status { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "La reserva seleccionada no es válida.")]
    public int? ReservationId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(Status)
            && !ReviewModerationStatuses.All.Contains(Status))
        {
            yield return new ValidationResult(
                "El estado seleccionado no es válido.",
                [nameof(Status)]);
        }
    }
}

public class ReviewRequest
{
    [Range(1, 5)]
    public int Rating { get; set; }

    [Required, StringLength(1000, MinimumLength = 10)]
    public string Comment { get; set; } = string.Empty;
}

public class ReviewResponse
{
    public int Id { get; set; }
    public int ReservationId { get; set; }
    public int UserId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public int ExperienceId { get; set; }
    public int HostId { get; set; }
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public string ModerationStatus { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public enum ReviewMutationStatus
{
    Success,
    NotFound,
    Forbidden,
    OwnExperience,
    ReservationNotCompleted,
    Duplicate,
    EditWindowExpired
}

public record ReviewMutationResult(ReviewMutationStatus Status, ReviewResponse? Review = null);
