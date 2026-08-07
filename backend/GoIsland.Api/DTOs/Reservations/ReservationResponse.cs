using System.ComponentModel.DataAnnotations;

namespace GoIsland.Api.DTOs.Reservations;

public class ReservationResponse
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int ExperienceId { get; set; }
    public int ScheduleId { get; set; }
    public string ExperienceSlug { get; set; } = string.Empty;
    public string ExperienceTitle { get; set; } = string.Empty;
    public string ExperienceLocation { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string SchedulingMode { get; set; } = string.Empty;
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public int Quantity { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime ReservationDate { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public IReadOnlyCollection<ReservationStatusHistoryResponse> StatusHistory { get; set; } = [];
    public ReservationChangeRequestResponse? PendingChangeRequest { get; set; }
}

public class ReservationStatusHistoryResponse
{
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CancelReservationRequest
{
    [StringLength(500, ErrorMessage = "El motivo no puede exceder 500 caracteres.")]
    public string? Reason { get; set; }
}

public class RescheduleReservationRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "El horario no es valido.")]
    public int ScheduleId { get; set; }
}

public class RescheduleSelfScheduledReservationRequest
{
    public DateTime StartsAtLocal { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser mayor que cero.")]
    public int Quantity { get; set; }
}
