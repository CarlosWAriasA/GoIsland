using Microsoft.Extensions.Options;

namespace GoIsland.Api.Services.Payments;

public sealed class PaymentPricingOptions
{
    public const string SectionName = "Payments";

    public string Currency { get; set; } = "USD";
    public decimal ServiceFeePercent { get; set; } = 5m;
    public decimal CommissionPercent { get; set; } = 12m;
}

public sealed record PaymentBreakdown(
    string Currency,
    decimal Subtotal,
    decimal ServiceFee,
    decimal Total,
    decimal Commission,
    decimal HostNet);

public interface IPaymentPricingService
{
    PaymentBreakdown Calculate(decimal subtotal);
}

public sealed class PaymentPricingService : IPaymentPricingService
{
    private readonly PaymentPricingOptions _options;

    public PaymentPricingService(IOptions<PaymentPricingOptions> options)
    {
        _options = options.Value;
    }

    public PaymentBreakdown Calculate(decimal subtotal)
    {
        if (subtotal < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(subtotal));
        }

        var roundedSubtotal = Math.Round(subtotal, 2, MidpointRounding.AwayFromZero);
        var serviceFee = Math.Round(
            roundedSubtotal * _options.ServiceFeePercent / 100m,
            2,
            MidpointRounding.AwayFromZero);
        var commission = Math.Round(
            roundedSubtotal * _options.CommissionPercent / 100m,
            2,
            MidpointRounding.AwayFromZero);

        return new PaymentBreakdown(
            _options.Currency.ToUpperInvariant(),
            roundedSubtotal,
            serviceFee,
            roundedSubtotal + serviceFee,
            commission,
            roundedSubtotal - commission);
    }
}
