using System.ComponentModel.DataAnnotations;
using System.Net;
using GoIsland.Api.DTOs.Common;

namespace GoIsland.Api.DTOs.Notifications;

public sealed class NotificationListRequest : PaginationRequest
{
    public bool UnreadOnly { get; set; }
}

public class NotificationResponse
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ActionUrl { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class NotificationPreferenceResponse
{
    public bool DashboardEnabled { get; set; }
    public bool EmailEnabled { get; set; }
    public bool PushEnabled { get; set; }
}

public class UpdateNotificationPreferenceRequest
{
    public bool DashboardEnabled { get; set; } = true;
    public bool EmailEnabled { get; set; } = true;
    public bool PushEnabled { get; set; } = true;
}

public class RegisterDeviceRequest : IValidatableObject
{
    [Required, StringLength(4096, MinimumLength = 20)]
    public string Endpoint { get; set; } = string.Empty;

    [Required, StringLength(512, MinimumLength = 20)]
    public string P256dh { get; set; } = string.Empty;

    [Required, StringLength(512, MinimumLength = 8)]
    public string Auth { get; set; } = string.Empty;

    public DateTime? ExpirationTime { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out var endpoint)
            || endpoint.Scheme != Uri.UriSchemeHttps
            || IsUnsafeHost(endpoint.Host))
        {
            yield return new ValidationResult(
                "Endpoint debe ser una URL HTTPS publica.",
                [nameof(Endpoint)]);
        }
        if (!IsBase64UrlValue(P256dh, 65))
        {
            yield return new ValidationResult(
                "P256dh debe ser una clave publica P-256 valida.",
                [nameof(P256dh)]);
        }
        if (!IsBase64UrlValue(Auth, 16))
        {
            yield return new ValidationResult(
                "Auth debe ser un secreto Web Push valido.",
                [nameof(Auth)]);
        }
    }

    private static bool IsUnsafeHost(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
        || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
        || host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase)
        || !host.Contains('.')
        || IPAddress.TryParse(host, out _);

    private static bool IsBase64UrlValue(string value, int expectedBytes)
    {
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

public class DeviceResponse
{
    public int Id { get; set; }
    public DateTime? ExpirationTime { get; set; }
    public DateTime LastSeenAt { get; set; }
}

public class WebPushPublicKeyResponse
{
    public string PublicKey { get; set; } = string.Empty;
}
