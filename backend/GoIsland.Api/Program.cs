using System.Text;
using GoIsland.Api.Data;
using GoIsland.Api.Repositories;
using GoIsland.Api.Services.Auth;
using GoIsland.Api.Services.Email;
using GoIsland.Api.Services.Experiences;
using GoIsland.Api.Services.Reservations;
using GoIsland.Api.Services.Reservations.Observers;
using GoIsland.Api.Services.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
const string FrontendCorsPolicy = "FrontendCorsPolicy";

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

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

            return new BadRequestObjectResult(new
            {
                message = "La solicitud tiene errores de validacion.",
                errors
            });
        };
    });
var frontendUrl = builder.Configuration["Cors:FrontendUrl"] ?? "http://localhost:5173";

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
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddSingleton<IPasswordResetTokenGenerator, PasswordResetTokenGenerator>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IExperienceService, ExperienceService>();
builder.Services.AddScoped<IReservationObserver, EmailNotificationObserver>();
builder.Services.AddScoped<IReservationObserver, PushNotificationObserver>();
builder.Services.AddScoped<IReservationObserver, CapacityManagerObserver>();
builder.Services.AddScoped<IReservationObserver, DashboardObserver>();
builder.Services.AddScoped<IReservationService, ReservationService>();

var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key no esta configurado.");
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
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsJsonAsync(new
        {
            message = databaseUnavailable
                ? "La base de datos no esta disponible temporalmente."
                : "Ocurrio un error inesperado al procesar la solicitud."
        });
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(FrontendCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

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
