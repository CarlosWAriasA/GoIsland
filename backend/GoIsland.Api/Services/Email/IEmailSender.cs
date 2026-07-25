namespace GoIsland.Api.Services.Email;

public interface IEmailSender
{
    bool IsConfigured { get; }
    Task SendPasswordResetAsync(string email, string fullName, string resetToken);
    Task SendNotificationAsync(string email, string fullName, string subject, string message, string? actionUrl);
}
