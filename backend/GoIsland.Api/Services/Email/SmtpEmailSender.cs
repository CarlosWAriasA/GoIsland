using System.Net;
using System.Net.Mail;
using System.Text.Encodings.Web;

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
        && !string.IsNullOrWhiteSpace(_configuration["Smtp:FromEmail"])
        && !string.IsNullOrWhiteSpace(_configuration["Smtp:ResetPasswordUrl"]);

    public async Task SendPasswordResetAsync(string email, string fullName, string resetToken)
    {
        var host = GetRequiredSetting("Smtp:Host");
        var fromEmail = GetRequiredSetting("Smtp:FromEmail");
        var resetPasswordUrl = GetRequiredSetting("Smtp:ResetPasswordUrl");
        var fromName = _configuration["Smtp:FromName"] ?? "GoIsland";
        var port = _configuration.GetValue<int?>("Smtp:Port") ?? 587;
        var enableSsl = _configuration.GetValue<bool?>("Smtp:EnableSsl") ?? true;
        var username = _configuration["Smtp:Username"];
        var password = _configuration["Smtp:Password"];

        if (string.IsNullOrWhiteSpace(username) != string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Smtp:Username y Smtp:Password deben configurarse juntos.");
        }

        var separator = resetPasswordUrl.Contains('?') ? '&' : '?';
        var resetLink = $"{resetPasswordUrl}{separator}token={Uri.EscapeDataString(resetToken)}";
        var safeName = HtmlEncoder.Default.Encode(fullName);
        var safeLink = HtmlEncoder.Default.Encode(resetLink);

        using var message = new MailMessage
        {
            From = new MailAddress(fromEmail, fromName),
            Subject = "Restablece tu contrasena de GoIsland",
            Body = $"<p>Hola {safeName},</p><p>Usa el siguiente enlace para restablecer tu contrasena:</p><p><a href=\"{safeLink}\">Restablecer contrasena</a></p><p>El enlace es de un solo uso y expirara pronto.</p>",
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

    private string GetRequiredSetting(string key)
    {
        return _configuration[key]
            ?? throw new InvalidOperationException($"{key} no esta configurado.");
    }
}
