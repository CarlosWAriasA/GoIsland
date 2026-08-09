using GoIsland.Api.DTOs.Reservations;
using GoIsland.Api.Models;
using GoIsland.Api.Services.Payments;
using GoIsland.Api.Services.Reservations;
using GoIsland.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GoIsland.Api.Tests.Integration;

public class PaymentIntegrationTests : PostgresIntegrationTestBase
{
    [Fact]
    public async Task Create_PersistsPendingPaymentWithServerCalculatedBreakdown()
    {
        var seed = await SeedReservationAsync(quantity: 2, price: 55m);
        var service = GetRequiredService<IPaymentService>();

        var result = await service.CreateAsync(seed.Tourist.Id, seed.Reservation.Id, $"pay-{Guid.NewGuid():N}");

        Assert.Equal(PaymentOperationStatus.Success, result.Status);
        var payment = result.Payment!;
        Assert.Equal(MockPaymentGateway.Provider, payment.Provider);
        Assert.Equal(PaymentStatuses.Pending, payment.Status);
        Assert.StartsWith("mock_pay_", payment.ProviderPaymentId);
        Assert.Equal("USD", payment.Currency);
        Assert.Equal(110.00m, payment.SubtotalAmount);
        Assert.Equal(5.50m, payment.ServiceFeeAmount);
        Assert.Equal(115.50m, payment.TotalAmount);
        Assert.Null(payment.PaidAt);

        var stored = await Context.Payments.AsNoTracking().SingleAsync(item => item.Id == payment.Id);
        Assert.Equal(seed.Tourist.Id, stored.UserId);
        Assert.Equal(13.20m, stored.PlatformCommissionAmount);
        Assert.Equal(96.80m, stored.HostNetAmount);

        var reservation = await Context.Reservations.AsNoTracking()
            .SingleAsync(item => item.Id == seed.Reservation.Id);
        Assert.Equal(ReservationStatuses.PendingPayment, reservation.Status);

        var attempt = await Context.PaymentGatewayAttempts.AsNoTracking()
            .SingleAsync(item => item.PaymentId == payment.Id);
        Assert.Equal(PaymentGatewayAttemptOutcomes.Created, attempt.Outcome);
        Assert.Equal(payment.ProviderPaymentId, attempt.ProviderReferenceId);
    }

    [Fact]
    public async Task Create_WithSameIdempotencyKey_ReturnsSamePaymentWithoutDuplicating()
    {
        var seed = await SeedReservationAsync(quantity: 1, price: 40m);
        var service = GetRequiredService<IPaymentService>();
        var key = $"pay-{Guid.NewGuid():N}";

        var first = await service.CreateAsync(seed.Tourist.Id, seed.Reservation.Id, key);
        var second = await service.CreateAsync(seed.Tourist.Id, seed.Reservation.Id, key);

        Assert.Equal(PaymentOperationStatus.Success, first.Status);
        Assert.Equal(PaymentOperationStatus.Success, second.Status);
        Assert.Equal(first.Payment!.Id, second.Payment!.Id);
        Assert.Equal(1, await Context.Payments.CountAsync(item => item.ReservationId == seed.Reservation.Id));
    }

    [Fact]
    public async Task Create_WithSameKeyForDifferentReservation_ReturnsIdempotencyConflict()
    {
        var seed = await SeedReservationAsync(quantity: 1, price: 40m);
        var other = await SeedReservationAsync(quantity: 1, price: 40m, tourist: seed.Tourist);
        var service = GetRequiredService<IPaymentService>();
        var key = $"pay-{Guid.NewGuid():N}";

        var first = await service.CreateAsync(seed.Tourist.Id, seed.Reservation.Id, key);
        var conflict = await service.CreateAsync(seed.Tourist.Id, other.Reservation.Id, key);

        Assert.Equal(PaymentOperationStatus.Success, first.Status);
        Assert.Equal(PaymentOperationStatus.IdempotencyConflict, conflict.Status);
    }

