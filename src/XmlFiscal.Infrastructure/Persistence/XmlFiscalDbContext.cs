using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using XmlFiscal.Domain;

namespace XmlFiscal.Infrastructure.Persistence;

public sealed class XmlFiscalDbContext : DbContext
{
    public XmlFiscalDbContext(DbContextOptions<XmlFiscalDbContext> options) : base(options) { }
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductAlias> ProductAliases => Set<ProductAlias>();
    public DbSet<ProductMatchSuggestion> ProductMatchSuggestions => Set<ProductMatchSuggestion>();
    public DbSet<ProductMatchDecision> ProductMatchDecisions => Set<ProductMatchDecision>();
    public DbSet<FiscalDocument> FiscalDocuments => Set<FiscalDocument>();
    public DbSet<FiscalDocumentItem> FiscalDocumentItems => Set<FiscalDocumentItem>();
    public DbSet<AccountPayable> AccountsPayable => Set<AccountPayable>();
    public DbSet<AccountReceivable> AccountsReceivable => Set<AccountReceivable>();
    public DbSet<Installment> Installments => Set<Installment>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<ImportedFile> ImportedFiles => Set<ImportedFile>();
    public DbSet<ImportError> ImportErrors => Set<ImportError>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTimeOffset>()
            .HaveConversion<DateTimeOffsetToUtcTicksConverter>();
        base.ConfigureConventions(configurationBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureCompany(modelBuilder.Entity<Company>());
        ConfigureCustomer(modelBuilder.Entity<Customer>());
        ConfigureSupplier(modelBuilder.Entity<Supplier>());
        ConfigureProduct(modelBuilder.Entity<Product>());
        ConfigureProductAlias(modelBuilder.Entity<ProductAlias>());
        ConfigureMatching(modelBuilder);
        ConfigureFiscalDocument(modelBuilder.Entity<FiscalDocument>());
        ConfigureFiscalItem(modelBuilder.Entity<FiscalDocumentItem>());
        ConfigureFinancial(modelBuilder);
        ConfigureImport(modelBuilder);
        ConfigureAudit(modelBuilder.Entity<AuditLog>());
        base.OnModelCreating(modelBuilder);
    }

