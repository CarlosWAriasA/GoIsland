using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;

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

        using var message = BuildMessage(fromEmail, fromName, email, content);

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
        var content = NotificationEmailContent.Build(fullName, subject, body, actionUrl);
        using var message = BuildMessage(fromEmail, fromName, email, content);
        using var client = BuildClient(host);
        await client.SendMailAsync(message);
    }

    private static MailMessage BuildMessage(
        string fromEmail,
        string fromName,
        string email,
        EmailContent content)
    {
        var message = new MailMessage
        {
            From = new MailAddress(fromEmail, fromName),
            Subject = content.Subject,
            SubjectEncoding = Encoding.UTF8,
            Body = content.HtmlBody,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = true
        };
        message.To.Add(new MailAddress(email));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            content.TextBody, Encoding.UTF8, MediaTypeNames.Text.Plain));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            content.HtmlBody, Encoding.UTF8, MediaTypeNames.Text.Html));
        return message;
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
