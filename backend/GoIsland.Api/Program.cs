using System.Text;
using System.Net;
using System.Threading.RateLimiting;
using GoIsland.Api.Data;
using GoIsland.Api.DTOs.Common;
using GoIsland.Api.Middleware;
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
using GoIsland.Api.Services.Schedules;
using GoIsland.Api.Services.Security;
using GoIsland.Api.Services.Reviews;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
const string FrontendCorsPolicy = "FrontendCorsPolicy";

var platformPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(platformPort))
{
    if (!int.TryParse(platformPort, out var parsedPort) || parsedPort is < 1 or > 65535)
    {
        throw new InvalidOperationException("PORT debe ser un número entre 1 y 65535.");
    }

    builder.WebHost.UseUrls($"http://0.0.0.0:{parsedPort}");
}

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fffK";
});

// Add services to the container.

builder.Services
    .AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value!.Errors.Select(error => error.ErrorMessage).ToArray());

            return new BadRequestObjectResult(ApiProblemDetailsFactory.CreateValidation(
                context.HttpContext,
                errors,
                "Revisa los datos indicados e inténtalo nuevamente."));
        };
    });
var frontendUrl = SecurityConfiguration.ResolveFrontendOrigin(
    builder.Configuration,
    builder.Environment.EnvironmentName);

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy
            .WithOrigins(frontendUrl)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.RequireHeaderSymmetry = true;

    if (builder.Configuration.GetValue<bool>("ForwardedHeaders:TrustManagedProxy"))
    {
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    }

    foreach (var configuredProxy in builder.Configuration
        .GetSection("ForwardedHeaders:KnownProxies")
        .Get<string[]>() ?? Array.Empty<string>())
    {
        if (!IPAddress.TryParse(configuredProxy, out var proxyAddress))
        {
            throw new InvalidOperationException(
                $"ForwardedHeaders:KnownProxies contiene una direccion IP invalida: {configuredProxy}.");
        }

        options.KnownProxies.Add(proxyAddress);
    }
});

builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                Math.Ceiling(retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        context.HttpContext.Response.ContentType = "application/problem+json";
        var problem = ApiProblemDetailsFactory.Create(
            context.HttpContext,
            StatusCodes.Status429TooManyRequests,
            "Demasiadas solicitudes",
            "Espera un momento antes de intentarlo nuevamente.");
        await context.HttpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
    };

    options.AddPolicy(RateLimitPolicyNames.Authentication, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetClientPartitionKey(httpContext),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy(RateLimitPolicyNames.PasswordRecovery, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetClientPartitionKey(httpContext),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

var databaseConnectionString = NormalizePostgresConnectionString(
    builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection no esta configurado."));

builder.Services.AddDbContext<GoIslandDbContext>(options =>
    options.UseNpgsql(databaseConnectionString));

builder.Services.AddScoped<IUserRepository, EfUserRepository>();
builder.Services.AddScoped<IExperienceRepository, EfExperienceRepository>();
builder.Services.AddScoped<IReservationRepository, EfReservationRepository>();
builder.Services.AddScoped<IPaymentRepository, EfPaymentRepository>();
builder.Services.AddScoped<IPasswordResetTokenRepository, EfPasswordResetTokenRepository>();
builder.Services.AddScoped<IUserExternalLoginRepository, EfUserExternalLoginRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddSingleton<IPasswordResetTokenGenerator, PasswordResetTokenGenerator>();
builder.Services.AddSingleton<IGoogleIdentityVerifier, GoogleIdentityVerifier>();
var emailProvider = builder.Configuration["Email:Provider"] ?? "Smtp";
if (emailProvider.Equals("Smtp", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
}
else if (emailProvider.Equals("Resend", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHttpClient<IEmailSender, ResendEmailSender>(client =>
        client.BaseAddress = new Uri("https://api.resend.com/"));
}
else
{
    throw new InvalidOperationException("Email:Provider debe ser Smtp o Resend.");
}
// El proveedor se resuelve antes de registrar servicios: Production no puede arrancar con
// Mock, Stripe solo acepta modo Sandbox y cualquier clave live detiene la aplicacion.
var paymentsProvider = PaymentProviderStartup.ResolveProvider(
    builder.Configuration["Payments:Provider"],
    builder.Configuration["Payments:Mode"],
    builder.Environment.EnvironmentName,
    builder.Configuration["Stripe:SecretKey"],
    builder.Configuration["Stripe:WebhookSecret"]);
if (paymentsProvider.Equals(PaymentProviderStartup.MockProvider, StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IPaymentGateway, MockPaymentGateway>();
}
else
{
    builder.Services.AddSingleton<IPaymentGateway, StripePaymentGateway>();
}
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IExperienceService, ExperienceService>();
builder.Services.AddScoped<IExperienceManagementService, ExperienceManagementService>();
builder.Services.AddScoped<IExperienceImageService, ExperienceImageService>();
builder.Services.AddSingleton<IImageStorage, CloudinaryImageStorage>();
builder.Services.AddScoped<IHostService, HostService>();
builder.Services.AddScoped<IHostDashboardService, HostDashboardService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<INotificationService>(services => services.GetRequiredService<NotificationService>());
builder.Services.AddScoped<IOutboxWriter>(services => services.GetRequiredService<NotificationService>());
builder.Services.AddScoped<IOutboxProcessor, OutboxProcessor>();
builder.Services.AddSingleton<IPushNotificationSender, WebPushSender>();
builder.Services.AddHostedService<OutboxBackgroundService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddOptions<ReservationExpirationOptions>()
    .Bind(builder.Configuration.GetSection(ReservationExpirationOptions.SectionName))
    .Validate(options => options.HoldMinutes is >= 1 and <= 1440,
        "Reservations:Expiration:HoldMinutes debe estar entre 1 y 1440.")
    .Validate(options => options.PollIntervalSeconds is >= 5 and <= 3600,
        "Reservations:Expiration:PollIntervalSeconds debe estar entre 5 y 3600.")
    .Validate(options => options.BatchSize is >= 1 and <= 500,
        "Reservations:Expiration:BatchSize debe estar entre 1 y 500.")
    .ValidateOnStart();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IReservationExpirationService, ReservationExpirationService>();
builder.Services.AddScoped<IReservationObserver, EmailNotificationObserver>();
builder.Services.AddScoped<IReservationObserver, PushNotificationObserver>();
builder.Services.AddScoped<IReservationObserver, CapacityManagerObserver>();
builder.Services.AddScoped<IReservationObserver, DashboardObserver>();
builder.Services.AddScoped<IReservationService, ReservationService>();
builder.Services.AddScoped<IScheduleService, ScheduleService>();
builder.Services.AddHostedService<ReservationExpirationBackgroundService>();

var jwtKey = SecurityConfiguration.GetRequiredJwtKey(
    builder.Configuration,
    builder.Environment.EnvironmentName);
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = signingKey,
            ClockSkew = TimeSpan.Zero
        };
    });

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "GoIsland API",
        Version = "v1"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Ingresa el token JWT devuelto por /api/auth/login o /api/auth/register."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseForwardedHeaders();
app.UseMiddleware<CorrelationIdMiddleware>();

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        context.Context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
    }
});

