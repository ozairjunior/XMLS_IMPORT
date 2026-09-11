using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using XmlFiscal.Application;
using XmlFiscal.Domain;
using XmlFiscal.Infrastructure.Persistence;

namespace XmlFiscal.Infrastructure;

internal sealed record ProductResolution(
    Product Product,
    ProductAlias Alias,
    bool AutomaticConsolidation,
    IReadOnlyList<ProductMatchSuggestion> Suggestions,
    IReadOnlyList<string> Warnings);

public sealed class ProductConsolidationService
{
    private readonly IGtinNormalizer _gtinNormalizer;
    private readonly IProductDescriptionNormalizer _descriptionNormalizer;
    private readonly IProductMatchingService _matchingService;
    private readonly ProductMatchingOptions _options;

    public ProductConsolidationService(
        IGtinNormalizer gtinNormalizer,
        IProductDescriptionNormalizer descriptionNormalizer,
        IProductMatchingService matchingService,
        ProductMatchingOptions options)
    {
        _gtinNormalizer = gtinNormalizer;
        _descriptionNormalizer = descriptionNormalizer;
        _matchingService = matchingService;
        _options = options;
    }

    internal async Task<ProductResolution> ResolveAsync(
        XmlFiscalDbContext db,
        ParsedFiscalItem source,
        ImportedFile importedFile,
        Guid fiscalDocumentItemId,
        string? issuerTaxId,
        DateTimeOffset? issuedAt,
        CancellationToken cancellationToken)
    {
        var warnings = new List<string>();
        var commercialGtin = _gtinNormalizer.Normalize(source.CommercialGtin);
        var taxableGtin = _gtinNormalizer.Normalize(source.TaxableGtin);
        var gtin = commercialGtin.IsValid ? commercialGtin.Value : taxableGtin.IsValid ? taxableGtin.Value : null;
        if (commercialGtin.IsValid && taxableGtin.IsValid && commercialGtin.Value != taxableGtin.Value)
            warnings.Add($"Item {source.ItemNumber}: cEAN e cEANTrib possuem GTINs válidos diferentes; o produto foi marcado para revisão.");

        var normalizedDescription = _descriptionNormalizer.Normalize(source.Description);
        var normalizedNcm = DigitsOnly(source.Ncm);
        var normalizedUnit = NormalizeUnit(source.CommercialUnit ?? source.TaxableUnit);
        var measure = ExtractMeasure(normalizedDescription);
        var input = new ProductMatchInput(gtin, normalizedDescription, normalizedNcm, normalizedUnit, measure?.Value, measure?.Unit);

        var recurring = await FindRecurringAliasAsync(db, source, issuerTaxId, input, cancellationToken);
        if (recurring is not null)
        {
            var alias = CreateAlias(recurring, importedFile, fiscalDocumentItemId, source, issuerTaxId, issuedAt, normalizedDescription, gtin, normalizedNcm, normalizedUnit);
            db.ProductAliases.Add(alias);
            return new ProductResolution(recurring, alias, true, Array.Empty<ProductMatchSuggestion>(), warnings);
        }

        var candidates = await LoadCandidatesAsync(db, input, cancellationToken);
        var comparisons = candidates
            .Select(product => new CandidateComparison(product, _matchingService.Compare(input, ToMatchInput(product))))
            .OrderByDescending(x => x.Result.Score)
            .ThenBy(x => x.Product.CreatedAt)
            .ToList();

        var automaticCandidates = comparisons
            .Where(x => x.Result.Recommendation == ProductMatchRecommendation.AutomaticSameProduct)
            .ToList();

        var automatic = SelectUnambiguousAutomaticCandidate(automaticCandidates);
        if (automatic is not null && !HasGtinConflict(comparisons, gtin))
        {
            var alias = CreateAlias(automatic.Product, importedFile, fiscalDocumentItemId, source, issuerTaxId, issuedAt, normalizedDescription, gtin, normalizedNcm, normalizedUnit);
            db.ProductAliases.Add(alias);
            return new ProductResolution(automatic.Product, alias, true, Array.Empty<ProductMatchSuggestion>(), warnings);
        }

        var reviewCandidates = comparisons
            .Where(x => x.Result.Recommendation == ProductMatchRecommendation.ManualReview)
            .Take(3)
            .ToList();

        if (automaticCandidates.Count > 1)
        {
            warnings.Add($"Item {source.ItemNumber}: mais de um produto atingiu o limite automático; o caso foi enviado para revisão.");
            reviewCandidates = automaticCandidates.Take(3).Concat(reviewCandidates).DistinctBy(x => x.Product.Id).Take(3).ToList();
        }

        var requiresReview = reviewCandidates.Count > 0 || warnings.Count > 0;
        var product = new Product
        {
            Gtin = gtin,
            ConsolidatedDescription = string.IsNullOrWhiteSpace(source.Description) ? "PRODUTO SEM DESCRIÇÃO" : source.Description.Trim(),
            NormalizedDescription = normalizedDescription,
            Ncm = normalizedNcm,
            CommercialUnit = normalizedUnit,
            NetContent = measure?.Value,
            NetContentUnit = measure?.Unit,
            RequiresReview = requiresReview
        };
        db.Products.Add(product);

        var productAlias = CreateAlias(product, importedFile, fiscalDocumentItemId, source, issuerTaxId, issuedAt, normalizedDescription, gtin, normalizedNcm, normalizedUnit);
        db.ProductAliases.Add(productAlias);

        var suggestions = new List<ProductMatchSuggestion>();
        foreach (var candidate in reviewCandidates)
        {
            if (candidate.Product.Id == product.Id) continue;
            var suggestion = new ProductMatchSuggestion
            {
                SourceProductId = product.Id,
                SourceProduct = product,
                CandidateProductId = candidate.Product.Id,
                CandidateProduct = candidate.Product,
                Score = candidate.Result.Score,
                DescriptionSimilarity = candidate.Result.DescriptionSimilarity,
                GtinMatches = candidate.Result.GtinMatches,
                NcmMatches = candidate.Result.NcmMatches,
                UnitMatches = candidate.Result.UnitMatches,
                Reason = candidate.Result.Reason,
                Status = ProductMatchStatus.Pending
            };
            db.ProductMatchSuggestions.Add(suggestion);
            suggestions.Add(suggestion);
        }

        return new ProductResolution(product, productAlias, false, suggestions, warnings);
    }

