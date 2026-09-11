using XmlFiscal.Application;
using XmlFiscal.Domain;

namespace XmlFiscal.Tests;

public sealed class PartyResolverTests
{
    private readonly PartyResolver _resolver = new();
    private readonly Company _company = new() { TaxId = "98765432000110", LegalName = "Mercado Exemplo" };

    [Fact]
    public void Company_as_recipient_means_inbound()
    {
        var result = _resolver.Resolve([_company], new ParsedParty { TaxId = "12345678000190" }, new ParsedParty { TaxId = _company.TaxId });
        Assert.Equal(DocumentDirection.Inbound, result.Direction);
        Assert.Equal(_company.Id, result.Company?.Id);
    }

    [Fact]
    public void Company_as_issuer_means_outbound()
    {
        var result = _resolver.Resolve([_company], new ParsedParty { TaxId = _company.TaxId }, new ParsedParty { TaxId = "12345678000190" });
        Assert.Equal(DocumentDirection.Outbound, result.Direction);
    }

    [Fact]
    public void Issuer_and_recipient_without_configured_company_is_unknown()
    {
        var result = _resolver.Resolve([_company], new ParsedParty { TaxId = "11111111000111" }, new ParsedParty { TaxId = "22222222000122" });
        Assert.Equal(DocumentDirection.Unknown, result.Direction);
        Assert.Null(result.Company);
    }
}