    [Fact]
    public async Task Create_ForForeignReservation_ReturnsNotFound()
    {
        var seed = await SeedReservationAsync(quantity: 1, price: 40m);
        var outsider = await SeedReservationAsync(quantity: 1, price: 40m);
        var service = GetRequiredService<IPaymentService>();

        var result = await service.CreateAsync(outsider.Tourist.Id, seed.Reservation.Id, $"pay-{Guid.NewGuid():N}");

        Assert.Equal(PaymentOperationStatus.ReservationNotFound, result.Status);
    }

    [Fact]
    public async Task Create_ForConfirmedReservation_ReturnsInvalidTransition()
    {
        var seed = await SeedReservationAsync(quantity: 1, price: 40m);
        var service = GetRequiredService<IPaymentService>();
        var created = await service.CreateAsync(seed.Tourist.Id, seed.Reservation.Id, $"pay-{Guid.NewGuid():N}");
        await service.MockConfirmAsync(created.Payment!.Id, seed.Tourist.Id, isAdmin: false);

        var result = await service.CreateAsync(seed.Tourist.Id, seed.Reservation.Id, $"pay-{Guid.NewGuid():N}");

        Assert.Equal(PaymentOperationStatus.InvalidTransition, result.Status);
    }

    [Fact]
    public async Task MockConfirm_ConfirmsPaymentAndReservationOnce()
    {
        var seed = await SeedReservationAsync(quantity: 2, price: 50m);
        var service = GetRequiredService<IPaymentService>();
        var created = await service.CreateAsync(seed.Tourist.Id, seed.Reservation.Id, $"pay-{Guid.NewGuid():N}");
        var paymentId = created.Payment!.Id;

        var confirmed = await service.MockConfirmAsync(paymentId, seed.Tourist.Id, isAdmin: false);
        var repeated = await service.MockConfirmAsync(paymentId, seed.Tourist.Id, isAdmin: false);

        Assert.Equal(PaymentOperationStatus.Success, confirmed.Status);
        Assert.Equal(PaymentOperationStatus.Success, repeated.Status);
        Assert.Equal(PaymentStatuses.Paid, confirmed.Payment!.Status);
        Assert.NotNull(confirmed.Payment.PaidAt);

        var reservation = await Context.Reservations.AsNoTracking()
            .SingleAsync(item => item.Id == seed.Reservation.Id);
        Assert.Equal(ReservationStatuses.Confirmed, reservation.Status);

        Assert.Equal(1, await Context.PaymentWebhookEvents.CountAsync(item => item.PaymentId == paymentId));
        Assert.Equal(1, await Context.PaymentGatewayAttempts.CountAsync(item =>
            item.PaymentId == paymentId && item.Outcome == PaymentGatewayAttemptOutcomes.Approved));
        Assert.Equal(1, await Context.ReservationStatusHistories.CountAsync(item =>
            item.ReservationId == reservation.Id && item.ToStatus == ReservationStatuses.Confirmed));
    }

