using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using XmlFiscal.Application;
using XmlFiscal.Domain;

namespace XmlFiscal.Infrastructure;

public sealed class UnsupportedFiscalXmlException(string message) : Exception(message);
public sealed class InvalidFiscalXmlException : Exception
{
    public InvalidFiscalXmlException(string message) : base(message) { }
    public InvalidFiscalXmlException(string message, Exception inner) : base(message, inner) { }
}

public sealed class XmlDocumentDetector
{
    public DocumentKind Detect(XDocument document)
    {
        var root = document.Root?.Name.LocalName;
        if (root is "cteProc" or "CTe") return DocumentKind.CTe;
        if (root is "mdfeProc" or "MDFe") return DocumentKind.MDFe;
        var inf = document.Descendants().FirstOrDefault(x => x.Name.LocalName == "infNFe");
        var model = inf?.Descendants().FirstOrDefault(x => x.Name.LocalName == "mod")?.Value;
        return model == "55" ? DocumentKind.NFe : model == "65" ? DocumentKind.NFCe : DocumentKind.Unknown;
    }
}

public sealed class FiscalXmlParser : IFiscalXmlParser
{
    private readonly XmlDocumentDetector _detector;
    private readonly NFeParser _nfeParser;
    private readonly XmlSecurityOptions _options;
    public FiscalXmlParser(XmlDocumentDetector detector, NFeParser nfeParser, XmlSecurityOptions options)
    { _detector = detector; _nfeParser = nfeParser; _options = options; }

    public async Task<ParsedFiscalDocument> ParseAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = new XmlReaderSettings
            {
                Async = true, DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                IgnoreComments = true, IgnoreProcessingInstructions = true, IgnoreWhitespace = true,
                MaxCharactersInDocument = _options.MaximumCharactersInDocument, CloseInput = false
            };
            using var reader = XmlReader.Create(stream, settings);
            var document = await XDocument.LoadAsync(reader, LoadOptions.SetLineInfo, cancellationToken);
            var kind = _detector.Detect(document);
            if (kind is not (DocumentKind.NFe or DocumentKind.NFCe))
                throw new UnsupportedFiscalXmlException($"Tipo de XML não suportado nesta etapa: {kind}.");
            var parsed = _nfeParser.Parse(document, kind);
            if (!_options.AllowNonOfficialNamespace &&
                (string.IsNullOrWhiteSpace(parsed.NamespaceUri) || !parsed.NamespaceUri.Contains("portalfiscal.inf.br/nfe", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidFiscalXmlException("Namespace da NF-e não é o namespace oficial esperado.");
            return parsed;
        }
        catch (UnsupportedFiscalXmlException) { throw; }
        catch (InvalidFiscalXmlException) { throw; }
        catch (XmlException ex) { throw new InvalidFiscalXmlException("XML malformado ou bloqueado pelas regras de segurança.", ex); }
        catch (Exception ex) when (ex is not OperationCanceledException) { throw new InvalidFiscalXmlException("Não foi possível interpretar o XML fiscal.", ex); }
    }
}

public sealed class NFeParser
{
    private static readonly IReadOnlyDictionary<string, string> PaymentDescriptions = new Dictionary<string, string>
    {
        ["01"]="Dinheiro", ["02"]="Cheque", ["03"]="Cartão de crédito", ["04"]="Cartão de débito",
        ["05"]="Crédito de loja", ["14"]="Duplicata mercantil", ["15"]="Boleto bancário", ["16"]="Depósito bancário",
        ["17"]="PIX", ["18"]="Transferência/carteira digital", ["90"]="Sem pagamento", ["99"]="Outros"
    };

