using ClosedXML.Excel;
using EgyptTax.Application.Invoices.Bulk;

namespace EgyptTax.UnitTests.Application.Invoices.Bulk;

/// <summary>
/// G1.2 — Excel-template parser tests. Builds each fixture in
/// memory via ClosedXML rather than from disk so the tests stay
/// hermetic and the column-order contract is asserted at the
/// builder level too.
/// </summary>
public class BulkSalesInvoiceParserTests
{
    private static readonly string[] PartialHeaders =
        new[] { "CustomerCode", "DocumentDate", "ItemCode" };

    private static readonly string[] LowerCaseHeaders =
        new[] { "customercode", "DOCUMENTDATE", "itemcode", "quantity", "UnitPrice", "vatcategorycode" };

    [Fact]
    public void Empty_Bytes_ReturnsFileLevelError()
    {
        var result = BulkSalesInvoiceParser.Parse(Array.Empty<byte>());

        result.Rows.Should().BeEmpty();
        result.HasFileLevelError.Should().BeTrue();
        result.Errors.Should().ContainSingle();
    }

    [Fact]
    public void NotAnXlsx_ReturnsFileLevelError()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("this is not xlsx");

        var result = BulkSalesInvoiceParser.Parse(bytes);

        result.Rows.Should().BeEmpty();
        result.HasFileLevelError.Should().BeTrue();
    }

    [Fact]
    public void MissingRequiredColumn_FailsFast()
    {
        // Build a workbook with only some columns present.
        var bytes = BuildXlsx(headers: PartialHeaders);

        var result = BulkSalesInvoiceParser.Parse(bytes);

        result.Rows.Should().BeEmpty();
        result.HasFileLevelError.Should().BeTrue();
        result.Errors[0].Should().Contain("Missing required column(s)");
        result.Errors[0].Should().Contain("Quantity");
        result.Errors[0].Should().Contain("UnitPrice");
        result.Errors[0].Should().Contain("VatCategoryCode");
    }

    [Fact]
    public void ValidRow_ParsesAllFields()
    {
        var bytes = BuildXlsx(
            headers: BulkSalesInvoiceParser.RequiredColumns.Append("Note").ToArray(),
            data: new[]
            {
                new object?[] { "C-001", new DateTime(2026, 5, 11), "ITEM-01", 2m, 500m, "VAT-14", "first invoice" }
            });

        var result = BulkSalesInvoiceParser.Parse(bytes);

        result.HasFileLevelError.Should().BeFalse();
        result.Rows.Should().HaveCount(1);
        var row = result.Rows[0];
        row.IsValid.Should().BeTrue();
        row.CustomerCode.Should().Be("C-001");
        row.DocumentDate.Should().Be(new DateOnly(2026, 5, 11));
        row.ItemCode.Should().Be("ITEM-01");
        row.Quantity.Should().Be(2m);
        row.UnitPrice.Should().Be(500m);
        row.VatCategoryCode.Should().Be("VAT-14");
        row.Note.Should().Be("first invoice");
    }

    [Fact]
    public void BlankRowsAreSilentlySkipped()
    {
        var bytes = BuildXlsx(
            headers: BulkSalesInvoiceParser.RequiredColumns,
            data: new[]
            {
                new object?[] { "C-001", new DateTime(2026, 5, 11), "ITEM-01", 1m, 100m, "VAT-14" },
                new object?[] { null, null, null, null, null, null },
                new object?[] { "C-002", new DateTime(2026, 5, 12), "ITEM-02", 3m, 300m, "VAT-14" },
            });

        var result = BulkSalesInvoiceParser.Parse(bytes);

        result.Rows.Should().HaveCount(2);
        result.Rows.Should().AllSatisfy(r => r.IsValid.Should().BeTrue());
    }

    [Fact]
    public void RowWithBlankRequired_ProducesError()
    {
        var bytes = BuildXlsx(
            headers: BulkSalesInvoiceParser.RequiredColumns,
            data: new[]
            {
                new object?[] { "", new DateTime(2026, 5, 11), "ITEM-01", 1m, 100m, "VAT-14" }
            });

        var result = BulkSalesInvoiceParser.Parse(bytes);

        // The "all-blank-row-is-skipped" guard only triggers when
        // customer + item + vat are ALL blank. Mixed-blank rows are
        // real errors.
        result.Rows.Should().HaveCount(1);
        result.Rows[0].IsValid.Should().BeFalse();
        result.Rows[0].ErrorMessages.Should().Contain(e => e.Contains("CustomerCode"));
    }

    [Fact]
    public void NegativeQuantity_Errors()
    {
        var bytes = BuildXlsx(
            headers: BulkSalesInvoiceParser.RequiredColumns,
            data: new[]
            {
                new object?[] { "C-001", new DateTime(2026, 5, 11), "ITEM-01", -1m, 100m, "VAT-14" }
            });

        var result = BulkSalesInvoiceParser.Parse(bytes);

        result.Rows[0].IsValid.Should().BeFalse();
        result.Rows[0].ErrorMessages.Should().Contain(e => e.Contains("Quantity"));
    }

    [Fact]
    public void Template_RoundTrips_Through_Parser()
    {
        // The template ships with one example row. Parsing it should
        // produce exactly one valid row.
        var bytes = BulkSalesInvoiceTemplate.Build();

        var result = BulkSalesInvoiceParser.Parse(bytes);

        result.HasFileLevelError.Should().BeFalse();
        result.Rows.Should().HaveCount(1);
        result.Rows[0].IsValid.Should().BeTrue();
        result.Rows[0].CustomerCode.Should().Be("C-001");
        result.Rows[0].ItemCode.Should().Be("ITEM-001");
        result.Rows[0].VatCategoryCode.Should().Be("VAT-14");
    }

    [Fact]
    public void CaseInsensitive_ColumnHeaders()
    {
        // Operator retypes the header with different casing — should
        // still work.
        var bytes = BuildXlsx(
            headers: LowerCaseHeaders,
            data: new[]
            {
                new object?[] { "C-001", new DateTime(2026, 5, 11), "ITEM-01", 1m, 100m, "VAT-14" }
            });

        var result = BulkSalesInvoiceParser.Parse(bytes);

        result.HasFileLevelError.Should().BeFalse();
        result.Rows.Should().HaveCount(1);
        result.Rows[0].IsValid.Should().BeTrue();
    }

    private static byte[] BuildXlsx(IReadOnlyList<string> headers, IReadOnlyList<object?[]>? data = null)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Test");
        for (var i = 0; i < headers.Count; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
        }
        if (data is not null)
        {
            for (var r = 0; r < data.Count; r++)
            {
                for (var c = 0; c < data[r].Length; c++)
                {
                    var value = data[r][c];
                    if (value is null) continue;
                    var cell = ws.Cell(r + 2, c + 1);
                    switch (value)
                    {
                        case DateTime dt: cell.Value = dt; cell.Style.NumberFormat.Format = "yyyy-mm-dd"; break;
                        case decimal d: cell.Value = d; break;
                        case int i: cell.Value = i; break;
                        case string s: cell.Value = s; break;
                        default: cell.Value = value.ToString(); break;
                    }
                }
            }
        }
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