    [Fact]
    public async Task Webhook_ConfirmsAndRefundsOnceWhenEventsAreRepeated()
    {
        var seed = await SeedReservationAsync(quantity: 2, price: 50m);
        var service = GetRequiredService<IPaymentService>();
        var created = await service.CreateAsync(
            seed.Tourist.Id,
            seed.Reservation.Id,
            $"pay-{Guid.NewGuid():N}");
        var payment = created.Payment!;
        var spotsAfterReservation = await Context.ExperienceSchedules.AsNoTracking()
            .Where(item => item.Id == seed.Schedule.Id)
            .Select(item => item.AvailableSpots)
            .SingleAsync();

        var succeeded = new GatewayWebhookEvent(
            MockPaymentGateway.Provider,
            $"evt-success-{Guid.NewGuid():N}",
            payment.ProviderPaymentId!,
            GatewayWebhookEventKind.PaymentSucceeded);
        Assert.Equal(WebhookProcessingStatus.Processed, await service.ProcessWebhookAsync(succeeded));
        Assert.Equal(WebhookProcessingStatus.Duplicate, await service.ProcessWebhookAsync(succeeded));

        var refunded = new GatewayWebhookEvent(
            MockPaymentGateway.Provider,
            $"evt-refund-{Guid.NewGuid():N}",
            payment.ProviderPaymentId!,
            GatewayWebhookEventKind.PaymentRefunded,
            ProviderRefundId: $"mock_re_{Guid.NewGuid():N}");
        Assert.Equal(WebhookProcessingStatus.Processed, await service.ProcessWebhookAsync(refunded));
        Assert.Equal(WebhookProcessingStatus.Duplicate, await service.ProcessWebhookAsync(refunded));

        Assert.Equal(PaymentStatuses.Refunded,
            await Context.Payments.Where(item => item.Id == payment.Id).Select(item => item.Status).SingleAsync());
        Assert.Equal(ReservationStatuses.Refunded,
            await Context.Reservations.Where(item => item.Id == seed.Reservation.Id)
                .Select(item => item.Status).SingleAsync());
        Assert.Equal(spotsAfterReservation + 2,
            await Context.ExperienceSchedules.Where(item => item.Id == seed.Schedule.Id)
                .Select(item => item.AvailableSpots).SingleAsync());
        Assert.Equal(2, await Context.PaymentWebhookEvents.CountAsync(item => item.PaymentId == payment.Id));
        Assert.Equal(1, await Context.Refunds.CountAsync(item => item.PaymentId == payment.Id));
    }

    [Fact]
    public async Task CancelPendingPayment_ClosesPaymentAndCheckout()
    {
        var seed = await SeedReservationAsync(quantity: 2, price: 50m);
        var payments = GetRequiredService<IPaymentService>();
        var created = await payments.CreateAsync(
            seed.Tourist.Id, seed.Reservation.Id, $"pay-{Guid.NewGuid():N}");
        var paymentId = created.Payment!.Id;
        var reservations = GetRequiredService<IReservationService>();

        var cancelled = await reservations.CancelAsync(
            seed.Reservation.Id,
            seed.Tourist.Id,
            new CancelReservationRequest { Reason = "Cambio de planes" },
            $"cancel-{Guid.NewGuid():N}");

        Assert.Equal(ReservationCreationStatus.Success, cancelled.Status);
        Assert.Equal(ReservationStatuses.CancelledByTourist, cancelled.Reservation!.Status);
        Assert.Equal(PaymentStatuses.Failed,
            await Context.Payments.Where(item => item.Id == paymentId)
                .Select(item => item.Status).SingleAsync());
        Assert.Equal(PaymentOperationStatus.InvalidTransition,
            (await payments.GetCheckoutAsync(paymentId, seed.Tourist.Id)).Status);
    }

    [Fact]
    public async Task PaymentSucceededAfterCancellation_IsRefundedOnceWithoutReleasingCapacityTwice()
    {
        var seed = await SeedReservationAsync(quantity: 2, price: 50m);
        var payments = GetRequiredService<IPaymentService>();
        var created = await payments.CreateAsync(
            seed.Tourist.Id, seed.Reservation.Id, $"pay-{Guid.NewGuid():N}");
        var reservations = GetRequiredService<IReservationService>();
        await reservations.CancelAsync(
            seed.Reservation.Id,
            seed.Tourist.Id,
            new CancelReservationRequest { Reason = "Cambio de planes" },
            $"cancel-{Guid.NewGuid():N}");

        var succeeded = new GatewayWebhookEvent(
            MockPaymentGateway.Provider,
            $"evt-success-{Guid.NewGuid():N}",
            created.Payment!.ProviderPaymentId!,
            GatewayWebhookEventKind.PaymentSucceeded);
        Assert.Equal(WebhookProcessingStatus.Processed, await payments.ProcessWebhookAsync(succeeded));
        Assert.Equal(WebhookProcessingStatus.Duplicate, await payments.ProcessWebhookAsync(succeeded));

        Assert.Equal(PaymentStatuses.Refunded,
            await Context.Payments.Where(item => item.Id == created.Payment.Id)
                .Select(item => item.Status).SingleAsync());
        Assert.Equal(ReservationStatuses.Refunded,
            await Context.Reservations.Where(item => item.Id == seed.Reservation.Id)
                .Select(item => item.Status).SingleAsync());
        Assert.Equal(10,
            await Context.ExperienceSchedules.Where(item => item.Id == seed.Schedule.Id)
                .Select(item => item.AvailableSpots).SingleAsync());
        Assert.Equal(1, await Context.Refunds.CountAsync(item => item.PaymentId == created.Payment.Id));
    }

