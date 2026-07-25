using System.Net;
using System.Net.Mail;

namespace GoIsland.Api.Services.Email;

public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;

    public SmtpEmailSender(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_configuration["Smtp:Host"])
        && !string.IsNullOrWhiteSpace(_configuration["Email:FromEmail"])
        && !string.IsNullOrWhiteSpace(_configuration["Email:ResetPasswordUrl"]);

    public async Task SendPasswordResetAsync(string email, string fullName, string resetToken)
    {
        var host = GetRequiredSetting("Smtp:Host");
        var fromEmail = GetRequiredSetting("Email:FromEmail");
        var resetPasswordUrl = GetRequiredSetting("Email:ResetPasswordUrl");
        var fromName = _configuration["Email:FromName"] ?? "GoIsland";
        var port = _configuration.GetValue<int?>("Smtp:Port") ?? 587;
        var enableSsl = _configuration.GetValue<bool?>("Smtp:EnableSsl") ?? true;
        var username = _configuration["Smtp:Username"];
        var password = _configuration["Smtp:Password"];

        if (string.IsNullOrWhiteSpace(username) != string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Smtp:Username y Smtp:Password deben configurarse juntos.");
        }

        var content = PasswordResetEmailContentBuilder.Build(resetPasswordUrl, fullName, resetToken);

        using var message = new MailMessage
        {
            From = new MailAddress(fromEmail, fromName),
            Subject = content.Subject,
            Body = content.HtmlBody,
            IsBodyHtml = true
        };
        message.To.Add(new MailAddress(email));

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl,
            UseDefaultCredentials = false
        };

        if (!string.IsNullOrWhiteSpace(username))
        {
            client.Credentials = new NetworkCredential(username, password);
        }

        await client.SendMailAsync(message);
    }

    public async Task SendNotificationAsync(string email, string fullName, string subject, string body, string? actionUrl)
    {
        var host = GetRequiredSetting("Smtp:Host");
        var fromEmail = GetRequiredSetting("Email:FromEmail");
        var fromName = _configuration["Email:FromName"] ?? "GoIsland";
        using var message = new MailMessage
        {
            From = new MailAddress(fromEmail, fromName),
            Subject = subject,
            Body = NotificationEmailContent.Build(fullName, body, actionUrl),
            IsBodyHtml = true
        };
        message.To.Add(new MailAddress(email));
        using var client = BuildClient(host);
        await client.SendMailAsync(message);
    }

    private SmtpClient BuildClient(string host)
    {
        var client = new SmtpClient(host, _configuration.GetValue<int?>("Smtp:Port") ?? 587)
        {
            EnableSsl = _configuration.GetValue<bool?>("Smtp:EnableSsl") ?? true,
            UseDefaultCredentials = false
        };
        var username = _configuration["Smtp:Username"];
        var password = _configuration["Smtp:Password"];
        if (!string.IsNullOrWhiteSpace(username)) client.Credentials = new NetworkCredential(username, password);
        return client;
    }

    private string GetRequiredSetting(string key)
    {
        return _configuration[key]
            ?? throw new InvalidOperationException($"{key} no esta configurado.");
    }
}
