namespace GoIsland.Api.Services.Email;

public interface IEmailSender
{
    bool IsConfigured { get; }
    Task SendPasswordResetAsync(string email, string fullName, string resetToken);
}
