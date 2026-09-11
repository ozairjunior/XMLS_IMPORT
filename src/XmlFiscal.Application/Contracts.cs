using XmlFiscal.Domain;

namespace XmlFiscal.Application;

public sealed class ParsedFiscalDocument
{
    public DocumentKind Kind { get; init; }
    public string? NamespaceUri { get; init; }
    public string AccessKey { get; init; } = string.Empty;
    public int? Model { get; init; }
    public string? Series { get; init; }
    public string? Number { get; init; }
    public DateTimeOffset? IssuedAt { get; init; }
    public string? OperationNature { get; init; }
    public int? OperationType { get; init; }
    public int? Purpose { get; init; }
    public ParsedParty Issuer { get; init; } = new();
    public ParsedParty Recipient { get; init; } = new();
    public decimal ProductsAmount { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal FreightAmount { get; init; }
    public decimal InsuranceAmount { get; init; }
    public decimal OtherExpensesAmount { get; init; }
    public decimal TaxesAmount { get; init; }
    public decimal TotalAmount { get; init; }
    public string? AuthorizationProtocol { get; init; }
    public DateTimeOffset? AuthorizedAt { get; init; }
    public string? AuthorizationStatusCode { get; init; }
    public FinancialInformationStatus FinancialInformationStatus { get; init; } = FinancialInformationStatus.NoFinancialInformation;
    public List<ParsedFiscalItem> Items { get; init; } = new();
    public List<ParsedInstallment> Installments { get; init; } = new();
    public List<ParsedPayment> Payments { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
}

public sealed class ParsedParty
{
    public string? TaxId { get; init; }
    public string? LegalName { get; init; }
    public string? TradeName { get; init; }
    public string? StateRegistration { get; init; }
    public string? StateRegistrationIndicator { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public ParsedAddress Address { get; init; } = new();
}

public sealed class ParsedAddress
{
    public string? Street { get; init; }
    public string? Number { get; init; }
    public string? Complement { get; init; }
    public string? District { get; init; }
    public string? City { get; init; }
    public string? CityIbgeCode { get; init; }
    public string? State { get; init; }
    public string? PostalCode { get; init; }
    public string? Country { get; init; }
    public string? CountryCode { get; init; }
}

public sealed class ParsedFiscalItem
{
    public int ItemNumber { get; init; }
    public string? SourceProductCode { get; init; }
    public string? CommercialGtin { get; init; }
    public string Description { get; init; } = string.Empty;
    public string? Ncm { get; init; }
    public string? Cest { get; init; }
    public string? Cfop { get; init; }
    public string? CommercialUnit { get; init; }
    public decimal CommercialQuantity { get; init; }
    public decimal CommercialUnitPrice { get; init; }
    public decimal ProductAmount { get; init; }
    public string? TaxableGtin { get; init; }
    public string? TaxableUnit { get; init; }
    public decimal? TaxableQuantity { get; init; }
    public decimal? TaxableUnitPrice { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal OtherExpensesAmount { get; init; }
}

public sealed record ParsedInstallment(string? Number, DateTimeOffset? DueDate, decimal Amount);
public sealed record ParsedPayment(string MethodCode, string? MethodDescription, decimal? Amount, string? PaymentIndicator, string? IntegrationType, string? AcquirerTaxId, string? CardBrandCode, string? AuthorizationCode);

public sealed record ImportFolderRequest(string FolderPath, Guid? CompanyId = null, bool Recursive = true, string? RequestedBy = null);
public sealed record ImportFilesRequest(IReadOnlyCollection<string> FilePaths, Guid? CompanyId = null, string? SourceDescription = null, string? RequestedBy = null, bool DeleteFilesAfterImport = false);
public sealed record ImportProgressDto(long Discovered, long Processed, long Imported, long Ignored, long Duplicated, long Errors, string? CurrentFile);
public sealed record ImportSummaryDto(Guid BatchId, long Discovered, long Processed, long Imported, long Ignored, long Duplicated, long Errors, DateTimeOffset StartedAt, DateTimeOffset CompletedAt);
public sealed record CompanyListItemDto(Guid Id, string TaxId, string LegalName, string? TradeName, bool IsActive = true);
public sealed record CompanyUpsertRequest(Guid? Id, string TaxId, string LegalName, string? TradeName, bool IsActive = true);
public sealed record DashboardDto(long XmlProcessed, long FiscalDocuments, long Customers, long Suppliers, long UniqueProducts, long ProductsWithGtin, long ProductsWithoutGtin, long ProductsAwaitingReview, long AccountsPayable, long AccountsReceivable, long ImportErrors);
public sealed record ProductReviewItemDto(Guid SuggestionId, Guid SourceProductId, string SourceDescription, string? SourceGtin, string? SourceNcm, Guid CandidateProductId, string CandidateDescription, string? CandidateGtin, string? CandidateNcm, decimal Score, decimal DescriptionSimilarity, string Reason);
public sealed record ProductReviewDecisionRequest(Guid SuggestionId, ProductMatchDecisionType Decision, string? DecidedBy = null, string? Notes = null);
public sealed record SearchRequest(string? Term = null, DateTimeOffset? StartDate = null, DateTimeOffset? EndDate = null, int Limit = 100);
public sealed record SearchResultDto(string Category, Guid Id, string PrimaryText, string? SecondaryText, DateTimeOffset? Date, decimal? Amount);
public enum ExportDataset { Customers, Suppliers, Products, FiscalDocuments, FiscalDocumentItems, AccountsPayable, AccountsReceivable, ProductsAwaitingReview }
public enum ExportFormat { Csv, Xlsx }
public sealed record ExportFileDto(byte[] Content, string ContentType, string FileName);

public enum ProductMatchRecommendation { AutomaticSameProduct, ManualReview, DifferentProducts }
public sealed record ProductMatchInput(string? Gtin, string NormalizedDescription, string? Ncm, string? Unit, decimal? NetContent = null, string? NetContentUnit = null);
public sealed record ProductMatchResult(decimal Score, decimal DescriptionSimilarity, bool? GtinMatches, bool? NcmMatches, bool? UnitMatches, bool? MeasureMatches, ProductMatchRecommendation Recommendation, string Reason);
public sealed record GtinNormalizationResult(string? Value, bool IsValid, string? Reason);
public sealed record PartyResolution(Company? Company, DocumentDirection Direction, bool IsIntercompany, string Reason);
