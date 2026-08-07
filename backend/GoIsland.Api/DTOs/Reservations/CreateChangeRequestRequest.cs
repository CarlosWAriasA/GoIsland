using System.ComponentModel.DataAnnotations;

namespace GoIsland.Api.DTOs.Reservations;

public class CreateCancellationRequestRequest
{
    [Required(ErrorMessage = "Debes indicar el motivo de la cancelación.")]
    [StringLength(500, MinimumLength = 3, ErrorMessage = "El motivo debe tener entre 3 y 500 caracteres.")]
    public string Reason { get; set; } = string.Empty;
}

public class CreateRescheduleRequestRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "El horario no es valido.")]
    public int ScheduleId { get; set; }

    [Required(ErrorMessage = "Debes indicar el motivo de la reprogramación.")]
    [StringLength(500, MinimumLength = 3, ErrorMessage = "El motivo debe tener entre 3 y 500 caracteres.")]
    public string Reason { get; set; } = string.Empty;
}
