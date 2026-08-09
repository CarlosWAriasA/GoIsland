using System.Net.Http.Json;

namespace GoIsland.Api.Services.Email;

/// <summary>
/// Envia correo con la API HTTPS de Brevo. A diferencia de Resend, Brevo admite verificar
/// una direccion suelta como remitente, sin necesidad de un dominio propio, y aun asi
/// entrega a cualquier destinatario. La autenticacion va en la cabecera api-key, no en
/// Authorization.
/// </summary>
public class BrevoEmailSender : IEmailSender
{
    private const string SendEndpoint = "v3/smtp/email";
    private const string ApiKeyHeader = "api-key";

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public BrevoEmailSender(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_configuration["Brevo:ApiKey"])
        && !string.IsNullOrWhiteSpace(_configuration["Email:FromEmail"])
        && !string.IsNullOrWhiteSpace(_configuration["Email:ResetPasswordUrl"]);

    public async Task SendPasswordResetAsync(string email, string fullName, string resetToken)
    {
        var resetPasswordUrl = GetRequiredSetting("Email:ResetPasswordUrl");
        var content = PasswordResetEmailContentBuilder.Build(resetPasswordUrl, fullName, resetToken);
        await SendAsync(email, fullName, content.Subject, content.HtmlBody, content.TextBody);
    }

    public async Task SendNotificationAsync(string email, string fullName, string subject, string message, string? actionUrl)
    {
        var content = NotificationEmailContent.Build(fullName, subject, message, actionUrl);
        await SendAsync(email, fullName, content.Subject, content.HtmlBody, content.TextBody);
    }

    private async Task SendAsync(string email, string fullName, string subject, string htmlBody, string textBody)
    {
        var apiKey = GetRequiredSetting("Brevo:ApiKey");
        var fromEmail = GetRequiredSetting("Email:FromEmail");
        var fromName = _configuration["Email:FromName"] ?? "GoIsland";

        using var request = new HttpRequestMessage(HttpMethod.Post, SendEndpoint)
        {
            Content = JsonContent.Create(new
            {
                sender = new { name = fromName, email = fromEmail },
                to = new[] { new { email, name = fullName } },
                subject,
                htmlContent = htmlBody,
                textContent = textBody
            })
        };
        request.Headers.Add(ApiKeyHeader, apiKey);

        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private string GetRequiredSetting(string key)
    {
        return _configuration[key]
            ?? throw new InvalidOperationException($"{key} no esta configurado.");
    }
}
