using XmlFiscal.Domain;

namespace XmlFiscal.Application;

public interface IFiscalXmlParser { Task<ParsedFiscalDocument> ParseAsync(Stream stream, CancellationToken cancellationToken = default); }
public interface IImportService
{
    Task<ImportSummaryDto> ImportFolderAsync(ImportFolderRequest request, IProgress<ImportProgressDto>? progress = null, CancellationToken cancellationToken = default);
    Task<ImportSummaryDto> ImportFilesAsync(ImportFilesRequest request, IProgress<ImportProgressDto>? progress = null, CancellationToken cancellationToken = default);
}
public interface IGtinNormalizer { GtinNormalizationResult Normalize(string? rawValue); }
public interface IProductDescriptionNormalizer { string Normalize(string? description); }
public interface IProductMatchingService { ProductMatchResult Compare(ProductMatchInput source, ProductMatchInput candidate); }
public interface IPartyResolver { PartyResolution Resolve(IReadOnlyCollection<Company> companies, ParsedParty issuer, ParsedParty recipient, Guid? preferredCompanyId = null); }
public interface IDashboardQueryService { Task<DashboardDto> GetAsync(CancellationToken cancellationToken = default); }
public interface ICompanyService
{
    Task<IReadOnlyList<CompanyListItemDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<CompanyListItemDto> SaveAsync(CompanyUpsertRequest request, CancellationToken cancellationToken = default);
}
public interface IProductReviewService
{
    Task<IReadOnlyList<ProductReviewItemDto>> GetPendingAsync(int limit = 200, CancellationToken cancellationToken = default);
    Task DecideAsync(ProductReviewDecisionRequest request, CancellationToken cancellationToken = default);
}
public interface ISearchQueryService { Task<IReadOnlyList<SearchResultDto>> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default); }
public interface IExportService { Task<ExportFileDto> ExportAsync(ExportDataset dataset, ExportFormat format, CancellationToken cancellationToken = default); }
