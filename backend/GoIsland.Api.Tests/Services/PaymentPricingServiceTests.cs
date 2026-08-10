using GoIsland.Api.Services.Payments;
using Microsoft.Extensions.Options;

namespace GoIsland.Api.Tests.Services;

public class PaymentPricingServiceTests
{
    [Fact]
    public void Calculate_UsesConfiguredPercentagesAndCommercialRounding()
    {
        var service = new PaymentPricingService(Options.Create(new PaymentPricingOptions
        {
            Currency = "usd",
            ServiceFeePercent = 5m,
            CommissionPercent = 12m
        }));

        var result = service.Calculate(33.33m);

        Assert.Equal("USD", result.Currency);
        Assert.Equal(33.33m, result.Subtotal);
        Assert.Equal(1.67m, result.ServiceFee);
        Assert.Equal(35m, result.Total);
        Assert.Equal(4m, result.Commission);
        Assert.Equal(29.33m, result.HostNet);
    }

    [Fact]
    public void Calculate_RejectsNegativeSubtotals()
    {
        var service = new PaymentPricingService(Options.Create(new PaymentPricingOptions()));

        Assert.Throws<ArgumentOutOfRangeException>(() => service.Calculate(-0.01m));
    }
}
