using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using XmlFiscal.Application;
using XmlFiscal.Domain;
using XmlFiscal.Infrastructure;
using XmlFiscal.Infrastructure.Persistence;

namespace XmlFiscal.Tests;

public sealed class ImportIntegrationTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private TestDbContextFactory _factory = null!;
    private string _tempRoot = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        var options = new DbContextOptionsBuilder<XmlFiscalDbContext>().UseSqlite(_connection).Options;
        _factory = new TestDbContextFactory(options);
        await using var db = _factory.CreateDbContext();
        await db.Database.EnsureCreatedAsync();
        db.Companies.Add(new Company { TaxId = "98765432000110", LegalName = "Mercado Exemplo Ltda." });
        await db.SaveChangesAsync();
        _tempRoot = Path.Combine(Path.GetTempPath(), "XmlFiscalTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public async Task Import_is_transactional_per_file_and_duplicate_safe()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "TestData", "nfe-inbound.xml");
        var copy = Path.Combine(_tempRoot, "nfe.xml");
        File.Copy(source, copy);
        var importOptions = new ImportOptions { ArchiveRootPath = Path.Combine(_tempRoot, "archive"), ParserParallelism = 2, ProgressPersistenceInterval = 1 };
        var description = new ProductDescriptionNormalizer();
        var matchingOptions = new ProductMatchingOptions();
        var service = new ImportService(
            _factory,
            new FiscalXmlParser(new XmlDocumentDetector(), new NFeParser(), new XmlSecurityOptions()),
            new PartyResolver(),
            new ProductConsolidationService(new GtinNormalizer(), description, new ProductMatchingService(matchingOptions), matchingOptions),
            importOptions);

        var first = await service.ImportFilesAsync(new ImportFilesRequest([copy]));
        var second = await service.ImportFilesAsync(new ImportFilesRequest([copy]));

        Assert.Equal(1, first.Imported);
        Assert.Equal(1, second.Duplicated);
        await using var db = _factory.CreateDbContext();
        Assert.Equal(1, await db.FiscalDocuments.CountAsync());
        Assert.Equal(1, await db.Suppliers.CountAsync());
        Assert.Equal(2, await db.Products.CountAsync());
        Assert.Equal(1, await db.AccountsPayable.CountAsync());
        Assert.Equal(3, await db.Installments.CountAsync());
        Assert.All(await db.Installments.AsNoTracking().ToListAsync(), installment =>
            Assert.Equal("15", installment.PaymentMethodCode));
        Assert.Equal(2, await db.ImportedFiles.CountAsync());
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, true); } catch { }
    }

    private sealed class TestDbContextFactory(DbContextOptions<XmlFiscalDbContext> options) : IDbContextFactory<XmlFiscalDbContext>
    {
        public XmlFiscalDbContext CreateDbContext() => new(options);
        public Task<XmlFiscalDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }
}
