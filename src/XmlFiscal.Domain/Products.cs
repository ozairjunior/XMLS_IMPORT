namespace XmlFiscal.Domain;

public sealed class Product : Entity
{
    public string? Gtin { get; set; }
    public string ConsolidatedDescription { get; set; } = string.Empty;
    public string NormalizedDescription { get; set; } = string.Empty;
    public string? Ncm { get; set; }
    public string? CommercialUnit { get; set; }
    public string? Brand { get; set; }
    public decimal? NetContent { get; set; }
    public string? NetContentUnit { get; set; }
    public bool RequiresReview { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? MergedIntoProductId { get; set; }
    public Product? MergedIntoProduct { get; set; }
    public ICollection<ProductAlias> Aliases { get; set; } = new List<ProductAlias>();
}

public sealed class ProductAlias : Entity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public Guid ImportedFileId { get; set; }
    public ImportedFile ImportedFile { get; set; } = null!;
    public Guid? FiscalDocumentItemId { get; set; }
    public FiscalDocumentItem? FiscalDocumentItem { get; set; }
    public string? SourceIssuerTaxId { get; set; }
    public DateTimeOffset? SourceDate { get; set; }
    public string? SourceCode { get; set; }
    public string OriginalDescription { get; set; } = string.Empty;
    public string NormalizedDescription { get; set; } = string.Empty;
    public string? OriginalGtin { get; set; }
    public string? NormalizedGtin { get; set; }
    public string? Ncm { get; set; }
    public string? Unit { get; set; }
}

public sealed class ProductMatchSuggestion : Entity
{
    public Guid SourceProductId { get; set; }
    public Product SourceProduct { get; set; } = null!;
    public Guid CandidateProductId { get; set; }
    public Product CandidateProduct { get; set; } = null!;
    public decimal Score { get; set; }
    public decimal DescriptionSimilarity { get; set; }
    public bool? GtinMatches { get; set; }
    public bool? NcmMatches { get; set; }
    public bool? UnitMatches { get; set; }
    public string Reason { get; set; } = string.Empty;
    public ProductMatchStatus Status { get; set; } = ProductMatchStatus.Pending;
}

public sealed class ProductMatchDecision : Entity
{
    public Guid ProductMatchSuggestionId { get; set; }
    public ProductMatchSuggestion ProductMatchSuggestion { get; set; } = null!;
    public ProductMatchDecisionType DecisionType { get; set; }
    public Guid SourceProductId { get; set; }
    public Guid? TargetProductId { get; set; }
    public string? DecidedBy { get; set; }
    public DateTimeOffset DecidedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? Notes { get; set; }
}
