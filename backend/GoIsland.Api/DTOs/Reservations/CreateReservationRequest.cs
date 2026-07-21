using System.ComponentModel.DataAnnotations;

namespace GoIsland.Api.DTOs.Reservations;

public class CreateReservationRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "El horario no es valido.")]
    public int ScheduleId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser mayor que cero.")]
    public int Quantity { get; set; }
}
