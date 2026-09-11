using XmlFiscal.Application;

namespace XmlFiscal.Tests;

public sealed class NormalizationTests
{
    private readonly GtinNormalizer _gtin = new();
    private readonly ProductDescriptionNormalizer _description = new();

    [Theory]
    [InlineData("7894900011517", "7894900011517")]
    [InlineData(" 7894900011517 ", "7894900011517")]
    public void Gtin_valid_is_preserved(string source, string expected)
    {
        var result = _gtin.Normalize(source);
        Assert.True(result.IsValid);
        Assert.Equal(expected, result.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("SEM GTIN")]
    [InlineData("0000000000000")]
    [InlineData("7894900011518")]
    [InlineData("ABC123")]
    public void Invalid_gtin_is_not_promoted_to_identifier(string? source)
    {
        var result = _gtin.Normalize(source);
        Assert.False(result.IsValid);
        Assert.Null(result.Value);
    }

    [Theory]
    [InlineData("COCA-COLA PET 2 LITROS")]
    [InlineData("Coca Cola Pet 2L")]
    public void Equivalent_descriptions_have_same_normalized_form(string source)
    {
        Assert.Equal("COCA COLA PET 2L", _description.Normalize(source));
    }

    [Fact]
    public void Normalization_preserves_size_information()
    {
        Assert.NotEqual(_description.Normalize("COCA COLA 1 LITRO"), _description.Normalize("COCA COLA 2 LITROS"));
    }
}
