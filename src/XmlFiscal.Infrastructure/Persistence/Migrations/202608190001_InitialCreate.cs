using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace XmlFiscal.Infrastructure.Persistence.Migrations;

[DbContext(typeof(XmlFiscalDbContext))]
[Migration("202608190001_InitialCreate")]
public sealed class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE "Companies" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Companies" PRIMARY KEY,
                "TaxId" TEXT NOT NULL,
                "LegalName" TEXT NOT NULL,
                "TradeName" TEXT NULL,
                "IsActive" INTEGER NOT NULL,
                "CreatedAt" INTEGER NOT NULL,
                "UpdatedAt" INTEGER NOT NULL
            );
            CREATE UNIQUE INDEX "IX_Companies_TaxId" ON "Companies" ("TaxId");

            CREATE TABLE "ImportBatches" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_ImportBatches" PRIMARY KEY,
                "RequestedCompanyId" TEXT NULL,
                "SourceDescription" TEXT NOT NULL,
                "RootPath" TEXT NULL,
                "RequestedBy" TEXT NULL,
                "StartedAt" INTEGER NOT NULL,
                "CompletedAt" INTEGER NULL,
                "Status" INTEGER NOT NULL,
                "DiscoveredCount" INTEGER NOT NULL,
                "ProcessedCount" INTEGER NOT NULL,
                "ImportedCount" INTEGER NOT NULL,
                "IgnoredCount" INTEGER NOT NULL,
                "DuplicateCount" INTEGER NOT NULL,
                "ErrorCount" INTEGER NOT NULL,
                "CreatedAt" INTEGER NOT NULL,
                "UpdatedAt" INTEGER NOT NULL,
                CONSTRAINT "FK_ImportBatches_Companies_RequestedCompanyId" FOREIGN KEY ("RequestedCompanyId") REFERENCES "Companies" ("Id") ON DELETE RESTRICT
            );
            CREATE INDEX "IX_ImportBatches_RequestedCompanyId" ON "ImportBatches" ("RequestedCompanyId");
            CREATE INDEX "IX_ImportBatches_StartedAt" ON "ImportBatches" ("StartedAt");

            CREATE TABLE "Customers" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Customers" PRIMARY KEY,
                "CompanyId" TEXT NOT NULL,
                "TaxId" TEXT NULL,
                "DeduplicationKey" TEXT NOT NULL,
                "LegalName" TEXT NOT NULL,
                "TradeName" TEXT NULL,
                "StateRegistration" TEXT NULL,
                "StateRegistrationIndicator" TEXT NULL,
                "AddressStreet" TEXT NULL,
                "AddressNumber" TEXT NULL,
                "AddressComplement" TEXT NULL,
                "AddressDistrict" TEXT NULL,
                "AddressCity" TEXT NULL,
                "AddressCityIbgeCode" TEXT NULL,
                "AddressState" TEXT NULL,
                "AddressPostalCode" TEXT NULL,
                "AddressCountry" TEXT NULL,
                "AddressCountryCode" TEXT NULL,
                "Phone" TEXT NULL,
                "Email" TEXT NULL,
                "CreatedAt" INTEGER NOT NULL,
                "UpdatedAt" INTEGER NOT NULL,
                CONSTRAINT "FK_Customers_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE RESTRICT
            );
            CREATE INDEX "IX_Customers_TaxId" ON "Customers" ("TaxId");
            CREATE UNIQUE INDEX "IX_Customers_CompanyId_DeduplicationKey" ON "Customers" ("CompanyId", "DeduplicationKey");

            CREATE TABLE "Suppliers" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Suppliers" PRIMARY KEY,
                "CompanyId" TEXT NOT NULL,
                "TaxId" TEXT NULL,
                "DeduplicationKey" TEXT NOT NULL,
                "LegalName" TEXT NOT NULL,
                "TradeName" TEXT NULL,
                "StateRegistration" TEXT NULL,
                "AddressStreet" TEXT NULL,
                "AddressNumber" TEXT NULL,
                "AddressComplement" TEXT NULL,
                "AddressDistrict" TEXT NULL,
                "AddressCity" TEXT NULL,
                "AddressCityIbgeCode" TEXT NULL,
                "AddressState" TEXT NULL,
                "AddressPostalCode" TEXT NULL,
                "AddressCountry" TEXT NULL,
                "AddressCountryCode" TEXT NULL,
                "Phone" TEXT NULL,
                "Email" TEXT NULL,
                "CreatedAt" INTEGER NOT NULL,
                "UpdatedAt" INTEGER NOT NULL,
                CONSTRAINT "FK_Suppliers_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE RESTRICT
            );
            CREATE INDEX "IX_Suppliers_TaxId" ON "Suppliers" ("TaxId");
            CREATE UNIQUE INDEX "IX_Suppliers_CompanyId_DeduplicationKey" ON "Suppliers" ("CompanyId", "DeduplicationKey");

            CREATE TABLE "Products" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Products" PRIMARY KEY,
                "Gtin" TEXT NULL,
                "ConsolidatedDescription" TEXT NOT NULL,
                "NormalizedDescription" TEXT NOT NULL,
                "Ncm" TEXT NULL,
                "CommercialUnit" TEXT NULL,
                "Brand" TEXT NULL,
                "NetContent" TEXT NULL,
                "NetContentUnit" TEXT NULL,
                "RequiresReview" INTEGER NOT NULL,
                "IsActive" INTEGER NOT NULL,
                "MergedIntoProductId" TEXT NULL,
                "CreatedAt" INTEGER NOT NULL,
                "UpdatedAt" INTEGER NOT NULL,
                CONSTRAINT "FK_Products_Products_MergedIntoProductId" FOREIGN KEY ("MergedIntoProductId") REFERENCES "Products" ("Id") ON DELETE RESTRICT
            );
            CREATE INDEX "IX_Products_Gtin" ON "Products" ("Gtin");
            CREATE INDEX "IX_Products_Ncm" ON "Products" ("Ncm");
            CREATE INDEX "IX_Products_NormalizedDescription" ON "Products" ("NormalizedDescription");
            CREATE INDEX "IX_Products_MergedIntoProductId" ON "Products" ("MergedIntoProductId");

            CREATE TABLE "ImportedFiles" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_ImportedFiles" PRIMARY KEY,
                "ImportBatchId" TEXT NOT NULL,
                "FileName" TEXT NOT NULL,
                "SourcePath" TEXT NOT NULL,
                "ArchivedPath" TEXT NULL,
                "Sha256" TEXT NOT NULL,
                "SizeBytes" INTEGER NOT NULL,
                "DocumentKind" INTEGER NOT NULL,
                "Status" INTEGER NOT NULL,
                "ProcessedAt" INTEGER NULL,
                "ResultMessage" TEXT NULL,
                "CreatedAt" INTEGER NOT NULL,
                "UpdatedAt" INTEGER NOT NULL,
                CONSTRAINT "FK_ImportedFiles_ImportBatches_ImportBatchId" FOREIGN KEY ("ImportBatchId") REFERENCES "ImportBatches" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX "IX_ImportedFiles_ImportBatchId" ON "ImportedFiles" ("ImportBatchId");
            CREATE INDEX "IX_ImportedFiles_Sha256" ON "ImportedFiles" ("Sha256");
            CREATE INDEX "IX_ImportedFiles_ProcessedAt" ON "ImportedFiles" ("ProcessedAt");

            CREATE TABLE "FiscalDocuments" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_FiscalDocuments" PRIMARY KEY,
                "ImportedFileId" TEXT NOT NULL,
                "CompanyId" TEXT NULL,
                "CustomerId" TEXT NULL,
                "SupplierId" TEXT NULL,
                "AccessKey" TEXT NOT NULL,
                "Kind" INTEGER NOT NULL,
                "Direction" INTEGER NOT NULL,
                "IsIntercompany" INTEGER NOT NULL,
                "Model" INTEGER NULL,
                "Series" TEXT NULL,
                "Number" TEXT NULL,
                "IssuedAt" INTEGER NULL,
                "OperationNature" TEXT NULL,
                "OperationType" INTEGER NULL,
                "Purpose" INTEGER NULL,
                "IssuerTaxId" TEXT NULL,
                "IssuerName" TEXT NULL,
                "RecipientTaxId" TEXT NULL,
                "RecipientName" TEXT NULL,
                "ProductsAmount" TEXT NOT NULL,
                "DiscountAmount" TEXT NOT NULL,
                "FreightAmount" TEXT NOT NULL,
                "InsuranceAmount" TEXT NOT NULL,
                "OtherExpensesAmount" TEXT NOT NULL,
                "TaxesAmount" TEXT NOT NULL,
                "TotalAmount" TEXT NOT NULL,
                "AuthorizationProtocol" TEXT NULL,
                "AuthorizedAt" INTEGER NULL,
                "AuthorizationStatusCode" TEXT NULL,
                "FinancialInformationStatus" INTEGER NOT NULL,
                "CreatedAt" INTEGER NOT NULL,
                "UpdatedAt" INTEGER NOT NULL,
                CONSTRAINT "FK_FiscalDocuments_ImportedFiles_ImportedFileId" FOREIGN KEY ("ImportedFileId") REFERENCES "ImportedFiles" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_FiscalDocuments_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_FiscalDocuments_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_FiscalDocuments_Suppliers_SupplierId" FOREIGN KEY ("SupplierId") REFERENCES "Suppliers" ("Id") ON DELETE RESTRICT
            );
            CREATE UNIQUE INDEX "IX_FiscalDocuments_ImportedFileId" ON "FiscalDocuments" ("ImportedFileId");
            CREATE UNIQUE INDEX "IX_FiscalDocuments_AccessKey" ON "FiscalDocuments" ("AccessKey");
            CREATE INDEX "IX_FiscalDocuments_CompanyId" ON "FiscalDocuments" ("CompanyId");
            CREATE INDEX "IX_FiscalDocuments_CustomerId" ON "FiscalDocuments" ("CustomerId");
            CREATE INDEX "IX_FiscalDocuments_SupplierId" ON "FiscalDocuments" ("SupplierId");
            CREATE INDEX "IX_FiscalDocuments_IssuedAt" ON "FiscalDocuments" ("IssuedAt");
            CREATE INDEX "IX_FiscalDocuments_Number" ON "FiscalDocuments" ("Number");
            CREATE INDEX "IX_FiscalDocuments_IssuerTaxId" ON "FiscalDocuments" ("IssuerTaxId");
            CREATE INDEX "IX_FiscalDocuments_RecipientTaxId" ON "FiscalDocuments" ("RecipientTaxId");

            CREATE TABLE "FiscalDocumentItems" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_FiscalDocumentItems" PRIMARY KEY,
                "FiscalDocumentId" TEXT NOT NULL,
                "ProductId" TEXT NOT NULL,
                "ItemNumber" INTEGER NOT NULL,
                "SourceProductCode" TEXT NULL,
                "CommercialGtin" TEXT NULL,
                "Description" TEXT NOT NULL,
                "Ncm" TEXT NULL,
                "Cest" TEXT NULL,
                "Cfop" TEXT NULL,
                "CommercialUnit" TEXT NULL,
                "CommercialQuantity" TEXT NOT NULL,
                "CommercialUnitPrice" TEXT NOT NULL,
                "ProductAmount" TEXT NOT NULL,
                "TaxableGtin" TEXT NULL,
                "TaxableUnit" TEXT NULL,
                "TaxableQuantity" TEXT NULL,
                "TaxableUnitPrice" TEXT NULL,
                "DiscountAmount" TEXT NOT NULL,
                "OtherExpensesAmount" TEXT NOT NULL,
                "CreatedAt" INTEGER NOT NULL,
                "UpdatedAt" INTEGER NOT NULL,
                CONSTRAINT "FK_FiscalDocumentItems_FiscalDocuments_FiscalDocumentId" FOREIGN KEY ("FiscalDocumentId") REFERENCES "FiscalDocuments" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_FiscalDocumentItems_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE RESTRICT
            );
            CREATE UNIQUE INDEX "IX_FiscalDocumentItems_FiscalDocumentId_ItemNumber" ON "FiscalDocumentItems" ("FiscalDocumentId", "ItemNumber");
            CREATE INDEX "IX_FiscalDocumentItems_ProductId" ON "FiscalDocumentItems" ("ProductId");
            CREATE INDEX "IX_FiscalDocumentItems_Ncm" ON "FiscalDocumentItems" ("Ncm");

            CREATE TABLE "ProductAliases" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_ProductAliases" PRIMARY KEY,
                "ProductId" TEXT NOT NULL,
                "ImportedFileId" TEXT NOT NULL,
                "FiscalDocumentItemId" TEXT NULL,
                "SourceIssuerTaxId" TEXT NULL,
                "SourceDate" INTEGER NULL,
                "SourceCode" TEXT NULL,
                "OriginalDescription" TEXT NOT NULL,
                "NormalizedDescription" TEXT NOT NULL,
                "OriginalGtin" TEXT NULL,
                "NormalizedGtin" TEXT NULL,
                "Ncm" TEXT NULL,
                "Unit" TEXT NULL,
                "CreatedAt" INTEGER NOT NULL,
                "UpdatedAt" INTEGER NOT NULL,
                CONSTRAINT "FK_ProductAliases_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_ProductAliases_ImportedFiles_ImportedFileId" FOREIGN KEY ("ImportedFileId") REFERENCES "ImportedFiles" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_ProductAliases_FiscalDocumentItems_FiscalDocumentItemId" FOREIGN KEY ("FiscalDocumentItemId") REFERENCES "FiscalDocumentItems" ("Id") ON DELETE RESTRICT
            );
            CREATE INDEX "IX_ProductAliases_ProductId" ON "ProductAliases" ("ProductId");
            CREATE INDEX "IX_ProductAliases_ImportedFileId" ON "ProductAliases" ("ImportedFileId");
            CREATE INDEX "IX_ProductAliases_FiscalDocumentItemId" ON "ProductAliases" ("FiscalDocumentItemId");
            CREATE INDEX "IX_ProductAliases_NormalizedGtin" ON "ProductAliases" ("NormalizedGtin");
            CREATE INDEX "IX_ProductAliases_SourceIssuerTaxId" ON "ProductAliases" ("SourceIssuerTaxId");

            CREATE TABLE "ProductMatchSuggestions" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_ProductMatchSuggestions" PRIMARY KEY,
                "SourceProductId" TEXT NOT NULL,
                "CandidateProductId" TEXT NOT NULL,
                "Score" REAL NOT NULL,
                "DescriptionSimilarity" REAL NOT NULL,
                "GtinMatches" INTEGER NULL,
                "NcmMatches" INTEGER NULL,
                "UnitMatches" INTEGER NULL,
                "Reason" TEXT NOT NULL,
                "Status" INTEGER NOT NULL,
                "CreatedAt" INTEGER NOT NULL,
                "UpdatedAt" INTEGER NOT NULL,
                CONSTRAINT "CK_ProductMatchSuggestion_Different" CHECK ("SourceProductId" <> "CandidateProductId"),
                CONSTRAINT "FK_ProductMatchSuggestions_Products_SourceProductId" FOREIGN KEY ("SourceProductId") REFERENCES "Products" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_ProductMatchSuggestions_Products_CandidateProductId" FOREIGN KEY ("CandidateProductId") REFERENCES "Products" ("Id") ON DELETE RESTRICT
            );
            CREATE INDEX "IX_ProductMatchSuggestions_SourceProductId" ON "ProductMatchSuggestions" ("SourceProductId");
            CREATE INDEX "IX_ProductMatchSuggestions_CandidateProductId" ON "ProductMatchSuggestions" ("CandidateProductId");
            CREATE INDEX "IX_ProductMatchSuggestions_Status" ON "ProductMatchSuggestions" ("Status");

            CREATE TABLE "ProductMatchDecisions" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_ProductMatchDecisions" PRIMARY KEY,
                "ProductMatchSuggestionId" TEXT NOT NULL,
                "DecisionType" INTEGER NOT NULL,
                "SourceProductId" TEXT NOT NULL,
                "TargetProductId" TEXT NULL,
                "DecidedBy" TEXT NULL,
                "DecidedAt" INTEGER NOT NULL,
                "Notes" TEXT NULL,
                "CreatedAt" INTEGER NOT NULL,
                "UpdatedAt" INTEGER NOT NULL,
                CONSTRAINT "FK_ProductMatchDecisions_ProductMatchSuggestions_ProductMatchSuggestionId" FOREIGN KEY ("ProductMatchSuggestionId") REFERENCES "ProductMatchSuggestions" ("Id") ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX "IX_ProductMatchDecisions_ProductMatchSuggestionId" ON "ProductMatchDecisions" ("ProductMatchSuggestionId");
            CREATE INDEX "IX_ProductMatchDecisions_DecidedAt" ON "ProductMatchDecisions" ("DecidedAt");

            CREATE TABLE "AccountsPayable" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_AccountsPayable" PRIMARY KEY,
                "CompanyId" TEXT NOT NULL,
                "SupplierId" TEXT NOT NULL,
                "FiscalDocumentId" TEXT NOT NULL,
                "IssueDate" INTEGER NULL,
                "TotalAmount" TEXT NOT NULL,
                "InformationStatus" INTEGER NOT NULL,
                "SourceStatus" TEXT NULL,
                "CreatedAt" INTEGER NOT NULL,
                "UpdatedAt" INTEGER NOT NULL,
                CONSTRAINT "FK_AccountsPayable_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_AccountsPayable_Suppliers_SupplierId" FOREIGN KEY ("SupplierId") REFERENCES "Suppliers" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_AccountsPayable_FiscalDocuments_FiscalDocumentId" FOREIGN KEY ("FiscalDocumentId") REFERENCES "FiscalDocuments" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX "IX_AccountsPayable_CompanyId" ON "AccountsPayable" ("CompanyId");
            CREATE INDEX "IX_AccountsPayable_SupplierId" ON "AccountsPayable" ("SupplierId");
            CREATE UNIQUE INDEX "IX_AccountsPayable_FiscalDocumentId" ON "AccountsPayable" ("FiscalDocumentId");
            CREATE INDEX "IX_AccountsPayable_IssueDate" ON "AccountsPayable" ("IssueDate");

            CREATE TABLE "AccountsReceivable" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_AccountsReceivable" PRIMARY KEY,
                "CompanyId" TEXT NOT NULL,
                "CustomerId" TEXT NOT NULL,
                "FiscalDocumentId" TEXT NOT NULL,
                "IssueDate" INTEGER NULL,
                "TotalAmount" TEXT NOT NULL,
                "InformationStatus" INTEGER NOT NULL,
                "SourceStatus" TEXT NULL,
                "CreatedAt" INTEGER NOT NULL,
                "UpdatedAt" INTEGER NOT NULL,
                CONSTRAINT "FK_AccountsReceivable_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_AccountsReceivable_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_AccountsReceivable_FiscalDocuments_FiscalDocumentId" FOREIGN KEY ("FiscalDocumentId") REFERENCES "FiscalDocuments" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX "IX_AccountsReceivable_CompanyId" ON "AccountsReceivable" ("CompanyId");
            CREATE INDEX "IX_AccountsReceivable_CustomerId" ON "AccountsReceivable" ("CustomerId");
            CREATE UNIQUE INDEX "IX_AccountsReceivable_FiscalDocumentId" ON "AccountsReceivable" ("FiscalDocumentId");
            CREATE INDEX "IX_AccountsReceivable_IssueDate" ON "AccountsReceivable" ("IssueDate");

            CREATE TABLE "Installments" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Installments" PRIMARY KEY,
                "AccountPayableId" TEXT NULL,
                "AccountReceivableId" TEXT NULL,
                "Number" TEXT NULL,
                "DueDate" INTEGER NULL,
                "Amount" TEXT NOT NULL,
                "PaymentMethodCode" TEXT NULL,
                "SourceStatus" TEXT NULL,
                "CreatedAt" INTEGER NOT NULL,
                "UpdatedAt" INTEGER NOT NULL,
                CONSTRAINT "CK_Installment_OneOwner" CHECK (("AccountPayableId" IS NOT NULL AND "AccountReceivableId" IS NULL) OR ("AccountPayableId" IS NULL AND "AccountReceivableId" IS NOT NULL)),
                CONSTRAINT "FK_Installments_AccountsPayable_AccountPayableId" FOREIGN KEY ("AccountPayableId") REFERENCES "AccountsPayable" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_Installments_AccountsReceivable_AccountReceivableId" FOREIGN KEY ("AccountReceivableId") REFERENCES "AccountsReceivable" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX "IX_Installments_AccountPayableId" ON "Installments" ("AccountPayableId");
            CREATE INDEX "IX_Installments_AccountReceivableId" ON "Installments" ("AccountReceivableId");
            CREATE INDEX "IX_Installments_DueDate" ON "Installments" ("DueDate");

            CREATE TABLE "Payments" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Payments" PRIMARY KEY,
                "FiscalDocumentId" TEXT NOT NULL,
                "MethodCode" TEXT NOT NULL,
                "MethodDescription" TEXT NULL,
                "Amount" TEXT NULL,
                "PaymentIndicator" TEXT NULL,
                "IntegrationType" TEXT NULL,
                "AcquirerTaxId" TEXT NULL,
                "CardBrandCode" TEXT NULL,
                "AuthorizationCode" TEXT NULL,
                "CreatedAt" INTEGER NOT NULL,
                "UpdatedAt" INTEGER NOT NULL,
                CONSTRAINT "FK_Payments_FiscalDocuments_FiscalDocumentId" FOREIGN KEY ("FiscalDocumentId") REFERENCES "FiscalDocuments" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX "IX_Payments_FiscalDocumentId" ON "Payments" ("FiscalDocumentId");

            CREATE TABLE "ImportErrors" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_ImportErrors" PRIMARY KEY,
                "ImportBatchId" TEXT NOT NULL,
                "ImportedFileId" TEXT NULL,
                "Severity" INTEGER NOT NULL,
                "Code" TEXT NOT NULL,
                "Message" TEXT NOT NULL,
                "Details" TEXT NULL,
                "CreatedAt" INTEGER NOT NULL,
                "UpdatedAt" INTEGER NOT NULL,
                CONSTRAINT "FK_ImportErrors_ImportBatches_ImportBatchId" FOREIGN KEY ("ImportBatchId") REFERENCES "ImportBatches" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_ImportErrors_ImportedFiles_ImportedFileId" FOREIGN KEY ("ImportedFileId") REFERENCES "ImportedFiles" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX "IX_ImportErrors_ImportBatchId" ON "ImportErrors" ("ImportBatchId");
            CREATE INDEX "IX_ImportErrors_ImportedFileId" ON "ImportErrors" ("ImportedFileId");
            CREATE INDEX "IX_ImportErrors_CreatedAt" ON "ImportErrors" ("CreatedAt");

            CREATE TABLE "AuditLogs" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_AuditLogs" PRIMARY KEY,
                "Severity" INTEGER NOT NULL,
                "Category" TEXT NOT NULL,
                "Action" TEXT NOT NULL,
                "EntityType" TEXT NULL,
                "EntityId" TEXT NULL,
                "UserName" TEXT NULL,
                "DataJson" TEXT NULL,
                "OccurredAt" INTEGER NOT NULL,
                "CreatedAt" INTEGER NOT NULL,
                "UpdatedAt" INTEGER NOT NULL
            );
            CREATE INDEX "IX_AuditLogs_OccurredAt" ON "AuditLogs" ("OccurredAt");
            CREATE INDEX "IX_AuditLogs_EntityType_EntityId" ON "AuditLogs" ("EntityType", "EntityId");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS "AuditLogs";
            DROP TABLE IF EXISTS "ImportErrors";
            DROP TABLE IF EXISTS "Payments";
            DROP TABLE IF EXISTS "Installments";
            DROP TABLE IF EXISTS "AccountsReceivable";
            DROP TABLE IF EXISTS "AccountsPayable";
            DROP TABLE IF EXISTS "ProductMatchDecisions";
            DROP TABLE IF EXISTS "ProductMatchSuggestions";
            DROP TABLE IF EXISTS "ProductAliases";
            DROP TABLE IF EXISTS "FiscalDocumentItems";
            DROP TABLE IF EXISTS "FiscalDocuments";
            DROP TABLE IF EXISTS "ImportedFiles";
            DROP TABLE IF EXISTS "Products";
            DROP TABLE IF EXISTS "Suppliers";
            DROP TABLE IF EXISTS "Customers";
            DROP TABLE IF EXISTS "ImportBatches";
            DROP TABLE IF EXISTS "Companies";
            """);
    }
}
