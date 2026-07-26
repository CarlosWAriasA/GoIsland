using System.ComponentModel.DataAnnotations;
using GoIsland.Api.DTOs.Notifications;

namespace GoIsland.Api.Tests.Services;

public class RegisterDeviceRequestTests
{
    [Fact]
    public void Validate_AcceptsPublicWebPushSubscription()
    {
        var request = CreateRequest("https://updates.push.services.mozilla.com/wpush/v2/example");

        var results = Validate(request);

        Assert.Empty(results);
    }

    [Theory]
    [InlineData("http://push.example.com/subscription")]
    [InlineData("https://localhost/subscription")]
    [InlineData("https://127.0.0.1/subscription")]
    [InlineData("https://push.internal/subscription")]
    public void Validate_RejectsNonPublicEndpoint(string endpoint)
    {
        var request = CreateRequest(endpoint);

        var results = Validate(request);

        Assert.Contains(results, result => result.MemberNames.Contains(nameof(RegisterDeviceRequest.Endpoint)));
    }

    [Fact]
    public void Validate_RejectsMalformedEncryptionKeys()
    {
        var request = CreateRequest("https://push.example.com/subscription");
        request.P256dh = new string('x', 65);
        request.Auth = new string('y', 20);

        var results = Validate(request);

        Assert.Contains(results, result => result.MemberNames.Contains(nameof(RegisterDeviceRequest.P256dh)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(RegisterDeviceRequest.Auth)));
    }

    private static RegisterDeviceRequest CreateRequest(string endpoint) => new()
    {
        Endpoint = endpoint,
        P256dh = ToBase64Url(Enumerable.Range(1, 65).Select(value => (byte)value).ToArray()),
        Auth = ToBase64Url(Enumerable.Range(1, 16).Select(value => (byte)value).ToArray())
    };

    private static List<ValidationResult> Validate(RegisterDeviceRequest request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results;
    }

    private static string ToBase64Url(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
