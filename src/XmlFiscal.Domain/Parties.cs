namespace XmlFiscal.Domain;

public sealed class Company : Entity
{
    public string TaxId { get; set; } = string.Empty;
    public string LegalName { get; set; } = string.Empty;
    public string? TradeName { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<Customer> Customers { get; set; } = new List<Customer>();
    public ICollection<Supplier> Suppliers { get; set; } = new List<Supplier>();
}

public sealed class Customer : Entity
{
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public string? TaxId { get; set; }
    public string DeduplicationKey { get; set; } = string.Empty;
    public string LegalName { get; set; } = string.Empty;
    public string? TradeName { get; set; }
    public string? StateRegistration { get; set; }
    public string? StateRegistrationIndicator { get; set; }
    public PostalAddress Address { get; set; } = new();
    public string? Phone { get; set; }
    public string? Email { get; set; }
}

public sealed class Supplier : Entity
{
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public string? TaxId { get; set; }
    public string DeduplicationKey { get; set; } = string.Empty;
    public string LegalName { get; set; } = string.Empty;
    public string? TradeName { get; set; }
    public string? StateRegistration { get; set; }
    public PostalAddress Address { get; set; } = new();
    public string? Phone { get; set; }
    public string? Email { get; set; }
}
