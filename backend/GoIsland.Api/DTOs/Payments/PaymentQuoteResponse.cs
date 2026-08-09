namespace GoIsland.Api.DTOs.Payments;

public sealed class PaymentQuoteResponse
{
    public string Currency { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal SubtotalAmount { get; set; }
    public decimal ServiceFeeAmount { get; set; }
    public decimal TotalAmount { get; set; }
}