    [Fact]
    public async Task HostCancelPaidReservation_RefundsPaymentAndReservation()
    {
        var seed = await SeedReservationAsync(quantity: 2, price: 50m);
        var payments = GetRequiredService<IPaymentService>();
        var created = await payments.CreateAsync(
            seed.Tourist.Id, seed.Reservation.Id, $"pay-{Guid.NewGuid():N}");
        await payments.MockConfirmAsync(created.Payment!.Id, seed.Tourist.Id, isAdmin: false);
        var reservations = GetRequiredService<IReservationService>();

        var cancelled = await reservations.CancelByHostAsync(
            seed.Reservation.Id,
            seed.Host.Id,
            new CancelReservationRequest { Reason = "No podré realizar la experiencia" },
            $"host-cancel-{Guid.NewGuid():N}");

        Assert.Equal(ReservationCreationStatus.Success, cancelled.Status);
        Assert.Equal(ReservationStatuses.Refunded, cancelled.Reservation!.Status);
        Assert.Equal(PaymentStatuses.Refunded,
            await Context.Payments.Where(item => item.Id == created.Payment.Id)
                .Select(item => item.Status).SingleAsync());
        Assert.Equal(10,
            await Context.ExperienceSchedules.Where(item => item.Id == seed.Schedule.Id)
                .Select(item => item.AvailableSpots).SingleAsync());
    }

    [Fact]
    public async Task ApproveCancellationRequest_ReusesIdempotencyKeyWithoutDuplicatingRefund()
    {
        var seed = await SeedReservationAsync(quantity: 1, price: 50m);
        var payments = GetRequiredService<IPaymentService>();
        var created = await payments.CreateAsync(
            seed.Tourist.Id, seed.Reservation.Id, $"pay-{Guid.NewGuid():N}");
        await payments.MockConfirmAsync(created.Payment!.Id, seed.Tourist.Id, isAdmin: false);
        var requests = GetRequiredService<IReservationChangeRequestService>();
        await requests.RequestCancellationAsync(
            seed.Tourist.Id, seed.Reservation.Id, "Ya no podré asistir");
        var requestId = await Context.ReservationChangeRequests
            .Where(item => item.ReservationId == seed.Reservation.Id)
            .Select(item => item.Id)
            .SingleAsync();
        var key = $"review-{Guid.NewGuid():N}";

        var first = await requests.ReviewAsync(seed.Host.Id, requestId, true, null, key);
        var repeated = await requests.ReviewAsync(seed.Host.Id, requestId, true, null, key);
        var conflict = await requests.ReviewAsync(seed.Host.Id, requestId, false, "Otro resultado", key);

        Assert.Equal(ReservationChangeRequestOperationStatus.Success, first.Status);
        Assert.Equal(ReservationChangeRequestOperationStatus.Success, repeated.Status);
        Assert.Equal(ReservationChangeRequestOperationStatus.IdempotencyConflict, conflict.Status);
        Assert.Equal(1, await Context.Refunds.CountAsync(item => item.PaymentId == created.Payment.Id));
        Assert.Equal(ReservationChangeRequestStatuses.Approved,
            await Context.ReservationChangeRequests.Where(item => item.Id == requestId)
                .Select(item => item.Status).SingleAsync());
    }

