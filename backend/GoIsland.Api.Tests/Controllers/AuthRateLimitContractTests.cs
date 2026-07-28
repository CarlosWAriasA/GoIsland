using System.Reflection;
using GoIsland.Api.Controllers;
using GoIsland.Api.Services.Security;
using Microsoft.AspNetCore.RateLimiting;

namespace GoIsland.Api.Tests.Controllers;

public class AuthRateLimitContractTests
{
    [Theory]
    [InlineData(nameof(AuthController.Register), RateLimitPolicyNames.Authentication)]
    [InlineData(nameof(AuthController.Login), RateLimitPolicyNames.Authentication)]
    [InlineData(nameof(AuthController.Google), RateLimitPolicyNames.Authentication)]
    [InlineData(nameof(AuthController.ForgotPassword), RateLimitPolicyNames.PasswordRecovery)]
    [InlineData(nameof(AuthController.ResetPassword), RateLimitPolicyNames.PasswordRecovery)]
    public void AnonymousAuthenticationEndpoints_RequireExpectedRateLimit(
        string actionName,
        string expectedPolicy)
    {
        var action = typeof(AuthController).GetMethod(actionName)
            ?? throw new InvalidOperationException($"No se encontro {actionName}.");
        var attribute = action.GetCustomAttribute<EnableRateLimitingAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(expectedPolicy, attribute.PolicyName);
    }
}
