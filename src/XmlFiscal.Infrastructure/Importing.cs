using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using XmlFiscal.Application;
using XmlFiscal.Domain;
using XmlFiscal.Infrastructure.Persistence;

namespace XmlFiscal.Infrastructure;

public sealed class ImportService : IImportService
{
    private readonly IDbContextFactory<XmlFiscalDbContext> _dbFactory;
    private readonly IFiscalXmlParser _parser;
    private readonly IPartyResolver _partyResolver;
    private readonly ProductConsolidationService _productConsolidation;
    private readonly ImportOptions _options;

    public ImportService(
        IDbContextFactory<XmlFiscalDbContext> dbFactory,
        IFiscalXmlParser parser,
        IPartyResolver partyResolver,
        ProductConsolidationService productConsolidation,
        ImportOptions options)
    {
        _dbFactory = dbFactory;
        _parser = parser;
        _partyResolver = partyResolver;
        _productConsolidation = productConsolidation;
        _options = options;
    }

    public Task<ImportSummaryDto> ImportFolderAsync(
        ImportFolderRequest request,
        IProgress<ImportProgressDto>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FolderPath)) throw new ArgumentException("Informe a pasta de importação.", nameof(request));
        var fullPath = Path.GetFullPath(request.FolderPath);
        if (!Directory.Exists(fullPath)) throw new DirectoryNotFoundException($"Pasta não encontrada: {fullPath}");

        var searchOption = request.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.EnumerateFiles(fullPath, "*.xml", searchOption)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return ImportInternalAsync(
            files,
            request.CompanyId,
            $"Pasta: {fullPath}",
            fullPath,
            request.RequestedBy,
            deleteFilesAfterImport: false,
            progress,
            cancellationToken);
    }

    public Task<ImportSummaryDto> ImportFilesAsync(
        ImportFilesRequest request,
        IProgress<ImportProgressDto>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request.FilePaths);
        var files = request.FilePaths
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return ImportInternalAsync(
            files,
            request.CompanyId,
            request.SourceDescription ?? "Arquivos selecionados",
            rootPath: null,
            request.RequestedBy,
            request.DeleteFilesAfterImport,
            progress,
            cancellationToken);
    }

    private async Task<ImportSummaryDto> ImportInternalAsync(
        IReadOnlyCollection<string> paths,
        Guid? companyId,
        string sourceDescription,
        string? rootPath,
        string? requestedBy,
        bool deleteFilesAfterImport,
        IProgress<ImportProgressDto>? progress,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var batch = new ImportBatch
        {
            RequestedCompanyId = companyId,
            SourceDescription = sourceDescription,
            RootPath = rootPath,
            RequestedBy = requestedBy,
            StartedAt = startedAt,
            Status = ImportBatchStatus.Running,
            DiscoveredCount = paths.Count
        };

        await using (var db = await _dbFactory.CreateDbContextAsync(cancellationToken))
        {
            db.ImportBatches.Add(batch);
            await db.SaveChangesAsync(cancellationToken);
        }

        var counters = new ImportCounters(paths.Count);
        progress?.Report(counters.Snapshot(null));

        var channel = Channel.CreateBounded<FileWorkItem>(new BoundedChannelOptions(Math.Max(1, _options.ChannelCapacity))
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

        var producer = ProduceAsync(paths, deleteFilesAfterImport, channel.Writer, cancellationToken);
        var consumer = ConsumeAsync(batch.Id, companyId, requestedBy, channel.Reader, counters, progress, cancellationToken);

        Exception? fatalException = null;
        try
        {
            await Task.WhenAll(producer, consumer);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            fatalException = new OperationCanceledException("Importação cancelada.", cancellationToken);
        }
        catch (Exception ex)
        {
            fatalException = ex;
        }

        var completedAt = DateTimeOffset.UtcNow;
        await FinalizeBatchAsync(batch.Id, counters, completedAt, fatalException, cancellationToken.IsCancellationRequested ? CancellationToken.None : cancellationToken);

        if (fatalException is OperationCanceledException) throw fatalException;
        if (fatalException is not null) throw new InvalidOperationException("A importação sofreu uma falha estrutural. Os XMLs já persistidos foram preservados.", fatalException);

        return new ImportSummaryDto(batch.Id, counters.Discovered, counters.Processed, counters.Imported, counters.Ignored,
            counters.Duplicated, counters.Errors, startedAt, completedAt);
    }

    private async Task ProduceAsync(
        IReadOnlyCollection<string> paths,
        bool deleteAfterImport,
        ChannelWriter<FileWorkItem> writer,
        CancellationToken cancellationToken)
    {
        try
        {
            await Parallel.ForEachAsync(paths, new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Math.Max(1, _options.ParserParallelism)
            }, async (path, ct) =>
            {
                var item = await ReadAndParseAsync(path, deleteAfterImport, ct);
                await writer.WriteAsync(item, ct);
            });
            writer.TryComplete();
        }
        catch (Exception ex)
        {
            writer.TryComplete(ex);
            throw;
        }
    }

    private async Task<FileWorkItem> ReadAndParseAsync(string path, bool deleteAfterImport, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(path)) return FileWorkItem.Error(path, 0, string.Empty, "FILE_NOT_FOUND", "Arquivo não encontrado.", null, deleteAfterImport);
            if (!string.Equals(Path.GetExtension(path), ".xml", StringComparison.OrdinalIgnoreCase))
                return FileWorkItem.Ignored(path, 0, string.Empty, "Extensão diferente de .xml.", deleteAfterImport);

            var info = new FileInfo(path);
            if (info.Length > _options.MaximumFileSizeBytes)
                return FileWorkItem.Error(path, info.Length, string.Empty, "FILE_TOO_LARGE",
                    $"Arquivo maior que o limite configurado de {_options.MaximumFileSizeBytes} bytes.", null, deleteAfterImport);

            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var hashBytes = await SHA256.HashDataAsync(stream, cancellationToken);
            var hash = Convert.ToHexString(hashBytes);
            stream.Position = 0;

            try
            {
                var parsed = await _parser.ParseAsync(stream, cancellationToken);
                return FileWorkItem.Success(path, info.Length, hash, parsed, deleteAfterImport);
            }
            catch (UnsupportedFiscalXmlException ex)
            {
                return FileWorkItem.Ignored(path, info.Length, hash, ex.Message, deleteAfterImport);
            }
            catch (InvalidFiscalXmlException ex)
            {
                return FileWorkItem.Error(path, info.Length, hash, "INVALID_XML", ex.Message, ex.ToString(), deleteAfterImport);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return FileWorkItem.Error(path, SafeLength(path), string.Empty, "FILE_READ_ERROR", ex.Message, ex.ToString(), deleteAfterImport);
        }
    }

    private async Task ConsumeAsync(
        Guid batchId,
        Guid? preferredCompanyId,
        string? requestedBy,
        ChannelReader<FileWorkItem> reader,
        ImportCounters counters,
        IProgress<ImportProgressDto>? progress,
        CancellationToken cancellationToken)
    {
        var persistedSinceLastBatchUpdate = 0;
        await foreach (var workItem in reader.ReadAllAsync(cancellationToken))
        {
            string? currentFile = Path.GetFileName(workItem.Path);
            try
            {
                var status = await PersistWorkItemAsync(batchId, preferredCompanyId, requestedBy, workItem, cancellationToken);
                counters.Register(status);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                await RecordPersistenceFailureAsync(batchId, workItem, ex, cancellationToken);
                counters.Register(ImportedFileStatus.Error);
            }
            finally
            {
                if (workItem.DeleteAfterImport)
                {
                    try { File.Delete(workItem.Path); }
                    catch { /* O arquivo temporário será limpo pelo sistema operacional ou por rotina externa. */ }
                }
            }

            persistedSinceLastBatchUpdate++;
            progress?.Report(counters.Snapshot(currentFile));
            if (persistedSinceLastBatchUpdate >= Math.Max(1, _options.ProgressPersistenceInterval))
            {
                await PersistBatchCountersAsync(batchId, counters, cancellationToken);
                persistedSinceLastBatchUpdate = 0;
            }
        }

        await PersistBatchCountersAsync(batchId, counters, cancellationToken);
        progress?.Report(counters.Snapshot(null));
    }

    private async Task<ImportedFileStatus> PersistWorkItemAsync(
        Guid batchId,
        Guid? preferredCompanyId,
        string? requestedBy,
        FileWorkItem workItem,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        if (workItem.Status is ImportedFileStatus.Error or ImportedFileStatus.Ignored)
        {
            var file = CreateImportedFile(batchId, workItem);
            db.ImportedFiles.Add(file);
            if (workItem.Status == ImportedFileStatus.Error)
                db.ImportErrors.Add(CreateImportError(batchId, file.Id, AuditSeverity.Error, workItem.ErrorCode ?? "IMPORT_ERROR", workItem.Message ?? "Erro de importação.", workItem.Details));
            await db.SaveChangesAsync(cancellationToken);
            return workItem.Status;
        }

        var hashDuplicate = !string.IsNullOrWhiteSpace(workItem.Sha256) &&
                            await db.ImportedFiles.AsNoTracking().AnyAsync(x => x.Sha256 == workItem.Sha256 && x.Status == ImportedFileStatus.Imported, cancellationToken);
        var keyDuplicate = workItem.Parsed is not null &&
                           await db.FiscalDocuments.AsNoTracking().AnyAsync(x => x.AccessKey == workItem.Parsed.AccessKey, cancellationToken);
        if (hashDuplicate || keyDuplicate)
        {
            var duplicate = CreateImportedFile(batchId, workItem, ImportedFileStatus.Duplicate,
                hashDuplicate ? "Arquivo já importado (SHA-256)." : "Documento já importado (chave de acesso).");
            db.ImportedFiles.Add(duplicate);
            await db.SaveChangesAsync(cancellationToken);
            return ImportedFileStatus.Duplicate;
        }

        if (workItem.Parsed is null) throw new InvalidOperationException("Item sem XML analisado.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var importedFile = CreateImportedFile(batchId, workItem, ImportedFileStatus.Imported, "Importado com sucesso.");
        db.ImportedFiles.Add(importedFile);

        var companies = await db.Companies.Where(x => x.IsActive).ToListAsync(cancellationToken);
        var resolution = _partyResolver.Resolve(companies, workItem.Parsed.Issuer, workItem.Parsed.Recipient, preferredCompanyId);
        Customer? customer = null;
        Supplier? supplier = null;
        if (resolution.Company is not null && resolution.Direction == DocumentDirection.Outbound)
            customer = await UpsertCustomerAsync(db, resolution.Company, workItem.Parsed.Recipient, cancellationToken);
        if (resolution.Company is not null && resolution.Direction == DocumentDirection.Inbound)
            supplier = await UpsertSupplierAsync(db, resolution.Company, workItem.Parsed.Issuer, cancellationToken);

        var document = MapDocument(importedFile, workItem.Parsed, resolution, customer, supplier);
        db.FiscalDocuments.Add(document);

        var automaticConsolidations = 0;
        var reviewSuggestions = 0;
        foreach (var parsedItem in workItem.Parsed.Items)
        {
            var item = MapItem(document, parsedItem);
            var productResolution = await _productConsolidation.ResolveAsync(db, parsedItem, importedFile, item.Id,
                workItem.Parsed.Issuer.TaxId, workItem.Parsed.IssuedAt, cancellationToken);
            item.ProductId = productResolution.Product.Id;
            item.Product = productResolution.Product;
            document.Items.Add(item);
            automaticConsolidations += productResolution.AutomaticConsolidation ? 1 : 0;
            reviewSuggestions += productResolution.Suggestions.Count;
            foreach (var warning in productResolution.Warnings)
                db.ImportErrors.Add(CreateImportError(batchId, importedFile.Id, AuditSeverity.Warning, "PRODUCT_REVIEW", warning, null));
        }

        foreach (var payment in workItem.Parsed.Payments)
            document.Payments.Add(MapPayment(document, payment));

        AddFinancialRecords(db, document, workItem.Parsed, resolution.Company, customer, supplier);

        foreach (var warning in workItem.Parsed.Warnings)
            db.ImportErrors.Add(CreateImportError(batchId, importedFile.Id, AuditSeverity.Warning, "XML_WARNING", warning, null));

        db.AuditLogs.Add(new AuditLog
        {
            Severity = reviewSuggestions > 0 ? AuditSeverity.Warning : AuditSeverity.Information,
            Category = "Importação",
            Action = "DocumentoImportado",
            EntityType = nameof(FiscalDocument),
            EntityId = document.Id,
            UserName = requestedBy,
            DataJson = JsonSerializer.Serialize(new
            {
                document.AccessKey,
                document.Direction,
                Resolution = resolution.Reason,
                Items = document.Items.Count,
                AutomaticConsolidations = automaticConsolidations,
                ReviewSuggestions = reviewSuggestions,
                File = importedFile.SourcePath
            })
        });

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var archivedPath = await TryArchiveAsync(workItem.Path, batchId, workItem.Parsed, cancellationToken);
        await PersistArchiveResultAsync(importedFile.Id, batchId, archivedPath, cancellationToken);

        return ImportedFileStatus.Imported;
    }

    private static FiscalDocument MapDocument(
        ImportedFile file,
        ParsedFiscalDocument source,
        PartyResolution resolution,
        Customer? customer,
        Supplier? supplier) => new()
    {
        ImportedFileId = file.Id,
        ImportedFile = file,
        CompanyId = resolution.Company?.Id,
        Company = resolution.Company,
        CustomerId = customer?.Id,
        Customer = customer,
        SupplierId = supplier?.Id,
        Supplier = supplier,
        AccessKey = source.AccessKey,
        Kind = source.Kind,
        Direction = resolution.Direction,
        IsIntercompany = resolution.IsIntercompany,
        Model = source.Model,
        Series = source.Series,
        Number = source.Number,
        IssuedAt = source.IssuedAt,
        OperationNature = source.OperationNature,
        OperationType = source.OperationType,
        Purpose = source.Purpose,
        IssuerTaxId = TaxIdNormalizer.Normalize(source.Issuer.TaxId),
        IssuerName = source.Issuer.LegalName,
        RecipientTaxId = TaxIdNormalizer.Normalize(source.Recipient.TaxId),
        RecipientName = source.Recipient.LegalName,
        ProductsAmount = source.ProductsAmount,
        DiscountAmount = source.DiscountAmount,
        FreightAmount = source.FreightAmount,
        InsuranceAmount = source.InsuranceAmount,
        OtherExpensesAmount = source.OtherExpensesAmount,
        TaxesAmount = source.TaxesAmount,
        TotalAmount = source.TotalAmount,
        AuthorizationProtocol = source.AuthorizationProtocol,
        AuthorizedAt = source.AuthorizedAt,
        AuthorizationStatusCode = source.AuthorizationStatusCode,
        FinancialInformationStatus = source.FinancialInformationStatus
    };

    private static FiscalDocumentItem MapItem(FiscalDocument document, ParsedFiscalItem source) => new()
    {
        FiscalDocumentId = document.Id,
        FiscalDocument = document,
        ItemNumber = source.ItemNumber,
        SourceProductCode = source.SourceProductCode,
        CommercialGtin = source.CommercialGtin,
        Description = source.Description,
        Ncm = source.Ncm,
        Cest = source.Cest,
        Cfop = source.Cfop,
        CommercialUnit = source.CommercialUnit,
        CommercialQuantity = source.CommercialQuantity,
        CommercialUnitPrice = source.CommercialUnitPrice,
        ProductAmount = source.ProductAmount,
        TaxableGtin = source.TaxableGtin,
        TaxableUnit = source.TaxableUnit,
        TaxableQuantity = source.TaxableQuantity,
        TaxableUnitPrice = source.TaxableUnitPrice,
        DiscountAmount = source.DiscountAmount,
        OtherExpensesAmount = source.OtherExpensesAmount
    };

    private static Payment MapPayment(FiscalDocument document, ParsedPayment source) => new()
    {
        FiscalDocumentId = document.Id,
        FiscalDocument = document,
        MethodCode = source.MethodCode,
        MethodDescription = source.MethodDescription,
        Amount = source.Amount,
        PaymentIndicator = source.PaymentIndicator,
        IntegrationType = source.IntegrationType,
        AcquirerTaxId = TaxIdNormalizer.Normalize(source.AcquirerTaxId),
        CardBrandCode = source.CardBrandCode,
        AuthorizationCode = source.AuthorizationCode
    };

    private static void AddFinancialRecords(
        XmlFiscalDbContext db,
        FiscalDocument document,
        ParsedFiscalDocument source,
        Company? company,
        Customer? customer,
        Supplier? supplier)
    {
        var hasFinancialEvidence = source.Installments.Count > 0 || source.Payments.Count > 0;
        if (!hasFinancialEvidence || company is null) return;
        var paymentMethodCode = SinglePaymentMethodCode(source.Payments);

        if (document.Direction == DocumentDirection.Inbound && supplier is not null)
        {
            var payable = new AccountPayable
            {
                CompanyId = company.Id,
                Company = company,
                SupplierId = supplier.Id,
                Supplier = supplier,
                FiscalDocumentId = document.Id,
                FiscalDocument = document,
                IssueDate = source.IssuedAt,
                TotalAmount = source.TotalAmount,
                InformationStatus = source.FinancialInformationStatus,
                SourceStatus = "Extraído do XML; vencimentos não informados permanecem nulos."
            };
            foreach (var parsed in source.Installments)
                payable.Installments.Add(new Installment
                {
                    AccountPayableId = payable.Id,
                    AccountPayable = payable,
                    Number = parsed.Number,
                    DueDate = parsed.DueDate,
                    Amount = parsed.Amount,
                    PaymentMethodCode = paymentMethodCode,
                    SourceStatus = "Extraído de cobr/fat/dup"
                });
            db.AccountsPayable.Add(payable);
        }
        else if (document.Direction == DocumentDirection.Outbound && customer is not null)
        {
            var receivable = new AccountReceivable
            {
                CompanyId = company.Id,
                Company = company,
                CustomerId = customer.Id,
                Customer = customer,
                FiscalDocumentId = document.Id,
                FiscalDocument = document,
                IssueDate = source.IssuedAt,
                TotalAmount = source.TotalAmount,
                InformationStatus = source.FinancialInformationStatus,
                SourceStatus = "Extraído do XML; vencimentos não informados permanecem nulos."
            };
            foreach (var parsed in source.Installments)
                receivable.Installments.Add(new Installment
                {
                    AccountReceivableId = receivable.Id,
                    AccountReceivable = receivable,
                    Number = parsed.Number,
                    DueDate = parsed.DueDate,
                    Amount = parsed.Amount,
                    PaymentMethodCode = paymentMethodCode,
                    SourceStatus = "Extraído de cobr/fat/dup"
                });
            db.AccountsReceivable.Add(receivable);
        }
    }

    private static string? SinglePaymentMethodCode(IReadOnlyCollection<ParsedPayment> payments)
    {
        var methods = payments
            .Select(x => x.MethodCode.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return methods.Length == 1 ? methods[0] : null;
    }

    private static async Task<Customer?> UpsertCustomerAsync(XmlFiscalDbContext db, Company company, ParsedParty source, CancellationToken cancellationToken)
    {
        if (!HasPartyIdentity(source)) return null;
        var key = PartyDeduplicationKeyBuilder.Build(source);
        var entity = db.Customers.Local.FirstOrDefault(x => x.CompanyId == company.Id && x.DeduplicationKey == key)
                     ?? await db.Customers.FirstOrDefaultAsync(x => x.CompanyId == company.Id && x.DeduplicationKey == key, cancellationToken);
        if (entity is null)
        {
            entity = new Customer { CompanyId = company.Id, Company = company, DeduplicationKey = key };
            db.Customers.Add(entity);
        }
        ApplyParty(entity, source);
        return entity;
    }

    private static async Task<Supplier?> UpsertSupplierAsync(XmlFiscalDbContext db, Company company, ParsedParty source, CancellationToken cancellationToken)
    {
        if (!HasPartyIdentity(source)) return null;
        var key = PartyDeduplicationKeyBuilder.Build(source);
        var entity = db.Suppliers.Local.FirstOrDefault(x => x.CompanyId == company.Id && x.DeduplicationKey == key)
                     ?? await db.Suppliers.FirstOrDefaultAsync(x => x.CompanyId == company.Id && x.DeduplicationKey == key, cancellationToken);
        if (entity is null)
        {
            entity = new Supplier { CompanyId = company.Id, Company = company, DeduplicationKey = key };
            db.Suppliers.Add(entity);
        }
        ApplyParty(entity, source);
        return entity;
    }

    private static void ApplyParty(Customer target, ParsedParty source)
    {
        target.TaxId = TaxIdNormalizer.Normalize(source.TaxId);
        target.LegalName = RequiredName(source);
        target.TradeName = Prefer(source.TradeName, target.TradeName);
        target.StateRegistration = Prefer(source.StateRegistration, target.StateRegistration);
        target.StateRegistrationIndicator = Prefer(source.StateRegistrationIndicator, target.StateRegistrationIndicator);
        target.Phone = Prefer(source.Phone, target.Phone);
        target.Email = Prefer(source.Email, target.Email);
        ApplyAddress(target.Address, source.Address);
        target.Touch();
    }

    private static void ApplyParty(Supplier target, ParsedParty source)
    {
        target.TaxId = TaxIdNormalizer.Normalize(source.TaxId);
        target.LegalName = RequiredName(source);
        target.TradeName = Prefer(source.TradeName, target.TradeName);
        target.StateRegistration = Prefer(source.StateRegistration, target.StateRegistration);
        target.Phone = Prefer(source.Phone, target.Phone);
        target.Email = Prefer(source.Email, target.Email);
        ApplyAddress(target.Address, source.Address);
        target.Touch();
    }

    private static void ApplyAddress(PostalAddress target, ParsedAddress source)
    {
        target.Street = Prefer(source.Street, target.Street);
        target.Number = Prefer(source.Number, target.Number);
        target.Complement = Prefer(source.Complement, target.Complement);
        target.District = Prefer(source.District, target.District);
        target.City = Prefer(source.City, target.City);
        target.CityIbgeCode = Prefer(source.CityIbgeCode, target.CityIbgeCode);
        target.State = Prefer(source.State, target.State);
        target.PostalCode = Prefer(source.PostalCode, target.PostalCode);
        target.Country = Prefer(source.Country, target.Country);
        target.CountryCode = Prefer(source.CountryCode, target.CountryCode);
    }

    private static bool HasPartyIdentity(ParsedParty source) =>
        TaxIdNormalizer.Normalize(source.TaxId) is not null || !string.IsNullOrWhiteSpace(source.LegalName);

    private static string RequiredName(ParsedParty source) =>
        Prefer(source.LegalName, null) ?? TaxIdNormalizer.Normalize(source.TaxId) ?? "NÃO INFORMADO";

    private static string? Prefer(string? incoming, string? current) =>
        string.IsNullOrWhiteSpace(incoming) ? current : incoming.Trim();

    private ImportedFile CreateImportedFile(Guid batchId, FileWorkItem item, ImportedFileStatus? status = null, string? message = null) => new()
    {
        ImportBatchId = batchId,
        FileName = Path.GetFileName(item.Path),
        SourcePath = item.Path,
        Sha256 = item.Sha256,
        SizeBytes = item.SizeBytes,
        DocumentKind = item.Parsed?.Kind ?? DocumentKind.Unknown,
        Status = status ?? item.Status,
        ProcessedAt = DateTimeOffset.UtcNow,
        ResultMessage = message ?? item.Message
    };

    private static ImportError CreateImportError(Guid batchId, Guid? fileId, AuditSeverity severity, string code, string message, string? details) => new()
    {
        ImportBatchId = batchId,
        ImportedFileId = fileId,
        Severity = severity,
        Code = code,
        Message = message,
        Details = details
    };

    private async Task RecordPersistenceFailureAsync(Guid batchId, FileWorkItem item, Exception exception, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var file = CreateImportedFile(batchId, item, ImportedFileStatus.Error, "Falha ao persistir o XML.");
        db.ImportedFiles.Add(file);
        db.ImportErrors.Add(CreateImportError(batchId, file.Id, AuditSeverity.Error, "PERSISTENCE_ERROR", exception.Message, exception.ToString()));
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<string?> TryArchiveAsync(string sourcePath, Guid batchId, ParsedFiscalDocument document, CancellationToken cancellationToken)
    {
        try
        {
            var date = document.IssuedAt ?? DateTimeOffset.UtcNow;
            var folder = Path.Combine(_options.ArchiveRootPath, date.Year.ToString("0000"), date.Month.ToString("00"), batchId.ToString("N"));
            Directory.CreateDirectory(folder);
            var destination = Path.Combine(folder, $"{document.AccessKey}.xml");
            if (File.Exists(destination)) destination = Path.Combine(folder, $"{document.AccessKey}-{Guid.NewGuid():N}.xml");
            await using var input = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await input.CopyToAsync(output, cancellationToken);
            return destination;
        }
        catch (OperationCanceledException) { throw; }
        catch { return null; }
    }

    private async Task PersistArchiveResultAsync(
        Guid importedFileId,
        Guid batchId,
        string? archivedPath,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var importedFile = await db.ImportedFiles.SingleAsync(x => x.Id == importedFileId, cancellationToken);
            if (archivedPath is not null)
            {
                importedFile.ArchivedPath = archivedPath;
                importedFile.Touch();
            }
            else
            {
                db.ImportErrors.Add(CreateImportError(batchId, importedFileId, AuditSeverity.Warning, "ARCHIVE_NOT_CREATED",
                    "O XML foi importado, mas não foi possível criar a cópia no arquivo permanente.", null));
            }
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            // A importação principal já foi confirmada. Uma falha de auditoria do arquivo não deve reclassificá-la como erro de persistência.
        }
    }

    private async Task PersistBatchCountersAsync(
    Guid batchId,
    ImportCounters counters,
    CancellationToken cancellationToken)
    {
    var updatedAt = DateTimeOffset.UtcNow;

    await using var db =
        await _dbFactory.CreateDbContextAsync(cancellationToken);

    await db.ImportBatches
        .Where(x => x.Id == batchId)
        .ExecuteUpdateAsync(update => update
            .SetProperty(x => x.ProcessedCount, counters.Processed)
            .SetProperty(x => x.ImportedCount, counters.Imported)
            .SetProperty(x => x.IgnoredCount, counters.Ignored)
            .SetProperty(x => x.DuplicateCount, counters.Duplicated)
            .SetProperty(x => x.ErrorCount, counters.Errors)
            .SetProperty(x => x.UpdatedAt, updatedAt),
            cancellationToken);
    }
    private async Task FinalizeBatchAsync(
    Guid batchId,
    ImportCounters counters,
    DateTimeOffset completedAt,
    Exception? fatalException,
    CancellationToken cancellationToken)
    {
    await using var db =
        await _dbFactory.CreateDbContextAsync(cancellationToken);

    var status = fatalException switch
    {
        OperationCanceledException => ImportBatchStatus.Canceled,
        not null => ImportBatchStatus.Failed,
        _ when counters.Errors > 0 =>
            ImportBatchStatus.CompletedWithErrors,
        _ => ImportBatchStatus.Completed
    };

    await db.ImportBatches
        .Where(x => x.Id == batchId)
        .ExecuteUpdateAsync(update => update
            .SetProperty(x => x.ProcessedCount, counters.Processed)
            .SetProperty(x => x.ImportedCount, counters.Imported)
            .SetProperty(x => x.IgnoredCount, counters.Ignored)
            .SetProperty(x => x.DuplicateCount, counters.Duplicated)
            .SetProperty(x => x.ErrorCount, counters.Errors)
            .SetProperty(x => x.CompletedAt, completedAt)
            .SetProperty(x => x.Status, status)
            .SetProperty(x => x.UpdatedAt, completedAt),
            cancellationToken);

    if (fatalException is not null)
    {
        db.ImportErrors.Add(
            CreateImportError(
                batchId,
                null,
                AuditSeverity.Error,
                "BATCH_FATAL_ERROR",
                fatalException.Message,
                fatalException.ToString()));

        await db.SaveChangesAsync(cancellationToken);
    }
    }

    private static long SafeLength(string path)
    {
        try { return File.Exists(path) ? new FileInfo(path).Length : 0; }
        catch { return 0; }
    }

    private sealed record FileWorkItem(
        string Path,
        long SizeBytes,
        string Sha256,
        ImportedFileStatus Status,
        ParsedFiscalDocument? Parsed,
        string? ErrorCode,
        string? Message,
        string? Details,
        bool DeleteAfterImport)
    {
        public static FileWorkItem Success(string path, long size, string hash, ParsedFiscalDocument parsed, bool delete) =>
            new(path, size, hash, ImportedFileStatus.Imported, parsed, null, null, null, delete);
        public static FileWorkItem Ignored(string path, long size, string hash, string message, bool delete) =>
            new(path, size, hash, ImportedFileStatus.Ignored, null, null, message, null, delete);
        public static FileWorkItem Error(string path, long size, string hash, string code, string message, string? details, bool delete) =>
            new(path, size, hash, ImportedFileStatus.Error, null, code, message, details, delete);
    }

    private sealed class ImportCounters
    {
        private long _processed;
        private long _imported;
        private long _ignored;
        private long _duplicated;
        private long _errors;
        public ImportCounters(long discovered) => Discovered = discovered;
        public long Discovered { get; }
        public long Processed => Interlocked.Read(ref _processed);
        public long Imported => Interlocked.Read(ref _imported);
        public long Ignored => Interlocked.Read(ref _ignored);
        public long Duplicated => Interlocked.Read(ref _duplicated);
        public long Errors => Interlocked.Read(ref _errors);
        public void Register(ImportedFileStatus status)
        {
            Interlocked.Increment(ref _processed);
            switch (status)
            {
                case ImportedFileStatus.Imported: Interlocked.Increment(ref _imported); break;
                case ImportedFileStatus.Ignored: Interlocked.Increment(ref _ignored); break;
                case ImportedFileStatus.Duplicate: Interlocked.Increment(ref _duplicated); break;
                case ImportedFileStatus.Error: Interlocked.Increment(ref _errors); break;
            }
        }
        public ImportProgressDto Snapshot(string? currentFile) => new(Discovered, Processed, Imported, Ignored, Duplicated, Errors, currentFile);
    }
}
