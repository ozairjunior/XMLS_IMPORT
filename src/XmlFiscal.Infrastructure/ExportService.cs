using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using XmlFiscal.Application;
using XmlFiscal.Domain;
using XmlFiscal.Infrastructure.Persistence;

namespace XmlFiscal.Infrastructure;

public sealed class ExportService(IDbContextFactory<XmlFiscalDbContext> dbFactory) : IExportService
{
    private static readonly CultureInfo BrazilianCulture = CultureInfo.GetCultureInfo("pt-BR");

    public async Task<ExportFileDto> ExportAsync(ExportDataset dataset, ExportFormat format, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var table = await LoadTableAsync(db, dataset, cancellationToken);
        var baseName = $"xmlfiscal-{DatasetName(dataset)}-{DateTimeOffset.Now:yyyyMMdd-HHmmss}";
        return format switch
        {
            ExportFormat.Csv => new ExportFileDto(ToCsv(table), "text/csv; charset=utf-8", baseName + ".csv"),
            ExportFormat.Xlsx => new ExportFileDto(ToXlsx(table, DatasetName(dataset)),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", baseName + ".xlsx"),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
    }

    private static async Task<ExportTable> LoadTableAsync(XmlFiscalDbContext db, ExportDataset dataset, CancellationToken cancellationToken) =>
        dataset switch
        {
            ExportDataset.Customers => await CustomersAsync(db, cancellationToken),
            ExportDataset.Suppliers => await SuppliersAsync(db, cancellationToken),
            ExportDataset.Products => await ProductsAsync(db, cancellationToken),
            ExportDataset.FiscalDocuments => await DocumentsAsync(db, cancellationToken),
            ExportDataset.FiscalDocumentItems => await ItemsAsync(db, cancellationToken),
            ExportDataset.AccountsPayable => await PayablesAsync(db, cancellationToken),
            ExportDataset.AccountsReceivable => await ReceivablesAsync(db, cancellationToken),
            ExportDataset.ProductsAwaitingReview => await ReviewAsync(db, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(dataset))
        };

    private static async Task<ExportTable> CustomersAsync(XmlFiscalDbContext db, CancellationToken ct)
    {
        var data = await db.Customers.AsNoTracking().Include(x => x.Company).OrderBy(x => x.LegalName).ToListAsync(ct);
        return new(
            ["Empresa", "CPF/CNPJ", "Razão social/Nome", "Nome fantasia", "IE", "Indicador IE", "Logradouro", "Número", "Complemento", "Bairro", "Município", "Código IBGE", "UF", "CEP", "País", "Telefone", "E-mail"],
            data.Select(x => Row(x.Company.LegalName, x.TaxId, x.LegalName, x.TradeName, x.StateRegistration, x.StateRegistrationIndicator,
                x.Address.Street, x.Address.Number, x.Address.Complement, x.Address.District, x.Address.City, x.Address.CityIbgeCode,
                x.Address.State, x.Address.PostalCode, x.Address.Country, x.Phone, x.Email)).ToList());
    }

    private static async Task<ExportTable> SuppliersAsync(XmlFiscalDbContext db, CancellationToken ct)
    {
        var data = await db.Suppliers.AsNoTracking().Include(x => x.Company).OrderBy(x => x.LegalName).ToListAsync(ct);
        return new(
            ["Empresa", "CPF/CNPJ", "Razão social", "Nome fantasia", "IE", "Logradouro", "Número", "Complemento", "Bairro", "Município", "Código IBGE", "UF", "CEP", "País", "Telefone", "E-mail"],
            data.Select(x => Row(x.Company.LegalName, x.TaxId, x.LegalName, x.TradeName, x.StateRegistration,
                x.Address.Street, x.Address.Number, x.Address.Complement, x.Address.District, x.Address.City, x.Address.CityIbgeCode,
                x.Address.State, x.Address.PostalCode, x.Address.Country, x.Phone, x.Email)).ToList());
    }

    private static async Task<ExportTable> ProductsAsync(XmlFiscalDbContext db, CancellationToken ct)
    {
        var data = await db.Products.AsNoTracking()
            .Where(x => x.IsActive && x.MergedIntoProductId == null)
            .OrderBy(x => x.ConsolidatedDescription)
            .Select(x => new
            {
                x.Id, x.Gtin, x.ConsolidatedDescription, x.NormalizedDescription, x.Ncm, x.CommercialUnit,
                x.Brand, x.NetContent, x.NetContentUnit, x.RequiresReview, AliasCount = x.Aliases.Count,
                x.CreatedAt, x.UpdatedAt
            }).ToListAsync(ct);
        return new(
            ["ID", "GTIN", "Descrição consolidada", "Descrição normalizada", "NCM", "Unidade", "Marca", "Conteúdo", "Unidade conteúdo", "Requer revisão", "Aliases", "Criado em", "Atualizado em"],
            data.Select(x => Row(x.Id, x.Gtin, x.ConsolidatedDescription, x.NormalizedDescription, x.Ncm, x.CommercialUnit,
                x.Brand, x.NetContent, x.NetContentUnit, x.RequiresReview, x.AliasCount, x.CreatedAt, x.UpdatedAt)).ToList());
    }

    private static async Task<ExportTable> DocumentsAsync(XmlFiscalDbContext db, CancellationToken ct)
    {
        var data = await db.FiscalDocuments.AsNoTracking().Include(x => x.Company).OrderByDescending(x => x.IssuedAt).ToListAsync(ct);
        return new(
            ["Chave", "Modelo", "Série", "Número", "Emissão", "Direção", "Empresa analisada", "Natureza", "Emitente CPF/CNPJ", "Emitente", "Destinatário CPF/CNPJ", "Destinatário", "Produtos", "Desconto", "Frete", "Seguro", "Outras despesas", "Impostos", "Total", "Protocolo", "Status financeiro"],
            data.Select(x => Row(x.AccessKey, x.Model, x.Series, x.Number, x.IssuedAt, DirectionName(x.Direction), x.Company?.LegalName,
                x.OperationNature, x.IssuerTaxId, x.IssuerName, x.RecipientTaxId, x.RecipientName, x.ProductsAmount, x.DiscountAmount,
                x.FreightAmount, x.InsuranceAmount, x.OtherExpensesAmount, x.TaxesAmount, x.TotalAmount, x.AuthorizationProtocol,
                FinancialStatusName(x.FinancialInformationStatus))).ToList());
    }

    private static async Task<ExportTable> ItemsAsync(XmlFiscalDbContext db, CancellationToken ct)
    {
        var data = await db.FiscalDocumentItems.AsNoTracking()
            .Include(x => x.FiscalDocument)
            .Include(x => x.Product)
            .OrderByDescending(x => x.FiscalDocument.IssuedAt)
            .ThenBy(x => x.ItemNumber)
            .ToListAsync(ct);
        return new(
            ["Chave NF-e", "Número NF", "Emissão", "Item", "Produto consolidado ID", "Produto consolidado", "cProd origem", "cEAN", "Descrição original", "NCM", "CEST", "CFOP", "Unidade comercial", "Quantidade", "Valor unitário", "Valor total", "cEANTrib", "Unidade tributável", "Quantidade tributável", "Valor tributável", "Desconto", "Outras despesas"],
            data.Select(x => Row(x.FiscalDocument.AccessKey, x.FiscalDocument.Number, x.FiscalDocument.IssuedAt, x.ItemNumber,
                x.ProductId, x.Product.ConsolidatedDescription, x.SourceProductCode, x.CommercialGtin, x.Description, x.Ncm, x.Cest,
                x.Cfop, x.CommercialUnit, x.CommercialQuantity, x.CommercialUnitPrice, x.ProductAmount, x.TaxableGtin,
                x.TaxableUnit, x.TaxableQuantity, x.TaxableUnitPrice, x.DiscountAmount, x.OtherExpensesAmount)).ToList());
    }

    private static async Task<ExportTable> PayablesAsync(XmlFiscalDbContext db, CancellationToken ct)
    {
        var data = await db.AccountsPayable.AsNoTracking()
            .Include(x => x.Company).Include(x => x.Supplier).Include(x => x.FiscalDocument).ThenInclude(x => x.Payments).Include(x => x.Installments)
            .OrderByDescending(x => x.IssueDate).ToListAsync(ct);
        var rows = new List<object?[]>();
        foreach (var title in data)
        {
            if (title.Installments.Count == 0)
                rows.Add(Row(title.Company.LegalName, title.Supplier.LegalName, title.Supplier.TaxId, title.FiscalDocument.Number,
                    title.FiscalDocument.AccessKey, null, title.IssueDate, null, title.TotalAmount, PaymentMethods(title.FiscalDocument), FinancialStatusName(title.InformationStatus), title.SourceStatus));
            else
                rows.AddRange(title.Installments.OrderBy(x => x.DueDate).Select(installment => Row(title.Company.LegalName,
                    title.Supplier.LegalName, title.Supplier.TaxId, title.FiscalDocument.Number, title.FiscalDocument.AccessKey,
                    installment.Number, title.IssueDate, installment.DueDate?.Date, installment.Amount,
                    PaymentMethods(title.FiscalDocument, installment.PaymentMethodCode), FinancialStatusName(title.InformationStatus), installment.SourceStatus)));
        }
        return new(["Empresa", "Fornecedor", "CPF/CNPJ", "NF", "Chave", "Parcela", "Emissão", "Vencimento", "Valor", "Forma de pagamento", "Status financeiro", "Origem"], rows);
    }

    private static async Task<ExportTable> ReceivablesAsync(XmlFiscalDbContext db, CancellationToken ct)
    {
        var data = await db.AccountsReceivable.AsNoTracking()
            .Include(x => x.Company).Include(x => x.Customer).Include(x => x.FiscalDocument).ThenInclude(x => x.Payments).Include(x => x.Installments)
            .OrderByDescending(x => x.IssueDate).ToListAsync(ct);
        var rows = new List<object?[]>();
        foreach (var title in data)
        {
            if (title.Installments.Count == 0)
                rows.Add(Row(title.Company.LegalName, title.Customer.LegalName, title.Customer.TaxId, title.FiscalDocument.Number,
                    title.FiscalDocument.AccessKey, null, title.IssueDate, null, title.TotalAmount, PaymentMethods(title.FiscalDocument), FinancialStatusName(title.InformationStatus), title.SourceStatus));
            else
                rows.AddRange(title.Installments.OrderBy(x => x.DueDate).Select(installment => Row(title.Company.LegalName,
                    title.Customer.LegalName, title.Customer.TaxId, title.FiscalDocument.Number, title.FiscalDocument.AccessKey,
                    installment.Number, title.IssueDate, installment.DueDate?.Date, installment.Amount,
                    PaymentMethods(title.FiscalDocument, installment.PaymentMethodCode), FinancialStatusName(title.InformationStatus), installment.SourceStatus)));
        }
        return new(["Empresa", "Cliente", "CPF/CNPJ", "NF", "Chave", "Parcela", "Emissão", "Vencimento", "Valor", "Forma de pagamento", "Status financeiro", "Origem"], rows);
    }

    private static async Task<ExportTable> ReviewAsync(XmlFiscalDbContext db, CancellationToken ct)
    {
        var data = await db.ProductMatchSuggestions.AsNoTracking()
            .Where(x => x.Status == ProductMatchStatus.Pending)
            .Include(x => x.SourceProduct).Include(x => x.CandidateProduct)
            .OrderByDescending(x => x.Score).ToListAsync(ct);
        return new(
            ["Sugestão ID", "Produto encontrado", "GTIN encontrado", "NCM encontrado", "Possível correspondente", "GTIN candidato", "NCM candidato", "Score", "Similaridade descrição", "Motivo"],
            data.Select(x => Row(x.Id, x.SourceProduct.ConsolidatedDescription, x.SourceProduct.Gtin, x.SourceProduct.Ncm,
                x.CandidateProduct.ConsolidatedDescription, x.CandidateProduct.Gtin, x.CandidateProduct.Ncm, x.Score,
                x.DescriptionSimilarity, x.Reason)).ToList());
    }

    private static byte[] ToCsv(ExportTable table)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(';', table.Headers.Select(EscapeCsv)));
        foreach (var row in table.Rows)
            builder.AppendLine(string.Join(';', row.Select(value => EscapeCsv(FormatValue(value)))));
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(builder.ToString());
    }

