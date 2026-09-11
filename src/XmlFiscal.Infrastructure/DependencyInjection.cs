using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XmlFiscal.Application;
using XmlFiscal.Infrastructure.Persistence;

namespace XmlFiscal.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddXmlFiscalInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string contentRootPath)
    {
        var connectionString = MakeConnectionStringAbsolute(
            configuration.GetConnectionString("XmlFiscal") ?? "Data Source=App_Data/xmlfiscal.db",
            contentRootPath);
        var sqlite = new SqliteConnectionStringBuilder(connectionString)
        {
            ForeignKeys = true,
            Pooling = true
        };
        connectionString = sqlite.ToString();
        if (!string.IsNullOrWhiteSpace(sqlite.DataSource) && sqlite.DataSource != ":memory:")
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(sqlite.DataSource))!);

        var matchingOptions = new ProductMatchingOptions();
        configuration.GetSection("XmlFiscal:ProductMatching").Bind(matchingOptions);
        var importOptions = new ImportOptions();
        configuration.GetSection("XmlFiscal:Import").Bind(importOptions);
        if (!Path.IsPathRooted(importOptions.ArchiveRootPath))
            importOptions.ArchiveRootPath = Path.GetFullPath(Path.Combine(contentRootPath, importOptions.ArchiveRootPath));
        Directory.CreateDirectory(importOptions.ArchiveRootPath);
        var securityOptions = new XmlSecurityOptions();
        configuration.GetSection("XmlFiscal:XmlSecurity").Bind(securityOptions);

        services.AddSingleton(matchingOptions);
        services.AddSingleton(importOptions);
        services.AddSingleton(securityOptions);
        services.AddPooledDbContextFactory<XmlFiscalDbContext>(options =>
        {
            options.UseSqlite(connectionString);
            options.EnableDetailedErrors();
        });

        services.AddSingleton<IGtinNormalizer, GtinNormalizer>();
        services.AddSingleton<IProductDescriptionNormalizer, ProductDescriptionNormalizer>();
        services.AddSingleton<IProductMatchingService, ProductMatchingService>();
        services.AddSingleton<IPartyResolver, PartyResolver>();
        services.AddSingleton<XmlDocumentDetector>();
        services.AddSingleton<NFeParser>();
        services.AddSingleton<IFiscalXmlParser, FiscalXmlParser>();
        services.AddSingleton<ProductConsolidationService>();
        services.AddSingleton<IImportService, ImportService>();
        services.AddSingleton<IDashboardQueryService, DashboardQueryService>();
        services.AddSingleton<ICompanyService, CompanyService>();
        services.AddSingleton<IProductReviewService, ProductReviewService>();
        services.AddSingleton<ISearchQueryService, SearchQueryService>();
        services.AddSingleton<IExportService, ExportService>();
        services.AddSingleton<DatabaseInitializer>();
        return services;
    }

    private static string MakeConnectionStringAbsolute(string connectionString, string contentRootPath)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.DataSource) || builder.DataSource == ":memory:" || Path.IsPathRooted(builder.DataSource))
            return builder.ToString();
        builder.DataSource = Path.GetFullPath(Path.Combine(contentRootPath, builder.DataSource));
        return builder.ToString();
    }
}

public sealed class DatabaseInitializer(IDbContextFactory<XmlFiscalDbContext> dbFactory)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode = WAL;", cancellationToken);
    }
}