    private static void ConfigureCompany(EntityTypeBuilder<Company> e)
    {
        e.ToTable("Companies"); e.HasKey(x => x.Id);
        e.Property(x => x.TaxId).HasMaxLength(14).IsRequired();
        e.Property(x => x.LegalName).HasMaxLength(200).IsRequired();
        e.Property(x => x.TradeName).HasMaxLength(200);
        e.HasIndex(x => x.TaxId).IsUnique();
    }
    private static void ConfigureCustomer(EntityTypeBuilder<Customer> e)
    {
        e.ToTable("Customers"); e.HasKey(x => x.Id);
        e.Property(x => x.TaxId).HasMaxLength(14); e.Property(x => x.DeduplicationKey).HasMaxLength(500).IsRequired();
        e.Property(x => x.LegalName).HasMaxLength(200).IsRequired(); e.Property(x => x.TradeName).HasMaxLength(200);
        e.Property(x => x.StateRegistration).HasMaxLength(30); e.Property(x => x.StateRegistrationIndicator).HasMaxLength(10);
        e.Property(x => x.Phone).HasMaxLength(30); e.Property(x => x.Email).HasMaxLength(200);
        e.HasIndex(x => x.TaxId); e.HasIndex(x => new { x.CompanyId, x.DeduplicationKey }).IsUnique();
        e.HasOne(x => x.Company).WithMany(x => x.Customers).HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        ConfigureAddress(e.OwnsOne(x => x.Address));
    }
    private static void ConfigureSupplier(EntityTypeBuilder<Supplier> e)
    {
        e.ToTable("Suppliers"); e.HasKey(x => x.Id);
        e.Property(x => x.TaxId).HasMaxLength(14); e.Property(x => x.DeduplicationKey).HasMaxLength(500).IsRequired();
        e.Property(x => x.LegalName).HasMaxLength(200).IsRequired(); e.Property(x => x.TradeName).HasMaxLength(200);
        e.Property(x => x.StateRegistration).HasMaxLength(30); e.Property(x => x.Phone).HasMaxLength(30); e.Property(x => x.Email).HasMaxLength(200);
        e.HasIndex(x => x.TaxId); e.HasIndex(x => new { x.CompanyId, x.DeduplicationKey }).IsUnique();
        e.HasOne(x => x.Company).WithMany(x => x.Suppliers).HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        ConfigureAddress(e.OwnsOne(x => x.Address));
    }
    private static void ConfigureAddress<TOwner>(OwnedNavigationBuilder<TOwner, PostalAddress> e) where TOwner : class
    {
        e.Property(x => x.Street).HasColumnName("AddressStreet").HasMaxLength(200);
        e.Property(x => x.Number).HasColumnName("AddressNumber").HasMaxLength(30);
        e.Property(x => x.Complement).HasColumnName("AddressComplement").HasMaxLength(100);
        e.Property(x => x.District).HasColumnName("AddressDistrict").HasMaxLength(100);
        e.Property(x => x.City).HasColumnName("AddressCity").HasMaxLength(100);
        e.Property(x => x.CityIbgeCode).HasColumnName("AddressCityIbgeCode").HasMaxLength(10);
        e.Property(x => x.State).HasColumnName("AddressState").HasMaxLength(2);
        e.Property(x => x.PostalCode).HasColumnName("AddressPostalCode").HasMaxLength(10);
        e.Property(x => x.Country).HasColumnName("AddressCountry").HasMaxLength(100);
        e.Property(x => x.CountryCode).HasColumnName("AddressCountryCode").HasMaxLength(10);
    }
    private static void ConfigureProduct(EntityTypeBuilder<Product> e)
    {
        e.ToTable("Products"); e.HasKey(x => x.Id);
        e.Property(x => x.Gtin).HasMaxLength(14); e.Property(x => x.ConsolidatedDescription).HasMaxLength(500).IsRequired();
        e.Property(x => x.NormalizedDescription).HasMaxLength(500).IsRequired(); e.Property(x => x.Ncm).HasMaxLength(10);
        e.Property(x => x.CommercialUnit).HasMaxLength(20); e.Property(x => x.Brand).HasMaxLength(100);
        e.Property(x => x.NetContent).HasPrecision(18, 6); e.Property(x => x.NetContentUnit).HasMaxLength(10);
        e.HasIndex(x => x.Gtin); e.HasIndex(x => x.Ncm); e.HasIndex(x => x.NormalizedDescription);
        e.HasOne(x => x.MergedIntoProduct).WithMany().HasForeignKey(x => x.MergedIntoProductId).OnDelete(DeleteBehavior.Restrict);
    }
    private static void ConfigureProductAlias(EntityTypeBuilder<ProductAlias> e)
    {
        e.ToTable("ProductAliases"); e.HasKey(x => x.Id);
        e.Property(x => x.SourceIssuerTaxId).HasMaxLength(14); e.Property(x => x.SourceCode).HasMaxLength(100);
        e.Property(x => x.OriginalDescription).HasMaxLength(500).IsRequired(); e.Property(x => x.NormalizedDescription).HasMaxLength(500).IsRequired();
        e.Property(x => x.OriginalGtin).HasMaxLength(30); e.Property(x => x.NormalizedGtin).HasMaxLength(14);
        e.Property(x => x.Ncm).HasMaxLength(10); e.Property(x => x.Unit).HasMaxLength(20);
        e.HasIndex(x => x.NormalizedGtin); e.HasIndex(x => x.SourceIssuerTaxId);
        e.HasOne(x => x.Product).WithMany(x => x.Aliases).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.ImportedFile).WithMany().HasForeignKey(x => x.ImportedFileId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.FiscalDocumentItem).WithMany().HasForeignKey(x => x.FiscalDocumentItemId).OnDelete(DeleteBehavior.Restrict);
    }
    private static void ConfigureMatching(ModelBuilder modelBuilder)
    {
        var s = modelBuilder.Entity<ProductMatchSuggestion>();
        s.ToTable("ProductMatchSuggestions", t => t.HasCheckConstraint("CK_ProductMatchSuggestion_Different", "SourceProductId <> CandidateProductId")); s.HasKey(x => x.Id);
        s.Property(x => x.Score).HasConversion<double>();
        s.Property(x => x.DescriptionSimilarity).HasConversion<double>();
        s.Property(x => x.Reason).HasMaxLength(1000);
        s.HasIndex(x => x.Status); s.HasOne(x => x.SourceProduct).WithMany().HasForeignKey(x => x.SourceProductId).OnDelete(DeleteBehavior.Restrict);
        s.HasOne(x => x.CandidateProduct).WithMany().HasForeignKey(x => x.CandidateProductId).OnDelete(DeleteBehavior.Restrict);
        var d = modelBuilder.Entity<ProductMatchDecision>();
        d.ToTable("ProductMatchDecisions"); d.HasKey(x => x.Id); d.Property(x => x.DecidedBy).HasMaxLength(200); d.Property(x => x.Notes).HasMaxLength(2000);
        d.HasIndex(x => x.ProductMatchSuggestionId).IsUnique(); d.HasIndex(x => x.DecidedAt);
        d.HasOne(x => x.ProductMatchSuggestion).WithMany().HasForeignKey(x => x.ProductMatchSuggestionId).OnDelete(DeleteBehavior.Cascade);
    }
    private static void ConfigureFiscalDocument(EntityTypeBuilder<FiscalDocument> e)
    {
        e.ToTable("FiscalDocuments"); e.HasKey(x => x.Id);
        e.Property(x => x.AccessKey).HasMaxLength(44).IsRequired(); e.Property(x => x.Series).HasMaxLength(10); e.Property(x => x.Number).HasMaxLength(20);
        e.Property(x => x.OperationNature).HasMaxLength(200); e.Property(x => x.IssuerTaxId).HasMaxLength(14); e.Property(x => x.IssuerName).HasMaxLength(200);
        e.Property(x => x.RecipientTaxId).HasMaxLength(14); e.Property(x => x.RecipientName).HasMaxLength(200);
        e.Property(x => x.AuthorizationProtocol).HasMaxLength(50); e.Property(x => x.AuthorizationStatusCode).HasMaxLength(10);
        e.Property(x => x.ProductsAmount).HasPrecision(18, 2); e.Property(x => x.DiscountAmount).HasPrecision(18, 2); e.Property(x => x.FreightAmount).HasPrecision(18, 2);
        e.Property(x => x.InsuranceAmount).HasPrecision(18, 2); e.Property(x => x.OtherExpensesAmount).HasPrecision(18, 2); e.Property(x => x.TaxesAmount).HasPrecision(18, 2); e.Property(x => x.TotalAmount).HasPrecision(18, 2);
        e.HasIndex(x => x.AccessKey).IsUnique(); e.HasIndex(x => x.IssuedAt); e.HasIndex(x => x.Number); e.HasIndex(x => x.IssuerTaxId); e.HasIndex(x => x.RecipientTaxId);
        e.HasOne(x => x.ImportedFile).WithOne(x => x.FiscalDocument).HasForeignKey<FiscalDocument>(x => x.ImportedFileId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
    }
    private static void ConfigureFiscalItem(EntityTypeBuilder<FiscalDocumentItem> e)
    {
        e.ToTable("FiscalDocumentItems"); e.HasKey(x => x.Id);
        e.Property(x => x.SourceProductCode).HasMaxLength(100); e.Property(x => x.CommercialGtin).HasMaxLength(30); e.Property(x => x.Description).HasMaxLength(500).IsRequired();
        e.Property(x => x.Ncm).HasMaxLength(10); e.Property(x => x.Cest).HasMaxLength(10); e.Property(x => x.Cfop).HasMaxLength(10); e.Property(x => x.CommercialUnit).HasMaxLength(20);
        e.Property(x => x.TaxableGtin).HasMaxLength(30); e.Property(x => x.TaxableUnit).HasMaxLength(20);
        e.Property(x => x.CommercialQuantity).HasPrecision(18, 6); e.Property(x => x.CommercialUnitPrice).HasPrecision(18, 10); e.Property(x => x.ProductAmount).HasPrecision(18, 2);
        e.Property(x => x.TaxableQuantity).HasPrecision(18, 6); e.Property(x => x.TaxableUnitPrice).HasPrecision(18, 10); e.Property(x => x.DiscountAmount).HasPrecision(18, 2); e.Property(x => x.OtherExpensesAmount).HasPrecision(18, 2);
        e.HasIndex(x => new { x.FiscalDocumentId, x.ItemNumber }).IsUnique(); e.HasIndex(x => x.Ncm);
        e.HasOne(x => x.FiscalDocument).WithMany(x => x.Items).HasForeignKey(x => x.FiscalDocumentId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
    private static void ConfigureFinancial(ModelBuilder modelBuilder)
    {
        var ap = modelBuilder.Entity<AccountPayable>(); ap.ToTable("AccountsPayable"); ap.HasKey(x => x.Id); ap.Property(x => x.TotalAmount).HasPrecision(18, 2);
        ap.HasIndex(x => x.FiscalDocumentId).IsUnique(); ap.HasIndex(x => x.IssueDate); ap.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        ap.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict); ap.HasOne(x => x.FiscalDocument).WithOne().HasForeignKey<AccountPayable>(x => x.FiscalDocumentId).OnDelete(DeleteBehavior.Cascade);
        var ar = modelBuilder.Entity<AccountReceivable>(); ar.ToTable("AccountsReceivable"); ar.HasKey(x => x.Id); ar.Property(x => x.TotalAmount).HasPrecision(18, 2);
        ar.HasIndex(x => x.FiscalDocumentId).IsUnique(); ar.HasIndex(x => x.IssueDate); ar.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        ar.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict); ar.HasOne(x => x.FiscalDocument).WithOne().HasForeignKey<AccountReceivable>(x => x.FiscalDocumentId).OnDelete(DeleteBehavior.Cascade);
        var i = modelBuilder.Entity<Installment>(); i.ToTable("Installments", t => t.HasCheckConstraint("CK_Installment_OneOwner", "(AccountPayableId IS NOT NULL AND AccountReceivableId IS NULL) OR (AccountPayableId IS NULL AND AccountReceivableId IS NOT NULL)")); i.HasKey(x => x.Id);
        i.Property(x => x.Amount).HasPrecision(18, 2); i.HasIndex(x => x.DueDate); i.HasOne(x => x.AccountPayable).WithMany(x => x.Installments).HasForeignKey(x => x.AccountPayableId).OnDelete(DeleteBehavior.Cascade);
        i.HasOne(x => x.AccountReceivable).WithMany(x => x.Installments).HasForeignKey(x => x.AccountReceivableId).OnDelete(DeleteBehavior.Cascade);
        var p = modelBuilder.Entity<Payment>(); p.ToTable("Payments"); p.HasKey(x => x.Id); p.Property(x => x.Amount).HasPrecision(18, 2); p.Property(x => x.MethodCode).HasMaxLength(10);
        p.HasOne(x => x.FiscalDocument).WithMany(x => x.Payments).HasForeignKey(x => x.FiscalDocumentId).OnDelete(DeleteBehavior.Cascade);
    }
    private static void ConfigureImport(ModelBuilder modelBuilder)
    {
        var b = modelBuilder.Entity<ImportBatch>(); b.ToTable("ImportBatches"); b.HasKey(x => x.Id); b.Property(x => x.SourceDescription).HasMaxLength(500); b.Property(x => x.RootPath).HasMaxLength(2000); b.HasIndex(x => x.StartedAt);
        b.HasOne(x => x.RequestedCompany).WithMany().HasForeignKey(x => x.RequestedCompanyId).OnDelete(DeleteBehavior.Restrict);
        var f = modelBuilder.Entity<ImportedFile>(); f.ToTable("ImportedFiles"); f.HasKey(x => x.Id); f.Property(x => x.FileName).HasMaxLength(500); f.Property(x => x.SourcePath).HasMaxLength(2000); f.Property(x => x.ArchivedPath).HasMaxLength(2000); f.Property(x => x.Sha256).HasMaxLength(64);
        f.HasIndex(x => x.Sha256); f.HasIndex(x => x.ProcessedAt); f.HasOne(x => x.ImportBatch).WithMany().HasForeignKey(x => x.ImportBatchId).OnDelete(DeleteBehavior.Cascade);
        var e = modelBuilder.Entity<ImportError>(); e.ToTable("ImportErrors"); e.HasKey(x => x.Id); e.Property(x => x.Code).HasMaxLength(100); e.Property(x => x.Message).HasMaxLength(2000); e.Property(x => x.Details).HasMaxLength(8000); e.HasIndex(x => x.CreatedAt);
        e.HasOne(x => x.ImportBatch).WithMany().HasForeignKey(x => x.ImportBatchId).OnDelete(DeleteBehavior.Cascade); e.HasOne(x => x.ImportedFile).WithMany().HasForeignKey(x => x.ImportedFileId).OnDelete(DeleteBehavior.Cascade);
    }
    private static void ConfigureAudit(EntityTypeBuilder<AuditLog> e)
    {
        e.ToTable("AuditLogs"); e.HasKey(x => x.Id); e.Property(x => x.Category).HasMaxLength(100); e.Property(x => x.Action).HasMaxLength(100); e.Property(x => x.DataJson).HasMaxLength(16000); e.HasIndex(x => x.OccurredAt); e.HasIndex(x => new { x.EntityType, x.EntityId });
    }
}

public sealed class DesignTimeDbContextFactory : Microsoft.EntityFrameworkCore.Design.IDesignTimeDbContextFactory<XmlFiscalDbContext>
{
    public XmlFiscalDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<XmlFiscalDbContext>().UseSqlite("Data Source=xmlfiscal-design.db").Options);
}

public sealed class DateTimeOffsetToUtcTicksConverter : ValueConverter<DateTimeOffset, long>
{
    public DateTimeOffsetToUtcTicksConverter()
        : base(
            value => value.UtcDateTime.Ticks,
            value => new DateTimeOffset(value, TimeSpan.Zero))
    {
    }
}