    public ParsedFiscalDocument Parse(XDocument document, DocumentKind detectedKind)
    {
        var nfe = SelfAndDesc(document, "NFe").FirstOrDefault() ?? throw new InvalidFiscalXmlException("Elemento NFe não encontrado.");
        var inf = SelfAndDesc(nfe, "infNFe").FirstOrDefault() ?? throw new InvalidFiscalXmlException("Elemento infNFe não encontrado.");
        var ide = Child(inf, "ide") ?? throw new InvalidFiscalXmlException("Elemento ide não encontrado.");
        var emit = Child(inf, "emit") ?? throw new InvalidFiscalXmlException("Elemento emit não encontrado.");
        var dest = Child(inf, "dest") ?? new XElement(inf.Name.Namespace + "dest");
        var total = Desc(inf, "ICMSTot").FirstOrDefault();
        var accessKey = ReadAccessKey(document, inf);
        if (accessKey.Length != 44 || accessKey.Any(c => !char.IsDigit(c))) throw new InvalidFiscalXmlException("Chave de acesso ausente ou inválida.");
        var model = Int(Val(ide, "mod"));
        var kind = model == 65 ? DocumentKind.NFCe : model == 55 ? DocumentKind.NFe : detectedKind;
        var totalAmount = Dec(Val(total, "vNF"));
        var installments = Desc(inf, "dup").Select(x => new ParsedInstallment(Val(x, "nDup"), Date(Val(x, "dVenc")), Dec(Val(x, "vDup")))).ToList();
        var payments = Desc(inf, "detPag").Select(ParsePayment).ToList();
        var financialStatus = FinancialStatus(installments, payments, totalAmount);
        var protocol = Desc(document, "infProt").FirstOrDefault();
        var warnings = new List<string>();
        if (string.IsNullOrWhiteSpace(inf.Name.NamespaceName) || !inf.Name.NamespaceName.Contains("portalfiscal.inf.br/nfe", StringComparison.OrdinalIgnoreCase))
            warnings.Add("Namespace da NF-e ausente ou diferente do namespace oficial esperado.");

        return new ParsedFiscalDocument
        {
            Kind = kind, NamespaceUri = inf.Name.NamespaceName, AccessKey = accessKey, Model = model,
            Series = Val(ide, "serie"), Number = Val(ide, "nNF"), IssuedAt = Date(Val(ide, "dhEmi") ?? Val(ide, "dEmi")),
            OperationNature = Val(ide, "natOp"), OperationType = Int(Val(ide, "tpNF")), Purpose = Int(Val(ide, "finNFe")),
            Issuer = Party(emit, "enderEmit"), Recipient = Party(dest, "enderDest"),
            ProductsAmount = Dec(Val(total, "vProd")), DiscountAmount = Dec(Val(total, "vDesc")), FreightAmount = Dec(Val(total, "vFrete")),
            InsuranceAmount = Dec(Val(total, "vSeg")), OtherExpensesAmount = Dec(Val(total, "vOutro")), TaxesAmount = Taxes(total), TotalAmount = totalAmount,
            AuthorizationProtocol = Val(protocol, "nProt"), AuthorizedAt = Date(Val(protocol, "dhRecbto")), AuthorizationStatusCode = Val(protocol, "cStat"),
            FinancialInformationStatus = financialStatus, Items = Items(inf), Installments = installments, Payments = payments, Warnings = warnings
        };
    }

    private static List<ParsedFiscalItem> Items(XElement inf)
    {
        var result = new List<ParsedFiscalItem>();
        foreach (var det in inf.Elements().Where(x => x.Name.LocalName == "det"))
        {
            var p = Child(det, "prod"); if (p is null) continue;
            result.Add(new ParsedFiscalItem
            {
                ItemNumber = Int(det.Attribute("nItem")?.Value) ?? result.Count + 1,
                SourceProductCode = Val(p, "cProd"), CommercialGtin = Val(p, "cEAN"), Description = Val(p, "xProd") ?? string.Empty,
                Ncm = Val(p, "NCM"), Cest = Val(p, "CEST"), Cfop = Val(p, "CFOP"), CommercialUnit = Val(p, "uCom"),
                CommercialQuantity = Dec(Val(p, "qCom")), CommercialUnitPrice = Dec(Val(p, "vUnCom")), ProductAmount = Dec(Val(p, "vProd")),
                TaxableGtin = Val(p, "cEANTrib"), TaxableUnit = Val(p, "uTrib"), TaxableQuantity = DecNull(Val(p, "qTrib")), TaxableUnitPrice = DecNull(Val(p, "vUnTrib")),
                DiscountAmount = Dec(Val(p, "vDesc")), OtherExpensesAmount = Dec(Val(p, "vOutro"))
            });
        }
        return result;
    }

