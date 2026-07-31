using GoIsland.Api.DTOs.Auth;
using GoIsland.Api.Data;
using GoIsland.Api.Models;
using GoIsland.Api.Services.Email;
using GoIsland.Api.Services.Security;
using Microsoft.EntityFrameworkCore;

namespace GoIsland.Api.Services.Auth;

public class AuthService : IAuthService
{
    private const string GoogleProvider = "Google";
    private const string PasswordAuthenticationMethod = "Password";
    private const string GoogleAuthenticationMethod = "Google";
    private const string ExternalLoginOnlyPasswordHash = "EXTERNAL_LOGIN_ONLY";
    private const int LockoutThreshold = 5;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IPasswordResetTokenGenerator _resetTokenGenerator;
    private readonly IEmailSender _emailSender;
    private readonly IGoogleIdentityVerifier _googleIdentityVerifier;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;
    private readonly string _dummyPasswordHash;

    public AuthService(
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IPasswordResetTokenGenerator resetTokenGenerator,
        IEmailSender emailSender,
        IGoogleIdentityVerifier googleIdentityVerifier,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _resetTokenGenerator = resetTokenGenerator;
        _emailSender = emailSender;
        _googleIdentityVerifier = googleIdentityVerifier;
        _configuration = configuration;
        _logger = logger;
        _dummyPasswordHash = _passwordHasher.Hash("GoIslandDummyPassword2026");
    }

    public async Task<AuthResponse?> RegisterAsync(RegisterRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await _unitOfWork.Users.GetByEmailAsync(email) is not null)
        {
            return null;
        }

