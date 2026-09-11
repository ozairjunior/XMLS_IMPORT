namespace XmlFiscal.Domain;

public sealed class AccountPayable : Entity
{
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public Guid SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public Guid FiscalDocumentId { get; set; }
    public FiscalDocument FiscalDocument { get; set; } = null!;
    public DateTimeOffset? IssueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public FinancialInformationStatus InformationStatus { get; set; }
    public string? SourceStatus { get; set; }
    public ICollection<Installment> Installments { get; set; } = new List<Installment>();
}

public sealed class AccountReceivable : Entity
{
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public Guid FiscalDocumentId { get; set; }
    public FiscalDocument FiscalDocument { get; set; } = null!;
    public DateTimeOffset? IssueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public FinancialInformationStatus InformationStatus { get; set; }
    public string? SourceStatus { get; set; }
    public ICollection<Installment> Installments { get; set; } = new List<Installment>();
}

public sealed class Installment : Entity
{
    public Guid? AccountPayableId { get; set; }
    public AccountPayable? AccountPayable { get; set; }
    public Guid? AccountReceivableId { get; set; }
    public AccountReceivable? AccountReceivable { get; set; }
    public string? Number { get; set; }
    public DateTimeOffset? DueDate { get; set; }
    public decimal Amount { get; set; }
    public string? PaymentMethodCode { get; set; }
    public string? SourceStatus { get; set; }
}

public sealed class Payment : Entity
{
    public Guid FiscalDocumentId { get; set; }
    public FiscalDocument FiscalDocument { get; set; } = null!;
    public string MethodCode { get; set; } = string.Empty;
    public string? MethodDescription { get; set; }
    public decimal? Amount { get; set; }
    public string? PaymentIndicator { get; set; }
    public string? IntegrationType { get; set; }
    public string? AcquirerTaxId { get; set; }
    public string? CardBrandCode { get; set; }
    public string? AuthorizationCode { get; set; }
}
