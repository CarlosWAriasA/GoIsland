using System.Net;
using System.Text.Json;
using GoIsland.Api.Services.Email;
using Microsoft.Extensions.Configuration;

namespace GoIsland.Api.Tests.Integration;

public class EmailSenderTests
{
    [Fact]
    public async Task ResendEmailSender_SendsExpectedApiRequest()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Resend:ApiKey"] = "re_test_key",
            ["Email:FromEmail"] = "no-reply@goisland.test",
            ["Email:FromName"] = "GoIsland",
            ["Email:ResetPasswordUrl"] = "http://localhost:5173/reset-password"
        });
        var handler = new RecordingHttpMessageHandler();
        var sender = new ResendEmailSender(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.com/") },
            configuration);

        await sender.SendPasswordResetAsync(
            "usuario@goisland.test",
            "Usuario <Seguro>",
            "token con espacios");

        Assert.True(sender.IsConfigured);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("https://api.resend.com/emails", handler.RequestUri?.ToString());
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("re_test_key", handler.AuthorizationParameter);
        using var payload = JsonDocument.Parse(handler.Content);
        var html = payload.RootElement.GetProperty("html").GetString();
        var text = payload.RootElement.GetProperty("text").GetString();
        Assert.Equal("usuario@goisland.test", payload.RootElement.GetProperty("to")[0].GetString());
        Assert.Contains("token%20con%20espacios", html);
        Assert.Contains("Usuario &lt;Seguro&gt;", html);
        Assert.Contains("GoIsland", html);
        Assert.Contains(WebUtility.HtmlEncode("Crear nueva contraseña"), html);
        Assert.Contains("token%20con%20espacios", text);
    }

    [Fact]
    public async Task BrevoEmailSender_SendsExpectedApiRequest()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Brevo:ApiKey"] = "xkeysib-test-key",
            ["Email:FromEmail"] = "no-reply@goisland.test",
            ["Email:FromName"] = "GoIsland",
            ["Email:ResetPasswordUrl"] = "http://localhost:5173/reset-password"
        });
        var handler = new RecordingHttpMessageHandler();
        var sender = new BrevoEmailSender(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.brevo.com/") },
            configuration);

        await sender.SendPasswordResetAsync(
            "usuario@goisland.test",
            "Usuario <Seguro>",
            "token con espacios");

        Assert.True(sender.IsConfigured);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("https://api.brevo.com/v3/smtp/email", handler.RequestUri?.ToString());
        // Brevo autentica con la cabecera api-key, no con Authorization.
        Assert.Equal("xkeysib-test-key", handler.ApiKeyHeader);
        Assert.Null(handler.AuthorizationScheme);

        using var payload = JsonDocument.Parse(handler.Content);
        var root = payload.RootElement;
        Assert.Equal("no-reply@goisland.test", root.GetProperty("sender").GetProperty("email").GetString());
        Assert.Equal("GoIsland", root.GetProperty("sender").GetProperty("name").GetString());
        Assert.Equal("usuario@goisland.test", root.GetProperty("to")[0].GetProperty("email").GetString());

        var html = root.GetProperty("htmlContent").GetString();
        var text = root.GetProperty("textContent").GetString();
        Assert.Contains("token%20con%20espacios", html);
        Assert.Contains("Usuario &lt;Seguro&gt;", html);
        Assert.Contains(WebUtility.HtmlEncode("Crear nueva contraseña"), html);
        Assert.Contains("token%20con%20espacios", text);
    }

    [Fact]
    public void NotificationEmailContent_UsesBrandedTemplateAndEncodesUserContent()
    {
        var content = NotificationEmailContent.Build(
            "Turista <Prueba>",
            "Reserva confirmada",
            "Tu reserva <especial> está lista.",
            "https://goisland.test/reservations/42?source=email");

        Assert.Equal("Reserva confirmada", content.Subject);
        Assert.Contains("Go<span", content.HtmlBody);
        Assert.Contains("Turista &lt;Prueba&gt;", content.HtmlBody);
        Assert.Contains(WebUtility.HtmlEncode("Tu reserva <especial> está lista."), content.HtmlBody);
        Assert.Contains("Ver en GoIsland", content.HtmlBody);
        Assert.DoesNotContain("<especial>", content.HtmlBody);
        Assert.Contains("https://goisland.test/reservations/42?source=email", content.TextBody);
    }

    [Fact]
    public void Senders_RequireTheirProviderSpecificSettings()
    {
        var commonSettings = new Dictionary<string, string?>
        {
            ["Email:FromEmail"] = "no-reply@goisland.test",
            ["Email:ResetPasswordUrl"] = "http://localhost:5173/reset-password"
        };
        var smtpSettings = new Dictionary<string, string?>(commonSettings)
        {
            ["Smtp:Host"] = "smtp.goisland.test"
        };
        var resendSettings = new Dictionary<string, string?>(commonSettings)
        {
            ["Resend:ApiKey"] = "re_test_key"
        };
        var brevoSettings = new Dictionary<string, string?>(commonSettings)
        {
            ["Brevo:ApiKey"] = "xkeysib-test-key"
        };

        Assert.True(new SmtpEmailSender(BuildConfiguration(smtpSettings)).IsConfigured);
        Assert.True(new ResendEmailSender(new HttpClient(), BuildConfiguration(resendSettings)).IsConfigured);
        Assert.True(new BrevoEmailSender(new HttpClient(), BuildConfiguration(brevoSettings)).IsConfigured);
        Assert.False(new SmtpEmailSender(BuildConfiguration(commonSettings)).IsConfigured);
        Assert.False(new ResendEmailSender(new HttpClient(), BuildConfiguration(commonSettings)).IsConfigured);
        Assert.False(new BrevoEmailSender(new HttpClient(), BuildConfiguration(commonSettings)).IsConfigured);
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }
        public string? ApiKeyHeader { get; private set; }
        public string Content { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            ApiKeyHeader = request.Headers.TryGetValues("api-key", out var apiKeyValues)
                ? apiKeyValues.FirstOrDefault()
                : null;
            Content = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"id\":\"email-id\"}")
            };
        }
    }
}
