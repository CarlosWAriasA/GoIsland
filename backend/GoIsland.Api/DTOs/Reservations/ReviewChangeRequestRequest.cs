using System.ComponentModel.DataAnnotations;

namespace GoIsland.Api.DTOs.Reservations;

public class ReviewChangeRequestRequest
{
    public bool Approve { get; set; }

    [StringLength(500, ErrorMessage = "El motivo no puede exceder 500 caracteres.")]
    public string? DecisionReason { get; set; }
}
