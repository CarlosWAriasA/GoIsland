namespace GoIsland.Api.DTOs.Reservations;

public class ReservationResponse
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int ExperienceId { get; set; }
    public int Quantity { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime ReservationDate { get; set; }
}
