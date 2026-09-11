namespace XmlFiscal.Domain;

public sealed class ImportBatch : Entity
{
    public Guid? RequestedCompanyId { get; set; }
    public Company? RequestedCompany { get; set; }
    public string SourceDescription { get; set; } = string.Empty;
    public string? RootPath { get; set; }
    public string? RequestedBy { get; set; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public ImportBatchStatus Status { get; set; }
    public long DiscoveredCount { get; set; }
    public long ProcessedCount { get; set; }
    public long ImportedCount { get; set; }
    public long IgnoredCount { get; set; }
    public long DuplicateCount { get; set; }
    public long ErrorCount { get; set; }
}

public sealed class ImportedFile : Entity
{
    public Guid ImportBatchId { get; set; }
    public ImportBatch ImportBatch { get; set; } = null!;
    public string FileName { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string? ArchivedPath { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DocumentKind DocumentKind { get; set; }
    public ImportedFileStatus Status { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public string? ResultMessage { get; set; }
    public FiscalDocument? FiscalDocument { get; set; }
}

public sealed class ImportError : Entity
{
    public Guid ImportBatchId { get; set; }
    public ImportBatch ImportBatch { get; set; } = null!;
    public Guid? ImportedFileId { get; set; }
    public ImportedFile? ImportedFile { get; set; }
    public AuditSeverity Severity { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
}

public sealed class AuditLog : Entity
{
    public AuditSeverity Severity { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string? UserName { get; set; }
    public string? DataJson { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}