app.UseExceptionHandler(errorApplication =>
{
    errorApplication.Run(async context =>
    {
        var exception = context.Features
            .Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?
            .Error;
        var databaseUnavailable = HasInnerException<NpgsqlException>(exception);
        var logger = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("GlobalExceptionHandler");

        logger.LogError(exception, "Unhandled exception while processing {Path}.", context.Request.Path);

        context.Response.StatusCode = databaseUnavailable
            ? StatusCodes.Status503ServiceUnavailable
            : StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        var message = databaseUnavailable
            ? "GoIsland no está disponible temporalmente. Inténtalo de nuevo en unos minutos."
            : "Ocurrió un error inesperado. Inténtalo nuevamente.";
        var title = databaseUnavailable ? "Servicio no disponible" : "Error inesperado";
        await context.Response.WriteAsJsonAsync(ApiProblemDetailsFactory.Create(
            context,
            context.Response.StatusCode,
            title,
            message));
    });
});

app.UseStatusCodePages(async statusCodeContext =>
{
    var context = statusCodeContext.HttpContext;
    var (title, message) = context.Response.StatusCode switch
    {
        StatusCodes.Status401Unauthorized =>
            ("Acceso requerido", "Inicia sesión para continuar."),
        StatusCodes.Status403Forbidden =>
            ("Acción no permitida", "No tienes permiso para realizar esta acción."),
        StatusCodes.Status404NotFound =>
            ("No encontrado", "No encontramos lo que buscas."),
        StatusCodes.Status405MethodNotAllowed =>
            ("Acción no disponible", "Esta acción no está disponible."),
        _ => ("Solicitud no completada", "No pudimos completar la solicitud.")
    };

    context.Response.ContentType = "application/problem+json";
    await context.Response.WriteAsJsonAsync(ApiProblemDetailsFactory.Create(
        context,
        context.Response.StatusCode,
        title,
        message));
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseRouting();
app.UseCors(FrontendCorsPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

static string GetClientPartitionKey(HttpContext context)
{
    return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

static string NormalizePostgresConnectionString(string connectionString)
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

    var query = ParseQueryString(uri.Query);
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

static Dictionary<string, string> ParseQueryString(string query)
{
    return query
        .TrimStart('?')
        .Split('&', StringSplitOptions.RemoveEmptyEntries)
        .Select(part => part.Split('=', 2))
        .Where(parts => parts.Length == 2)
        .ToDictionary(
            parts => Uri.UnescapeDataString(parts[0]).ToLowerInvariant(),
            parts => Uri.UnescapeDataString(parts[1]));
}

static bool HasInnerException<TException>(Exception? exception)
    where TException : Exception
{
    while (exception is not null)
    {
        if (exception is TException)
        {
            return true;
        }

        exception = exception.InnerException;
    }

    return false;
}
