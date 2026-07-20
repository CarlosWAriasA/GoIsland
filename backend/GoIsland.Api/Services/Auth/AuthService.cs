using GoIsland.Api.DTOs.Auth;
using GoIsland.Api.Data;
using GoIsland.Api.Models;
using GoIsland.Api.Services.Email;
using GoIsland.Api.Services.Security;

namespace GoIsland.Api.Services.Auth;

public class AuthService : IAuthService
{
    private const string GoogleProvider = "Google";
    private const string ExternalLoginOnlyPasswordHash = "EXTERNAL_LOGIN_ONLY";
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IPasswordResetTokenGenerator _resetTokenGenerator;
    private readonly IEmailSender _emailSender;
    private readonly IGoogleIdentityVerifier _googleIdentityVerifier;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

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
        return CreateAuthResponse(user);
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        var user = await _unitOfWork.Users.GetByEmailAsync(request.Email);
        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return null;
        }

        return CreateAuthResponse(user);
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
                : new GoogleAuthResult(GoogleAuthStatus.Success, CreateAuthResponse(linkedUser));
        }

        var user = await _unitOfWork.Users.GetByEmailAsync(identity.Email);
        var isNewUser = user is null;
        if (user is not null && !identity.CanLinkExistingAccountByEmail)
        {
            return new GoogleAuthResult(GoogleAuthStatus.AccountConflict);
        }

        if (user is null)
        {
            user = new User
            {
                FullName = identity.FullName,
                Email = identity.Email,
                PasswordHash = ExternalLoginOnlyPasswordHash,
                Role = UserRoles.Tourist
            };
            await _unitOfWork.Users.AddAsync(user);
        }

        if (!isNewUser)
        {
            var existingProviderLogin = await _unitOfWork.UserExternalLogins.GetByUserAndProviderAsync(
                user.Id,
                GoogleProvider);
            if (existingProviderLogin is not null)
            {
                return new GoogleAuthResult(GoogleAuthStatus.AccountConflict);
            }
        }

        await _unitOfWork.UserExternalLogins.AddAsync(new UserExternalLogin
        {
            UserId = user.Id,
            User = user,
            Provider = GoogleProvider,
            ProviderSubject = identity.Subject
        });
        await _unitOfWork.CommitAsync();

        return new GoogleAuthResult(GoogleAuthStatus.Success, CreateAuthResponse(user));
    }

    public async Task<ChangePasswordStatus> ChangePasswordAsync(int userId, ChangePasswordRequest request)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user is null)
        {
            return ChangePasswordStatus.UserNotFound;
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

    private AuthResponse CreateAuthResponse(User user)
    {
        var token = _jwtTokenService.CreateToken(user);

        return new AuthResponse
        {
            Token = token.Token,
            ExpiresAt = token.ExpiresAt,
            User = ToResponse(user)
        };
    }

    public static UserResponse ToResponse(User user)
    {
        return new UserResponse
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role,
            CreatedAt = user.CreatedAt
        };
    }
}
