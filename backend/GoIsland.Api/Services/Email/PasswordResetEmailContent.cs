using System.Text.Encodings.Web;

namespace GoIsland.Api.Services.Email;

public record PasswordResetEmailContent(string Subject, string HtmlBody);

public static class PasswordResetEmailContentBuilder
{
    public static PasswordResetEmailContent Build(
        string resetPasswordUrl,
        string fullName,
        string resetToken)
    {
        var separator = resetPasswordUrl.Contains('?') ? '&' : '?';
        var resetLink = $"{resetPasswordUrl}{separator}token={Uri.EscapeDataString(resetToken)}";
        var safeName = HtmlEncoder.Default.Encode(fullName);
        var safeLink = HtmlEncoder.Default.Encode(resetLink);

        return new PasswordResetEmailContent(
            "Restablece tu contrasena de GoIsland",
            $"<p>Hola {safeName},</p><p>Usa el siguiente enlace para restablecer tu contrasena:</p><p><a href=\"{safeLink}\">Restablecer contrasena</a></p><p>El enlace es de un solo uso y expirara pronto.</p>");
    }
}
