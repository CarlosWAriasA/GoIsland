namespace GoIsland.Api.Models;

public class Reservation
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int ExperienceId { get; set; }
    public int ScheduleId { get; set; }
    public int Quantity { get; set; }
    public string Status { get; set; } = ReservationStatuses.PendingPayment;
    public decimal TotalAmount { get; set; }
    public DateTime ReservationDate { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CancelledAt { get; set; }
}
