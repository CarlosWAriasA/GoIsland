using GoIsland.Api.Data;
using GoIsland.Api.DTOs.Auth;
using GoIsland.Api.Models;
using GoIsland.Api.Services.Auth;
using GoIsland.Api.Services.Security;
using GoIsland.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GoIsland.Api.Tests.Integration;

public class PasswordIntegrationTests : PostgresIntegrationTestBase
{
    [Fact]
    public async Task ChangePassword_WithCurrentPassword_UpdatesRealUserCredentials()
    {
        var authService = GetRequiredService<IAuthService>();
        var email = $"change-password-{Guid.NewGuid():N}@goisland.test";
        const string currentPassword = "Password123";
        const string newPassword = "NuevaPassword456";
        var registration = await RegisterAsync(authService, email, currentPassword);

        var result = await authService.ChangePasswordAsync(
            registration.User.Id,
            new ChangePasswordRequest
            {
                CurrentPassword = currentPassword,
                NewPassword = newPassword,
                ConfirmPassword = newPassword
            });

        Assert.Equal(ChangePasswordStatus.Success, result);
        Assert.Null(await authService.LoginAsync(new LoginRequest
        {
            Email = email,
            Password = currentPassword
        }));
        Assert.NotNull(await authService.LoginAsync(new LoginRequest
        {
            Email = email,
            Password = newPassword
        }));
    }

    [Fact]
    public async Task ChangePassword_WithIncorrectCurrentPassword_DoesNotModifyUser()
    {
        var authService = GetRequiredService<IAuthService>();
        var email = $"wrong-password-{Guid.NewGuid():N}@goisland.test";
        const string currentPassword = "Password123";
        var registration = await RegisterAsync(authService, email, currentPassword);

        var result = await authService.ChangePasswordAsync(
            registration.User.Id,
            new ChangePasswordRequest
            {
                CurrentPassword = "Incorrecta123",
                NewPassword = "NuevaPassword456",
                ConfirmPassword = "NuevaPassword456"
            });

        Assert.Equal(ChangePasswordStatus.InvalidCurrentPassword, result);
        Assert.NotNull(await authService.LoginAsync(new LoginRequest
        {
            Email = email,
            Password = currentPassword
        }));
    }

    [Fact]
    public async Task ResetPassword_WithValidToken_ChangesPasswordAndConsumesTokenOnce()
    {
        var authService = GetRequiredService<IAuthService>();
        var tokenGenerator = GetRequiredService<IPasswordResetTokenGenerator>();
        var unitOfWork = GetRequiredService<IUnitOfWork>();
        var email = $"reset-password-{Guid.NewGuid():N}@goisland.test";
        const string oldPassword = "Password123";
        const string newPassword = "Restaurada789";
        var registration = await RegisterAsync(authService, email, oldPassword);
        var generatedToken = tokenGenerator.CreateToken();
        var storedToken = new PasswordResetToken
        {
            UserId = registration.User.Id,
            TokenHash = generatedToken.TokenHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };
        await unitOfWork.PasswordResetTokens.AddAsync(storedToken);
        await unitOfWork.CommitAsync();

        var result = await authService.ResetPasswordAsync(new ResetPasswordRequest
        {
            Token = generatedToken.Token,
            NewPassword = newPassword,
            ConfirmPassword = newPassword
        });
        var reusedResult = await authService.ResetPasswordAsync(new ResetPasswordRequest
        {
            Token = generatedToken.Token,
            NewPassword = "OtraPassword999",
            ConfirmPassword = "OtraPassword999"
        });

        Assert.Equal(ResetPasswordStatus.Success, result);
        Assert.Equal(ResetPasswordStatus.InvalidOrExpiredToken, reusedResult);
        Assert.Null(await authService.LoginAsync(new LoginRequest
        {
            Email = email,
            Password = oldPassword
        }));
        Assert.NotNull(await authService.LoginAsync(new LoginRequest
        {
            Email = email,
            Password = newPassword
        }));

        Context.ChangeTracker.Clear();
        var persistedToken = await Context.PasswordResetTokens.AsNoTracking()
            .SingleAsync(token => token.Id == storedToken.Id);
        Assert.NotNull(persistedToken.UsedAt);
    }

    [Fact]
    public async Task ForgotPassword_ForUnknownEmail_ReturnsUniformAcceptedStatus()
    {
        var authService = GetRequiredService<IAuthService>();

        var result = await authService.RequestPasswordResetAsync(new ForgotPasswordRequest
        {
            Email = $"smtp-{Guid.NewGuid():N}@goisland.test"
        });

        Assert.Equal(RequestPasswordResetStatus.Accepted, result);
    }

    private static async Task<AuthResponse> RegisterAsync(
        IAuthService authService,
        string email,
        string password)
    {
        var result = await authService.RegisterAsync(new RegisterRequest
        {
            FullName = "Usuario Contrasena",
            Email = email,
            Password = password,
            Role = UserRoles.Tourist
        });

        return Assert.IsType<AuthResponse>(result);
    }
}
