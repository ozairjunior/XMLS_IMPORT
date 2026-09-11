namespace XmlFiscal.Domain;

public enum DocumentKind { Unknown, NFe, NFCe, CTe, MDFe }
public enum DocumentDirection { Unknown, Inbound, Outbound }
public enum ImportBatchStatus { Pending, Running, Completed, CompletedWithErrors, Failed, Canceled }
public enum ImportedFileStatus { Pending, Imported, Ignored, Duplicate, Error }
public enum FinancialInformationStatus
{
    InstallmentsFound = 1,
    PaymentFoundWithoutInstallments,
    PaymentMethodOnly,
    NoFinancialInformation,
    InconsistentFinancialInformation
}
public enum ProductMatchStatus { Pending, ConfirmedSame, ConfirmedDifferent, Merged, Ignored }
public enum ProductMatchDecisionType { SameProduct = 1, DifferentProducts, MergeProducts, IgnoreSuggestion }
public enum AuditSeverity { Information = 1, Warning, Error }
