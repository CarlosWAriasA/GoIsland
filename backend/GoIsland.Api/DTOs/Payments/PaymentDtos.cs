using System.ComponentModel.DataAnnotations;

namespace GoIsland.Api.DTOs.Payments;

public class PaymentResponse
{
    public int Id { get; set; }
    public int ReservationId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string? ProviderPaymentId { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal SubtotalAmount { get; set; }
    public decimal ServiceFeeAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? FailureCode { get; set; }
    public DateTime? PaidAt { get; set; }
    public decimal? RefundedAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class MockRejectPaymentRequest
{
    [StringLength(80, ErrorMessage = "El codigo de fallo no puede exceder 80 caracteres.")]
    public string? FailureCode { get; set; }
}

public class RefundPaymentRequest
{
    [Required(ErrorMessage = "Debes indicar el motivo del reembolso.")]
    [StringLength(500, MinimumLength = 3, ErrorMessage = "El motivo debe tener entre 3 y 500 caracteres.")]
    public string Reason { get; set; } = string.Empty;
}
