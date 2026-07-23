namespace GoIsland.Api.Services.Payments;

/// <summary>
/// Resuelve y valida el proveedor de pagos configurado antes de que la aplicacion acepte
/// solicitudes. El gateway mock solo se permite en Development/QA y cualquier proveedor
/// desconocido detiene el arranque.
/// </summary>
public static class PaymentProviderStartup
{
    public const string MockProvider = MockPaymentGateway.Provider;

    public static string ResolveProvider(string? configuredProvider, string environmentName)
    {
        var provider = string.IsNullOrWhiteSpace(configuredProvider)
            ? MockProvider
            : configuredProvider.Trim();

        if (!provider.Equals(MockProvider, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Payments:Provider '{provider}' no esta soportado. Proveedores validos: {MockProvider}.");
        }

        var mockAllowed = environmentName.Equals("Development", StringComparison.OrdinalIgnoreCase)
            || environmentName.Equals("QA", StringComparison.OrdinalIgnoreCase);
        if (!mockAllowed)
        {
            throw new InvalidOperationException(
                $"Payments:Provider=Mock no esta permitido en el ambiente '{environmentName}'. "
                + "Solo se admite en Development y QA.");
        }

        return MockProvider;
    }
}
