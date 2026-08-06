using GoIsland.Api.DTOs.Experiences;
using GoIsland.Api.Models;
using GoIsland.Api.Services.Experiences;
using GoIsland.Api.Services.Hosts;
using GoIsland.Api.Tests.Infrastructure;

namespace GoIsland.Api.Tests.Integration;

public class LocationAndDashboardIntegrationTests : PostgresIntegrationTestBase
{
    [Fact]
    public async Task Nearby_ReturnsOnlyApprovedExperiencesInsideRadiusOrderedByDistance()
    {
        var host = await SeedApprovedHostAsync();
        Context.Experiences.AddRange(
            NewExperience(host.Id, "Zona Colonial", 18.4727m, -69.8838m, approved: true),
            NewExperience(host.Id, "Santiago", 19.4517m, -70.6970m, approved: true),
            NewExperience(host.Id, "Borrador cercano", 18.4750m, -69.8800m, approved: false),
            NewExperience(host.Id, "Sin ubicación", null, null, approved: true));
        await Context.SaveChangesAsync();

        var results = await GetRequiredService<IExperienceService>().GetNearbyAsync(new NearbyExperiencesRequest
        {
            Latitude = 18.4861m,
            Longitude = -69.9312m,
            RadiusKm = 25m
        });

        var result = Assert.Single(results.Items);
        Assert.Equal(1, results.TotalItems);
        Assert.Equal("Zona Colonial", result.Title);
        Assert.NotNull(result.DistanceKm);
        Assert.InRange(result.DistanceKm!.Value, 4m, 7m);
    }

    [Fact]
    public async Task Dashboard_AggregatesOnlyDataOwnedByApprovedHost()
    {
        var host = await SeedApprovedHostAsync();
        var tourist = new User
        {
            FullName = "Turista Dashboard",
            Email = $"tourist-dashboard-{Guid.NewGuid():N}@goisland.test",
            PasswordHash = "hash",
            Role = UserRoles.Tourist
        };
        Context.Users.Add(tourist);
        var experience = NewExperience(host.Id, "Ruta del cacao", 18.9m, -70.2m, approved: true);
        Context.Experiences.Add(experience);
        await Context.SaveChangesAsync();

        var upcoming = new ExperienceSchedule
        {
            ExperienceId = experience.Id,
            StartsAt = DateTime.UtcNow.AddDays(3),
            EndsAt = DateTime.UtcNow.AddDays(3).AddHours(2),
            Capacity = 10,
            AvailableSpots = 7,
            Status = ScheduleStatuses.Scheduled
        };
        var completedSchedule = new ExperienceSchedule
        {
            ExperienceId = experience.Id,
            StartsAt = DateTime.UtcNow.AddDays(-4),
            EndsAt = DateTime.UtcNow.AddDays(-4).AddHours(2),
            Capacity = 8,
            AvailableSpots = 6,
            Status = ScheduleStatuses.Completed
        };
        Context.ExperienceSchedules.AddRange(upcoming, completedSchedule);
        await Context.SaveChangesAsync();

        var confirmed = new Reservation
        {
            UserId = tourist.Id,
            ExperienceId = experience.Id,
            ScheduleId = upcoming.Id,
            Quantity = 3,
            Status = ReservationStatuses.Confirmed,
            TotalAmount = 300m
        };
        var completed = new Reservation
        {
            UserId = tourist.Id,
            ExperienceId = experience.Id,
            ScheduleId = completedSchedule.Id,
            Quantity = 2,
            Status = ReservationStatuses.Completed,
            TotalAmount = 200m
        };
        Context.Reservations.AddRange(confirmed, completed);
        await Context.SaveChangesAsync();
        Context.Payments.Add(new Payment
        {
            ReservationId = confirmed.Id,
            UserId = tourist.Id,
            Provider = "Mock",
            ProviderPaymentId = $"pay_{Guid.NewGuid():N}",
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            RequestHash = "dashboard",
            Currency = "USD",
            Amount = 315m,
            SubtotalAmount = 300m,
            ServiceFeeAmount = 15m,
            PlatformCommissionAmount = 36m,
            HostNetAmount = 264m,
            Status = PaymentStatuses.Paid,
            PaidAt = DateTime.UtcNow
        });
        Context.Reviews.Add(new Review
        {
            ReservationId = completed.Id,
            UserId = tourist.Id,
            ExperienceId = experience.Id,
            HostId = host.Id,
            Rating = 5,
            Comment = "Una experiencia excelente y bien organizada.",
            ModerationStatus = ReviewModerationStatuses.Visible
        });
        await Context.SaveChangesAsync();

        var dashboard = await GetRequiredService<IHostDashboardService>().GetAsync(host.Id);

        Assert.NotNull(dashboard);
        Assert.Equal(1, dashboard.TotalExperiences);
        Assert.Equal(1, dashboard.PublishedExperiences);
        Assert.Equal(1, dashboard.UpcomingSchedules);
        Assert.Equal(1, dashboard.UpcomingReservations);
        Assert.Equal(3, dashboard.ReservedSpots);
        Assert.Equal(1, dashboard.CompletedReservations);
        Assert.Equal(264m, dashboard.NetEarnings);
        Assert.Equal(5m, dashboard.AverageRating);
        Assert.Equal(1, dashboard.ReviewCount);
        Assert.Single(dashboard.NextSchedules);
    }

    private async Task<User> SeedApprovedHostAsync()
    {
        var host = new User
        {
            FullName = "Anfitrión Métricas",
            Email = $"host-dashboard-{Guid.NewGuid():N}@goisland.test",
            PasswordHash = "hash",
            Role = UserRoles.Host
        };
        Context.Users.Add(host);
        await Context.SaveChangesAsync();
        Context.HostProfiles.Add(new HostProfile
        {
            UserId = host.Id,
            DisplayName = host.FullName,
            Description = "Perfil aprobado para mapas y métricas.",
            PhoneNumber = "+18095550000",
            VerificationStatus = HostVerificationStatuses.Approved
        });
        await Context.SaveChangesAsync();
        return host;
    }

    private static Experience NewExperience(
        int hostId,
        string title,
        decimal? latitude,
        decimal? longitude,
        bool approved) => new()
    {
        HostId = hostId,
        Slug = $"location-test-{Guid.NewGuid():N}",
        Title = title,
        Description = "Experiencia con ubicación verificable.",
        Location = "República Dominicana",
        Latitude = latitude,
        Longitude = longitude,
        Category = "Cultura",
        Price = 100m,
        Capacity = 10,
        AvailableSpots = 10,
        ApprovalStatus = approved ? ExperienceApprovalStatuses.Approved : ExperienceApprovalStatuses.Draft,
        IsApproved = approved
    };
}
