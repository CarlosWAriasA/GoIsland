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
            Role = UserRoles.Admin
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
            Role = UserRoles.Admin
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
    public async Task Administrator_CannotApplyForHostProfile()
    {
        var hostService = GetRequiredService<IHostService>();
        var marker = Guid.NewGuid().ToString("N");
        var admin = new User
        {
            FullName = "Admin Sin Solicitud",
            Email = $"admin-apply-{marker}@goisland.test",
            PasswordHash = "hash-no-usado",
            Role = UserRoles.Admin
        };
        Context.Users.Add(admin);
        await Context.SaveChangesAsync();

        var result = await hostService.ApplyAsync(admin.Id, new HostApplicationRequest
        {
            DisplayName = "Admin no anfitrion",
            Description = "Esta solicitud debe rechazarse por la regla de separacion de roles.",
            PhoneNumber = "+1 809 555 0177"
        });

        Assert.Equal(HostOperationStatus.Forbidden, result.Status);
        Assert.False(await Context.HostProfiles.AnyAsync(profile => profile.UserId == admin.Id));
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
}
