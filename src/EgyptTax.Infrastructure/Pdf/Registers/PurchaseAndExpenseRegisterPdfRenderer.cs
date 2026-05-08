using EgyptTax.Domain.MasterData;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EgyptTax.Infrastructure.Pdf.Registers;

/// <summary>
/// FR-048 / T233 — purchase + expense register. Combines posted
/// purchase invoices (with VAT) and posted expenses (header-only,
/// usually no VAT) in a single chronological listing. The
/// <c>Kind</c> column distinguishes the two so an inspector can
/// scan for "deductible expense without attachment" patterns at
/// a glance. Totals are split: purchase subtotal + VAT, expense
/// total, deductible VAT total.
/// </summary>
public static class PurchaseAndExpenseRegisterPdfRenderer
{
    public enum RowKind { PurchaseInvoice, Expense }

    public sealed record Row(
        RowKind Kind,
        string DocumentNumber,
        DateOnly DocumentDate,
        string CounterpartyEn,
        string CounterpartyAr,
        string? SupplierTin,
        string? SupplierInvoiceNumber,
        decimal Subtotal,
        decimal Vat,
        decimal Total,
        bool DeductibleFlag);

    public static byte[] Render(
        Company company,
        DateOnly periodStart,
        DateOnly periodEnd,
        DateTime generatedAtUtc,
        IReadOnlyList<Row> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return RegisterPageShell.Render(
            titleEn: "Purchase Invoice & Expense Register",
            titleAr: "سجل فواتير الشراء والمصروفات",
            company: company,
            periodStart: periodStart,
            periodEnd: periodEnd,
            generatedAtUtc: generatedAtUtc,
            renderBody: c => RenderBody(c, rows));
    }

    private static void RenderBody(IContainer container, IReadOnlyList<Row> rows)
    {
        if (rows.Count == 0)
        {
            container.AlignCenter().Text("No posted purchase invoices or expenses in this period.").Italic();
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
                c.ConstantColumn(28);  // #
                c.ConstantColumn(50);  // kind
                c.ConstantColumn(70);  // doc no
                c.ConstantColumn(70);  // date
                c.RelativeColumn();    // counterparty
                c.ConstantColumn(70);  // TIN / supplier inv
                c.ConstantColumn(40);  // ded?
                c.ConstantColumn(60);  // subtotal
                c.ConstantColumn(50);  // VAT
                c.ConstantColumn(60);  // total
            });

            table.Header(h =>
            {
                h.Cell().Text("#").Bold();
                h.Cell().Text("Kind").Bold();
                h.Cell().Text("Doc no.").Bold();
                h.Cell().Text("Date").Bold();
                h.Cell().Text("Counterparty / الطرف الآخر").Bold();
                h.Cell().Text("TIN / Sup. inv.").Bold();
                h.Cell().AlignCenter().Text("Ded?").Bold();
                h.Cell().AlignRight().Text("Subtotal").Bold();
                h.Cell().AlignRight().Text("VAT").Bold();
                h.Cell().AlignRight().Text("Total").Bold();
            });

            var i = 1;
            foreach (var r in rows)
            {
                table.Cell().Text(i.ToString(System.Globalization.CultureInfo.InvariantCulture));
                table.Cell().Text(r.Kind == RowKind.PurchaseInvoice ? "PUR" : "EXP");
                table.Cell().Text(r.DocumentNumber);
                table.Cell().Text(RegisterPageShell.Date(r.DocumentDate));
                table.Cell().Column(cc =>
                {
                    cc.Item().Text(r.CounterpartyEn);
                    cc.Item().Text(r.CounterpartyAr).FontSize(8);
                });
                table.Cell().Column(cc =>
                {
                    cc.Item().Text(r.SupplierTin ?? "—").FontSize(8);
                    if (!string.IsNullOrWhiteSpace(r.SupplierInvoiceNumber))
                    {
                        cc.Item().Text(r.SupplierInvoiceNumber).FontSize(8);
                    }
                });
                table.Cell().AlignCenter().Text(r.DeductibleFlag ? "Y" : "N");
                table.Cell().AlignRight().Text(RegisterPageShell.Money(r.Subtotal));
                table.Cell().AlignRight().Text(RegisterPageShell.Money(r.Vat));
                table.Cell().AlignRight().Text(RegisterPageShell.Money(r.Total));
                i++;
            }
        });
    }

    private static void RenderTotals(IContainer container, IReadOnlyList<Row> rows)
    {
        var purchaseSubtotal = rows.Where(r => r.Kind == RowKind.PurchaseInvoice).Sum(r => r.Subtotal);
        var purchaseVat = rows.Where(r => r.Kind == RowKind.PurchaseInvoice).Sum(r => r.Vat);
        var purchaseTotal = rows.Where(r => r.Kind == RowKind.PurchaseInvoice).Sum(r => r.Total);
        var expenseTotal = rows.Where(r => r.Kind == RowKind.Expense).Sum(r => r.Total);
        var deductibleVat = rows.Where(r => r.DeductibleFlag).Sum(r => r.Vat);

        container.AlignRight().Column(col =>
        {
            col.Item().AlignRight().Text(
                $"Purchase subtotal: {RegisterPageShell.Money(purchaseSubtotal)} EGP").Bold();
            col.Item().AlignRight().Text(
                $"Purchase VAT: {RegisterPageShell.Money(purchaseVat)} EGP").Bold();
            col.Item().AlignRight().Text(
                $"Purchase total: {RegisterPageShell.Money(purchaseTotal)} EGP").Bold();
            col.Item().AlignRight().Text(
                $"Expense total: {RegisterPageShell.Money(expenseTotal)} EGP").Bold();
            col.Item().AlignRight().Text(
                $"Deductible-flagged input VAT: {RegisterPageShell.Money(deductibleVat)} EGP")
                .Bold().FontSize(11);
            col.Item().AlignRight().Text(
                $"Rows: {rows.Count}").FontSize(9);
        });
    }
}
