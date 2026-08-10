using GoIsland.Api.DTOs.Experiences;
using GoIsland.Api.Models;
using GoIsland.Api.Services.Experiences;
using GoIsland.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

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
        var admin = await CreateUserAsync($"admin-{marker}@goisland.test", UserRoles.Tourist, isAdmin: true);
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

        var result = Assert.Single(search.Items);
        Assert.Equal(1, search.TotalItems);
        Assert.Equal(1, search.TotalPages);
        Assert.Equal(approved.Experience.Id, result.Id);
        Assert.True(result.IsApproved);
        Assert.Null(await publicService.GetByIdAsync(draft.Experience!.Id));
        Assert.NotNull(await publicService.GetByIdAsync(approved.Experience.Id));
        Assert.Null(await publicService.GetBySlugAsync(draft.Experience.Slug));
        Assert.Equal(
            approved.Experience.Id,
            (await publicService.GetBySlugAsync(approved.Experience.Slug))?.Id);
    }

    [Fact]
    public async Task PublicSearch_AppliesTextFiltersSortingAndPaginationOnTheServer()
    {
        var service = GetRequiredService<IExperienceManagementService>();
        var publicService = GetRequiredService<IExperienceService>();
        var marker = Guid.NewGuid().ToString("N");
        var host = await CreateApprovedHostAsync(marker);
        var admin = await CreateUserAsync($"admin-search-{marker}@goisland.test", UserRoles.Tourist, isAdmin: true);

        foreach (var (title, price, language, difficulty) in new[]
        {
            ($"Cacao premium {marker}", 90m, "Español", ExperienceDifficulties.Easy),
            ($"Cacao familiar {marker}", 35m, "Español", ExperienceDifficulties.Easy),
            ($"Ruta costera {marker}", 15m, "Inglés", ExperienceDifficulties.Moderate)
        })
        {
            var created = await service.CreateAsync(host.Id, CreateRequest(
                title,
                $"Lugar-{marker}",
                $"Tipo-{marker}",
                price));
            var entity = await Context.Experiences.FindAsync(created.Experience!.Id);
            entity!.Languages = [language];
            entity.Difficulty = difficulty;
            entity.Tags = title.StartsWith("Cacao", StringComparison.Ordinal) ? ["cacao local"] : [];
            await Context.SaveChangesAsync();
            await AddCoverAsync(created.Experience.Id, $"{marker}-{created.Experience.Id}");
            await service.SubmitAsync(host.Id, created.Experience.Id);
            await service.ReviewAsync(created.Experience.Id, admin.Id, ExperienceReviewAction.Approve, null);
        }

        var firstPage = await publicService.SearchAsync(new SearchExperiencesRequest
        {
            Query = "cacao",
            Language = "español",
            Difficulty = ExperienceDifficulties.Easy,
            Sort = ExperienceSortOptions.PriceAscending,
            Page = 1,
            PageSize = 1
        });

        var first = Assert.Single(firstPage.Items);
        Assert.Contains("familiar", first.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, firstPage.TotalItems);
        Assert.Equal(2, firstPage.TotalPages);

        var secondPage = await publicService.SearchAsync(new SearchExperiencesRequest
        {
            Query = "cacao",
            Language = "español",
            Difficulty = ExperienceDifficulties.Easy,
            Sort = ExperienceSortOptions.PriceAscending,
            Page = 2,
            PageSize = 1
        });

        Assert.Contains("premium", Assert.Single(secondPage.Items).Title, StringComparison.OrdinalIgnoreCase);

        var hostPage = await service.GetMineAsync(host.Id, new ManagedExperienceListRequest
        {
            Query = "cacao",
            Status = ExperienceApprovalStatuses.Approved,
            Page = 1,
            PageSize = 1
        });
        Assert.Single(hostPage.Items);
        Assert.Equal(2, hostPage.TotalItems);

        var moderationPage = await service.GetForAdminAsync(new ManagedExperienceListRequest
        {
            Query = marker,
            Status = ExperienceApprovalStatuses.Approved,
            PageSize = 2
        });
        Assert.Equal(2, moderationPage.Items.Count);
        Assert.Equal(3, moderationPage.TotalItems);
    }

    [Fact]
    public async Task HostLifecycle_EnforcesOwnershipAndPersistsModerationAudit()
    {
        var service = GetRequiredService<IExperienceManagementService>();
        var marker = Guid.NewGuid().ToString("N");
        var owner = await CreateApprovedHostAsync($"owner-{marker}");
        var otherHost = await CreateApprovedHostAsync($"other-{marker}");
        var admin = await CreateUserAsync($"admin-{marker}@goisland.test", UserRoles.Tourist, isAdmin: true);

        var incomplete = await service.CreateAsync(owner.Id, new CreateExperienceRequest
        {
            Title = $"Borrador incompleto {marker}"
        });
        var blockedSubmission = await service.SubmitAsync(owner.Id, incomplete.Experience!.Id);
        Assert.Equal(ExperienceManagementStatus.Incomplete, blockedSubmission.Status);
        Assert.Equal("Revisa los campos marcados antes de enviar la experiencia.", blockedSubmission.Message);
        Assert.Equal("Escribe un resumen de la experiencia.", blockedSubmission.Errors!["ShortDescription"][0]);
        Assert.DoesNotContain("DurationMinutes", blockedSubmission.Errors);
        Assert.Equal("Agrega una foto de portada.", blockedSubmission.Errors["CoverImage"][0]);

        var completeWithoutDuration = CreateRequest(
            $"Propiedad {marker}", $"Lugar-{marker}", $"Tipo-{marker}", 50m, 8);
        completeWithoutDuration.DurationMinutes = null;
        var created = await service.CreateAsync(owner.Id, completeWithoutDuration);
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

    [Fact]
    public async Task Submit_AllowsOptionalExperienceDetailsToRemainEmpty()
    {
        var service = GetRequiredService<IExperienceManagementService>();
        var marker = Guid.NewGuid().ToString("N");
        var host = await CreateApprovedHostAsync($"optional-{marker}");
        var created = await service.CreateAsync(host.Id, new CreateExperienceRequest
        {
            Title = $"Paseo gratis {marker}",
            ShortDescription = "Una salida sencilla para disfrutar el río.",
            Description = "Una experiencia comunitaria gratuita para conocer y disfrutar el entorno.",
            DurationMinutes = 90,
            Location = $"Río San Juan-{marker}",
            Category = $"Tipo-{marker}",
            Price = 0m,
            Capacity = 10
        });
        await AddCoverAsync(created.Experience!.Id, $"optional-{marker}");

        var submitted = await service.SubmitAsync(host.Id, created.Experience.Id);

        Assert.Equal(ExperienceManagementStatus.Success, submitted.Status);
        Assert.Equal(ExperienceApprovalStatuses.PendingReview, submitted.Experience!.ApprovalStatus);
        Assert.Empty(submitted.Experience.WhatIsIncluded);
        Assert.Empty(submitted.Experience.WhatToBring);
        Assert.Empty(submitted.Experience.Languages);
        Assert.Empty(submitted.Experience.Itinerary);
        Assert.Equal(string.Empty, submitted.Experience.MeetingPointInstructions);
        Assert.Equal(string.Empty, submitted.Experience.GuestRequirements);
        Assert.Equal(string.Empty, submitted.Experience.Difficulty);
        Assert.Equal(string.Empty, submitted.Experience.CancellationPolicy);
    }

    [Fact]
    public async Task Management_RejectsPaidSelfGuidedExperience()
    {
        var marker = Guid.NewGuid().ToString("N");
        var host = await CreateApprovedHostAsync($"self-guided-{marker}");
        var request = CreateRequest(
            $"Autoguiada de pago {marker}",
            $"Lugar-{marker}",
            $"Tipo-{marker}",
            25m);
        request.SchedulingMode = ExperienceSchedulingModes.SelfGuided;

        var result = await GetRequiredService<IExperienceManagementService>().CreateAsync(host.Id, request);

        Assert.Equal(ExperienceManagementStatus.Incomplete, result.Status);
        Assert.Contains(nameof(CreateExperienceRequest.Price), result.Errors!.Keys);
        Assert.False(await Context.Experiences.AnyAsync(item => item.Title == request.Title));
    }

    [Fact]
    public async Task Delete_WithSchedulesButNoReservations_RemovesBoth()
    {
        var marker = Guid.NewGuid().ToString("N");
        var host = await CreateApprovedHostAsync($"delete-{marker}");
        var created = await GetRequiredService<IExperienceManagementService>().CreateAsync(
            host.Id,
            CreateRequest($"Con horario {marker}", $"Lugar-{marker}", $"Tipo-{marker}", 30m));
        Context.ExperienceSchedules.Add(BuildSchedule(created.Experience!.Id));
        await Context.SaveChangesAsync();

        var result = await GetRequiredService<IExperienceManagementService>()
            .DeleteAsync(host.Id, created.Experience.Id);

        Context.ChangeTracker.Clear();
        Assert.Equal(ExperienceManagementStatus.Success, result.Status);
        Assert.False(await Context.Experiences.AnyAsync(item => item.Id == created.Experience.Id));
        Assert.False(await Context.ExperienceSchedules.AnyAsync(
            item => item.ExperienceId == created.Experience.Id));
    }

    [Fact]
    public async Task Delete_WithReservations_KeepsExperienceAndSuggestsHiding()
    {
        var marker = Guid.NewGuid().ToString("N");
        var host = await CreateApprovedHostAsync($"delete-booked-{marker}");
        var tourist = await CreateUserAsync($"turista-{marker}@goisland.test", UserRoles.Tourist);
        var created = await GetRequiredService<IExperienceManagementService>().CreateAsync(
            host.Id,
            CreateRequest($"Con reserva {marker}", $"Lugar-{marker}", $"Tipo-{marker}", 30m));
        var schedule = BuildSchedule(created.Experience!.Id);
        Context.ExperienceSchedules.Add(schedule);
        await Context.SaveChangesAsync();
        Context.Reservations.Add(new Reservation
        {
            UserId = tourist.Id,
            ExperienceId = created.Experience.Id,
            ScheduleId = schedule.Id,
            Quantity = 1,
            Status = ReservationStatuses.Confirmed,
            TotalAmount = 30m,
            ReservationDate = DateTime.UtcNow
        });
        await Context.SaveChangesAsync();

        var result = await GetRequiredService<IExperienceManagementService>()
            .DeleteAsync(host.Id, created.Experience.Id);

        Assert.Equal(ExperienceManagementStatus.Conflict, result.Status);
        Assert.Contains("ocúltala", result.Message!, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.Experience!.HasReservations);
        Assert.True(await Context.Experiences.AnyAsync(item => item.Id == created.Experience.Id));
    }

    [Fact]
    public async Task Hiding_TakesTheExperienceOutOfTheCatalogWithoutLosingItsBookings()
    {
        var service = GetRequiredService<IExperienceManagementService>();
        var publicService = GetRequiredService<IExperienceService>();
        var marker = Guid.NewGuid().ToString("N");
        var host = await CreateApprovedHostAsync($"hide-{marker}");
        var admin = await CreateUserAsync($"admin-hide-{marker}@goisland.test", UserRoles.Tourist, isAdmin: true);
        var tourist = await CreateUserAsync($"turista-hide-{marker}@goisland.test", UserRoles.Tourist);
        var location = $"Ubicacion-{marker}";

        var created = await service.CreateAsync(host.Id, CreateRequest(
            $"Experiencia visible {marker}", location, $"Categoria-{marker}", 40m));
        var experienceId = created.Experience!.Id;
        await AddCoverAsync(experienceId, $"{marker}-{experienceId}");
        await service.SubmitAsync(host.Id, experienceId);
        await service.ReviewAsync(experienceId, admin.Id, ExperienceReviewAction.Approve, null);

        var schedule = BuildSchedule(experienceId);
        Context.ExperienceSchedules.Add(schedule);
        await Context.SaveChangesAsync();
        Context.Reservations.Add(new Reservation
        {
            UserId = tourist.Id,
            ExperienceId = experienceId,
            ScheduleId = schedule.Id,
            Quantity = 1,
            Status = ReservationStatuses.Confirmed,
            TotalAmount = 40m,
            ReservationDate = DateTime.UtcNow
        });
        await Context.SaveChangesAsync();

        Assert.Single((await publicService.SearchAsync(new SearchExperiencesRequest { Location = location })).Items);

        var hidden = await service.SetVisibilityAsync(host.Id, experienceId, isHidden: true);

        Context.ChangeTracker.Clear();
        Assert.Equal(ExperienceManagementStatus.Success, hidden.Status);
        Assert.True(hidden.Experience!.IsHidden);
        Assert.Empty((await publicService.SearchAsync(new SearchExperiencesRequest { Location = location })).Items);
        Assert.Null(await publicService.GetByIdAsync(experienceId));
        // Quien ya reservó conserva el acceso a la ficha para consultar su visita.
        Assert.NotNull(await publicService.GetByIdAsync(experienceId, tourist.Id));

        var shown = await service.SetVisibilityAsync(host.Id, experienceId, isHidden: false);

        Context.ChangeTracker.Clear();
        Assert.Equal(ExperienceManagementStatus.Success, shown.Status);
        Assert.False(shown.Experience!.IsHidden);
        Assert.NotNull(await publicService.GetByIdAsync(experienceId));
    }

    [Fact]
    public async Task Hiding_ADraftIsNotAllowed()
    {
        var marker = Guid.NewGuid().ToString("N");
        var host = await CreateApprovedHostAsync($"hide-draft-{marker}");
        var created = await GetRequiredService<IExperienceManagementService>().CreateAsync(
            host.Id,
            CreateRequest($"Borrador {marker}", $"Lugar-{marker}", $"Tipo-{marker}", 20m));

        var result = await GetRequiredService<IExperienceManagementService>()
            .SetVisibilityAsync(host.Id, created.Experience!.Id, isHidden: true);

        Assert.Equal(ExperienceManagementStatus.InvalidTransition, result.Status);
        Assert.False(result.Experience!.IsHidden);
    }

    private static ExperienceSchedule BuildSchedule(int experienceId) => new()
    {
        ExperienceId = experienceId,
        StartsAt = DateTime.UtcNow.AddDays(5),
        EndsAt = DateTime.UtcNow.AddDays(5).AddHours(2),
        Capacity = 10,
        AvailableSpots = 10,
        Status = ScheduleStatuses.Scheduled
    };

    [Fact]
    public async Task EditingImage_ReturnsApprovedExperienceToDraft()
    {
        var marker = Guid.NewGuid().ToString("N");
        var host = await CreateApprovedHostAsync($"image-{marker}");
        var experience = new Experience
        {
            HostId = host.Id,
            Title = $"Imagen aprobada {marker}",
            Description = "Experiencia aprobada cuya imagen será actualizada.",
            Location = "Santo Domingo",
            Category = "Cultura",
            Price = 30m,
            Capacity = 10,
            AvailableSpots = 10,
            IsApproved = true,
            ApprovalStatus = ExperienceApprovalStatuses.Approved
        };
        Context.Experiences.Add(experience);
        await Context.SaveChangesAsync();
        await AddCoverAsync(experience.Id, marker);
        var image = await Context.ExperienceImages.SingleAsync(item => item.ExperienceId == experience.Id);

        var result = await GetRequiredService<IExperienceImageService>().UpdateAsync(
            host.Id,
            experience.Id,
            image.Id,
            new UpdateExperienceImageRequest { AltText = "Nueva descripción de la portada", IsCover = true });

        Context.ChangeTracker.Clear();
        Assert.Equal(ExperienceImageStatus.Success, result.Status);
        var stored = await Context.Experiences.SingleAsync(item => item.Id == experience.Id);
        Assert.False(stored.IsApproved);
        Assert.Equal(ExperienceApprovalStatuses.Draft, stored.ApprovalStatus);
    }

    [Fact]
    public async Task PublicExperience_UsesCapacityFromNextReservableSchedule()
    {
        var marker = Guid.NewGuid().ToString("N");
        var host = await CreateApprovedHostAsync($"availability-{marker}");
        var experience = new Experience
        {
            HostId = host.Id,
            Title = $"Disponibilidad real {marker}",
            Description = "Experiencia con capacidad diferente en cada fecha.",
            Location = "Samaná",
            Category = "Naturaleza",
            Price = 30m,
            Capacity = 20,
            AvailableSpots = 20,
            IsApproved = true,
            ApprovalStatus = ExperienceApprovalStatuses.Approved
        };
        Context.Experiences.Add(experience);
        await Context.SaveChangesAsync();
        Context.ExperienceSchedules.AddRange(
            new ExperienceSchedule
            {
                ExperienceId = experience.Id,
                StartsAt = DateTime.UtcNow.AddDays(2),
                EndsAt = DateTime.UtcNow.AddDays(2).AddHours(2),
                Capacity = 6,
                AvailableSpots = 2,
                Status = ScheduleStatuses.Scheduled
            },
            new ExperienceSchedule
            {
                ExperienceId = experience.Id,
                StartsAt = DateTime.UtcNow.AddDays(3),
                EndsAt = DateTime.UtcNow.AddDays(3).AddHours(2),
                Capacity = 12,
                AvailableSpots = 12,
                Status = ScheduleStatuses.Scheduled
            });
        await Context.SaveChangesAsync();

        var result = await GetRequiredService<IExperienceService>().GetByIdAsync(experience.Id);

        Assert.NotNull(result);
        Assert.Equal(6, result.Capacity);
        Assert.Equal(2, result.AvailableSpots);
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

    private async Task<User> CreateUserAsync(string email, string role, bool isAdmin = false)
    {
        var user = new User
        {
            FullName = "Usuario Integracion",
            Email = email,
            PasswordHash = "hash-no-usado",
            Role = role,
            IsAdmin = isAdmin
        };
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        return user;
    }

    [Theory]
    [InlineData("samana")]      // sin tildes encuentra el titulo con tilde
    [InlineData("Samaná")]      // con tilde encuentra igual
    [InlineData("SAMANÁ")]      // mayusculas y tildes
    [InlineData("samaná")]
    public async Task PublicSearch_IgnoresAccentsAndCasingInTheQuery(string query)
    {
        var (marker, experienceId) = await SeedAccentedExperienceAsync();

        var search = await GetRequiredService<IExperienceService>().SearchAsync(new SearchExperiencesRequest
        {
            Query = query,
            Location = $"Lugar-{marker}"
        });

        var result = Assert.Single(search.Items);
        Assert.Equal(experienceId, result.Id);
    }

    [Fact]
    public async Task PublicSearch_IgnoresAccentsInLocationAndCategoryFilters()
    {
        var (marker, experienceId) = await SeedAccentedExperienceAsync();

        // La categoria almacenada es "Gastronomía-…" y se consulta sin tilde ni mayusculas.
        var search = await GetRequiredService<IExperienceService>().SearchAsync(new SearchExperiencesRequest
        {
            Location = $"lugar-{marker}",
            Category = $"gastronomia-{marker}"
        });

        var result = Assert.Single(search.Items);
        Assert.Equal(experienceId, result.Id);
    }

    /// <summary>Crea una experiencia aprobada con tildes en titulo, lugar y categoria.</summary>
    private async Task<(string Marker, int ExperienceId)> SeedAccentedExperienceAsync()
    {
        var service = GetRequiredService<IExperienceManagementService>();
        var marker = Guid.NewGuid().ToString("N");
        var host = await CreateApprovedHostAsync(marker);
        var admin = await CreateUserAsync($"admin-accents-{marker}@goisland.test", UserRoles.Tourist, isAdmin: true);

        var created = await service.CreateAsync(host.Id, CreateRequest(
            $"Bahía de Samaná {marker}",
            $"Lugar-{marker}",
            $"Gastronomía-{marker}",
            50m));

        var experienceId = created.Experience!.Id;
        await AddCoverAsync(experienceId, $"{marker}-{experienceId}");
        await service.SubmitAsync(host.Id, experienceId);
        await service.ReviewAsync(experienceId, admin.Id, ExperienceReviewAction.Approve, null);

        return (marker, experienceId);
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
