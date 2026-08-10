using GoIsland.Api.DTOs.Hosts;
using GoIsland.Api.Models;
using GoIsland.Api.Services.Auth;
using GoIsland.Api.Services.Hosts;
using GoIsland.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GoIsland.Api.Tests.Integration;

public class HostIntegrationTests : PostgresIntegrationTestBase
{
    [Fact]
    public async Task ApplicationAndApproval_PromoteUserAndPersistAuditAtomically()
    {
        var authService = GetRequiredService<IAuthService>();
        var hostService = GetRequiredService<IHostService>();
        var marker = Guid.NewGuid().ToString("N");
        var registration = await authService.RegisterAsync(new()
        {
            FullName = "Aspirante Anfitrion",
            Email = $"aspirante-{marker}@goisland.test",
            Password = "Password123",
            Role = UserRoles.Host
        });
        var admin = new User
        {
            FullName = "Admin Integracion",
            Email = $"admin-{marker}@goisland.test",
            PasswordHash = "hash-no-usado",
            Role = UserRoles.Tourist,
            IsAdmin = true
        };
        Context.Users.Add(admin);
        await Context.SaveChangesAsync();

        Assert.NotNull(registration);
        Assert.Equal(UserRoles.Tourist, registration.User.Role);

        var application = await hostService.ApplyAsync(registration.User.Id, new HostApplicationRequest
        {
            DisplayName = "Rutas con Ana",
            Description = "Guia local enfocada en experiencias culturales responsables.",
            PhoneNumber = "+1 809 555 0199"
        });
        Assert.Equal(HostVerificationStatuses.Pending, application.Profile!.VerificationStatus);

        var approval = await hostService.ReviewAsync(
            application.Profile.Id,
            admin.Id,
            HostReviewAction.Approve,
            null);

        Context.ChangeTracker.Clear();
        Assert.Equal(HostVerificationStatuses.Approved, approval.Profile!.VerificationStatus);
        Assert.Equal(UserRoles.Host, (await Context.Users.FindAsync(registration.User.Id))!.Role);
        Assert.True(await Context.AdminAuditLogs.AnyAsync(log =>
            log.EntityType == nameof(HostProfile)
            && log.EntityId == application.Profile.Id
            && log.Action == nameof(HostReviewAction.Approve)));
    }

    [Fact]
    public async Task Rejection_RequiresReasonAndAllowsARealResubmission()
    {
        var authService = GetRequiredService<IAuthService>();
        var hostService = GetRequiredService<IHostService>();
        var marker = Guid.NewGuid().ToString("N");
        var registration = await authService.RegisterAsync(new()
        {
            FullName = "Aspirante Rechazo",
            Email = $"rechazo-{marker}@goisland.test",
            Password = "Password123"
        });
        var admin = new User
        {
            FullName = "Admin Integracion",
            Email = $"admin-rechazo-{marker}@goisland.test",
            PasswordHash = "hash-no-usado",
            Role = UserRoles.Tourist,
            IsAdmin = true
        };
        Context.Users.Add(admin);
        await Context.SaveChangesAsync();

        var request = new HostApplicationRequest
        {
            DisplayName = "Experiencias del Cibao",
            Description = "Propuesta local para compartir patrimonio y gastronomia regional.",
            PhoneNumber = "+1 809 555 0188"
        };
        var application = await hostService.ApplyAsync(registration!.User.Id, request);
        var withoutReason = await hostService.ReviewAsync(
            application.Profile!.Id,
            admin.Id,
            HostReviewAction.Reject,
            null);
        Assert.Equal(HostOperationStatus.ReasonRequired, withoutReason.Status);

        var rejected = await hostService.ReviewAsync(
            application.Profile.Id,
            admin.Id,
            HostReviewAction.Reject,
            "Completa la informacion de contacto.");
        Assert.Equal(HostVerificationStatuses.Rejected, rejected.Profile!.VerificationStatus);

        var resubmitted = await hostService.ApplyAsync(registration.User.Id, request);
        Assert.Equal(HostVerificationStatuses.Pending, resubmitted.Profile!.VerificationStatus);
        Assert.Null(resubmitted.Profile.RejectionReason);
    }

