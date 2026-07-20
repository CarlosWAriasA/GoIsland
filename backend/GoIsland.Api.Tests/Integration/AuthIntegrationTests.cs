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
}
