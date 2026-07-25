using System.Net.Http.Headers;
using System.Net.Http.Json;
using Google.Apis.Auth.OAuth2;
using System.Text.Json;

namespace GoIsland.Api.Services.Notifications;

public interface IPushNotificationSender
{
    bool IsConfigured { get; }
    Task SendAsync(string token, string title, string message, string? actionUrl);
}

public class FirebasePushSender : IPushNotificationSender
{
    private readonly HttpClient _client;
    private readonly IConfiguration _configuration;

    public FirebasePushSender(HttpClient client, IConfiguration configuration)
    {
        _client = client;
        _configuration = configuration;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_configuration["Firebase:ProjectId"])
        && !string.IsNullOrWhiteSpace(_configuration["Firebase:ServiceAccountJson"]);

    public async Task SendAsync(string token, string title, string message, string? actionUrl)
    {
        var projectId = _configuration["Firebase:ProjectId"]
            ?? throw new InvalidOperationException("Firebase:ProjectId no esta configurado.");
        var json = _configuration["Firebase:ServiceAccountJson"]
            ?? throw new InvalidOperationException("Firebase:ServiceAccountJson no esta configurado.");
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var clientEmail = root.GetProperty("client_email").GetString()
            ?? throw new InvalidOperationException("Firebase:ServiceAccountJson no contiene client_email.");
        var privateKey = root.GetProperty("private_key").GetString()
            ?? throw new InvalidOperationException("Firebase:ServiceAccountJson no contiene private_key.");
        var credential = new ServiceAccountCredential(
            new ServiceAccountCredential.Initializer(clientEmail)
            {
                Scopes = ["https://www.googleapis.com/auth/firebase.messaging"]
            }.FromPrivateKey(privateKey));
        var accessToken = await credential.GetAccessTokenForRequestAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://fcm.googleapis.com/v1/projects/{Uri.EscapeDataString(projectId)}/messages:send")
        {
            Content = JsonContent.Create(new
            {
                message = new
                {
                    token,
                    notification = new { title, body = message },
                    data = new Dictionary<string, string> { ["actionUrl"] = actionUrl ?? string.Empty }
                }
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }
}
