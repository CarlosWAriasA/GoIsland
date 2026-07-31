using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace GoIsland.Api.Services.Email;

public class ResendEmailSender : IEmailSender
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public ResendEmailSender(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_configuration["Resend:ApiKey"])
        && !string.IsNullOrWhiteSpace(_configuration["Email:FromEmail"])
        && !string.IsNullOrWhiteSpace(_configuration["Email:ResetPasswordUrl"]);

    public async Task SendPasswordResetAsync(string email, string fullName, string resetToken)
    {
        var apiKey = GetRequiredSetting("Resend:ApiKey");
        var fromEmail = GetRequiredSetting("Email:FromEmail");
        var fromName = _configuration["Email:FromName"] ?? "GoIsland";
        var resetPasswordUrl = GetRequiredSetting("Email:ResetPasswordUrl");
        var content = PasswordResetEmailContentBuilder.Build(resetPasswordUrl, fullName, resetToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, "emails")
        {
            Content = JsonContent.Create(new
            {
                from = $"{fromName} <{fromEmail}>",
                to = new[] { email },
                subject = content.Subject,
                html = content.HtmlBody,
                text = content.TextBody
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    public async Task SendNotificationAsync(string email, string fullName, string subject, string message, string? actionUrl)
    {
        var apiKey = GetRequiredSetting("Resend:ApiKey");
        var fromEmail = GetRequiredSetting("Email:FromEmail");
        var fromName = _configuration["Email:FromName"] ?? "GoIsland";
        var content = NotificationEmailContent.Build(fullName, subject, message, actionUrl);
        using var request = new HttpRequestMessage(HttpMethod.Post, "emails")
        {
            Content = JsonContent.Create(new
            {
                from = $"{fromName} <{fromEmail}>",
                to = new[] { email },
                subject = content.Subject,
                html = content.HtmlBody,
                text = content.TextBody
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private string GetRequiredSetting(string key)
    {
        return _configuration[key]
            ?? throw new InvalidOperationException($"{key} no esta configurado.");
    }
}
