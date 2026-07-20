using GoIsland.Api.Data;
using GoIsland.Api.Repositories;
using GoIsland.Api.Services.Auth;
using GoIsland.Api.Services.Email;
using GoIsland.Api.Services.Experiences;
using GoIsland.Api.Services.Reservations;
using GoIsland.Api.Services.Reservations.Observers;
using GoIsland.Api.Services.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace GoIsland.Api.Tests.Infrastructure;

public abstract class PostgresIntegrationTestBase : IAsyncLifetime
{
    private ServiceProvider _serviceProvider = null!;
    private IServiceScope _scope = null!;
    private IDbContextTransaction _transaction = null!;

    protected GoIslandDbContext Context { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var configurationDirectory = FindConfigurationDirectory();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(configurationDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection no esta configurado.");

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddDbContext<GoIslandDbContext>(options =>
            options.UseNpgsql(NormalizePostgresConnectionString(connectionString)));
        services.AddScoped<IUserRepository, EfUserRepository>();
        services.AddScoped<IExperienceRepository, EfExperienceRepository>();
        services.AddScoped<IReservationRepository, EfReservationRepository>();
        services.AddScoped<IPaymentRepository, EfPaymentRepository>();
        services.AddScoped<IPasswordResetTokenRepository, EfPasswordResetTokenRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IPasswordResetTokenGenerator, PasswordResetTokenGenerator>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IExperienceService, ExperienceService>();
        services.AddScoped<IReservationObserver, EmailNotificationObserver>();
        services.AddScoped<IReservationObserver, PushNotificationObserver>();
        services.AddScoped<IReservationObserver, CapacityManagerObserver>();
        services.AddScoped<IReservationObserver, DashboardObserver>();
        services.AddScoped<IReservationService, ReservationService>();

        _serviceProvider = services.BuildServiceProvider(validateScopes: true);
        _scope = _serviceProvider.CreateScope();
        Context = _scope.ServiceProvider.GetRequiredService<GoIslandDbContext>();

        Assert.True(
            await Context.Database.CanConnectAsync(),
            "No fue posible conectar con la base PostgreSQL configurada.");

        _transaction = await Context.Database.BeginTransactionAsync();

        var passwordResetScript = await File.ReadAllTextAsync(Path.Combine(
            configurationDirectory,
            "Database",
            "Scripts",
            "002_create_password_reset_tokens.sql"));
        await Context.Database.ExecuteSqlRawAsync(passwordResetScript);

        // Fuerza una consulta real y falla temprano si el esquema no esta aplicado.
        await Context.Users.AsNoTracking().AnyAsync();
    }

    public async Task DisposeAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
        }

        _scope?.Dispose();
        await _serviceProvider.DisposeAsync();
    }

    protected T GetRequiredService<T>() where T : notnull
    {
        return _scope.ServiceProvider.GetRequiredService<T>();
    }

    private static string FindConfigurationDirectory()
    {
        var candidates = new[]
        {
            AppContext.BaseDirectory,
            Path.Combine(Directory.GetCurrentDirectory(), "GoIsland.Api"),
            Path.Combine(Directory.GetCurrentDirectory(), "backend", "GoIsland.Api"),
            Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "GoIsland.Api"))
        };

        return candidates.FirstOrDefault(candidate =>
                File.Exists(Path.Combine(candidate, "appsettings.json")))
            ?? throw new DirectoryNotFoundException(
                "No se encontro la configuracion de GoIsland.Api.");
    }

    private static string NormalizePostgresConnectionString(string connectionString)
    {
        if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var uri)
            || (uri.Scheme != "postgresql" && uri.Scheme != "postgres"))
        {
            return connectionString;
        }

        var userInfo = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = Uri.UnescapeDataString(userInfo.ElementAtOrDefault(0) ?? string.Empty),
            Password = Uri.UnescapeDataString(userInfo.ElementAtOrDefault(1) ?? string.Empty)
        };

        var query = uri.Query
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(
                parts => Uri.UnescapeDataString(parts[0]).ToLowerInvariant(),
                parts => Uri.UnescapeDataString(parts[1]));

        if (query.TryGetValue("sslmode", out var sslModeValue)
            && Enum.TryParse<SslMode>(sslModeValue, ignoreCase: true, out var sslMode))
        {
            builder.SslMode = sslMode;
        }

        if (query.TryGetValue("channel_binding", out var channelBindingValue)
            && Enum.TryParse<ChannelBinding>(channelBindingValue, ignoreCase: true, out var channelBinding))
        {
            builder.ChannelBinding = channelBinding;
        }

        return builder.ConnectionString;
    }
}
