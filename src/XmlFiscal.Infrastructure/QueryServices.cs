using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using XmlFiscal.Application;
using XmlFiscal.Domain;
using XmlFiscal.Infrastructure.Persistence;

namespace XmlFiscal.Infrastructure;

public sealed class DashboardQueryService(IDbContextFactory<XmlFiscalDbContext> dbFactory) : IDashboardQueryService
{
    public async Task<DashboardDto> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return new DashboardDto(
            await db.ImportedFiles.LongCountAsync(x => x.Status == ImportedFileStatus.Imported, cancellationToken),
            await db.FiscalDocuments.LongCountAsync(cancellationToken),
            await db.Customers.LongCountAsync(cancellationToken),
            await db.Suppliers.LongCountAsync(cancellationToken),
            await db.Products.LongCountAsync(x => x.IsActive && x.MergedIntoProductId == null, cancellationToken),
            await db.Products.LongCountAsync(x => x.IsActive && x.MergedIntoProductId == null && x.Gtin != null, cancellationToken),
            await db.Products.LongCountAsync(x => x.IsActive && x.MergedIntoProductId == null && x.Gtin == null, cancellationToken),
            await db.ProductMatchSuggestions.LongCountAsync(x => x.Status == ProductMatchStatus.Pending, cancellationToken),
            await db.AccountsPayable.LongCountAsync(cancellationToken),
            await db.AccountsReceivable.LongCountAsync(cancellationToken),
            await db.ImportErrors.LongCountAsync(x => x.Severity == AuditSeverity.Error, cancellationToken));
    }
}

