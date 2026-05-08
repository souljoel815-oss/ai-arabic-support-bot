using EgyptTax.Domain.MasterData;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EgyptTax.Infrastructure.Pdf.Registers;

/// <summary>
/// FR-048 / T233 — sales-invoice register. Lists every posted
/// (non-credit-note) sales invoice in the period with the line
/// items the inspector cares about: doc number, date, customer
/// name + TIN, subtotal, VAT, grand total. Totals row at the
/// bottom matches the period's VAT-monthly-report sales-side
/// totals (cross-checks both reports against each other).
/// </summary>
public static class SalesInvoiceRegisterPdfRenderer
{
    public sealed record Row(
        string DocumentNumber,
        DateOnly DocumentDate,
        string CustomerNameEn,
        string CustomerNameAr,
        string? CustomerTin,
        decimal Subtotal,
        decimal Vat,
        decimal Total
    );

    public static byte[] Render(
        Company company,
        DateOnly periodStart,
        DateOnly periodEnd,
        DateTime generatedAtUtc,
        IReadOnlyList<Row> rows
    )
    {
        ArgumentNullException.ThrowIfNull(rows);
        return RegisterPageShell.Render(
            titleEn: "Sales Invoice Register",
            titleAr: "سجل فواتير البيع",
            company: company,
            periodStart: periodStart,
            periodEnd: periodEnd,
            generatedAtUtc: generatedAtUtc,
            renderBody: c => RenderBody(c, rows)
        );
    }

    private static void RenderBody(IContainer container, IReadOnlyList<Row> rows)
    {
        if (rows.Count == 0)
        {
            container.AlignCenter().Text("No posted sales invoices in this period.").Italic();
            return;
        }

        container.Column(col =>
        {
            col.Item().Element(c => RenderTable(c, rows));
            col.Item().PaddingTop(8).Element(c => RenderTotals(c, rows));
        });
    }

    private static void RenderTable(IContainer container, IReadOnlyList<Row> rows)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(28); // #
                c.ConstantColumn(80); // doc no
                c.ConstantColumn(70); // date
                c.RelativeColumn(); // customer
                c.ConstantColumn(70); // TIN
                c.ConstantColumn(70); // subtotal
                c.ConstantColumn(60); // VAT
                c.ConstantColumn(70); // total
            });

            table.Header(h =>
            {
                h.Cell().Text("#").Bold();
                h.Cell().Text("Doc no.").Bold();
                h.Cell().Text("Date").Bold();
                h.Cell().Text("Customer / العميل").Bold();
                h.Cell().Text("TIN").Bold();
                h.Cell().AlignRight().Text("Subtotal").Bold();
                h.Cell().AlignRight().Text("VAT").Bold();
                h.Cell().AlignRight().Text("Total").Bold();
            });

            var i = 1;
            foreach (var r in rows)
            {
                table.Cell().Text(i.ToString(System.Globalization.CultureInfo.InvariantCulture));
                table.Cell().Text(r.DocumentNumber);
                table.Cell().Text(RegisterPageShell.Date(r.DocumentDate));
                table
                    .Cell()
                    .Column(cc =>
                    {
                        cc.Item().Text(r.CustomerNameEn);
                        cc.Item().Text(r.CustomerNameAr).FontSize(8);
                    });
                table.Cell().Text(r.CustomerTin ?? "—");
                table.Cell().AlignRight().Text(RegisterPageShell.Money(r.Subtotal));
                table.Cell().AlignRight().Text(RegisterPageShell.Money(r.Vat));
                table.Cell().AlignRight().Text(RegisterPageShell.Money(r.Total));
                i++;
            }
        });
    }

    private static void RenderTotals(IContainer container, IReadOnlyList<Row> rows)
    {
        var subtotal = rows.Sum(r => r.Subtotal);
        var vat = rows.Sum(r => r.Vat);
        var total = rows.Sum(r => r.Total);
        container
            .AlignRight()
            .Column(col =>
            {
                col.Item()
                    .AlignRight()
                    .Text($"Subtotal: {RegisterPageShell.Money(subtotal)} EGP")
                    .Bold();
                col.Item()
                    .AlignRight()
                    .Text($"VAT total: {RegisterPageShell.Money(vat)} EGP")
                    .Bold();
                col.Item()
                    .AlignRight()
                    .Text($"Grand total: {RegisterPageShell.Money(total)} EGP")
                    .Bold()
                    .FontSize(11);
                col.Item().AlignRight().Text($"Invoices: {rows.Count}").FontSize(9);
            });
    }
}