    [Fact]
    public async Task Administrator_CanApplyForHostProfileAndKeepsAdminPermission()
    {
        var hostService = GetRequiredService<IHostService>();
        var marker = Guid.NewGuid().ToString("N");
        var admin = new User
        {
            FullName = "Admin Anfitrion",
            Email = $"admin-apply-{marker}@goisland.test",
            PasswordHash = "hash-no-usado",
            Role = UserRoles.Tourist,
            IsAdmin = true
        };
        var reviewer = new User
        {
            FullName = "Admin Revisor",
            Email = $"admin-revisor-{marker}@goisland.test",
            PasswordHash = "hash-no-usado",
            Role = UserRoles.Tourist,
            IsAdmin = true
        };
        Context.Users.AddRange(admin, reviewer);
        await Context.SaveChangesAsync();

        var application = await hostService.ApplyAsync(admin.Id, new HostApplicationRequest
        {
            DisplayName = "Admin anfitrion",
            Description = "Administrar no impide participar como anfitrion en la plataforma.",
            PhoneNumber = "+1 809 555 0177"
        });
        Assert.Equal(HostOperationStatus.Success, application.Status);

        var approved = await hostService.ReviewAsync(
            application.Profile!.Id,
            reviewer.Id,
            HostReviewAction.Approve,
            null);
        Assert.Equal(HostOperationStatus.Success, approved.Status);

        var stored = await Context.Users.FindAsync(admin.Id);
        Assert.Equal(UserRoles.Host, stored!.Role);
        Assert.True(stored.IsAdmin);
    }

    [Fact]
    public async Task Administrator_CannotReviewOwnHostApplication()
    {
        var hostService = GetRequiredService<IHostService>();
        var marker = Guid.NewGuid().ToString("N");
        var admin = new User
        {
            FullName = "Admin Autorevision",
            Email = $"admin-autorevision-{marker}@goisland.test",
            PasswordHash = "hash-no-usado",
            Role = UserRoles.Tourist,
            IsAdmin = true
        };
        Context.Users.Add(admin);
        await Context.SaveChangesAsync();

        var application = await hostService.ApplyAsync(admin.Id, new HostApplicationRequest
        {
            DisplayName = "Admin autorevision",
            Description = "La solicitud de quien administra debe revisarla otra persona.",
            PhoneNumber = "+1 809 555 0166"
        });
        var result = await hostService.ReviewAsync(
            application.Profile!.Id,
            admin.Id,
            HostReviewAction.Approve,
            null);

        Assert.Equal(HostOperationStatus.Forbidden, result.Status);
        Assert.Equal(
            HostVerificationStatuses.Pending,
            (await Context.HostProfiles.FindAsync(application.Profile.Id))!.VerificationStatus);
    }

    [Fact]
    public async Task AdminList_SearchesFiltersAndPaginatesApplicationsOnTheServer()
    {
        var authService = GetRequiredService<IAuthService>();
        var hostService = GetRequiredService<IHostService>();
        var marker = Guid.NewGuid().ToString("N");

        foreach (var name in new[] { $"Bahía {marker}", $"Montaña {marker}" })
        {
            var registration = await authService.RegisterAsync(new()
            {
                FullName = $"Responsable {name}",
                Email = $"{Guid.NewGuid():N}@goisland.test",
                Password = "Password123"
            });
            await hostService.ApplyAsync(registration!.User.Id, new HostApplicationRequest
            {
                DisplayName = name,
                Description = "Solicitud preparada para validar el listado administrativo.",
                PhoneNumber = "+1 809 555 0110"
            });
        }

        var page = await hostService.GetForAdminAsync(new HostApplicationListRequest
        {
            Query = marker,
            Status = HostVerificationStatuses.Pending,
            Page = 2,
            PageSize = 1
        });

        Assert.Single(page.Items);
        Assert.Equal(2, page.TotalItems);
        Assert.Equal(2, page.TotalPages);
        Assert.Contains(marker, page.Items.Single().DisplayName);
    }

