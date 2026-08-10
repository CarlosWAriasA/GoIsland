using System.Text.RegularExpressions;
using GoIsland.Api.Data;
using GoIsland.Api.Repositories;
using GoIsland.Api.Services.Auth;
using GoIsland.Api.Services.Email;
using GoIsland.Api.Services.Experiences;
using GoIsland.Api.Services.Hosts;
using GoIsland.Api.Services.Images;
using GoIsland.Api.Services.Payments;
using GoIsland.Api.Services.Notifications;
using GoIsland.Api.Services.Reservations;
using GoIsland.Api.Services.Reservations.Observers;
using GoIsland.Api.Services.Security;
using GoIsland.Api.Services.Schedules;
using GoIsland.Api.Services.Reviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
            .AddUserSecrets(typeof(GoIslandDbContext).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();

        // TestConnection tiene prioridad para poder apuntar a una base local sin tocar la
        // configuracion de desarrollo, que suele apuntar al entorno administrado.
        var connectionString = configuration.GetConnectionString("TestConnection")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection no esta configurado.");

        GuardAgainstRemoteDatabase(connectionString);

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
        services.AddScoped<IUserExternalLoginRepository, EfUserExternalLoginRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IPasswordResetTokenGenerator, PasswordResetTokenGenerator>();
        services.AddScoped<IEmailSender, FakeConfiguredEmailSender>();
        services.AddSingleton<IGoogleIdentityVerifier, FakeGoogleIdentityVerifier>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IExperienceService, ExperienceService>();
        services.AddScoped<IExperienceManagementService, ExperienceManagementService>();
        services.AddScoped<IExperienceImageService, ExperienceImageService>();
        services.AddSingleton<IImageStorage, FakeImageStorage>();
        services.AddScoped<IHostService, HostService>();
        services.AddScoped<IHostDashboardService, HostDashboardService>();
        services.AddScoped<NotificationService>();
        services.AddScoped<INotificationService>(provider => provider.GetRequiredService<NotificationService>());
        services.AddScoped<IOutboxWriter>(provider => provider.GetRequiredService<NotificationService>());
        services.AddSingleton<IPushNotificationSender, WebPushSender>();
        services.AddScoped<IOutboxProcessor, OutboxProcessor>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IOptions<ReservationExpirationOptions>>(
            Options.Create(new ReservationExpirationOptions()));
        services.AddScoped<IReservationExpirationService, ReservationExpirationService>();
        services.AddScoped<IReservationCompletionService, ReservationCompletionService>();
        services.AddScoped<IReservationObserver, EmailNotificationObserver>();
        services.AddScoped<IReservationObserver, PushNotificationObserver>();
        services.AddScoped<IReservationObserver, CapacityManagerObserver>();
        services.AddScoped<IReservationObserver, DashboardObserver>();
        services.AddScoped<IReservationService, ReservationService>();
        services.AddScoped<IReservationChangeRequestService, ReservationChangeRequestService>();
        services.AddScoped<IScheduleService, ScheduleService>();
        services.AddSingleton<IPaymentGateway, MockPaymentGateway>();
        services.AddSingleton<IOptions<PaymentPricingOptions>>(
            Options.Create(configuration.GetSection(PaymentPricingOptions.SectionName)
                .Get<PaymentPricingOptions>() ?? new PaymentPricingOptions()));
        services.AddSingleton<IPaymentPricingService, PaymentPricingService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IRefundRecoveryService, RefundRecoveryService>();

        _serviceProvider = services.BuildServiceProvider(validateScopes: true);
        _scope = _serviceProvider.CreateScope();
        Context = _scope.ServiceProvider.GetRequiredService<GoIslandDbContext>();

        Assert.True(
            await CanConnectWithRetryAsync(),
            "No fue posible conectar con la base PostgreSQL configurada después de tres intentos.");

        _transaction = await Context.Database.BeginTransactionAsync();

        // Aplica todos los scripts de esquema en orden numerico. Al leer el directorio se
        // garantiza que cualquier script nuevo quede cubierto por las pruebas.
        var scriptsDirectory = Path.Combine(configurationDirectory, "Database", "Scripts");
        var scriptFiles = Directory
            .GetFiles(scriptsDirectory, "*.sql")
            .OrderBy(Path.GetFileName, StringComparer.Ordinal);

        foreach (var scriptFile in scriptFiles)
        {
            var script = await File.ReadAllTextAsync(scriptFile);
            // ExecuteSqlRawAsync interpreta las llaves como marcadores de formato.
            await Context.Database.ExecuteSqlRawAsync(StripOwnTransaction(script).Replace("{}", "{{}}"));
        }

        // Fuerza una consulta real y falla temprano si el esquema no esta aplicado.
        await Context.Users.AsNoTracking().AnyAsync();
    }

    private const string AllowRemoteVariable = "GOISLAND_ALLOW_REMOTE_TEST_DB";

    /// <summary>
    /// Las pruebas reaplican todo el esquema y toman locks exclusivos sobre las tablas, asi que
    /// no deben ejecutarse contra una base compartida o de produccion. Se exige una base local
    /// salvo que se autorice explicitamente lo contrario.
    /// </summary>
    private static void GuardAgainstRemoteDatabase(string connectionString)
    {
        if (string.Equals(
                Environment.GetEnvironmentVariable(AllowRemoteVariable),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var host = ResolveHost(connectionString);
        string[] localHosts = ["localhost", "127.0.0.1", "::1", "host.docker.internal", "postgres", "db"];
        if (localHosts.Contains(host, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Las pruebas de integracion apuntan a '{host}', que no es una base local. " +
            "Reaplican todo el esquema y bloquean tablas, asi que no deben correr contra una " +
            "base compartida.\n\n" +
            "Levanta una base local:\n" +
            "  docker run -d --name goisland-tests -e POSTGRES_PASSWORD=postgres " +
            "-e POSTGRES_DB=goisland_tests -p 5432:5432 postgres:16\n\n" +
            "Y apunta las pruebas a ella:\n" +
            "  dotnet user-secrets set \"ConnectionStrings:TestConnection\" " +
            "\"Host=localhost;Port=5432;Database=goisland_tests;Username=postgres;Password=postgres\" " +
            "--project GoIsland.Api\n\n" +
            $"Para autorizar una base remota a proposito: {AllowRemoteVariable}=true");
    }

    /// <summary>Obtiene el host tanto de una cadena clave=valor como de un URI postgresql://.</summary>
    private static string ResolveHost(string connectionString)
    {
        var trimmed = connectionString.TrimStart();
        if (trimmed.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ? uri.Host : trimmed;
        }

        try
        {
            return new NpgsqlConnectionStringBuilder(connectionString).Host ?? string.Empty;
        }
        catch (ArgumentException)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Quita el BEGIN/COMMIT propio de un script. Las pruebas ya ejecutan dentro de una
    /// transaccion que se revierte al terminar, y un COMMIT dentro del script la cerraria
    /// dejando datos visibles para las demas pruebas.
    /// </summary>
    private static string StripOwnTransaction(string script) => Regex.Replace(
        script,
        @"^[ \t]*(BEGIN|COMMIT)[ \t]*;[ \t]*\r?$",
        string.Empty,
        RegexOptions.Multiline | RegexOptions.IgnoreCase);

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

    private async Task<bool> CanConnectWithRetryAsync()
    {
        const int maximumAttempts = 3;

        for (var attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            try
            {
                if (await Context.Database.CanConnectAsync())
                {
                    return true;
                }
            }
            catch (Exception)
            {
                // Neon puede reactivar o reemplazar una conexión entre pruebas. El assert del
                // llamador conserva un diagnóstico estable si se agotan los intentos.
            }

            if (attempt < maximumAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt));
            }
        }

        return false;
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