    private static byte[] ToXlsx(ExportTable table, string worksheetName)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(worksheetName.Length > 31 ? worksheetName[..31] : worksheetName);
        for (var column = 0; column < table.Headers.Length; column++)
            worksheet.Cell(1, column + 1).Value = table.Headers[column];

        for (var row = 0; row < table.Rows.Count; row++)
        {
            for (var column = 0; column < table.Headers.Length; column++)
            {
                var cell = worksheet.Cell(row + 2, column + 1);
                var value = column < table.Rows[row].Length ? table.Rows[row][column] : null;
                switch (value)
                {
                    case null: cell.Value = string.Empty; break;
                    case DateTimeOffset dto: cell.Value = dto.LocalDateTime; cell.Style.DateFormat.Format = "dd/MM/yyyy HH:mm:ss"; break;
                    case DateTime dt: cell.Value = dt; cell.Style.DateFormat.Format = "dd/MM/yyyy HH:mm:ss"; break;
                    case decimal number: cell.Value = number; cell.Style.NumberFormat.Format = "#,##0.00####"; break;
                    case double number: cell.Value = number; break;
                    case int number: cell.Value = number; break;
                    case long number: cell.Value = number; break;
                    case bool boolean: cell.Value = boolean ? "Sim" : "Não"; break;
                    default: cell.Value = Convert.ToString(value, BrazilianCulture) ?? string.Empty; break;
                }
            }
        }