    [Fact]
    public async Task Suspension_UnpublishesExperiencesAndClosesFutureSchedulesWithoutCancellingReservations()
    {
        var marker = Guid.NewGuid().ToString("N");
        var host = new User
        {
            FullName = "Anfitrión suspendido",
            Email = $"suspended-host-{marker}@goisland.test",
            PasswordHash = "hash-integracion",
            Role = UserRoles.Host
        };
        var tourist = new User
        {
            FullName = "Turista con reserva",
            Email = $"suspended-tourist-{marker}@goisland.test",
            PasswordHash = "hash-integracion",
            Role = UserRoles.Tourist
        };
        var admin = new User
        {
            FullName = "Admin suspensión",
            Email = $"suspended-admin-{marker}@goisland.test",
            PasswordHash = "hash-integracion",
            Role = UserRoles.Tourist,
            IsAdmin = true
        };
        Context.Users.AddRange(host, tourist, admin);
        await Context.SaveChangesAsync();
        var profile = new HostProfile
        {
            UserId = host.Id,
            DisplayName = host.FullName,
            Description = "Perfil aprobado antes de la suspensión.",
            PhoneNumber = "+1 809 555 0123",
            VerificationStatus = HostVerificationStatuses.Approved
        };
        var experience = new Experience
        {
            HostId = host.Id,
            Title = $"Experiencia suspendida {marker}",
            Description = "Experiencia publicada antes de suspender al anfitrión.",
            Location = "Santo Domingo",
            Category = "Cultura",
            Price = 50m,
            Capacity = 8,
            AvailableSpots = 7,
            IsApproved = true,
            ApprovalStatus = ExperienceApprovalStatuses.Approved
        };
        Context.HostProfiles.Add(profile);
        Context.Experiences.Add(experience);
        await Context.SaveChangesAsync();
        var schedule = new ExperienceSchedule
        {
            ExperienceId = experience.Id,
            StartsAt = DateTime.UtcNow.AddDays(5),
            EndsAt = DateTime.UtcNow.AddDays(5).AddHours(2),
            Capacity = 8,
            AvailableSpots = 7,
            Status = ScheduleStatuses.Scheduled
        };
        Context.ExperienceSchedules.Add(schedule);
        await Context.SaveChangesAsync();
        var reservation = new Reservation
        {
            UserId = tourist.Id,
            ExperienceId = experience.Id,
            ScheduleId = schedule.Id,
            Quantity = 1,
            Status = ReservationStatuses.Confirmed,
            TotalAmount = 50m
        };
        Context.Reservations.Add(reservation);
        await Context.SaveChangesAsync();

        var result = await GetRequiredService<IHostService>().ReviewAsync(
            profile.Id,
            admin.Id,
            HostReviewAction.Suspend,
            "Incumplimiento revisado por administración.");

        Context.ChangeTracker.Clear();
        Assert.Equal(HostOperationStatus.Success, result.Status);
        Assert.Equal(HostVerificationStatuses.Suspended,
            await Context.HostProfiles.Where(item => item.Id == profile.Id).Select(item => item.VerificationStatus).SingleAsync());
        Assert.False(await Context.Experiences.Where(item => item.Id == experience.Id).Select(item => item.IsApproved).SingleAsync());
        Assert.Equal(ExperienceApprovalStatuses.Suspended,
            await Context.Experiences.Where(item => item.Id == experience.Id).Select(item => item.ApprovalStatus).SingleAsync());
        Assert.Equal(ScheduleStatuses.Closed,
            await Context.ExperienceSchedules.Where(item => item.Id == schedule.Id).Select(item => item.Status).SingleAsync());
        Assert.Equal(ReservationStatuses.Confirmed,
            await Context.Reservations.Where(item => item.Id == reservation.Id).Select(item => item.Status).SingleAsync());
    }
}
