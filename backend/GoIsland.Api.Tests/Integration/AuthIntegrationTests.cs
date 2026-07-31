using GoIsland.Api.DTOs.Auth;
using GoIsland.Api.Models;
using GoIsland.Api.Services.Auth;
using GoIsland.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GoIsland.Api.Tests.Integration;

public class AuthIntegrationTests : PostgresIntegrationTestBase
{
    [Fact]
    public async Task RegisterAndLogin_PersistHashedUserAndReturnRealJwt()
    {
        var authService = GetRequiredService<IAuthService>();
        var email = $"integration-{Guid.NewGuid():N}@goisland.test";
        const string password = "Password123";

        var registration = await authService.RegisterAsync(new RegisterRequest
        {
            FullName = "Usuario Integracion",
            Email = email,
            Password = password,
            Role = UserRoles.Tourist
        });

        Assert.NotNull(registration);
        Assert.NotEmpty(registration.Token);

        var storedUser = await Context.Users.AsNoTracking().SingleAsync(user => user.Email == email);
        Assert.NotEqual(password, storedUser.PasswordHash);

        var login = await authService.LoginAsync(new LoginRequest
        {
            Email = email,
            Password = password
        });

        Assert.NotNull(login);
        Assert.NotEmpty(login.Token);
        Assert.Equal(storedUser.Id, login.User.Id);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_IsRejectedInRealRepository()
    {
        var authService = GetRequiredService<IAuthService>();
        var email = $"duplicate-{Guid.NewGuid():N}@goisland.test";
        var request = new RegisterRequest
        {
            FullName = "Usuario Duplicado",
            Email = email,
            Password = "Password123"
        };

        var first = await authService.RegisterAsync(request);
        var second = await authService.RegisterAsync(request);

        Assert.NotNull(first);
        Assert.Null(second);
        Assert.Equal(1, await Context.Users.CountAsync(user => user.Email == email));
    }

    [Fact]
    public async Task Login_WithInvalidPassword_IsRejected()
    {
        var authService = GetRequiredService<IAuthService>();
        var email = $"login-{Guid.NewGuid():N}@goisland.test";
        await authService.RegisterAsync(new RegisterRequest
        {
            FullName = "Usuario Login",
            Email = email,
            Password = "Password123"
        });

        var result = await authService.LoginAsync(new LoginRequest
        {
            Email = email,
            Password = "Incorrecta123"
        });

        Assert.Null(result);
    }

    [Fact]
    public async Task Login_AfterRepeatedFailures_IsLockedAndSuccessfulLoginResetsProtection()
    {
        var authService = GetRequiredService<IAuthService>();
        var email = $"lockout-{Guid.NewGuid():N}@goisland.test";
        const string password = "GoIslandSegura2026";
        await authService.RegisterAsync(new RegisterRequest
        {
            FullName = "Usuario Bloqueo",
            Email = email,
            Password = password
        });

        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.Null(await authService.LoginAsync(new LoginRequest
            {
                Email = email,
                Password = "ContrasenaIncorrecta2026"
            }));
        }

        var lockedUser = await Context.Users.SingleAsync(user => user.Email == email);
        Assert.Equal(5, lockedUser.FailedLoginAttempts);
        Assert.True(lockedUser.LockoutEnd > DateTime.UtcNow);
        Assert.Null(await authService.LoginAsync(new LoginRequest
        {
            Email = email,
            Password = password
        }));

        lockedUser.LockoutEnd = DateTime.UtcNow.AddSeconds(-1);
        await Context.SaveChangesAsync();

        var successfulLogin = await authService.LoginAsync(new LoginRequest
        {
            Email = email,
            Password = password
        });

        Assert.NotNull(successfulLogin);
        Assert.Equal(0, lockedUser.FailedLoginAttempts);
        Assert.Null(lockedUser.LockoutEnd);
    }

    [Fact]
    public async Task PublicRegistration_CannotGrantHostOrAdminRole()
    {
        var authService = GetRequiredService<IAuthService>();

        var result = await authService.RegisterAsync(new RegisterRequest
        {
            FullName = "Intento Anfitrion",
            Email = $"role-{Guid.NewGuid():N}@goisland.test",
            Password = "Password123",
            Role = UserRoles.Host
        });

        Assert.NotNull(result);
        Assert.Equal("Password", result.AuthenticationMethod);
        Assert.Equal(UserRoles.Tourist, result.User.Role);
    }

    [Fact]
    public async Task GoogleAuth_CreatesAndReusesAccountByProviderSubject()
    {
        var authService = GetRequiredService<IAuthService>();
        var email = $"google-{Guid.NewGuid():N}@goisland.test";
        var subject = Guid.NewGuid().ToString("N");
        var request = new GoogleAuthRequest
        {
            Credential = $"valid|{subject}|{email}|Usuario Google"
        };

        var first = await authService.AuthenticateWithGoogleAsync(request);
        var second = await authService.AuthenticateWithGoogleAsync(request);

        Assert.Equal(GoogleAuthStatus.Success, first.Status);
        Assert.Equal(GoogleAuthStatus.Success, second.Status);
        Assert.NotNull(first.Response);
        Assert.Equal("Google", first.Response.AuthenticationMethod);
        Assert.False(first.Response.User.HasPassword);
        var passwordChange = await authService.ChangePasswordAsync(first.Response.User.Id, new ChangePasswordRequest
        {
            CurrentPassword = "NoExiste123",
            NewPassword = "NuevaPassword123",
            ConfirmPassword = "NuevaPassword123"
        });
        Assert.Equal(ChangePasswordStatus.PasswordNotAvailable, passwordChange);
        Assert.Equal(first.Response.User.Id, second.Response!.User.Id);
        Assert.Equal(1, await Context.Users.CountAsync(user => user.Email == email));
        Assert.Equal(1, await Context.UserExternalLogins.CountAsync(login =>
            login.Provider == "Google" && login.ProviderSubject == subject));
    }

    [Fact]
    public async Task GoogleAuth_WithExistingLocalAccount_InformsUserWithoutLinking()
    {
        var authService = GetRequiredService<IAuthService>();
        var email = $"google-link-{Guid.NewGuid():N}@goisland.test";
        var registration = await authService.RegisterAsync(new RegisterRequest
        {
            FullName = "Usuario Existente",
            Email = email,
            Password = "Password123"
        });

        var result = await authService.AuthenticateWithGoogleAsync(new GoogleAuthRequest
        {
            Credential = $"valid|{Guid.NewGuid():N}|{email}|Nombre Google"
        });

        Assert.NotNull(registration);
        Assert.Equal(GoogleAuthStatus.LocalAccountExists, result.Status);
        Assert.Null(result.Response);
        Assert.Equal(1, await Context.Users.CountAsync(user => user.Email == email));
        Assert.Equal(0, await Context.UserExternalLogins.CountAsync(login => login.UserId == registration.User.Id));
    }

    [Fact]
    public async Task GoogleAuth_WithInvalidCredential_IsRejected()
    {
        var authService = GetRequiredService<IAuthService>();

        var result = await authService.AuthenticateWithGoogleAsync(new GoogleAuthRequest
        {
            Credential = "invalid"
        });

        Assert.Equal(GoogleAuthStatus.InvalidCredential, result.Status);
        Assert.Null(result.Response);
    }
}