public sealed class CompanyService(IDbContextFactory<XmlFiscalDbContext> dbFactory) : ICompanyService
{
    public async Task<IReadOnlyList<CompanyListItemDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Companies.AsNoTracking()
            .OrderBy(x => x.LegalName)
            .Select(x => new CompanyListItemDto(x.Id, x.TaxId, x.LegalName, x.TradeName, x.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<CompanyListItemDto> SaveAsync(CompanyUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var taxId = TaxIdNormalizer.Normalize(request.TaxId)
                    ?? throw new ArgumentException("Informe um CPF/CNPJ válido para a empresa.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.LegalName)) throw new ArgumentException("Informe a razão social.", nameof(request));

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var duplicate = await db.Companies.AnyAsync(x => x.TaxId == taxId && (!request.Id.HasValue || x.Id != request.Id.Value), cancellationToken);
        if (duplicate) throw new InvalidOperationException("Já existe uma empresa cadastrada com esse CPF/CNPJ.");

        Company entity;
        if (request.Id is null)
        {
            entity = new Company();
            db.Companies.Add(entity);
        }
        else
        {
            entity = await db.Companies.SingleOrDefaultAsync(x => x.Id == request.Id.Value, cancellationToken)
                     ?? throw new KeyNotFoundException("Empresa não encontrada.");
        }

        entity.TaxId = taxId;
        entity.LegalName = request.LegalName.Trim();
        entity.TradeName = string.IsNullOrWhiteSpace(request.TradeName) ? null : request.TradeName.Trim();
        entity.IsActive = request.IsActive;
        entity.Touch();
        await db.SaveChangesAsync(cancellationToken);
        return new CompanyListItemDto(entity.Id, entity.TaxId, entity.LegalName, entity.TradeName, entity.IsActive);
    }
}

public sealed class ProductReviewService(IDbContextFactory<XmlFiscalDbContext> dbFactory) : IProductReviewService
{
    public async Task<IReadOnlyList<ProductReviewItemDto>> GetPendingAsync(int limit = 200, CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 1000);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.ProductMatchSuggestions.AsNoTracking()
            .Where(x => x.Status == ProductMatchStatus.Pending)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.CreatedAt)
            .Take(limit)
            .Select(x => new ProductReviewItemDto(
                x.Id,
                x.SourceProductId,
                x.SourceProduct.ConsolidatedDescription,
                x.SourceProduct.Gtin,
                x.SourceProduct.Ncm,
                x.CandidateProductId,
                x.CandidateProduct.ConsolidatedDescription,
                x.CandidateProduct.Gtin,
                x.CandidateProduct.Ncm,
                x.Score,
                x.DescriptionSimilarity,
                x.Reason))
            .ToListAsync(cancellationToken);
    }

    public async Task DecideAsync(ProductReviewDecisionRequest request, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var suggestion = await db.ProductMatchSuggestions
            .Include(x => x.SourceProduct)
            .Include(x => x.CandidateProduct)
            .SingleOrDefaultAsync(x => x.Id == request.SuggestionId, cancellationToken)
            ?? throw new KeyNotFoundException("Sugestão de produto não encontrada.");

        if (suggestion.Status != ProductMatchStatus.Pending)
            throw new InvalidOperationException("Esta sugestão já foi decidida.");

        Guid? targetProductId = null;
        switch (request.Decision)
        {
            case ProductMatchDecisionType.SameProduct:
            case ProductMatchDecisionType.MergeProducts:
                targetProductId = suggestion.CandidateProductId;
                await MergeProductsAsync(db, suggestion.SourceProduct, suggestion.CandidateProduct, suggestion.Id, cancellationToken);
                suggestion.Status = request.Decision == ProductMatchDecisionType.MergeProducts
                    ? ProductMatchStatus.Merged
                    : ProductMatchStatus.ConfirmedSame;
                break;
            case ProductMatchDecisionType.DifferentProducts:
                suggestion.Status = ProductMatchStatus.ConfirmedDifferent;
                break;
            case ProductMatchDecisionType.IgnoreSuggestion:
                suggestion.Status = ProductMatchStatus.Ignored;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(request), "Decisão não reconhecida.");
        }

        var decision = new ProductMatchDecision
        {
            ProductMatchSuggestionId = suggestion.Id,
            ProductMatchSuggestion = suggestion,
            DecisionType = request.Decision,
            SourceProductId = suggestion.SourceProductId,
            TargetProductId = targetProductId,
            DecidedBy = string.IsNullOrWhiteSpace(request.DecidedBy) ? null : request.DecidedBy.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            DecidedAt = DateTimeOffset.UtcNow
        };
        db.ProductMatchDecisions.Add(decision);
        db.AuditLogs.Add(new AuditLog
        {
            Severity = AuditSeverity.Information,
            Category = "Consolidação de produtos",
            Action = request.Decision.ToString(),
            EntityType = nameof(ProductMatchSuggestion),
            EntityId = suggestion.Id,
            UserName = decision.DecidedBy,
            DataJson = JsonSerializer.Serialize(new
            {
                suggestion.SourceProductId,
                suggestion.CandidateProductId,
                suggestion.Score,
                Decision = request.Decision,
                request.Notes
            })
        });

        await db.SaveChangesAsync(cancellationToken);

        suggestion.SourceProduct.RequiresReview = await db.ProductMatchSuggestions
            .AnyAsync(x => x.SourceProductId == suggestion.SourceProductId && x.Status == ProductMatchStatus.Pending, cancellationToken);
        if (targetProductId.HasValue)
            suggestion.CandidateProduct.RequiresReview = await db.ProductMatchSuggestions
                .AnyAsync(x => (x.SourceProductId == targetProductId || x.CandidateProductId == targetProductId) && x.Status == ProductMatchStatus.Pending, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task MergeProductsAsync(
        XmlFiscalDbContext db,
        Product source,
        Product target,
        Guid currentSuggestionId,
        CancellationToken cancellationToken)
    {
        if (source.Id == target.Id) throw new InvalidOperationException("Produto de origem e destino são iguais.");
        if (!source.IsActive || source.MergedIntoProductId is not null) throw new InvalidOperationException("O produto de origem já foi mesclado.");
        if (!target.IsActive || target.MergedIntoProductId is not null) throw new InvalidOperationException("O produto de destino não está ativo.");

        await db.FiscalDocumentItems.Where(x => x.ProductId == source.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.ProductId, target.Id), cancellationToken);
        await db.ProductAliases.Where(x => x.ProductId == source.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.ProductId, target.Id), cancellationToken);
        var updatedAt = DateTimeOffset.UtcNow;

        await db.ProductMatchSuggestions
            .Where(x => x.Id != currentSuggestionId &&
                x.Status == ProductMatchStatus.Pending &&
                (x.SourceProductId == source.Id ||
                 x.CandidateProductId == source.Id))
            .ExecuteUpdateAsync(update => update
                .SetProperty(x => x.Status, ProductMatchStatus.Ignored)
                .SetProperty(x => x.UpdatedAt, updatedAt),
                cancellationToken);

        source.IsActive = false;
        source.MergedIntoProductId = target.Id;
        source.MergedIntoProduct = target;
        source.RequiresReview = false;
        source.Touch();
        target.Touch();
    }
}

