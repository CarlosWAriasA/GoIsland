using GoIsland.Api.DTOs.Experiences;
using GoIsland.Api.Models;
using GoIsland.Api.Services.Experiences;
using GoIsland.Api.Tests.Infrastructure;

namespace GoIsland.Api.Tests.Integration;

public class ExperienceIntegrationTests : PostgresIntegrationTestBase
{
    [Fact]
    public async Task PublicQueries_ReturnOnlyApprovedExperiencesAndApplyFilters()
    {
        var service = GetRequiredService<IExperienceManagementService>();
        var publicService = GetRequiredService<IExperienceService>();
        var marker = Guid.NewGuid().ToString("N");
        var host = await CreateApprovedHostAsync(marker);
        var admin = await CreateUserAsync($"admin-{marker}@goisland.test", UserRoles.Admin);
        var location = $"Ubicacion-{marker}";
        var category = $"Categoria-{marker}";

        var approved = await service.CreateAsync(host.Id, CreateRequest(
            $"Experiencia aprobada {marker}", location, category, 75m));
        await AddCoverAsync(approved.Experience!.Id, marker);
        await service.SubmitAsync(host.Id, approved.Experience!.Id);
        await service.ReviewAsync(
            approved.Experience.Id,
            admin.Id,
            ExperienceReviewAction.Approve,
            null);

        var draft = await service.CreateAsync(host.Id, CreateRequest(
            $"Experiencia borrador {marker}", location, category, 25m));

        var search = await publicService.SearchAsync(new SearchExperiencesRequest
        {
            Location = location.ToLowerInvariant(),
            Category = category.ToLowerInvariant(),
            MaxPrice = 100m
        });

        var result = Assert.Single(search);
        Assert.Equal(approved.Experience.Id, result.Id);
        Assert.True(result.IsApproved);
        Assert.Null(await publicService.GetByIdAsync(draft.Experience!.Id));
        Assert.NotNull(await publicService.GetByIdAsync(approved.Experience.Id));
    }

    [Fact]
    public async Task HostLifecycle_EnforcesOwnershipAndPersistsModerationAudit()
    {
        var service = GetRequiredService<IExperienceManagementService>();
        var marker = Guid.NewGuid().ToString("N");
        var owner = await CreateApprovedHostAsync($"owner-{marker}");
        var otherHost = await CreateApprovedHostAsync($"other-{marker}");
        var admin = await CreateUserAsync($"admin-{marker}@goisland.test", UserRoles.Admin);

        var incomplete = await service.CreateAsync(owner.Id, new CreateExperienceRequest
        {
            Title = $"Borrador incompleto {marker}",
            Description = "Este borrador todavía no está listo para revisión.",
            Location = $"Lugar-{marker}",
            Category = $"Tipo-{marker}",
            Price = 10m,
            Capacity = 2
        });
        var blockedSubmission = await service.SubmitAsync(owner.Id, incomplete.Experience!.Id);
        Assert.Equal(ExperienceManagementStatus.Incomplete, blockedSubmission.Status);
        Assert.Contains("Completa antes de enviar", blockedSubmission.Message);

        var created = await service.CreateAsync(owner.Id, CreateRequest(
            $"Propiedad {marker}", $"Lugar-{marker}", $"Tipo-{marker}", 50m, 8));
        await AddCoverAsync(created.Experience!.Id, marker);

        Assert.Equal(ExperienceApprovalStatuses.Draft, created.Experience!.ApprovalStatus);
        Assert.Equal(8, created.Experience.AvailableSpots);
        Assert.Null(await service.GetMineByIdAsync(otherHost.Id, created.Experience.Id));

        var submitted = await service.SubmitAsync(owner.Id, created.Experience.Id);
        Assert.Equal(ExperienceApprovalStatuses.PendingReview, submitted.Experience!.ApprovalStatus);

        var rejected = await service.ReviewAsync(
            created.Experience.Id,
            admin.Id,
            ExperienceReviewAction.Reject,
            "Falta detalle del punto de encuentro.");

        Assert.Equal(ExperienceApprovalStatuses.Rejected, rejected.Experience!.ApprovalStatus);
        Assert.False((await Context.Experiences.FindAsync(created.Experience.Id))!.IsApproved);
        Assert.Contains(
            Context.AdminAuditLogs,
            log => log.EntityType == nameof(Experience)
                && log.EntityId == created.Experience.Id
                && log.Action == nameof(ExperienceReviewAction.Reject));
    }

    private async Task<User> CreateApprovedHostAsync(string marker)
    {
        var user = await CreateUserAsync($"host-{marker}@goisland.test", UserRoles.Host);
        Context.HostProfiles.Add(new HostProfile
        {
            UserId = user.Id,
            DisplayName = $"Anfitrion {marker}",
            Description = "Perfil aprobado para una prueba de integracion real.",
            PhoneNumber = "+1 809 555 0101",
            VerificationStatus = HostVerificationStatuses.Approved
        });
        await Context.SaveChangesAsync();
        return user;
    }

    private async Task AddCoverAsync(int experienceId, string marker)
    {
        Context.ExperienceImages.Add(new ExperienceImage
        {
            ExperienceId = experienceId,
            Provider = ImageStorageProviders.Cloudinary,
            PublicId = $"tests/{marker}",
            SecureUrl = $"https://res.cloudinary.com/test/image/upload/tests/{marker}.jpg",
            Width = 1200,
            Height = 800,
            Format = "jpg",
            FileName = $"{marker}.jpg",
            ContentType = "image/jpeg",
            AltText = "Vista de la experiencia",
            IsCover = true,
            SortOrder = 0
        });
        await Context.SaveChangesAsync();
    }

    private async Task<User> CreateUserAsync(string email, string role)
    {
        var user = new User
        {
            FullName = "Usuario Integracion",
            Email = email,
            PasswordHash = "hash-no-usado",
            Role = role
        };
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        return user;
    }

    private static CreateExperienceRequest CreateRequest(
        string title,
        string location,
        string category,
        decimal price,
        int capacity = 10)
    {
        return new CreateExperienceRequest
        {
            Title = title,
            ShortDescription = "Una experiencia preparada para pruebas.",
            Description = "Descripcion creada por una prueba de integracion real.",
            DurationMinutes = 120,
            MeetingPointInstructions = "Encuentro en la entrada principal.",
            WhatIsIncluded = ["Guía local"],
            WhatToBring = ["Agua"],
            GuestRequirements = "Llegar diez minutos antes.",
            Difficulty = ExperienceDifficulties.Easy,
            Languages = ["Español"],
            CancellationPolicy = CancellationPolicies.Flexible,
            Itinerary =
            [
                new ExperienceItineraryItemRequest
                {
                    Title = "Bienvenida",
                    Description = "Presentación y recorrido inicial.",
                    DurationMinutes = 30
                }
            ],
            Location = location,
            Category = category,
            Price = price,
            Capacity = capacity
        };
    }
}
