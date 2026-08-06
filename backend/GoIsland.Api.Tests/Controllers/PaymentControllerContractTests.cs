using System.Security.Claims;
using GoIsland.Api.Controllers;
using GoIsland.Api.DTOs.Payments;
using GoIsland.Api.Services.Payments;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GoIsland.Api.Tests.Controllers;

public class PaymentControllerContractTests
{
    [Fact]
    public async Task CreatePayment_ReturnsLocationForCreatedPayment()
    {
        var payment = new PaymentResponse { Id = 42, ReservationId = 7 };
        var controller = new ReservationsController(
            null!,
            new SuccessfulPaymentService(payment))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, "9")],
                        "Test"))
                }
            }
        };
        controller.Request.Headers["Idempotency-Key"] = "payment-test";

        var response = await controller.CreatePayment(7);

        var created = Assert.IsType<CreatedAtRouteResult>(response.Result);
        Assert.Equal(PaymentsController.GetPaymentByIdRouteName, created.RouteName);
        Assert.Equal(payment.Id, created.RouteValues!["id"]);
        var checkout = Assert.IsType<PaymentCheckoutResponse>(created.Value);
        Assert.Same(payment, checkout.Payment);
    }

    private sealed class SuccessfulPaymentService : IPaymentService
    {
        private readonly PaymentResponse _payment;

        public SuccessfulPaymentService(PaymentResponse payment)
        {
            _payment = payment;
        }

        public Task<PaymentOperationResult> CreateAsync(int userId, int reservationId, string? idempotencyKey) =>
            Task.FromResult(new PaymentOperationResult(PaymentOperationStatus.Success, _payment));

        public Task<IReadOnlyCollection<PaymentResponse>?> GetForReservationAsync(
            int userId, int reservationId, bool isAdmin) => throw new NotSupportedException();

        public Task<PaymentResponse?> GetByIdAsync(int id, int userId, bool isAdmin) =>
            throw new NotSupportedException();

        public Task<PaymentOperationResult> GetCheckoutAsync(int id, int userId) =>
            throw new NotSupportedException();

        public Task<WebhookProcessingStatus> ProcessWebhookAsync(
            GatewayWebhookEvent webhookEvent,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaymentOperationResult> MockConfirmAsync(int id, int actorUserId, bool isAdmin) =>
            throw new NotSupportedException();

        public Task<PaymentOperationResult> MockRejectAsync(
            int id, int actorUserId, bool isAdmin, string? failureCode) => throw new NotSupportedException();

        public Task<PaymentOperationResult> RefundAsync(int id, int adminUserId, string reason) =>
            throw new NotSupportedException();
    }
}
