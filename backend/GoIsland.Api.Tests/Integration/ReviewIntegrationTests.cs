using GoIsland.Api.DTOs.Reviews;
using GoIsland.Api.Models;
using GoIsland.Api.Services.Experiences;
using GoIsland.Api.Services.Reviews;
using GoIsland.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GoIsland.Api.Tests.Integration;

public class ReviewIntegrationTests : PostgresIntegrationTestBase
{
    [Fact]
    public async Task CompletedReservation_AllowsOneVerifiedReview_AndUpdatesAggregate()
    {
        var seed = await SeedAsync(ReservationStatuses.Completed);
        var service = GetRequiredService<IReviewService>();

        var created = await service.CreateAsync(seed.Tourist.Id, seed.Reservation.Id,
            new ReviewRequest { Rating = 5, Comment = "Una experiencia excelente y muy bien organizada." });
        var duplicate = await service.CreateAsync(seed.Tourist.Id, seed.Reservation.Id,
            new ReviewRequest { Rating = 4, Comment = "Intento duplicado que debe ser rechazado." });
        var experience = await GetRequiredService<IExperienceService>().GetByIdAsync(seed.Experience.Id);

        Assert.Equal(ReviewMutationStatus.Success, created.Status);
        Assert.Equal(ReviewMutationStatus.Duplicate, duplicate.Status);
        Assert.Equal(5m, experience!.AverageRating);
        Assert.Equal(1, experience.ReviewCount);
    }

    [Fact]
    public async Task PendingReservation_CannotBeReviewed()
    {
        var seed = await SeedAsync(ReservationStatuses.Confirmed);
        var result = await GetRequiredService<IReviewService>().CreateAsync(seed.Tourist.Id, seed.Reservation.Id,
            new ReviewRequest { Rating = 5, Comment = "Todavia no corresponde publicar esta opinion." });
        Assert.Equal(ReviewMutationStatus.ReservationNotCompleted, result.Status);
    }

    [Fact]
    public async Task HostCannotReviewOwnExperience()
    {
        var seed = await SeedAsync(ReservationStatuses.Completed);
        seed.Reservation.UserId = seed.Experience.HostId;
        await Context.SaveChangesAsync();

        var result = await GetRequiredService<IReviewService>().CreateAsync(
            seed.Experience.HostId,
            seed.Reservation.Id,
            new ReviewRequest { Rating = 5, Comment = "Opinión del propio anfitrión." });

        Assert.Equal(ReviewMutationStatus.OwnExperience, result.Status);
        Assert.False(await Context.Reviews.AnyAsync(item => item.ReservationId == seed.Reservation.Id));
    }

    [Fact]
    public async Task HiddenReview_DisappearsFromPublicReputation_AndKeepsAudit()
    {
        var seed = await SeedAsync(ReservationStatuses.Completed);
        var admin = NewUser("Admin", UserRoles.Admin);
        Context.Users.Add(admin);
        await Context.SaveChangesAsync();
        var service = GetRequiredService<IReviewService>();
        var created = await service.CreateAsync(seed.Tourist.Id, seed.Reservation.Id,
            new ReviewRequest { Rating = 2, Comment = "La actividad necesita mejorar varios detalles." });

        var hidden = await service.HideAsync(admin.Id, created.Review!.Id, "Contenido reportado y revisado.");
        var publicReviews = await service.GetForExperienceAsync(
            seed.Experience.Id,
            new ReviewListRequest { Query = "mejorar", PageSize = 1 });

        Assert.Equal(ReviewMutationStatus.Success, hidden.Status);
        Assert.Empty(publicReviews.Items);
        Assert.Equal(0, publicReviews.TotalItems);
        Assert.True(await Context.AdminAuditLogs.AnyAsync(item => item.EntityType == "Review" && item.EntityId == created.Review.Id));
    }

    private async Task<(User Tourist, Experience Experience, Reservation Reservation)> SeedAsync(string status)
    {
        var tourist = NewUser("Turista", UserRoles.Tourist);
        var host = NewUser("Anfitrion", UserRoles.Host);
        Context.Users.AddRange(tourist, host);
        await Context.SaveChangesAsync();
        Context.HostProfiles.Add(new HostProfile
        {
            UserId = host.Id,
            DisplayName = host.FullName,
            Description = "Perfil aprobado para recibir opiniones.",
            PhoneNumber = "+1 809 555 0122",
            VerificationStatus = HostVerificationStatuses.Approved
        });
        var experience = new Experience
        {
            HostId = host.Id, Title = "Ruta cultural", Description = "Recorrido cultural verificado.",
            Location = "Santo Domingo", Category = "Cultura", Price = 40m, Capacity = 8,
            AvailableSpots = 7, IsApproved = true, ApprovalStatus = ExperienceApprovalStatuses.Approved
        };
        Context.Experiences.Add(experience);
        await Context.SaveChangesAsync();
        var schedule = new ExperienceSchedule
        {
            ExperienceId = experience.Id, StartsAt = DateTime.UtcNow.AddDays(-2), EndsAt = DateTime.UtcNow.AddDays(-2).AddHours(2),
            Capacity = 8, AvailableSpots = 7, Status = ScheduleStatuses.Completed
        };
        Context.ExperienceSchedules.Add(schedule);
        await Context.SaveChangesAsync();
        var reservation = new Reservation
        {
            UserId = tourist.Id, ExperienceId = experience.Id, ScheduleId = schedule.Id,
            Quantity = 1, Status = status, TotalAmount = 40m
        };
        Context.Reservations.Add(reservation);
        await Context.SaveChangesAsync();
        return (tourist, experience, reservation);
    }

    private static User NewUser(string name, string role) => new()
    {
        FullName = name, Email = $"{Guid.NewGuid():N}@goisland.test", PasswordHash = "hash-integracion", Role = role
    };
}
