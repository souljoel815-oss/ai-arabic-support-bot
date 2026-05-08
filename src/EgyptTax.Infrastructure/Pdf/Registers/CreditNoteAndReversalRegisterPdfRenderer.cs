using EgyptTax.Domain.MasterData;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EgyptTax.Infrastructure.Pdf.Registers;

/// <summary>
/// FR-013 / FR-048 / T233 — credit-note + reversal register.
/// Lists every posted credit note in the period (sales-side
/// CreditNoteOfInvoiceId is non-null) plus any voided documents
/// dated in the period (the "reversal" axis: the operator either
/// voided a draft or unwound a posted doc via credit note). The
/// "Reason" column carries the FR-013 free-text justification —
/// inspectors flag credit notes lacking a reason as suspicious.
/// </summary>
public static class CreditNoteAndReversalRegisterPdfRenderer
{
    public sealed record Row(
        string DocumentNumber,
        DateOnly DocumentDate,
        string CustomerNameEn,
        string CustomerNameAr,
        string OriginalDocumentNumber,
        DateOnly OriginalDocumentDate,
        string Reason,
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
            titleEn: "Credit Note & Reversal Register",
            titleAr: "سجل إشعارات الخصم وعكس القيود",
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
            container
                .AlignCenter()
                .Text("No credit notes or reversals posted in this period.")
                .Italic();
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
                c.ConstantColumn(70); // CN doc no
                c.ConstantColumn(65); // CN date
                c.RelativeColumn(1.2f); // customer
                c.ConstantColumn(70); // orig doc no
                c.ConstantColumn(65); // orig date
                c.RelativeColumn(1.4f); // reason
                c.ConstantColumn(60); // subtotal
                c.ConstantColumn(50); // VAT
                c.ConstantColumn(60); // total
            });

            table.Header(h =>
            {
                h.Cell().Text("#").Bold();
                h.Cell().Text("CN no.").Bold();
                h.Cell().Text("CN date").Bold();
                h.Cell().Text("Customer / العميل").Bold();
                h.Cell().Text("Orig. no.").Bold();
                h.Cell().Text("Orig. date").Bold();
                h.Cell().Text("Reason / السبب").Bold();
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
                table.Cell().Text(r.OriginalDocumentNumber);
                table.Cell().Text(RegisterPageShell.Date(r.OriginalDocumentDate));
                table.Cell().Text(r.Reason).FontSize(8);
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
                    .Text($"Credit subtotal: {RegisterPageShell.Money(subtotal)} EGP")
                    .Bold();
                col.Item()
                    .AlignRight()
                    .Text($"Credit VAT: {RegisterPageShell.Money(vat)} EGP")
                    .Bold();
                col.Item()
                    .AlignRight()
                    .Text($"Credit total: {RegisterPageShell.Money(total)} EGP")
                    .Bold()
                    .FontSize(11);
                col.Item().AlignRight().Text($"Credit notes: {rows.Count}").FontSize(9);
            });
    }
}
