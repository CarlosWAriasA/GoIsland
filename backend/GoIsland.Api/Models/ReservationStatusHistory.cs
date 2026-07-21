namespace GoIsland.Api.Models;

public class ReservationStatusHistory
{
    public long Id { get; set; }
    public int ReservationId { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public int ChangedByUserId { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Reservation Reservation { get; set; } = null!;
}

public class ReservationIdempotencyKey
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public int ReservationId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Reservation Reservation { get; set; } = null!;
}

public static class ReservationStatuses
{
    public const string PendingPayment = "PendingPayment";
    public const string Confirmed = "Confirmed";
    public const string CancelledByTourist = "CancelledByTourist";
    public const string CancelledByHost = "CancelledByHost";
    public const string Completed = "Completed";
    public const string RefundPending = "RefundPending";
    public const string Refunded = "Refunded";

    public static bool IsActive(string status) =>
        status is PendingPayment or Confirmed;
}
