using System.Globalization;
using System.Text.RegularExpressions;

namespace XmlFiscal.Application;

public sealed class ProductMatchingService : IProductMatchingService
{
    private readonly ProductMatchingOptions _options;
    public ProductMatchingService(ProductMatchingOptions options) => _options = options;

    public ProductMatchResult Compare(ProductMatchInput source, ProductMatchInput candidate)
    {
        var sg = Empty(source.Gtin); var cg = Empty(candidate.Gtin);
        var bothGtin = sg is not null && cg is not null;
        bool? gtinMatches = bothGtin ? sg == cg : null;
        var similarity = DescriptionSimilarity(source.NormalizedDescription, candidate.NormalizedDescription);
        var ncmMatches = CompareOptional(source.Ncm, candidate.Ncm);
        var unitMatches = CompareOptional(source.Unit?.ToUpperInvariant(), candidate.Unit?.ToUpperInvariant());
        var measureMatches = CompareMeasure(source, candidate);
        var measureConflict = measureMatches == false;

        if (bothGtin && gtinMatches == false)
        {
            var score = Math.Min(_options.DifferentGtinMaximumScore,
                similarity * _options.DifferentGtinDescriptionWeight +
                Points(ncmMatches, _options.NcmWeightWithGtin) +
                Points(unitMatches, _options.UnitWeightWithGtin));
            return Result(score, similarity, gtinMatches, ncmMatches, unitMatches, measureMatches, ProductMatchRecommendation.DifferentProducts,
                "GTINs válidos e diferentes nunca são consolidados automaticamente.");
        }

        if (gtinMatches == true)
        {
            var score = _options.GtinWeight +
                        similarity * _options.DescriptionWeightWithGtin +
                        Points(ncmMatches, _options.NcmWeightWithGtin) +
                        Points(unitMatches, _options.UnitWeightWithGtin);
            if (measureConflict) score = Math.Min(score, _options.MeasureConflictMaximumScore);
            var auto = !measureConflict && similarity >= _options.SameGtinMinimumDescriptionSimilarity && score >= _options.AutomaticMatchThreshold;
            return Result(score, similarity, true, ncmMatches, unitMatches, measureMatches,
                auto ? ProductMatchRecommendation.AutomaticSameProduct : ProductMatchRecommendation.ManualReview,
                auto ? "GTIN igual, descrição altamente semelhante e atributos compatíveis."
                     : measureConflict ? "GTIN igual, porém peso/volume diverge; revisão manual obrigatória."
                     : "GTIN igual, mas a descrição não atingiu o limite automático.");
        }

        var noGtinScore = similarity * _options.DescriptionWeightWithoutGtin +
                          Points(ncmMatches, _options.NcmWeightWithoutGtin) +
                          Points(unitMatches, _options.UnitWeightWithoutGtin);
        if (measureConflict) noGtinScore = Math.Min(noGtinScore, _options.DifferentGtinMaximumScore);
        if (sg is null && cg is null && !measureConflict && ncmMatches == true && unitMatches == true &&
            similarity >= _options.NoGtinMinimumDescriptionSimilarity && noGtinScore >= _options.AutomaticMatchThreshold)
            return Result(noGtinScore, similarity, null, ncmMatches, unitMatches, measureMatches, ProductMatchRecommendation.AutomaticSameProduct,
                "Sem GTIN, com descrição, NCM e unidade fortemente compatíveis.");
        if (noGtinScore >= _options.ReviewThreshold && !measureConflict)
            return Result(noGtinScore, similarity, gtinMatches, ncmMatches, unitMatches, measureMatches, ProductMatchRecommendation.ManualReview,
                "Correspondência provável, mas sem evidência suficiente para consolidação automática.");
        return Result(noGtinScore, similarity, gtinMatches, ncmMatches, unitMatches, measureMatches, ProductMatchRecommendation.DifferentProducts,
            measureConflict ? "Peso ou volume conflitante." : "Score abaixo do limite de revisão.");
    }

    private static ProductMatchResult Result(decimal score, decimal sim, bool? gtin, bool? ncm, bool? unit, bool? measure,
        ProductMatchRecommendation recommendation, string reason) =>
        new(Math.Clamp(decimal.Round(score, 2), 0, 100), decimal.Round(sim, 4), gtin, ncm, unit, measure, recommendation, reason);

    private static decimal DescriptionSimilarity(string left, string right)
    {
        if (left == right) return 1m;
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return 0m;
        var lt = left.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        var rt = right.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        var union = lt.Union(rt).Count(); var intersection = lt.Intersect(rt).Count();
        var jaccard = union == 0 ? 0m : (decimal)intersection / union;
        var max = Math.Max(left.Length, right.Length);
        var lev = max == 0 ? 1m : 1m - (decimal)Levenshtein(left, right) / max;
        return Math.Clamp(jaccard * .60m + lev * .40m, 0, 1);
    }

    private static int Levenshtein(string left, string right)
    {
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        var current = new int[right.Length + 1];
        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }
            (previous, current) = (current, previous);
        }
        return previous[right.Length];
    }

    private static bool? CompareOptional(string? left, string? right)
    {
        left = Empty(left); right = Empty(right);
        return left is null || right is null ? null : string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }
    private static decimal Points(bool? match, decimal points) => match == true ? points : 0m;
    private static string? Empty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool? CompareMeasure(ProductMatchInput a, ProductMatchInput b)
    {
        var ma = Measure(a); var mb = Measure(b);
        if (ma is null || mb is null) return null;
        if (ma.Value.Dimension != mb.Value.Dimension) return false;
        return Math.Abs(ma.Value.BaseValue - mb.Value.BaseValue) <= 0.0001m;
    }
    private static (decimal BaseValue, string Dimension)? Measure(ProductMatchInput input)
    {
        decimal value;
        string unit;
        if (input.NetContent is not null && !string.IsNullOrWhiteSpace(input.NetContentUnit))
        {
            value = input.NetContent.Value;
            unit = input.NetContentUnit.Trim().ToUpperInvariant();
        }
        else
        {
            var match = Regex.Match(input.NormalizedDescription, @"\b(\d+(?:\.\d+)?)(ML|L|KG|G)\b");
            if (!match.Success || !decimal.TryParse(match.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out value)) return null;
            unit = match.Groups[2].Value;
        }

        return unit switch
        {
            "L" => (value * 1000m, "VOLUME"),
            "ML" => (value, "VOLUME"),
            "KG" => (value * 1000m, "WEIGHT"),
            "G" => (value, "WEIGHT"),
            _ => null
        };
    }
}
