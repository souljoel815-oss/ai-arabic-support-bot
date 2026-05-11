using ClosedXML.Excel;

namespace EgyptTax.Application.Invoices.Bulk;

/// <summary>
/// G1.2 — generates the XLSX template the operator downloads,
/// fills, and re-uploads. Adds:
///   * A header row with the canonical column names.
///   * One sample data row so the operator sees the expected
///     format (date shape, decimal separator).
///   * An "Instructions" sheet explaining what each column means +
///     where to find the codes (customer / item / VAT category
///     master-data pages).
///   * Header formatting (bold + light-blue fill) so the template
///     is visually obvious to fill in.
///
/// Generated as bytes — caller decides whether to stream to HTTP
/// or save to disk.
/// </summary>
public static class BulkSalesInvoiceTemplate
{
    public const string DataSheetName = "Invoices";
    public const string InstructionsSheetName = "Instructions";

    public static byte[] Build()
    {
        using var wb = new XLWorkbook();
        var data = wb.Worksheets.Add(DataSheetName);

        var allColumns = BulkSalesInvoiceParser.RequiredColumns
            .Append(BulkSalesInvoiceParser.OptionalNoteColumn)
            .ToArray();

        for (var i = 0; i < allColumns.Length; i++)
        {
            var col = i + 1;
            var cell = data.Cell(1, col);
            cell.Value = allColumns[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightBlue;
            cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        }

        // Sample row (row 2) — operator can overwrite. Realistic
        // shape so the operator copies the conventions.
        data.Cell(2, 1).Value = "C-001";                       // CustomerCode
        data.Cell(2, 2).Value = DateTime.UtcNow.Date;           // DocumentDate (Excel date type)
        data.Cell(2, 2).Style.NumberFormat.Format = "yyyy-mm-dd";
        data.Cell(2, 3).Value = "ITEM-001";                    // ItemCode
        data.Cell(2, 4).Value = 1m;                            // Quantity
        data.Cell(2, 5).Value = 1000m;                         // UnitPrice
        data.Cell(2, 6).Value = "VAT-14";                      // VatCategoryCode
        data.Cell(2, 7).Value = "Sample invoice — delete this row";

        // Auto-fit columns + freeze header.
        data.Columns().AdjustToContents();
        data.SheetView.FreezeRows(1);

        var help = wb.Worksheets.Add(InstructionsSheetName);
        help.Cell(1, 1).Value = "How to use this template";
        help.Cell(1, 1).Style.Font.Bold = true;
        help.Cell(1, 1).Style.Font.FontSize = 14;

        var rows = new (string Heading, string Detail)[]
        {
            ("CustomerCode",
                "The code from /customers (Customers page). Must already exist."),
            ("DocumentDate",
                "yyyy-MM-dd, e.g., 2026-05-11. Excel date cells also work."),
            ("ItemCode",
                "The code from /items (Items page). Must already exist + have a default VAT category set."),
            ("Quantity",
                "Positive decimal. No thousands separator."),
            ("UnitPrice",
                "Positive decimal in EGP, before VAT. No thousands separator."),
            ("VatCategoryCode",
                "Use VAT-14, VAT-0, EXEMPT, RC-14 — see /settings/vat-categories for the full list."),
            ("Note",
                "Optional — free text shown on the invoice."),
            ("",
                ""),
            ("One row = one invoice",
                "Each row produces a single-line sales invoice. Multi-line bulk invoicing isn't supported yet."),
            ("Blank rows are skipped",
                "Use blank rows to separate batches visually — they don't import."),
            ("Errors are shown before posting",
                "After upload, DaftarX shows a preview with errors highlighted. Fix in the source Excel and re-upload."),
        };
        var row = 3;
        foreach (var (heading, detail) in rows)
        {
            help.Cell(row, 1).Value = heading;
            help.Cell(row, 1).Style.Font.Bold = true;
            help.Cell(row, 2).Value = detail;
            row++;
        }
        help.Column(1).AdjustToContents();
        help.Column(2).Width = 80;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