        var role = string.IsNullOrWhiteSpace(request.Role) ? UserRoles.Tourist : request.Role.Trim();
        if (!UserRoles.PublicRegistration.Contains(role))
        {
            role = UserRoles.Tourist;
        }

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            Role = role
        };

        await _unitOfWork.Users.AddAsync(user);
        await _unitOfWork.CommitAsync();
        return CreateAuthResponse(user, PasswordAuthenticationMethod);
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        var user = await _unitOfWork.Users.GetByEmailAsync(request.Email);
        if (user is null)
        {
            _passwordHasher.Verify(request.Password, _dummyPasswordHash);
            return null;
        }

        var now = DateTime.UtcNow;
        var passwordIsValid = _passwordHasher.Verify(request.Password, user.PasswordHash);
        if (user.LockoutEnd > now)
        {
            return null;
        }

        if (!passwordIsValid)
        {
            await UpdateLoginProtectionAsync(user, currentUser =>
            {
                currentUser.FailedLoginAttempts++;
                if (currentUser.FailedLoginAttempts >= LockoutThreshold)
                {
                    currentUser.LockoutEnd = now.Add(GetLockoutDuration(currentUser.FailedLoginAttempts));
                }
            });
            return null;
        }

        if (user.FailedLoginAttempts > 0 || user.LockoutEnd is not null)
        {
            await UpdateLoginProtectionAsync(user, currentUser =>
            {
                currentUser.FailedLoginAttempts = 0;
                currentUser.LockoutEnd = null;
            });
        }

        return CreateAuthResponse(user, PasswordAuthenticationMethod);
    }

    public async Task<GoogleAuthResult> AuthenticateWithGoogleAsync(GoogleAuthRequest request)
    {
        if (!_googleIdentityVerifier.IsConfigured)
        {
            return new GoogleAuthResult(GoogleAuthStatus.NotConfigured);
        }

        var identity = await _googleIdentityVerifier.VerifyAsync(request.Credential);
        if (identity is null)
        {
            return new GoogleAuthResult(GoogleAuthStatus.InvalidCredential);
        }

        var externalLogin = await _unitOfWork.UserExternalLogins.GetByProviderSubjectAsync(
            GoogleProvider,
            identity.Subject);

        if (externalLogin is not null)
        {
            var linkedUser = await _unitOfWork.Users.GetByIdAsync(externalLogin.UserId);
            return linkedUser is null
                ? new GoogleAuthResult(GoogleAuthStatus.AccountConflict)
                : new GoogleAuthResult(
                    GoogleAuthStatus.Success,
                    CreateAuthResponse(linkedUser, GoogleAuthenticationMethod));
        }

        var user = await _unitOfWork.Users.GetByEmailAsync(identity.Email);
        if (user is not null)
        {
            var existingProviderLogin = await _unitOfWork.UserExternalLogins.GetByUserAndProviderAsync(
                user.Id,
                GoogleProvider);
            return new GoogleAuthResult(existingProviderLogin is null
                ? GoogleAuthStatus.LocalAccountExists
                : GoogleAuthStatus.AccountConflict);
        }

        user = new User
        {
            FullName = identity.FullName,
            Email = identity.Email,
            PasswordHash = ExternalLoginOnlyPasswordHash,
            Role = UserRoles.Tourist
        };
        await _unitOfWork.Users.AddAsync(user);

        await _unitOfWork.UserExternalLogins.AddAsync(new UserExternalLogin
        {
            UserId = user.Id,
            User = user,
            Provider = GoogleProvider,
            ProviderSubject = identity.Subject
        });
        await _unitOfWork.CommitAsync();

        return new GoogleAuthResult(
            GoogleAuthStatus.Success,
            CreateAuthResponse(user, GoogleAuthenticationMethod));
    }

    public async Task<ChangePasswordStatus> ChangePasswordAsync(int userId, ChangePasswordRequest request)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user is null)
        {
            return ChangePasswordStatus.UserNotFound;
        }

        if (!HasLocalPassword(user))
        {
            return ChangePasswordStatus.PasswordNotAvailable;
        }

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return ChangePasswordStatus.InvalidCurrentPassword;
        }

        if (_passwordHasher.Verify(request.NewPassword, user.PasswordHash))
        {
            return ChangePasswordStatus.NewPasswordMatchesCurrent;
        }

        var now = DateTime.UtcNow;
        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        await _unitOfWork.Users.UpdateAsync(user);
        await _unitOfWork.PasswordResetTokens.InvalidateActiveForUserAsync(user.Id, now);
        await _unitOfWork.CommitAsync();

        return ChangePasswordStatus.Success;
    }

    public async Task<RequestPasswordResetStatus> RequestPasswordResetAsync(ForgotPasswordRequest request)
    {
        if (!_emailSender.IsConfigured)
        {
            return RequestPasswordResetStatus.EmailDeliveryNotConfigured;
        }

        var user = await _unitOfWork.Users.GetByEmailAsync(request.Email);
        if (user is null)
        {
            return RequestPasswordResetStatus.Accepted;
        }

        var now = DateTime.UtcNow;
        var lifetimeMinutes = Math.Clamp(
            _configuration.GetValue<int?>("PasswordReset:TokenLifetimeMinutes") ?? 30,
            5,
            1440);
        var generatedToken = _resetTokenGenerator.CreateToken();
        var resetToken = new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = generatedToken.TokenHash,
            ExpiresAt = now.AddMinutes(lifetimeMinutes),
            CreatedAt = now
        };

        await _unitOfWork.PasswordResetTokens.InvalidateActiveForUserAsync(user.Id, now);
        await _unitOfWork.PasswordResetTokens.AddAsync(resetToken);
        await _unitOfWork.CommitAsync();

        try
        {
            await _emailSender.SendPasswordResetAsync(user.Email, user.FullName, generatedToken.Token);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "No se pudo enviar el correo de recuperacion para el usuario {UserId}.",
                user.Id);
        }

        return RequestPasswordResetStatus.Accepted;
    }

    public async Task<ResetPasswordStatus> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var now = DateTime.UtcNow;
        var tokenHash = _resetTokenGenerator.HashToken(request.Token);
        var resetToken = await _unitOfWork.PasswordResetTokens.GetValidByHashAsync(tokenHash, now);
        if (resetToken is null)
        {
            return ResetPasswordStatus.InvalidOrExpiredToken;
        }

        var user = await _unitOfWork.Users.GetByIdAsync(resetToken.UserId);
        if (user is null)
        {
            return ResetPasswordStatus.InvalidOrExpiredToken;
        }

        if (_passwordHasher.Verify(request.NewPassword, user.PasswordHash))
        {
            return ResetPasswordStatus.NewPasswordMatchesCurrent;
        }

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        resetToken.UsedAt = now;

        await _unitOfWork.Users.UpdateAsync(user);
        await _unitOfWork.PasswordResetTokens.InvalidateActiveForUserAsync(user.Id, now);
        await _unitOfWork.PasswordResetTokens.UpdateAsync(resetToken);
        await _unitOfWork.CommitAsync();

        return ResetPasswordStatus.Success;
    }

    private AuthResponse CreateAuthResponse(User user, string authenticationMethod)
    {
        var token = _jwtTokenService.CreateToken(user);

        return new AuthResponse
        {
            Token = token.Token,
            ExpiresAt = token.ExpiresAt,
            AuthenticationMethod = authenticationMethod,
            User = ToResponse(user)
        };
    }

    private static TimeSpan GetLockoutDuration(int failedAttempts)
    {
        return failedAttempts switch
        {
            LockoutThreshold => TimeSpan.FromMinutes(1),
            LockoutThreshold + 1 => TimeSpan.FromMinutes(5),
            LockoutThreshold + 2 => TimeSpan.FromMinutes(15),
            _ => TimeSpan.FromHours(1)
        };
    }

    private async Task UpdateLoginProtectionAsync(User user, Action<User> update)
    {
        const int maximumAttempts = 3;
        for (var attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            update(user);
            await _unitOfWork.Users.UpdateAsync(user);

            try
            {
                await _unitOfWork.CommitAsync();
                return;
            }
            catch (DbUpdateConcurrencyException) when (attempt < maximumAttempts)
            {
                await _unitOfWork.Users.ReloadAsync(user);
            }
        }
    }

    public static UserResponse ToResponse(User user)
    {
        return new UserResponse
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role,
            HasPassword = HasLocalPassword(user),
            CreatedAt = user.CreatedAt
        };
    }

    private static bool HasLocalPassword(User user) =>
        !string.Equals(user.PasswordHash, ExternalLoginOnlyPasswordHash, StringComparison.Ordinal);
}