    [Fact]
    public async Task Webhook_RejectionKeepsReservationPendingAndDoesNotDuplicateAttempts()
    {
        var seed = await SeedReservationAsync(quantity: 1, price: 50m);
        var service = GetRequiredService<IPaymentService>();
        var created = await service.CreateAsync(
            seed.Tourist.Id,
            seed.Reservation.Id,
            $"pay-{Guid.NewGuid():N}");
        var payment = created.Payment!;
        var rejected = new GatewayWebhookEvent(
            MockPaymentGateway.Provider,
            $"evt-failed-{Guid.NewGuid():N}",
            payment.ProviderPaymentId!,
            GatewayWebhookEventKind.PaymentFailed,
            "card_declined");

        Assert.Equal(WebhookProcessingStatus.Processed, await service.ProcessWebhookAsync(rejected));
        Assert.Equal(WebhookProcessingStatus.Duplicate, await service.ProcessWebhookAsync(rejected));
        Assert.Equal(PaymentStatuses.Pending,
            await Context.Payments.Where(item => item.Id == payment.Id).Select(item => item.Status).SingleAsync());
        Assert.Equal("card_declined",
            await Context.Payments.Where(item => item.Id == payment.Id).Select(item => item.FailureCode).SingleAsync());
        Assert.Equal(ReservationStatuses.PendingPayment,
            await Context.Reservations.Where(item => item.Id == seed.Reservation.Id)
                .Select(item => item.Status).SingleAsync());
        Assert.Equal(1, await Context.PaymentGatewayAttempts.CountAsync(item =>
            item.PaymentId == payment.Id && item.Outcome == PaymentGatewayAttemptOutcomes.Rejected));
    }

    [Fact]
    public async Task MockReject_KeepsReservationPendingAndAllowsNewPayment()
    {
        var seed = await SeedReservationAsync(quantity: 1, price: 60m);
        var service = GetRequiredService<IPaymentService>();
        var created = await service.CreateAsync(seed.Tourist.Id, seed.Reservation.Id, $"pay-{Guid.NewGuid():N}");

        var rejected = await service.MockRejectAsync(created.Payment!.Id, seed.Tourist.Id, isAdmin: false, "FondosInsuficientes");

        Assert.Equal(PaymentOperationStatus.Success, rejected.Status);
        Assert.Equal(PaymentStatuses.Failed, rejected.Payment!.Status);
        Assert.Equal("FondosInsuficientes", rejected.Payment.FailureCode);

        var reservation = await Context.Reservations.AsNoTracking()
            .SingleAsync(item => item.Id == seed.Reservation.Id);
        Assert.Equal(ReservationStatuses.PendingPayment, reservation.Status);

        var retry = await service.CreateAsync(seed.Tourist.Id, seed.Reservation.Id, $"pay-{Guid.NewGuid():N}");
        Assert.Equal(PaymentOperationStatus.Success, retry.Status);
        Assert.NotEqual(created.Payment!.Id, retry.Payment!.Id);
        Assert.Equal(2, await Context.Payments.CountAsync(item => item.ReservationId == seed.Reservation.Id));
    }

    [Fact]
    public async Task MockConfirm_OnRejectedPayment_ReturnsInvalidTransition()
    {
        var seed = await SeedReservationAsync(quantity: 1, price: 60m);
        var service = GetRequiredService<IPaymentService>();
        var created = await service.CreateAsync(seed.Tourist.Id, seed.Reservation.Id, $"pay-{Guid.NewGuid():N}");
        await service.MockRejectAsync(created.Payment!.Id, seed.Tourist.Id, isAdmin: false, null);

        var result = await service.MockConfirmAsync(created.Payment!.Id, seed.Tourist.Id, isAdmin: false);

        Assert.Equal(PaymentOperationStatus.InvalidTransition, result.Status);
        var reservation = await Context.Reservations.AsNoTracking()
            .SingleAsync(item => item.Id == seed.Reservation.Id);
        Assert.Equal(ReservationStatuses.PendingPayment, reservation.Status);
    }

