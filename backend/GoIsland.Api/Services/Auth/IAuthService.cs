using GoIsland.Api.DTOs.Auth;

namespace GoIsland.Api.Services.Auth;

public interface IAuthService
{
    Task<AuthResponse?> RegisterAsync(RegisterRequest request);
    Task<AuthResponse?> LoginAsync(LoginRequest request);
    Task<ChangePasswordStatus> ChangePasswordAsync(int userId, ChangePasswordRequest request);
    Task<RequestPasswordResetStatus> RequestPasswordResetAsync(ForgotPasswordRequest request);
    Task<ResetPasswordStatus> ResetPasswordAsync(ResetPasswordRequest request);
}
