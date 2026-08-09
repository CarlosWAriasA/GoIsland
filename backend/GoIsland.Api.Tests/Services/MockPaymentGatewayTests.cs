using GoIsland.Api.Services.Payments;
using Microsoft.Extensions.Logging.Abstractions;

namespace GoIsland.Api.Tests.Services;

public class MockPaymentGatewayTests
{
    [Fact]
    public async Task CreatePayment_ConcurrentSameKey_ReturnsOneProviderReference()
    {
        var gateway = new MockPaymentGateway(NullLogger<MockPaymentGateway>.Instance);
        var request = new GatewayPaymentRequest("payment:7:same-key", "USD", 105m, "Reserva #21");

        var results = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => gateway.CreatePaymentAsync(request)));

        Assert.All(results, result => Assert.True(result.Accepted));
        Assert.Single(results.Select(result => result.ProviderPaymentId).Distinct());
    }

    [Fact]
    public async Task Refund_ConcurrentSameKey_ReturnsOneProviderReference()
    {
        var gateway = new MockPaymentGateway(NullLogger<MockPaymentGateway>.Instance);
        var request = new GatewayRefundRequest("mock_pay_21", "USD", 105m, "refund:21");

        var results = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => gateway.RefundAsync(request)));

        Assert.All(results, result => Assert.True(result.Accepted));
        Assert.Single(results.Select(result => result.ProviderRefundId).Distinct());
    }

    [Fact]
    public async Task DifferentKeys_ReturnDifferentProviderReferences()
    {
        var gateway = new MockPaymentGateway(NullLogger<MockPaymentGateway>.Instance);

        var first = await gateway.CreatePaymentAsync(
            new GatewayPaymentRequest("payment:7:first", "USD", 105m, "Reserva #21"));
        var second = await gateway.CreatePaymentAsync(
            new GatewayPaymentRequest("payment:7:second", "USD", 105m, "Reserva #21"));

        Assert.NotEqual(first.ProviderPaymentId, second.ProviderPaymentId);
    }

    [Fact]
    public async Task CancelPayment_DisablesItsCheckoutSession()
    {
        var gateway = new MockPaymentGateway(NullLogger<MockPaymentGateway>.Instance);
        var created = await gateway.CreatePaymentAsync(
            new GatewayPaymentRequest("payment:7:cancel", "USD", 105m, "Reserva #21"));

        var cancelled = await gateway.CancelPaymentAsync(created.ProviderPaymentId!);
        var session = await gateway.GetPaymentSessionAsync(created.ProviderPaymentId!);

        Assert.True(cancelled.Cancelled);
        Assert.False(cancelled.Succeeded);
        Assert.False(session.Available);
    }
}