    private async Task<Product?> FindRecurringAliasAsync(
        XmlFiscalDbContext db,
        ParsedFiscalItem source,
        string? issuerTaxId,
        ProductMatchInput input,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(issuerTaxId) || string.IsNullOrWhiteSpace(source.SourceProductCode)) return null;

        var taxId = TaxIdNormalizer.Normalize(issuerTaxId);
        var sourceCode = source.SourceProductCode.Trim();
        var aliases = await db.ProductAliases
            .Include(x => x.Product)
            .Where(x => x.SourceIssuerTaxId == taxId && x.SourceCode == sourceCode && x.Product.IsActive && x.Product.MergedIntoProductId == null)
            .OrderByDescending(x => x.SourceDate)
            .Take(10)
            .ToListAsync(cancellationToken);

        foreach (var alias in aliases)
        {
            var comparison = _matchingService.Compare(input, ToMatchInput(alias.Product));
            if (comparison.Recommendation == ProductMatchRecommendation.AutomaticSameProduct)
                return alias.Product;
        }
        return null;
    }

    private async Task<List<Product>> LoadCandidatesAsync(XmlFiscalDbContext db, ProductMatchInput input, CancellationToken cancellationToken)
    {
        var result = db.Products.Local
            .Where(x => x.IsActive && x.MergedIntoProductId == null)
            .Where(x => IsPlausibleCandidate(x, input))
            .ToList();

        IQueryable<Product> query = db.Products
            .Where(x => x.IsActive && x.MergedIntoProductId == null);

        if (!string.IsNullOrWhiteSpace(input.Gtin))
        {
            var gtin = input.Gtin;
            var ncm = input.Ncm;
            var seed = DescriptionSeed(input.NormalizedDescription);
            query = query.Where(x => x.Gtin == gtin ||
                                     (ncm != null && x.Ncm == ncm) ||
                                     (seed != null && x.NormalizedDescription.Contains(seed)));
        }
        else
        {
            var ncm = input.Ncm;
            var unit = input.Unit;
            var seed = DescriptionSeed(input.NormalizedDescription);
            query = query.Where(x =>
                (ncm != null && x.Ncm == ncm && (unit == null || x.CommercialUnit == unit)) ||
                (seed != null && x.NormalizedDescription.Contains(seed)));
        }

        var databaseProducts = await query
            .OrderByDescending(x => x.Gtin == input.Gtin)
            .ThenByDescending(x => x.Ncm == input.Ncm)
            .Take(Math.Max(1, _options.CandidateLimit))
            .ToListAsync(cancellationToken);

        result.AddRange(databaseProducts.Where(x => result.All(local => local.Id != x.Id)));
        return result;
    }

    private static bool IsPlausibleCandidate(Product product, ProductMatchInput input)
    {
        if (!string.IsNullOrWhiteSpace(input.Gtin) && product.Gtin == input.Gtin) return true;
        if (!string.IsNullOrWhiteSpace(input.Ncm) && product.Ncm == input.Ncm) return true;
        var seed = DescriptionSeed(input.NormalizedDescription);
        return seed is not null && product.NormalizedDescription.Contains(seed, StringComparison.Ordinal);
    }

    private static CandidateComparison? SelectUnambiguousAutomaticCandidate(IReadOnlyList<CandidateComparison> candidates)
    {
        if (candidates.Count == 0) return null;
        if (candidates.Count == 1) return candidates[0];
        return candidates[0].Result.Score - candidates[1].Result.Score >= 5m ? candidates[0] : null;
    }

    private static bool HasGtinConflict(IEnumerable<CandidateComparison> comparisons, string? gtin)
    {
        if (string.IsNullOrWhiteSpace(gtin)) return false;
        return comparisons.Any(x => x.Product.Gtin == gtin &&
                                    x.Result.Recommendation == ProductMatchRecommendation.ManualReview &&
                                    x.Result.DescriptionSimilarity < .70m);
    }

    private static ProductAlias CreateAlias(
        Product product,
        ImportedFile importedFile,
        Guid fiscalDocumentItemId,
        ParsedFiscalItem source,
        string? issuerTaxId,
        DateTimeOffset? issuedAt,
        string normalizedDescription,
        string? gtin,
        string? ncm,
        string? unit) => new()
    {
        ProductId = product.Id,
        Product = product,
        ImportedFileId = importedFile.Id,
        ImportedFile = importedFile,
        FiscalDocumentItemId = fiscalDocumentItemId,
        SourceIssuerTaxId = TaxIdNormalizer.Normalize(issuerTaxId),
        SourceDate = issuedAt,
        SourceCode = source.SourceProductCode?.Trim(),
        OriginalDescription = source.Description.Trim(),
        NormalizedDescription = normalizedDescription,
        OriginalGtin = !string.IsNullOrWhiteSpace(source.CommercialGtin) ? source.CommercialGtin.Trim() : source.TaxableGtin?.Trim(),
        NormalizedGtin = gtin,
        Ncm = ncm,
        Unit = unit
    };

    private static ProductMatchInput ToMatchInput(Product product) => new(
        product.Gtin,
        product.NormalizedDescription,
        product.Ncm,
        product.CommercialUnit,
        product.NetContent,
        product.NetContentUnit);

    private static string? DescriptionSeed(string description) => description
        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Where(x => x.Length >= 4 && !Regex.IsMatch(x, @"^\d"))
        .OrderByDescending(x => x.Length)
        .FirstOrDefault();

    private static string? DigitsOnly(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Length == 0 ? null : digits;
    }

    private static string? NormalizeUnit(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var unit = Regex.Replace(value.Trim().ToUpperInvariant(), @"\s+", string.Empty);
        return unit switch
        {
            "UND" or "UNID" or "UNIDADE" => "UN",
            "LITRO" or "LT" => "L",
            "QUILO" => "KG",
            "PACOTE" => "PCT",
            "CAIXA" => "CX",
            _ => unit
        };
    }

    private static (decimal Value, string Unit)? ExtractMeasure(string normalizedDescription)
    {
        var match = Regex.Match(normalizedDescription, @"\b(\d+(?:\.\d+)?)(ML|L|KG|G)\b");
        if (!match.Success || !decimal.TryParse(match.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)) return null;
        return (value, match.Groups[2].Value);
    }

    private sealed record CandidateComparison(Product Product, ProductMatchResult Result);
}
