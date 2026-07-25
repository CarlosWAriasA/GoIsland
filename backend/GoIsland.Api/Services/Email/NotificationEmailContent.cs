using System.Net;

namespace GoIsland.Api.Services.Email;

public static class NotificationEmailContent
{
    public static string Build(string fullName, string message, string? actionUrl)
    {
        var safeName = WebUtility.HtmlEncode(fullName);
        var safeMessage = WebUtility.HtmlEncode(message);
        var action = string.IsNullOrWhiteSpace(actionUrl)
            ? string.Empty
            : $"<p><a href=\"{WebUtility.HtmlEncode(actionUrl)}\">Ver en GoIsland</a></p>";
        return $"<p>Hola, {safeName}.</p><p>{safeMessage}</p>{action}<p>Equipo GoIsland</p>";
    }
}
