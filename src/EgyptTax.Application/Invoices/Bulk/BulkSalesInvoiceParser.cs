using System.Globalization;
using ClosedXML.Excel;

namespace EgyptTax.Application.Invoices.Bulk;

/// <summary>
/// G1.2 — pure parser for the bulk-sales-invoice upload template.
/// Reads an XLSX byte stream, validates column headers, parses each
/// data row into a <see cref="BulkSalesInvoiceRow"/>, and reports
/// per-row errors so the UI can highlight them inline.
///
/// Each row in the template represents a complete single-line
/// sales invoice. Multi-line bulk invoicing is a v2 (collapses on
/// shared CustomerCode + DocumentDate); the v1 shape is one row =
/// one invoice for simplicity.
///
/// Canonical column order (case-insensitive, leading/trailing
/// whitespace trimmed):
///   1. CustomerCode      — must match an existing Customer.Code
///   2. DocumentDate      — yyyy-MM-dd OR Excel date cell
///   3. ItemCode          — must match an existing Item.Code
///   4. Quantity          — decimal &gt; 0
///   5. UnitPrice         — decimal &gt; 0
///   6. VatCategoryCode   — must match an existing VatCategory.Code
///   7. Note              — optional free text
/// </summary>
public static class BulkSalesInvoiceParser
{
    /// <summary>Required column headers in the order the template
    /// produces them. Lookup is case-insensitive — operators who
    /// retype the header don't break the import.</summary>
    public static readonly string[] RequiredColumns =
    {
        "CustomerCode",
        "DocumentDate",
        "ItemCode",
        "Quantity",
        "UnitPrice",
        "VatCategoryCode",
    };

    public const string OptionalNoteColumn = "Note";

    public static BulkSalesInvoiceParseResult Parse(byte[] xlsxBytes)
    {
        ArgumentNullException.ThrowIfNull(xlsxBytes);
        if (xlsxBytes.Length == 0)
        {
            return new BulkSalesInvoiceParseResult(
                Array.Empty<BulkSalesInvoiceRow>(),
                new List<string> { "Uploaded file is empty." });
        }

        using var ms = new MemoryStream(xlsxBytes, writable: false);
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(ms);
        }
        catch (Exception ex)
        {
            return new BulkSalesInvoiceParseResult(
                Array.Empty<BulkSalesInvoiceRow>(),
                new[] { $"Cannot open as XLSX: {ex.Message}" });
        }