    [Fact]
    public async Task Refund_ConfirmedReservation_ReleasesSpotsOnceAndPersistsAudit()
    {
        var seed = await SeedReservationAsync(quantity: 2, price: 50m);
        var service = GetRequiredService<IPaymentService>();
        var created = await service.CreateAsync(seed.Tourist.Id, seed.Reservation.Id, $"pay-{Guid.NewGuid():N}");
        var paymentId = created.Payment!.Id;
        await service.MockConfirmAsync(paymentId, seed.Tourist.Id, isAdmin: false);
        var spotsBeforeRefund = (await Context.ExperienceSchedules.AsNoTracking()
            .SingleAsync(item => item.Id == seed.Schedule.Id)).AvailableSpots;

        var refunded = await service.RefundAsync(paymentId, seed.Admin.Id, "Cancelacion por tormenta tropical.");
        var repeated = await service.RefundAsync(paymentId, seed.Admin.Id, "Cancelacion por tormenta tropical.");

        Assert.Equal(PaymentOperationStatus.Success, refunded.Status);
        Assert.Equal(PaymentOperationStatus.Success, repeated.Status);
        Assert.Equal(PaymentStatuses.Refunded, refunded.Payment!.Status);
        Assert.Equal(created.Payment.TotalAmount, refunded.Payment.RefundedAmount);

        Assert.Equal(1, await Context.Refunds.CountAsync(item => item.PaymentId == paymentId));
        var refund = await Context.Refunds.AsNoTracking().SingleAsync(item => item.PaymentId == paymentId);
        Assert.Equal(MockPaymentGateway.Provider, refund.Provider);
        Assert.StartsWith("mock_re_", refund.ProviderRefundId);
        Assert.Equal(seed.Admin.Id, refund.RequestedByUserId);
        Assert.Equal(RefundStatuses.Completed, refund.Status);

        var reservation = await Context.Reservations.AsNoTracking()
            .SingleAsync(item => item.Id == seed.Reservation.Id);
        Assert.Equal(ReservationStatuses.Refunded, reservation.Status);

        var schedule = await Context.ExperienceSchedules.AsNoTracking()
            .SingleAsync(item => item.Id == seed.Schedule.Id);
        Assert.Equal(spotsBeforeRefund + 2, schedule.AvailableSpots);
        Assert.Equal(1, await Context.ReservationStatusHistories.CountAsync(item =>
            item.ReservationId == reservation.Id && item.ToStatus == ReservationStatuses.Refunded));
    }

    [Fact]
    public async Task Refund_OnPendingPayment_ReturnsInvalidTransition()
    {
        var seed = await SeedReservationAsync(quantity: 1, price: 45m);
        var service = GetRequiredService<IPaymentService>();
        var created = await service.CreateAsync(seed.Tourist.Id, seed.Reservation.Id, $"pay-{Guid.NewGuid():N}");

        var result = await service.RefundAsync(created.Payment!.Id, seed.Admin.Id, "Aun no se cobra.");

        Assert.Equal(PaymentOperationStatus.InvalidTransition, result.Status);
        Assert.False(await Context.Refunds.AnyAsync(item => item.PaymentId == created.Payment!.Id));
    }

    [Fact]
    public async Task MockEndpoints_OnlyOwnerOrAdminCanDecide()
    {
        var seed = await SeedReservationAsync(quantity: 1, price: 45m);
        var outsider = await SeedReservationAsync(quantity: 1, price: 45m);
        var service = GetRequiredService<IPaymentService>();
        var created = await service.CreateAsync(seed.Tourist.Id, seed.Reservation.Id, $"pay-{Guid.NewGuid():N}");
        var paymentId = created.Payment!.Id;

        var foreignConfirm = await service.MockConfirmAsync(paymentId, outsider.Tourist.Id, isAdmin: false);
        var foreignReject = await service.MockRejectAsync(paymentId, outsider.Tourist.Id, isAdmin: false, null);
        var adminConfirm = await service.MockConfirmAsync(paymentId, seed.Admin.Id, isAdmin: true);

        Assert.Equal(PaymentOperationStatus.PaymentNotFound, foreignConfirm.Status);
        Assert.Equal(PaymentOperationStatus.PaymentNotFound, foreignReject.Status);
        Assert.Equal(PaymentOperationStatus.Success, adminConfirm.Status);
        Assert.Equal(PaymentStatuses.Paid, adminConfirm.Payment!.Status);
    }

