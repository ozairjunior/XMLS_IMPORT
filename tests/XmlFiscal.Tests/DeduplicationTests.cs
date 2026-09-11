using XmlFiscal.Application;

namespace XmlFiscal.Tests;

public sealed class DeduplicationTests
{
    [Fact]
    public void Tax_id_has_priority_in_party_key()
    {
        var a = new ParsedParty { TaxId = "12.345.678/0001-90", LegalName = "Nome A" };
        var b = new ParsedParty { TaxId = "12345678000190", LegalName = "Nome B" };
        Assert.Equal(PartyDeduplicationKeyBuilder.Build(a), PartyDeduplicationKeyBuilder.Build(b));
    }

    [Fact]
    public void Missing_tax_id_uses_name_city_and_state()
    {
        var party = new ParsedParty { LegalName = "José da Silva", Address = new ParsedAddress { City = "Fortaleza", State = "CE" } };
        Assert.Equal("NAME:JOSE DA SILVA|CITY:FORTALEZA|STATE:CE", PartyDeduplicationKeyBuilder.Build(party));
    }
}
