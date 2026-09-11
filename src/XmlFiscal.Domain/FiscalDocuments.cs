namespace XmlFiscal.Domain;

public sealed class FiscalDocument : Entity
{
    public Guid ImportedFileId { get; set; }
    public ImportedFile ImportedFile { get; set; } = null!;
    public Guid? CompanyId { get; set; }
    public Company? Company { get; set; }
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public string AccessKey { get; set; } = string.Empty;
    public DocumentKind Kind { get; set; }
    public DocumentDirection Direction { get; set; }
    public bool IsIntercompany { get; set; }
    public int? Model { get; set; }
    public string? Series { get; set; }
    public string? Number { get; set; }
    public DateTimeOffset? IssuedAt { get; set; }
    public string? OperationNature { get; set; }
    public int? OperationType { get; set; }
    public int? Purpose { get; set; }
    public string? IssuerTaxId { get; set; }
    public string? IssuerName { get; set; }
    public string? RecipientTaxId { get; set; }
    public string? RecipientName { get; set; }
    public decimal ProductsAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FreightAmount { get; set; }
    public decimal InsuranceAmount { get; set; }
    public decimal OtherExpensesAmount { get; set; }
    public decimal TaxesAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? AuthorizationProtocol { get; set; }
    public DateTimeOffset? AuthorizedAt { get; set; }
    public string? AuthorizationStatusCode { get; set; }
    public FinancialInformationStatus FinancialInformationStatus { get; set; }
    public ICollection<FiscalDocumentItem> Items { get; set; } = new List<FiscalDocumentItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

public sealed class FiscalDocumentItem : Entity
{
    public Guid FiscalDocumentId { get; set; }
    public FiscalDocument FiscalDocument { get; set; } = null!;
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int ItemNumber { get; set; }
    public string? SourceProductCode { get; set; }
    public string? CommercialGtin { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Ncm { get; set; }
    public string? Cest { get; set; }
    public string? Cfop { get; set; }
    public string? CommercialUnit { get; set; }
    public decimal CommercialQuantity { get; set; }
    public decimal CommercialUnitPrice { get; set; }
    public decimal ProductAmount { get; set; }
    public string? TaxableGtin { get; set; }
    public string? TaxableUnit { get; set; }
    public decimal? TaxableQuantity { get; set; }
    public decimal? TaxableUnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal OtherExpensesAmount { get; set; }
}