        try
        {
            var sheet = workbook.Worksheets.FirstOrDefault();
            if (sheet is null)
            {
                return new BulkSalesInvoiceParseResult(
                    Array.Empty<BulkSalesInvoiceRow>(),
                    new List<string> { "Workbook contains no sheets." });
            }

            var headerRow = sheet.FirstRowUsed();
            if (headerRow is null)
            {
                return new BulkSalesInvoiceParseResult(
                    Array.Empty<BulkSalesInvoiceRow>(),
                    new List<string> { "Sheet is empty." });
            }

            var columnIndex = MapColumns(headerRow);
            var missing = RequiredColumns
                .Where(c => !columnIndex.ContainsKey(c))
                .ToList();
            if (missing.Count > 0)
            {
                return new BulkSalesInvoiceParseResult(
                    Array.Empty<BulkSalesInvoiceRow>(),
                    new[] { $"Missing required column(s): {string.Join(", ", missing)}." });
            }

            var rows = new List<BulkSalesInvoiceRow>();
            var errors = new List<string>();

            foreach (var dataRow in sheet.RowsUsed().Skip(1))
            {
                var lineNumber = dataRow.RowNumber();
                var parsed = ParseRow(dataRow, columnIndex, lineNumber, errors);
                if (parsed is not null)
                {
                    rows.Add(parsed);
                }
            }

            return new BulkSalesInvoiceParseResult(rows, errors);
        }
        finally
        {
            workbook.Dispose();
        }
    }

    private static Dictionary<string, int> MapColumns(IXLRow headerRow)
    {
        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in headerRow.CellsUsed())
        {
            var key = cell.GetString().Trim();
            if (!string.IsNullOrWhiteSpace(key))
            {
                index[key] = cell.Address.ColumnNumber;
            }
        }
        return index;
    }

    private static BulkSalesInvoiceRow? ParseRow(
        IXLRow row,
        Dictionary<string, int> columnIndex,
        int lineNumber,
        List<string> errors)
    {
        var customerCode = row.Cell(columnIndex["CustomerCode"]).GetString().Trim();
        var itemCode = row.Cell(columnIndex["ItemCode"]).GetString().Trim();
        var vatCategoryCode = row.Cell(columnIndex["VatCategoryCode"]).GetString().Trim();

        // A row with all three codes blank is treated as an empty
        // separator row (e.g., spacer between batches) — silently
        // skipped, not an error.
        if (string.IsNullOrWhiteSpace(customerCode)
            && string.IsNullOrWhiteSpace(itemCode)
            && string.IsNullOrWhiteSpace(vatCategoryCode))
        {
            return null;
        }

        var rowErrors = new List<string>();

        if (string.IsNullOrWhiteSpace(customerCode))
            rowErrors.Add("CustomerCode is blank.");
        if (string.IsNullOrWhiteSpace(itemCode))
            rowErrors.Add("ItemCode is blank.");
        if (string.IsNullOrWhiteSpace(vatCategoryCode))
            rowErrors.Add("VatCategoryCode is blank.");

        var dateCell = row.Cell(columnIndex["DocumentDate"]);
        DateOnly? documentDate = null;
        if (dateCell.IsEmpty())
        {
            rowErrors.Add("DocumentDate is blank.");
        }
        else if (dateCell.DataType == XLDataType.DateTime && dateCell.TryGetValue<DateTime>(out var dt))
        {
            documentDate = DateOnly.FromDateTime(dt);
        }
        else
        {
            var raw = dateCell.GetString().Trim();
            if (DateOnly.TryParseExact(raw, "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                documentDate = parsed;
            }
            else if (DateOnly.TryParse(raw, CultureInfo.InvariantCulture,
                         DateTimeStyles.None, out parsed))
            {
                documentDate = parsed;
            }
            else
            {
                rowErrors.Add($"DocumentDate '{raw}' is not a valid date.");
            }
        }

        var qty = ParsePositiveDecimal(row, columnIndex["Quantity"], "Quantity", rowErrors);
        var unitPrice = ParsePositiveDecimal(row, columnIndex["UnitPrice"], "UnitPrice", rowErrors);

        string? note = null;
        if (columnIndex.TryGetValue(OptionalNoteColumn, out var noteCol))
        {
            note = row.Cell(noteCol).GetString().Trim();
            if (string.IsNullOrEmpty(note)) note = null;
        }

        if (rowErrors.Count > 0)
        {
            foreach (var msg in rowErrors)
            {
                errors.Add($"Row {lineNumber}: {msg}");
            }
            return new BulkSalesInvoiceRow(
                LineNumber: lineNumber,
                CustomerCode: customerCode,
                DocumentDate: documentDate ?? DateOnly.MinValue,
                ItemCode: itemCode,
                Quantity: qty ?? 0m,
                UnitPrice: unitPrice ?? 0m,
                VatCategoryCode: vatCategoryCode,
                Note: note,
                IsValid: false,
                ErrorMessages: rowErrors);
        }

        return new BulkSalesInvoiceRow(
            LineNumber: lineNumber,
            CustomerCode: customerCode,
            DocumentDate: documentDate!.Value,
            ItemCode: itemCode,
            Quantity: qty!.Value,
            UnitPrice: unitPrice!.Value,
            VatCategoryCode: vatCategoryCode,
            Note: note,
            IsValid: true,
            ErrorMessages: Array.Empty<string>());
    }

    private static decimal? ParsePositiveDecimal(
        IXLRow row, int col, string label, List<string> rowErrors)
    {
        var cell = row.Cell(col);
        if (cell.IsEmpty())
        {
            rowErrors.Add($"{label} is blank.");
            return null;
        }
        if (cell.TryGetValue<decimal>(out var n) && n > 0m)
        {
            return n;
        }
        // Fallback: string parse for text-typed cells.
        var raw = cell.GetString().Trim().Replace(",", "", StringComparison.Ordinal);
        if (decimal.TryParse(raw, NumberStyles.Number,
                CultureInfo.InvariantCulture, out var parsed) && parsed > 0m)
        {
            return parsed;
        }
        rowErrors.Add($"{label} '{cell.GetString()}' must be a positive number.");
        return null;
    }
}

public sealed record BulkSalesInvoiceParseResult(
    IReadOnlyList<BulkSalesInvoiceRow> Rows,
    IReadOnlyList<string> Errors)
{
    public int ValidRowCount => Rows.Count(r => r.IsValid);
    public int InvalidRowCount => Rows.Count(r => !r.IsValid);
    public bool HasFileLevelError => Errors.Any(e => !e.StartsWith("Row ", StringComparison.Ordinal));
}

public sealed record BulkSalesInvoiceRow(
    int LineNumber,
    string CustomerCode,
    DateOnly DocumentDate,
    string ItemCode,
    decimal Quantity,
    decimal UnitPrice,
    string VatCategoryCode,
    string? Note,
    bool IsValid,
    IReadOnlyList<string> ErrorMessages);