        if (table.Rows.Count > 0)
        {
            worksheet.Range(1, 1, table.Rows.Count + 1, table.Headers.Length).CreateTable();
        }
        else
        {
            worksheet.Range(1, 1, 1, table.Headers.Length).Style.Font.Bold = true;
        }

        worksheet.SheetView.FreezeRows(1);
        worksheet.Columns().AdjustToContents(1, Math.Min(table.Rows.Count + 1, 2000));
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static object?[] Row(params object?[] values) => values;
    private static string EscapeCsv(string value) => value.IndexOfAny([';', '"', '\r', '\n']) >= 0 ? '"' + value.Replace("\"", "\"\"") + '"' : value;
    private static string FormatValue(object? value) => value switch
    {
        null => string.Empty,
        DateTimeOffset dto => dto.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss", BrazilianCulture),
        DateTime date => date.ToString("dd/MM/yyyy HH:mm:ss", BrazilianCulture),
        decimal number => number.ToString("0.00######", BrazilianCulture),
        bool boolean => boolean ? "Sim" : "Não",
        _ => Convert.ToString(value, BrazilianCulture) ?? string.Empty
    };

    private static string DatasetName(ExportDataset dataset) => dataset switch
    {
        ExportDataset.Customers => "clientes",
        ExportDataset.Suppliers => "fornecedores",
        ExportDataset.Products => "mercadorias",
        ExportDataset.FiscalDocuments => "notas",
        ExportDataset.FiscalDocumentItems => "itens-notas",
        ExportDataset.AccountsPayable => "contas-pagar",
        ExportDataset.AccountsReceivable => "contas-receber",
        ExportDataset.ProductsAwaitingReview => "produtos-validar",
        _ => "dados"
    };

