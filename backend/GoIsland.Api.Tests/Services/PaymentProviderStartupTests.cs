using GoIsland.Api.Services.Payments;

namespace GoIsland.Api.Tests.Services;

public class PaymentProviderStartupTests
{
    [Fact]
    public void ResolveProvider_DefaultsToMockWhenNotConfigured()
    {
        Assert.Equal("Mock", PaymentProviderStartup.ResolveProvider(null, "Development"));
        Assert.Equal("Mock", PaymentProviderStartup.ResolveProvider("  ", "QA"));
    }

    [Fact]
    public void ResolveProvider_RejectsUnknownProviders()
    {
        Assert.Throws<InvalidOperationException>(() =>
            PaymentProviderStartup.ResolveProvider("Stripe", "Development"));
    }

    [Fact]
    public void ResolveProvider_RejectsMockOutsideDevelopmentAndQa()
    {
        Assert.Throws<InvalidOperationException>(() =>
            PaymentProviderStartup.ResolveProvider("Mock", "Production"));
        Assert.Throws<InvalidOperationException>(() =>
            PaymentProviderStartup.ResolveProvider(null, "Staging"));
        Assert.Throws<InvalidOperationException>(() =>
            PaymentProviderStartup.ResolveProvider("Mock", "UAT"));
    }

    [Fact]
    public void ResolveProvider_AcceptsMockInDevelopmentAndQa()
    {
        Assert.Equal("Mock", PaymentProviderStartup.ResolveProvider("mock", "Development"));
        Assert.Equal("Mock", PaymentProviderStartup.ResolveProvider("Mock", "qa"));
    }
}
