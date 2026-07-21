using System.ComponentModel.DataAnnotations;

namespace GoIsland.Api.DTOs.Hosts;

public class HostDecisionRequest
{
    [StringLength(500, ErrorMessage = "El motivo no puede exceder 500 caracteres.")]
    public string? Reason { get; set; }
}
