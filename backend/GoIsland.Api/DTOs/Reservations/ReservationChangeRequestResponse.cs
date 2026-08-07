using System.ComponentModel.DataAnnotations;
using GoIsland.Api.DTOs.Common;
using GoIsland.Api.Models;

namespace GoIsland.Api.DTOs.Reservations;

public class ReservationChangeRequestResponse
{
    public int Id { get; set; }
    public int ReservationId { get; set; }
    public int RequestedByUserId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public int? RequestedScheduleId { get; set; }
    public DateTime? RequestedScheduleStartsAt { get; set; }
    public int? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? DecisionReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public string ExperienceTitle { get; set; } = string.Empty;
    public DateTime ReservationStartsAt { get; set; }
    public int Quantity { get; set; }
}

public sealed class ReservationChangeRequestListRequest : PaginationRequest, IValidatableObject
{
    [StringLength(20, ErrorMessage = "El estado seleccionado no es válido.")]
    public string? Status { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(Status) && !ReservationChangeRequestStatuses.All.Contains(Status))
        {
            yield return new ValidationResult(
                "El estado seleccionado no es válido.",
                [nameof(Status)]);
        }
    }
}
