namespace GoIsland.Api.Services.Payments;

/// <summary>
/// Resuelve y valida el proveedor de pagos antes de aceptar solicitudes. El gateway mock solo se
/// permite en Development/QA y Stripe exige modo Sandbox, clave de prueba y webhook firmado.
/// </summary>
public static class PaymentProviderStartup
{
    public const string MockProvider = MockPaymentGateway.Provider;
    public const string StripeProvider = StripePaymentGateway.Provider;
    public const string SandboxMode = "Sandbox";

    public static string ResolveProvider(
        string? configuredProvider,
        string? configuredMode,
        string environmentName,
        string? stripeSecretKey = null,
        string? stripeWebhookSecret = null)
    {
        var provider = string.IsNullOrWhiteSpace(configuredProvider)
            ? MockProvider
            : configuredProvider.Trim();

        var isMock = provider.Equals(MockProvider, StringComparison.OrdinalIgnoreCase);
        var isStripe = provider.Equals(StripeProvider, StringComparison.OrdinalIgnoreCase);
        if (!isMock && !isStripe)
        {
            throw new InvalidOperationException(
                $"Payments:Provider '{provider}' no esta soportado. "
                + $"Proveedores validos: {MockProvider}, {StripeProvider}.");
        }

        var mockAllowed = environmentName.Equals("Development", StringComparison.OrdinalIgnoreCase)
            || environmentName.Equals("QA", StringComparison.OrdinalIgnoreCase);
        if (isMock && !mockAllowed)
        {
            throw new InvalidOperationException(
                $"Payments:Provider=Mock no esta permitido en el ambiente '{environmentName}'. "
                + "Solo se admite en Development y QA.");
        }

        if (isStripe)
        {
            if (!string.Equals(configuredMode?.Trim(), SandboxMode, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Payments:Mode debe ser Sandbox cuando Payments:Provider=Stripe.");
            }

            if (string.IsNullOrWhiteSpace(stripeSecretKey)
                || !stripeSecretKey.StartsWith("sk_test_", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Stripe:SecretKey debe ser una clave secreta de prueba sk_test_.");
            }

            if (string.IsNullOrWhiteSpace(stripeWebhookSecret)
                || !stripeWebhookSecret.StartsWith("whsec_", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Stripe:WebhookSecret debe contener el secreto de firma whsec_.");
            }
        }

        return isStripe ? StripeProvider : MockProvider;
    }
}
