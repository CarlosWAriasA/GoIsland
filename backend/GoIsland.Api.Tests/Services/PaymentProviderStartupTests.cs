using GoIsland.Api.Services.Payments;

namespace GoIsland.Api.Tests.Services;

public class PaymentProviderStartupTests
{
    [Fact]
    public void ResolveProvider_DefaultsToMockWhenNotConfigured()
    {
        Assert.Equal("Mock", PaymentProviderStartup.ResolveProvider(null, "Sandbox", "Development"));
        Assert.Equal("Mock", PaymentProviderStartup.ResolveProvider("  ", "Sandbox", "QA"));
    }

    [Fact]
    public void ResolveProvider_RejectsUnknownProviders()
    {
        Assert.Throws<InvalidOperationException>(() =>
            PaymentProviderStartup.ResolveProvider("Unknown", "Sandbox", "Development"));
    }

    [Fact]
    public void ResolveProvider_RejectsMockOutsideDevelopmentAndQa()
    {
        Assert.Throws<InvalidOperationException>(() =>
            PaymentProviderStartup.ResolveProvider("Mock", "Sandbox", "Production"));
        Assert.Throws<InvalidOperationException>(() =>
            PaymentProviderStartup.ResolveProvider(null, "Sandbox", "Staging"));
        Assert.Throws<InvalidOperationException>(() =>
            PaymentProviderStartup.ResolveProvider("Mock", "Sandbox", "UAT"));
    }

    [Fact]
    public void ResolveProvider_AcceptsMockInDevelopmentAndQa()
    {
        Assert.Equal("Mock", PaymentProviderStartup.ResolveProvider("mock", "Sandbox", "Development"));
        Assert.Equal("Mock", PaymentProviderStartup.ResolveProvider("Mock", "Sandbox", "qa"));
    }

    [Fact]
    public void ResolveProvider_AcceptsOnlyStripeSandboxTestCredentials()
    {
        Assert.Equal("Stripe", PaymentProviderStartup.ResolveProvider(
            "Stripe", "Sandbox", "Production", "sk_test_example", "whsec_example"));
        Assert.Throws<InvalidOperationException>(() => PaymentProviderStartup.ResolveProvider(
            "Stripe", "Live", "Production", "sk_test_example", "whsec_example"));
        Assert.Throws<InvalidOperationException>(() => PaymentProviderStartup.ResolveProvider(
            "Stripe", "Sandbox", "Production", "sk_live_example", "whsec_example"));
        Assert.Throws<InvalidOperationException>(() => PaymentProviderStartup.ResolveProvider(
            "Stripe", "Sandbox", "Production", "sk_test_example", "missing"));
    }
}
