using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace XmlFiscal.Application;

public static class TaxIdNormalizer
{
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Length is 11 or 14 ? digits : null;
    }
}

public sealed class GtinNormalizer : IGtinNormalizer
{
    private static readonly HashSet<string> EmptyMarkers = new(StringComparer.OrdinalIgnoreCase) { "SEM GTIN", "SEMGTIN", "NO GTIN", "NAO POSSUI", "NÃO POSSUI", "0" };
    public GtinNormalizationResult Normalize(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue)) return new(null, false, "GTIN ausente.");
        var value = rawValue.Trim();
        if (EmptyMarkers.Contains(value)) return new(null, false, "Marcador de ausência de GTIN.");
        if (value.Any(char.IsLetter)) return new(null, false, "GTIN contém letras.");
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length is not (8 or 12 or 13 or 14)) return new(null, false, "Comprimento inválido.");
        if (digits.All(c => c == '0')) return new(null, false, "GTIN composto somente por zeros.");
        return HasValidCheckDigit(digits) ? new(digits, true, null) : new(null, false, "Dígito verificador inválido.");
    }
    private static bool HasValidCheckDigit(string digits)
    {
        var sum = 0; var weight = 3;
        for (var i = digits.Length - 2; i >= 0; i--) { sum += (digits[i] - '0') * weight; weight = weight == 3 ? 1 : 3; }
        return (10 - sum % 10) % 10 == digits[^1] - '0';
    }
}

public sealed class ProductDescriptionNormalizer : IProductDescriptionNormalizer
{
    private static readonly (string Pattern, string Replacement)[] Rules =
    {
        ("LITROS?", "L"), ("LTS?", "L"), ("MILILITROS?", "ML"),
        ("QUILOGRAMAS?", "KG"), ("QUILOS?", "KG"), ("GRAMAS?", "G"),
        ("UNIDADES?", "UN"), ("UNIDS?", "UN"), ("UNDS?", "UN"),
        ("PACOTES?", "PCT"), ("PCTS?", "PCT"), ("CAIXAS?", "CX"),
        ("REFRIG\\.?", "REFRIGERANTE")
    };
    public string Normalize(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return string.Empty;
        var text = RemoveDiacritics(description).ToUpperInvariant();
        text = Regex.Replace(text, @"(?<=\d),(?=\d)", ".");
        text = Regex.Replace(text, @"[^A-Z0-9.]+", " ");
        foreach (var rule in Rules) text = Regex.Replace(text, $@"\b(?:{rule.Pattern})\b", rule.Replacement);
        text = Regex.Replace(text, @"\b(\d+(?:\.\d+)?)\s+(ML|L|KG|G|UN|CX|PCT)\b", "$1$2");
        return Regex.Replace(text, @"\s+", " ").Trim();
    }
    private static string RemoveDiacritics(string value)
    {
        var builder = new StringBuilder();
        foreach (var c in value.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) builder.Append(c);
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}

public static class PartyDeduplicationKeyBuilder
{
    public static string Build(ParsedParty party)
    {
        var taxId = TaxIdNormalizer.Normalize(party.TaxId);
        if (taxId is not null) return $"TAX:{taxId}";
        return $"NAME:{Normalize(party.LegalName)}|CITY:{Normalize(party.Address.City)}|STATE:{Normalize(party.Address.State)}";
    }
    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var builder = new StringBuilder();
        foreach (var c in value.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) builder.Append(c);
        return Regex.Replace(builder.ToString().ToUpperInvariant(), @"[^A-Z0-9]+", " ").Trim();
    }
}
