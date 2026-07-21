using System.ComponentModel.DataAnnotations;

namespace GoIsland.Api.DTOs.Experiences;

public class ExperienceDecisionRequest
{
    [StringLength(500, ErrorMessage = "El motivo no puede exceder 500 caracteres.")]
    public string? Reason { get; set; }
}