    [Fact]
    public async Task GetById_OnlyOwnerOrAdminCanRead()
    {
        var seed = await SeedReservationAsync(quantity: 1, price: 45m);
        var outsider = await SeedReservationAsync(quantity: 1, price: 45m);
        var service = GetRequiredService<IPaymentService>();
        var created = await service.CreateAsync(seed.Tourist.Id, seed.Reservation.Id, $"pay-{Guid.NewGuid():N}");
        var paymentId = created.Payment!.Id;

        Assert.NotNull(await service.GetByIdAsync(paymentId, seed.Tourist.Id, isAdmin: false));
        Assert.NotNull(await service.GetByIdAsync(paymentId, seed.Admin.Id, isAdmin: true));
        Assert.Null(await service.GetByIdAsync(paymentId, outsider.Tourist.Id, isAdmin: false));
    }

    [Fact]
    public async Task Expiration_ReleasesCapacityOnceAndRecordsHistoryAuditAndFailedPayment()
    {
        var seed = await SeedReservationAsync(quantity: 2, price: 50m);
        var paymentService = GetRequiredService<IPaymentService>();
        var payment = await paymentService.CreateAsync(
            seed.Tourist.Id, seed.Reservation.Id, $"pay-{Guid.NewGuid():N}");
        var reservation = await Context.Reservations.SingleAsync(item => item.Id == seed.Reservation.Id);
        reservation.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        var expiration = GetRequiredService<IReservationExpirationService>();

        var first = await expiration.ExpireReservationAsync(seed.Reservation.Id);
        Context.ChangeTracker.Clear();
        var repeated = await expiration.ExpireReservationAsync(seed.Reservation.Id);

        Assert.True(first);
        Assert.False(repeated);
        Assert.Equal(ReservationStatuses.Expired,
            await Context.Reservations.Where(item => item.Id == seed.Reservation.Id)
                .Select(item => item.Status).SingleAsync());
        Assert.Equal(10, await Context.ExperienceSchedules.Where(item => item.Id == seed.Schedule.Id)
            .Select(item => item.AvailableSpots).SingleAsync());
        Assert.Equal(PaymentStatuses.Failed,
            await Context.Payments.Where(item => item.Id == payment.Payment!.Id)
                .Select(item => item.Status).SingleAsync());
        Assert.Equal("ReservationExpired",
            await Context.Payments.Where(item => item.Id == payment.Payment!.Id)
                .Select(item => item.FailureCode).SingleAsync());
        Assert.Equal(1, await Context.ReservationStatusHistories.CountAsync(item =>
            item.ReservationId == seed.Reservation.Id && item.ToStatus == ReservationStatuses.Expired));
        Assert.Equal(1, await Context.CapacityAudits.CountAsync(item =>
            item.ReservationId == seed.Reservation.Id && item.Reason == "ReservationExpired"));
    }

    [Fact]
    public async Task PaymentAfterDeadline_IsRejectedWhetherItStartsOrConfirmsLate()
    {
        var startSeed = await SeedReservationAsync(quantity: 1, price: 45m);
        var service = GetRequiredService<IPaymentService>();
        var startReservation = await Context.Reservations
            .SingleAsync(item => item.Id == startSeed.Reservation.Id);
        startReservation.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        var lateStart = await service.CreateAsync(
            startSeed.Tourist.Id, startSeed.Reservation.Id, $"pay-{Guid.NewGuid():N}");
        Assert.Equal(PaymentOperationStatus.ReservationExpired, lateStart.Status);

        var confirmSeed = await SeedReservationAsync(quantity: 1, price: 45m);
        var created = await service.CreateAsync(
            confirmSeed.Tourist.Id, confirmSeed.Reservation.Id, $"pay-{Guid.NewGuid():N}");
        var confirmReservation = await Context.Reservations
            .SingleAsync(item => item.Id == confirmSeed.Reservation.Id);
        confirmReservation.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        var lateConfirm = await service.MockConfirmAsync(
            created.Payment!.Id, confirmSeed.Tourist.Id, isAdmin: false);

        Assert.Equal(PaymentOperationStatus.ReservationExpired, lateConfirm.Status);
        Assert.Equal(ReservationStatuses.Expired,
            await Context.Reservations.Where(item => item.Id == confirmSeed.Reservation.Id)
                .Select(item => item.Status).SingleAsync());
        Assert.NotEqual(PaymentStatuses.Paid,
            await Context.Payments.Where(item => item.Id == created.Payment.Id)
                .Select(item => item.Status).SingleAsync());
    }

