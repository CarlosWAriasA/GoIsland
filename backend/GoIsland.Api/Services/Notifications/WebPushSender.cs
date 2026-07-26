using System.Net;
using System.Text.Json;
using WebPush;

namespace GoIsland.Api.Services.Notifications;

public sealed record WebPushSubscriptionData(string Endpoint, string P256dh, string Auth);

public interface IPushNotificationSender
{
    bool IsConfigured { get; }
    string? PublicKey { get; }
    Task SendAsync(WebPushSubscriptionData subscription, string title, string message,
        string? actionUrl, CancellationToken cancellationToken = default);
}

public sealed class PushSubscriptionExpiredException : Exception
{
    public PushSubscriptionExpiredException(Exception innerException)
        : base("La suscripcion Web Push ya no es valida.", innerException)
    {
    }
}

public sealed class WebPushSender : IPushNotificationSender, IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly WebPushClient _client = new();

    public WebPushSender(IConfiguration configuration) => _configuration = configuration;

    public string? PublicKey => Normalize(_configuration["WebPush:PublicKey"]);

    public bool IsConfigured
    {
        get
        {
            var privateKey = Normalize(_configuration["WebPush:PrivateKey"]);
            var subject = Normalize(_configuration["WebPush:Subject"]);
            return IsValidBase64UrlKey(PublicKey, 65)
                && IsValidBase64UrlKey(privateKey, 32)
                && IsValidSubject(subject);
        }
    }

    public async Task SendAsync(WebPushSubscriptionData subscription, string title, string message,
        string? actionUrl, CancellationToken cancellationToken = default)
    {
        var publicKey = PublicKey
            ?? throw new InvalidOperationException("WebPush:PublicKey no esta configurado.");
        var privateKey = Normalize(_configuration["WebPush:PrivateKey"])
            ?? throw new InvalidOperationException("WebPush:PrivateKey no esta configurado.");
        var subject = Normalize(_configuration["WebPush:Subject"])
            ?? throw new InvalidOperationException("WebPush:Subject no esta configurado.");
        var payload = JsonSerializer.Serialize(new
        {
            title,
            body = message,
            actionUrl
        });

        try
        {
            await _client.SendNotificationAsync(
                new PushSubscription(subscription.Endpoint, subscription.P256dh, subscription.Auth),
                payload,
                new VapidDetails(subject, publicKey, privateKey),
                cancellationToken);
        }
        catch (WebPushException exception) when (
            exception.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            throw new PushSubscriptionExpiredException(exception);
        }
    }

    public void Dispose() => _client.Dispose();

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsValidSubject(string? subject) =>
        Uri.TryCreate(subject, UriKind.Absolute, out var uri)
        && uri.Scheme is "mailto" or "https";

    private static bool IsValidBase64UrlKey(string? value, int expectedBytes)
    {
        if (value is null) return false;
        try
        {
            var padded = value.Replace('-', '+').Replace('_', '/')
                .PadRight(value.Length + (4 - value.Length % 4) % 4, '=');
            return Convert.FromBase64String(padded).Length == expectedBytes;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