    private static string? PaymentMethods(FiscalDocument document, string? preferredCode = null)
    {
        IEnumerable<Payment> payments = document.Payments;
        if (!string.IsNullOrWhiteSpace(preferredCode))
            payments = payments.Where(x => string.Equals(x.MethodCode, preferredCode, StringComparison.OrdinalIgnoreCase));

        var values = payments
            .Select(x => string.IsNullOrWhiteSpace(x.MethodDescription) ? x.MethodCode : $"{x.MethodCode} - {x.MethodDescription}")
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (values.Length > 0) return string.Join(" | ", values);
        return string.IsNullOrWhiteSpace(preferredCode) ? null : preferredCode;
    }

    private static string DirectionName(DocumentDirection direction) => direction switch
    {
        DocumentDirection.Inbound => "Entrada",
        DocumentDirection.Outbound => "Saída",
        _ => "Não identificada"
    };

    private static string FinancialStatusName(FinancialInformationStatus status) => status switch
    {
        FinancialInformationStatus.InstallmentsFound => "Parcelas encontradas",
        FinancialInformationStatus.PaymentFoundWithoutInstallments => "Pagamento encontrado sem parcelas",
        FinancialInformationStatus.PaymentMethodOnly => "Somente forma de pagamento encontrada",
        FinancialInformationStatus.NoFinancialInformation => "Sem informação financeira",
        FinancialInformationStatus.InconsistentFinancialInformation => "Informação financeira inconsistente",
        _ => status.ToString()
    };

    private sealed record ExportTable(string[] Headers, List<object?[]> Rows);
}