    private async Task<PaymentSeed> SeedReservationAsync(
        int quantity,
        decimal price,
        User? tourist = null)
    {
        var marker = Guid.NewGuid().ToString("N");
        if (tourist is null)
        {
            tourist = new User
            {
                FullName = "Turista Pagos",
                Email = $"pagos-turista-{marker}@goisland.test",
                PasswordHash = "hash-integracion",
                Role = UserRoles.Tourist
            };
            Context.Users.Add(tourist);
        }
        else
        {
            Context.Users.Attach(tourist);
        }

        var host = new User
        {
            FullName = "Anfitrion Pagos",
            Email = $"pagos-anfitrion-{marker}@goisland.test",
            PasswordHash = "hash-integracion",
            Role = UserRoles.Host
        };
        var admin = new User
        {
            FullName = "Administrador Pagos",
            Email = $"pagos-admin-{marker}@goisland.test",
            PasswordHash = "hash-integracion",
            Role = UserRoles.Admin
        };
        Context.Users.AddRange(host, admin);
        await Context.SaveChangesAsync();
        Context.HostProfiles.Add(new HostProfile
        {
            UserId = host.Id,
            DisplayName = host.FullName,
            Description = "Anfitrión para pruebas de pagos.",
            PhoneNumber = "8095550101",
            VerificationStatus = HostVerificationStatuses.Approved,
            ReviewedAt = DateTime.UtcNow,
            ReviewedByAdminId = admin.Id
        });
        await Context.SaveChangesAsync();

        var experience = new Experience
        {
            HostId = host.Id,
            Slug = $"pagos-{marker}",
            Title = $"Pagos {marker}",
            Description = "Experiencia para validar el ciclo de pagos.",
            Location = $"Lugar-{marker}",
            Category = "Integracion",
            Price = price,
            Capacity = 10,
            AvailableSpots = 10,
            IsApproved = true,
            ApprovalStatus = ExperienceApprovalStatuses.Approved
        };
        Context.Experiences.Add(experience);
        await Context.SaveChangesAsync();

        var startsAt = DateTime.UtcNow.AddDays(3);
        var schedule = new ExperienceSchedule
        {
            ExperienceId = experience.Id,
            StartsAt = startsAt,
            EndsAt = startsAt.AddHours(2),
            Capacity = 10,
            AvailableSpots = 10,
            Status = ScheduleStatuses.Scheduled
        };
        Context.ExperienceSchedules.Add(schedule);
        await Context.SaveChangesAsync();

        var reservationService = GetRequiredService<IReservationService>();
        var creation = await reservationService.CreateAsync(tourist.Id, new CreateReservationRequest
        {
            ScheduleId = schedule.Id,
            Quantity = quantity
        });
        Assert.Equal(ReservationCreationStatus.Success, creation.Status);

        // El pago ocurre en una solicitud posterior y, por tanto, con un tracker nuevo.
        Context.ChangeTracker.Clear();
        return new PaymentSeed(tourist, host, admin, schedule, creation.Reservation!);
    }

    private record PaymentSeed(
        User Tourist,
        User Host,
        User Admin,
        ExperienceSchedule Schedule,
        ReservationResponse Reservation);
}
