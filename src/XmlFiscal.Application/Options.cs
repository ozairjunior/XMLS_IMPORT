namespace XmlFiscal.Application;

public sealed class ProductMatchingOptions
{
    public decimal AutomaticMatchThreshold { get; set; } = 95m;
    public decimal ReviewThreshold { get; set; } = 70m;
    public decimal SameGtinMinimumDescriptionSimilarity { get; set; } = 0.82m;
    public decimal NoGtinMinimumDescriptionSimilarity { get; set; } = 0.93m;
    public decimal GtinWeight { get; set; } = 60m;
    public decimal DescriptionWeightWithGtin { get; set; } = 30m;
    public decimal NcmWeightWithGtin { get; set; } = 6m;
    public decimal UnitWeightWithGtin { get; set; } = 4m;
    public decimal DescriptionWeightWithoutGtin { get; set; } = 75m;
    public decimal NcmWeightWithoutGtin { get; set; } = 15m;
    public decimal UnitWeightWithoutGtin { get; set; } = 10m;
    public decimal DifferentGtinDescriptionWeight { get; set; } = 60m;
    public decimal DifferentGtinMaximumScore { get; set; } = 69m;
    public decimal MeasureConflictMaximumScore { get; set; } = 84m;
    public int CandidateLimit { get; set; } = 150;
}

public sealed class ImportOptions
{
    public int ParserParallelism { get; set; } = Math.Max(2, Environment.ProcessorCount / 2);
    public int ChannelCapacity { get; set; } = 64;
    public long MaximumFileSizeBytes { get; set; } = 100L * 1024 * 1024;
    public int ProgressPersistenceInterval { get; set; } = 25;
    public string ArchiveRootPath { get; set; } = "xml-archive";
}

public sealed class XmlSecurityOptions
{
    public long MaximumCharactersInDocument { get; set; } = 120_000_000;
    public bool AllowNonOfficialNamespace { get; set; } = true;
}
