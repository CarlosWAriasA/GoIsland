using GoIsland.Api.Services.Security;
using Microsoft.Extensions.Configuration;

namespace GoIsland.Api.Tests.Services;

public class SecurityConfigurationTests
{
    [Fact]
    public void ResolveFrontendOrigin_RejectsMissingProductionOrigin()
    {
        var configuration = BuildConfiguration();

        Assert.Throws<InvalidOperationException>(() =>
            SecurityConfiguration.ResolveFrontendOrigin(configuration, "Production"));
    }

    [Fact]
    public void ResolveFrontendOrigin_RequiresHttpsInProduction()
    {
        var configuration = BuildConfiguration(("Cors:FrontendUrl", "http://goisland.example"));

        Assert.Throws<InvalidOperationException>(() =>
            SecurityConfiguration.ResolveFrontendOrigin(configuration, "Production"));
    }

    [Fact]
    public void ResolveFrontendOrigin_NormalizesConfiguredHttpsOrigin()
    {
        var configuration = BuildConfiguration(("Cors:FrontendUrl", "https://goisland.example/"));

        Assert.Equal(
            "https://goisland.example",
            SecurityConfiguration.ResolveFrontendOrigin(configuration, "Production"));
    }

    [Fact]
    public void GetRequiredJwtKey_RejectsShortOrPlaceholderKeys()
    {
        Assert.Throws<InvalidOperationException>(() =>
            SecurityConfiguration.GetRequiredJwtKey(
                BuildConfiguration(("Jwt:Key", "replace-with-a-secret")),
                "Production"));
        Assert.Throws<InvalidOperationException>(() =>
            SecurityConfiguration.GetRequiredJwtKey(
                BuildConfiguration(("Jwt:Key", "short")),
                "Development"));
    }

    [Fact]
    public void GetRequiredJwtKey_AcceptsLongRandomKey()
    {
        var key = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));

        Assert.Equal(
            key,
            SecurityConfiguration.GetRequiredJwtKey(BuildConfiguration(("Jwt:Key", key)), "Production"));
    }

    private static IConfiguration BuildConfiguration(params (string Key, string Value)[] values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(value => value.Key, value => (string?)value.Value))
            .Build();
    }
}
