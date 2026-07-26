using GoIsland.Api.Services.Notifications;
using Microsoft.Extensions.Configuration;
using WebPush;

namespace GoIsland.Api.Tests.Services;

public class WebPushSenderTests
{
    [Fact]
    public void IsConfigured_RequiresValidVapidConfiguration()
    {
        var keys = VapidHelper.GenerateVapidKeys();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WebPush:Subject"] = "mailto:notifications@goisland.test",
                ["WebPush:PublicKey"] = keys.PublicKey,
                ["WebPush:PrivateKey"] = keys.PrivateKey
            })
            .Build();

        using var sender = new WebPushSender(configuration);

        Assert.True(sender.IsConfigured);
        Assert.Equal(keys.PublicKey, sender.PublicKey);
    }

    [Theory]
    [InlineData(null, null, null)]
    [InlineData("mailto:notifications@goisland.test", "invalid", "invalid")]
    [InlineData("ftp://goisland.test", "invalid", "invalid")]
    public void IsConfigured_RejectsMissingOrInvalidConfiguration(
        string? subject, string? publicKey, string? privateKey)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WebPush:Subject"] = subject,
                ["WebPush:PublicKey"] = publicKey,
                ["WebPush:PrivateKey"] = privateKey
            })
            .Build();

        using var sender = new WebPushSender(configuration);

        Assert.False(sender.IsConfigured);
    }
}