    private static ParsedParty Party(XElement p, string addressName)
    {
        var a = Child(p, addressName);
        return new ParsedParty
        {
            TaxId = Val(p, "CNPJ") ?? Val(p, "CPF"), LegalName = Val(p, "xNome"), TradeName = Val(p, "xFant"),
            StateRegistration = Val(p, "IE"), StateRegistrationIndicator = Val(p, "indIEDest"), Email = Val(p, "email"),
            Phone = a is null ? null : Val(a, "fone"),
            Address = a is null ? new ParsedAddress() : new ParsedAddress
            {
                Street=Val(a,"xLgr"), Number=Val(a,"nro"), Complement=Val(a,"xCpl"), District=Val(a,"xBairro"), City=Val(a,"xMun"),
                CityIbgeCode=Val(a,"cMun"), State=Val(a,"UF"), PostalCode=Val(a,"CEP"), Country=Val(a,"xPais"), CountryCode=Val(a,"cPais")
            }
        };
    }

    private static ParsedPayment ParsePayment(XElement element)
    {
        var card = Child(element, "card"); var method = Val(element, "tPag") ?? string.Empty;
        return new(method, PaymentDescriptions.GetValueOrDefault(method), DecNull(Val(element,"vPag")), Val(element,"indPag"),
            card is null ? null : Val(card,"tpIntegra"), card is null ? null : Val(card,"CNPJ"), card is null ? null : Val(card,"tBand"), card is null ? null : Val(card,"cAut"));
    }

    private static FinancialInformationStatus FinancialStatus(IReadOnlyCollection<ParsedInstallment> installments, IReadOnlyCollection<ParsedPayment> payments, decimal total)
    {
        if (installments.Count > 0)
            return total > 0 && Math.Abs(installments.Sum(x => x.Amount) - total) > .05m ? FinancialInformationStatus.InconsistentFinancialInformation : FinancialInformationStatus.InstallmentsFound;
        if (payments.Count > 0) return payments.Any(x => x.Amount is > 0) ? FinancialInformationStatus.PaymentFoundWithoutInstallments : FinancialInformationStatus.PaymentMethodOnly;
        return FinancialInformationStatus.NoFinancialInformation;
    }

    private static decimal Taxes(XElement? total)
    {
        var burden = DecNull(Val(total,"vTotTrib"));
        return burden ?? Dec(Val(total,"vICMS")) + Dec(Val(total,"vIPI")) + Dec(Val(total,"vPIS")) + Dec(Val(total,"vCOFINS"));
    }
    private static string ReadAccessKey(XDocument doc, XElement inf)
    {
        var id = inf.Attribute("Id")?.Value?.Trim(); if (!string.IsNullOrWhiteSpace(id) && id.StartsWith("NFe", StringComparison.OrdinalIgnoreCase)) return id[3..];
        return Desc(doc,"chNFe").FirstOrDefault()?.Value?.Trim() ?? string.Empty;
    }
    private static XElement? Child(XElement? p, string name) => p?.Elements().FirstOrDefault(x => x.Name.LocalName == name);
    private static string? Val(XElement? p, string name) => Child(p,name)?.Value?.Trim();
    private static IEnumerable<XElement> Desc(XContainer c, string name) => c.Descendants().Where(x => x.Name.LocalName == name);
    private static IEnumerable<XElement> SelfAndDesc(XDocument d, string name) => (d.Root is not null && d.Root.Name.LocalName == name ? new[]{d.Root} : Array.Empty<XElement>()).Concat(d.Descendants().Where(x=>x.Name.LocalName==name));
    private static IEnumerable<XElement> SelfAndDesc(XElement e, string name) => e.DescendantsAndSelf().Where(x=>x.Name.LocalName==name);
    private static decimal Dec(string? value) => DecNull(value) ?? 0m;
    private static decimal? DecNull(string? value) => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;
    private static int? Int(string? value) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
    private static DateTimeOffset? Date(string? value) => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var v) ? v : null;
}
