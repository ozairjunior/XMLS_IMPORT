using System.Text;
using XmlFiscal.Application;
using XmlFiscal.Domain;
using XmlFiscal.Infrastructure;

namespace XmlFiscal.Tests;

public sealed class ParserTests
{
    private static FiscalXmlParser CreateParser(bool allowNonOfficialNamespace = false) =>
        new(new XmlDocumentDetector(), new NFeParser(), new XmlSecurityOptions { AllowNonOfficialNamespace = allowNonOfficialNamespace });

    [Fact]
    public async Task Parses_nfe_parties_items_payments_and_installments()
    {
        await using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "TestData", "nfe-inbound.xml"));
        var document = await CreateParser().ParseAsync(stream);
        Assert.Equal(DocumentKind.NFe, document.Kind);
        Assert.Equal("35260812345678000190550010000001231000001234", document.AccessKey);
        Assert.Equal("12345678000190", document.Issuer.TaxId);
        Assert.Equal("98765432000110", document.Recipient.TaxId);
        Assert.Equal(2, document.Items.Count);
        Assert.Equal(3, document.Installments.Count);
        Assert.Single(document.Payments);
        Assert.Equal(3000m, document.TotalAmount);
        Assert.Equal(FinancialInformationStatus.InstallmentsFound, document.FinancialInformationStatus);
    }

    [Fact]
    public async Task Dtd_and_external_entities_are_blocked()
    {
        const string xml = """<?xml version="1.0"?><!DOCTYPE x [<!ENTITY xxe SYSTEM="file:///etc/passwd">]><NFe xmlns="http://www.portalfiscal.inf.br/nfe">&xxe;</NFe>""";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        await Assert.ThrowsAsync<InvalidFiscalXmlException>(() => CreateParser().ParseAsync(stream));
    }
}
