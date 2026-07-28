using System.ComponentModel.DataAnnotations;

namespace GoIsland.Api.Services.Security;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class StrongPasswordAttribute : ValidationAttribute
{
    public const int MinimumLength = 12;
    public const int MaximumLength = 128;
    public const string PolicyMessage =
        "La contrasena debe tener entre 12 y 128 caracteres e incluir mayuscula, minuscula y numero.";

    public StrongPasswordAttribute()
        : base(PolicyMessage)
    {
    }

    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true;
        }

        if (value is not string password
            || password.Length < MinimumLength
            || password.Length > MaximumLength)
        {
            return false;
        }

        return password.Any(char.IsUpper)
            && password.Any(char.IsLower)
            && password.Any(char.IsDigit);
    }
}