public sealed class SearchQueryService(
    IDbContextFactory<XmlFiscalDbContext> dbFactory,
    IProductDescriptionNormalizer descriptionNormalizer) : ISearchQueryService
{
    public async Task<IReadOnlyList<SearchResultDto>> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {
        var term = request.Term?.Trim();
        var normalizedDescription = descriptionNormalizer.Normalize(term);
        var normalizedTaxId = TaxIdNormalizer.Normalize(term);
        var limit = Math.Clamp(request.Limit, 1, 500);
        var perCategory = Math.Max(5, limit / 5);
        var results = new List<SearchResultDto>(limit);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(term))
        {
            results.AddRange(await db.Customers.AsNoTracking()
                .Where(x => x.LegalName.Contains(term) || (x.TradeName != null && x.TradeName.Contains(term)) ||
                            (normalizedTaxId != null && x.TaxId == normalizedTaxId))
                .OrderBy(x => x.LegalName)
                .Take(perCategory)
                .Select(x => new SearchResultDto("Cliente", x.Id, x.LegalName, x.TaxId, null, null))
                .ToListAsync(cancellationToken));

            results.AddRange(await db.Suppliers.AsNoTracking()
                .Where(x => x.LegalName.Contains(term) || (x.TradeName != null && x.TradeName.Contains(term)) ||
                            (normalizedTaxId != null && x.TaxId == normalizedTaxId))
                .OrderBy(x => x.LegalName)
                .Take(perCategory)
                .Select(x => new SearchResultDto("Fornecedor", x.Id, x.LegalName, x.TaxId, null, null))
                .ToListAsync(cancellationToken));

            results.AddRange(await db.Products.AsNoTracking()
                .Where(x => x.IsActive && x.MergedIntoProductId == null &&
                            (x.ConsolidatedDescription.Contains(term) ||
                             (!string.IsNullOrEmpty(normalizedDescription) && x.NormalizedDescription.Contains(normalizedDescription)) ||
                             x.Gtin == term || x.Ncm == term))
                .OrderBy(x => x.ConsolidatedDescription)
                .Take(perCategory)
                .Select(x => new SearchResultDto("Produto", x.Id, x.ConsolidatedDescription,
                    (x.Gtin ?? "SEM GTIN") + (x.Ncm != null ? " | NCM " + x.Ncm : ""), null, null))
                .ToListAsync(cancellationToken));
        }

        var documentsQuery = db.FiscalDocuments.AsNoTracking().AsQueryable();
        if (request.StartDate.HasValue) documentsQuery = documentsQuery.Where(x => x.IssuedAt >= request.StartDate.Value);
        if (request.EndDate.HasValue) documentsQuery = documentsQuery.Where(x => x.IssuedAt <= request.EndDate.Value);
        if (!string.IsNullOrWhiteSpace(term))
            documentsQuery = documentsQuery.Where(x => x.AccessKey == term || x.Number == term ||
                (x.IssuerName != null && x.IssuerName.Contains(term)) ||
                (x.RecipientName != null && x.RecipientName.Contains(term)) ||
                (normalizedTaxId != null && (x.IssuerTaxId == normalizedTaxId || x.RecipientTaxId == normalizedTaxId)));

        if (!string.IsNullOrWhiteSpace(term) || request.StartDate.HasValue || request.EndDate.HasValue)
        {
            results.AddRange(await documentsQuery
                .OrderByDescending(x => x.IssuedAt)
                .Take(perCategory * 2)
                .Select(x => new SearchResultDto("Nota fiscal", x.Id,
                    "NF " + (x.Number ?? "S/N") + " | " + x.AccessKey,
                    (x.IssuerName ?? "Emitente não informado") + " → " + (x.RecipientName ?? "Destinatário não informado"),
                    x.IssuedAt, x.TotalAmount))
                .ToListAsync(cancellationToken));
        }

        return results
            .OrderByDescending(x => x.Date ?? DateTimeOffset.MinValue)
            .ThenBy(x => x.Category)
            .Take(limit)
            .ToList();
    }
}
