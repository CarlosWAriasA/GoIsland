using GoIsland.Api.DTOs.Experiences;
using GoIsland.Api.Services.Experiences;
using GoIsland.Api.Tests.Infrastructure;

namespace GoIsland.Api.Tests.Integration;

public class ExperienceIntegrationTests : PostgresIntegrationTestBase
{
    [Fact]
    public async Task PublicQueries_ReturnOnlyApprovedExperiencesAndApplyFilters()
    {
        var service = GetRequiredService<IExperienceService>();
        var marker = Guid.NewGuid().ToString("N");
        var location = $"Ubicacion-{marker}";
        var category = $"Categoria-{marker}";

        var approved = await service.CreateAsync(CreateRequest(
            $"Experiencia aprobada {marker}",
            location,
            category,
            75m), approveImmediately: true);

        var pending = await service.CreateAsync(CreateRequest(
            $"Experiencia pendiente {marker}",
            location,
            category,
            25m), approveImmediately: false);

        var search = await service.SearchAsync(new SearchExperiencesRequest
        {
            Location = location.ToLowerInvariant(),
            Category = category.ToLowerInvariant(),
            MaxPrice = 100m
        });

        var result = Assert.Single(search);
        Assert.Equal(approved.Id, result.Id);
        Assert.True(result.IsApproved);
        Assert.Null(await service.GetByIdAsync(pending.Id));
        Assert.NotNull(await service.GetByIdAsync(approved.Id));
    }

    [Fact]
    public async Task Create_InitializesAvailableSpotsAndPersistsThroughUnitOfWork()
    {
        var service = GetRequiredService<IExperienceService>();
        var marker = Guid.NewGuid().ToString("N");

        var created = await service.CreateAsync(CreateRequest(
            $"Capacidad {marker}",
            $"Lugar-{marker}",
            $"Tipo-{marker}",
            50m,
            capacity: 8), approveImmediately: true);

        Context.ChangeTracker.Clear();
        var stored = await Context.Experiences.FindAsync(created.Id);

        Assert.NotNull(stored);
        Assert.Equal(8, stored.Capacity);
        Assert.Equal(8, stored.AvailableSpots);
        Assert.True(stored.IsApproved);
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
            Description = "Descripcion creada por una prueba de integracion real.",
            Location = location,
            Category = category,
            Price = price,
            Capacity = capacity
        };
    }
}
