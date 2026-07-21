using GoIsland.Api.DTOs.Reservations;
using GoIsland.Api.Services.Reservations;
using GoIsland.Api.Services.Reservations.Observers;
using GoIsland.Api.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace GoIsland.Api.Tests.Integration;

public class ObserverIntegrationTests : PostgresIntegrationTestBase
{
    [Fact]
    public async Task DependencyInjection_ResolvesAndExecutesAllConcreteObservers()
    {
        var observers = GetRequiredService<IEnumerable<IReservationObserver>>().ToArray();

        Assert.Collection(
            observers,
            observer => Assert.IsType<EmailNotificationObserver>(observer),
            observer => Assert.IsType<PushNotificationObserver>(observer),
            observer => Assert.IsType<CapacityManagerObserver>(observer),
            observer => Assert.IsType<DashboardObserver>(observer));

        var reservationEvent = new ReservationEvent(
            ReservationEventType.Created,
            new ReservationResponse
            {
                Id = 1,
                UserId = 1,
                ExperienceId = 1,
                Quantity = 1,
                Status = "Pending",
                TotalAmount = 25m,
                ReservationDate = DateTime.UtcNow
            },
            RemainingSpots: 4,
            OccurredAt: DateTime.UtcNow);

        foreach (var observer in observers)
        {
            await observer.UpdateAsync(reservationEvent);
        }
    }

    [Fact]
    public async Task ReservationService_AllowsSubscribeNotifyAndUnsubscribeWithRealObserver()
    {
        var service = GetRequiredService<IReservationService>();
        var observer = GetRequiredService<IEnumerable<IReservationObserver>>().First();
        var reservationEvent = new ReservationEvent(
            ReservationEventType.Created,
            new ReservationResponse
            {
                Id = 2,
                UserId = 2,
                ExperienceId = 2,
                Quantity = 1,
                Status = "Pending",
                TotalAmount = 30m,
                ReservationDate = DateTime.UtcNow
            },
            RemainingSpots: 3,
            OccurredAt: DateTime.UtcNow);

        service.Subscribe(observer);
        await service.NotifyAsync(reservationEvent);
        service.Unsubscribe(observer);
    }
}
