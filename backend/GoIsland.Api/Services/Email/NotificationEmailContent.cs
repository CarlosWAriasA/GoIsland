using System.Net;

namespace GoIsland.Api.Services.Email;

public static class NotificationEmailContent
{
    public static EmailContent Build(string fullName, string subject, string message, string? actionUrl)
    {
        var safeMessage = WebUtility.HtmlEncode(message)
            .Replace("\r\n", "<br>", StringComparison.Ordinal)
            .Replace("\n", "<br>", StringComparison.Ordinal);
        var html = EmailTemplate.Render(
            message,
            "Novedades de tu cuenta",
            subject,
            $"Hola, {fullName}",
            $"<p style=\"margin:0;\">{safeMessage}</p>",
            string.IsNullOrWhiteSpace(actionUrl) ? null : "Ver en GoIsland",
            actionUrl);
        var actionText = string.IsNullOrWhiteSpace(actionUrl)
            ? string.Empty
            : $"\n\nVer en GoIsland:\n{actionUrl}";
        var text = $"Hola, {fullName}\n\n{message}{actionText}\n\nEquipo GoIsland";
        return new EmailContent(subject, html, text);
    }
}
