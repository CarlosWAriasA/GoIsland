using System.ComponentModel.DataAnnotations;
using GoIsland.Api.DTOs.Auth;

namespace GoIsland.Api.Tests.Services;

public class PasswordPolicyTests
{
    [Fact]
    public void RegisterRequest_AcceptsStrongPassword()
    {
        var request = new RegisterRequest
        {
            FullName = "Usuario Seguro",
            Email = "seguro@goisland.test",
            Password = "GoIslandSegura2026"
        };

        Assert.Empty(Validate(request));
    }

    [Theory]
    [InlineData("Password1")]
    [InlineData("goislandsegura2026")]
    [InlineData("GOISLANDSEGURA2026")]
    [InlineData("GoIslandSinNumero")]
    public void RegisterRequest_RejectsWeakPassword(string password)
    {
        var request = new RegisterRequest
        {
            FullName = "Usuario Seguro",
            Email = "seguro@goisland.test",
            Password = password
        };

        Assert.Contains(Validate(request), result => result.MemberNames.Contains(nameof(RegisterRequest.Password)));
    }

    [Fact]
    public void ChangeAndResetPassword_UseSamePolicy()
    {
        var change = new ChangePasswordRequest
        {
            CurrentPassword = "Anterior123",
            NewPassword = "debil",
            ConfirmPassword = "debil"
        };
        var reset = new ResetPasswordRequest
        {
            Token = "token-valido-para-prueba",
            NewPassword = "debil",
            ConfirmPassword = "debil"
        };

        Assert.Contains(Validate(change), result =>
            result.MemberNames.Contains(nameof(ChangePasswordRequest.NewPassword)));
        Assert.Contains(Validate(reset), result =>
            result.MemberNames.Contains(nameof(ResetPasswordRequest.NewPassword)));
    }

    private static IReadOnlyCollection<ValidationResult> Validate(object instance)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);
        return results;
    }
}
