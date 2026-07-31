namespace GoIsland.Api.Services.Email;

public static class PasswordResetEmailContentBuilder
{
    public static EmailContent Build(
        string resetPasswordUrl,
        string fullName,
        string resetToken)
    {
        var separator = resetPasswordUrl.Contains('?') ? '&' : '?';
        var resetLink = $"{resetPasswordUrl}{separator}token={Uri.EscapeDataString(resetToken)}";
        const string subject = "Restablece tu contraseña de GoIsland";
        var html = EmailTemplate.Render(
            "Recupera el acceso a tu cuenta de GoIsland.",
            "Seguridad de tu cuenta",
            "Restablece tu contraseña",
            $"Hola, {fullName}",
            """
            <p style="margin:0;">Recibimos una solicitud para cambiar la contraseña de tu cuenta. Utiliza el botón para crear una nueva.</p>
            """,
            "Crear nueva contraseña",
            resetLink,
            "Este enlace es personal, de un solo uso y expirará pronto. Si no solicitaste este cambio, puedes ignorar este mensaje.");
        var text = $"""
            Hola, {fullName}

            Recibimos una solicitud para cambiar la contraseña de tu cuenta de GoIsland.

            Crear nueva contraseña:
            {resetLink}

            Este enlace es personal, de un solo uso y expirará pronto. Si no solicitaste este cambio, puedes ignorar este mensaje.

            Equipo GoIsland
            """;

        return new EmailContent(subject, html, text);
    }
}
