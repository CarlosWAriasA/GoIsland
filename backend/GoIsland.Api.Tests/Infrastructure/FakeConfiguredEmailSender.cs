using GoIsland.Api.Services.Email;

namespace GoIsland.Api.Tests.Infrastructure;

public class FakeConfiguredEmailSender : IEmailSender
{
    public bool IsConfigured => true;

    public Task SendPasswordResetAsync(string email, string fullName, string resetToken)
    {
        return Task.CompletedTask;
    }

    public Task SendNotificationAsync(
        string email,
        string fullName,
        string subject,
        string body,
        string? actionUrl)
    {
        return Task.CompletedTask;
    }
}
