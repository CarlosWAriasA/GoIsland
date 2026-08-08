using System.Globalization;
using System.Text;

namespace GoIsland.Api.Data;

/// <summary>
/// Normalización de texto para búsquedas. Debe producir el mismo resultado que la función
/// <c>goisland_normalize</c> de PostgreSQL (script 020): sin diacríticos y en minúsculas.
/// De ese modo "samana" encuentra "Samaná" y "SAMANÁ" encuentra "samana".
/// </summary>
public static class SearchText
{
    /// <summary>Quita diacríticos y pasa a minúsculas.</summary>
    public static string Normalize(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .ToLowerInvariant();
    }

    /// <summary>Recorta y normaliza un término, devolviendo null cuando no hay búsqueda.</summary>
    public static string? NormalizeTerm(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Normalize(value.Trim());

    /// <summary>Escapa los comodines de LIKE usando la barra invertida.</summary>
    public static string EscapeLikePattern(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);

    /// <summary>Patrón "contiene" ya normalizado y escapado, listo para LIKE.</summary>
    public static string ToContainsPattern(string normalizedTerm) =>
        $"%{EscapeLikePattern(normalizedTerm)}%";

    /// <summary>Patrón "empieza por" ya normalizado y escapado, listo para LIKE.</summary>
    public static string ToStartsWithPattern(string normalizedTerm) =>
        $"{EscapeLikePattern(normalizedTerm)}%";
}
