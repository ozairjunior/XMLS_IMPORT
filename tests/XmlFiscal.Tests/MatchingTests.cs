using XmlFiscal.Application;

namespace XmlFiscal.Tests;

public sealed class MatchingTests
{
    private readonly ProductDescriptionNormalizer _normalizer = new();
    private readonly ProductMatchingService _service = new(new ProductMatchingOptions());

    [Fact]
    public void Same_gtin_and_similar_description_is_automatic()
    {
        var result = Compare("7894900011517", "COCA-COLA PET 2 LITROS", "7894900011517", "COCA COLA PET 2L");
        Assert.Equal(ProductMatchRecommendation.AutomaticSameProduct, result.Recommendation);
        Assert.True(result.GtinMatches);
    }

    [Fact]
    public void Same_gtin_with_different_size_requires_review()
    {
        var result = Compare("7894900011517", "COCA COLA PET 2L", "7894900011517", "COCA COLA PET 1L");
        Assert.Equal(ProductMatchRecommendation.ManualReview, result.Recommendation);
        Assert.False(result.MeasureMatches);
    }

    [Fact]
    public void Different_valid_gtins_are_never_merged_automatically()
    {
        var result = Compare("7894900011517", "COCA COLA PET 2L", "7894900011524", "COCA COLA PET 2L");
        Assert.Equal(ProductMatchRecommendation.DifferentProducts, result.Recommendation);
        Assert.True(result.Score < 70m);
    }

    [Fact]
    public void No_gtin_but_equal_description_ncm_and_unit_can_be_automatic()
    {
        var left = new ProductMatchInput(null, _normalizer.Normalize("ARROZ TIPO 1 PACOTE 5 KG"), "10063021", "PCT");
        var right = new ProductMatchInput(null, _normalizer.Normalize("ARROZ TIPO 1 PCT 5KG"), "10063021", "PCT");
        var result = _service.Compare(left, right);
        Assert.Equal(ProductMatchRecommendation.AutomaticSameProduct, result.Recommendation);
    }

    [Fact]
    public void Equivalent_measure_units_do_not_conflict()
    {
        var left = new ProductMatchInput("7894900011517", _normalizer.Normalize("BEBIDA 2L"), "22021000", "UN");
        var right = new ProductMatchInput("7894900011517", _normalizer.Normalize("BEBIDA 2000ML"), "22021000", "UN");
        var result = _service.Compare(left, right);
        Assert.True(result.MeasureMatches);
    }

    private ProductMatchResult Compare(string gtinA, string descriptionA, string gtinB, string descriptionB)
    {
        var left = new ProductMatchInput(gtinA, _normalizer.Normalize(descriptionA), "22021000", "UN");
        var right = new ProductMatchInput(gtinB, _normalizer.Normalize(descriptionB), "22021000", "UN");
        return _service.Compare(left, right);
    }
}
