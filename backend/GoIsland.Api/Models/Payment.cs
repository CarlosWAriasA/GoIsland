namespace GoIsland.Api.Models;

public class Payment
{
    public int Id { get; set; }
    public int ReservationId { get; set; }
    public int UserId { get; set; }
    public string Provider { get; set; } = "Mock";
    public string? ProviderPaymentId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public string Currency { get; set; } = "USD";
    public decimal Amount { get; set; }
    public decimal SubtotalAmount { get; set; }
    public decimal ServiceFeeAmount { get; set; }
    public decimal PlatformCommissionAmount { get; set; }
    public decimal HostNetAmount { get; set; }
    public string Status { get; set; } = PaymentStatuses.Pending;
    public string? FailureCode { get; set; }
    public DateTime? PaidAt { get; set; }
    public decimal? RefundedAmount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class PaymentGatewayAttempt
{
    public long Id { get; set; }
    public int PaymentId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string? ProviderReferenceId { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public string? FailureCode { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Payment Payment { get; set; } = null!;
}

public class PaymentWebhookEvent
{
    public long Id { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ProviderEventId { get; set; } = string.Empty;
    public int PaymentId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Payment Payment { get; set; } = null!;
}

public class Refund
{
    public int Id { get; set; }
    public int PaymentId { get; set; }
    public decimal Amount { get; set; }
    public string? Reason { get; set; }
    public string Status { get; set; } = RefundStatuses.Completed;
    public string Provider { get; set; } = string.Empty;
    public string? ProviderRefundId { get; set; }
    public int RequestedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Payment Payment { get; set; } = null!;
}

public static class PaymentStatuses
{
    public const string Pending = "Pending";
    public const string Paid = "Paid";
    public const string Failed = "Failed";
    public const string Refunded = "Refunded";
}

public static class PaymentGatewayAttemptOutcomes
{
    public const string Created = "Created";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Refunded = "Refunded";
}

public static class RefundStatuses
{
    public const string Completed = "Completed";
}
