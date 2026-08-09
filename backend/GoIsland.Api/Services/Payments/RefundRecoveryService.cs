using GoIsland.Api.Data;
using GoIsland.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GoIsland.Api.Services.Payments;

public interface IRefundRecoveryService
{
    Task<int> RetryFailedAsync(CancellationToken cancellationToken = default);
}

public class RefundRecoveryService : IRefundRecoveryService
{
    private const int MaxAttempts = 8;
    private readonly GoIslandDbContext _context;
    private readonly IPaymentService _payments;
    private readonly ILogger<RefundRecoveryService> _logger;

    public RefundRecoveryService(
        GoIslandDbContext context,
        IPaymentService payments,
        ILogger<RefundRecoveryService> logger)
    {
        _context = context;
        _payments = payments;
        _logger = logger;
    }

    public async Task<int> RetryFailedAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var candidates = await _context.Refunds.AsNoTracking()
            .Where(item => item.Status == RefundStatuses.Failed && item.AttemptCount < MaxAttempts)
            .OrderBy(item => item.UpdatedAt)
            .Take(20)
            .Select(item => new
            {
                item.PaymentId,
                item.RequestedByUserId,
                item.Reason,
                item.AttemptCount,
                item.UpdatedAt
            })
            .ToArrayAsync(cancellationToken);

        var recovered = 0;
        foreach (var candidate in candidates)
        {
            var delaySeconds = Math.Min(3600, Math.Pow(2, candidate.AttemptCount) * 15);
            if (candidate.UpdatedAt.AddSeconds(delaySeconds) > now) continue;

            var result = await _payments.RefundAsync(
                candidate.PaymentId,
                candidate.RequestedByUserId,
                candidate.Reason ?? "Reintento automático del reembolso.");
            if (result.Status == PaymentOperationStatus.Success)
            {
                recovered++;
            }
            else
            {
                _logger.LogWarning(
                    "El reintento automatico del reembolso para el pago {PaymentId} termino en {Status}.",
                    candidate.PaymentId,
                    result.Status);
            }
        }

        return recovered;
    }
}

public class RefundRecoveryBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RefundRecoveryBackgroundService> _logger;

    public RefundRecoveryBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<RefundRecoveryBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var recovery = scope.ServiceProvider.GetRequiredService<IRefundRecoveryService>();
                await recovery.RetryFailedAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "No fue posible reconciliar los reembolsos pendientes.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
