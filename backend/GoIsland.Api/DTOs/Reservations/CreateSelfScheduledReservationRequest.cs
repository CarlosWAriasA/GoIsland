namespace GoIsland.Api.DTOs.Reservations;

public class CreateSelfScheduledReservationRequest
{
    public int ExperienceId { get; set; }
    public DateTime StartsAtLocal { get; set; }
    public int Quantity { get; set; }
}
