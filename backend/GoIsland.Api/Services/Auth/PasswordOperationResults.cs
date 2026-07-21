namespace GoIsland.Api.Services.Auth;

public enum ChangePasswordStatus
{
    Success,
    UserNotFound,
    InvalidCurrentPassword,
    NewPasswordMatchesCurrent
}

public enum RequestPasswordResetStatus
{
    Accepted,
    EmailDeliveryNotConfigured
}

public enum ResetPasswordStatus
{
    Success,
    InvalidOrExpiredToken,
    NewPasswordMatchesCurrent
}
