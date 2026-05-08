using System.Globalization;
using EgyptTax.Application.Wht;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EgyptTax.Infrastructure.Pdf;

/// <summary>
/// FR-046 / US7 / T209 (PDF half) — bilingual Form 41 (نموذج 41)
/// renderer. Reads the same <see cref="Form41Payload"/> the
/// schema-validated JSON (T199) uses, so the PDF + JSON match
/// field-for-field. Layout (one or more A4 pages):
///
///   * Title band with company TIN + name + fiscal-year + quarter.
///   * Per-supplier line table (paginates automatically when long).
///   * Totals + per-category breakdown.
///   * Reconciliation block — flags dirty filings prominently.
///   * Footer with prepared-at / prepared-by / page numbers.
/// </summary>
public sealed class Form41PdfRenderer
{
    private const string PrimaryFontFamily = QuestPdfFontInitializer.PrimaryFontFamily;
    private const string ArabicFallbackFamily = QuestPdfFontInitializer.ArabicFallbackFamily;

    static Form41PdfRenderer() => QuestPdfFontInitializer.EnsureRegistered();

    public static byte[] Render(Form41Payload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var doc = Document.Create(c =>
        {
            c.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(t =>
                    t.FontSize(9).FontFamily(PrimaryFontFamily, ArabicFallbackFamily)
                );

                page.Header()
                    .Column(col =>
                    {
                        col.Item()
                            .AlignCenter()
                            .Text("Form 41 — Quarterly WHT Return / نموذج 41 — إقرار الخصم الفصلي")
                            .Bold()
                            .FontSize(14);
                        col.Item()
                            .AlignCenter()
                            .Text(
                                $"{payload.FilingHeader.CompanyName.En} ({payload.FilingHeader.CompanyName.Ar})"
                            )
                            .FontSize(11);
                        col.Item()
                            .AlignCenter()
                            .Text(
                                $"TIN: {payload.FilingHeader.CompanyTin}  •  FY {payload.FilingHeader.FiscalYear} Q{payload.FilingHeader.Quarter}  •  {payload.FilingHeader.FillingPeriodStart:yyyy-MM-dd} → {payload.FilingHeader.FillingPeriodEnd:yyyy-MM-dd}"
                            )
                            .FontSize(9);
                        col.Item().PaddingTop(6).LineHorizontal(0.5f);
                    });

                page.Content()
                    .PaddingVertical(10)
                    .Column(col =>
                    {
                        col.Item().Text("Lines / السطور").Bold().FontSize(11);
                        col.Item().PaddingTop(4).Element(e => RenderLinesTable(e, payload));

                        col.Item().PaddingTop(12).LineHorizontal(0.25f);

                        col.Item()
                            .PaddingTop(10)
                            .Row(row =>
                            {
                                row.RelativeItem().Element(e => RenderTotals(e, payload));
                                row.RelativeItem().Element(e => RenderReconciliation(e, payload));
                            });
                    });

                page.Footer()
                    .AlignCenter()
                    .Text(t =>
                    {
                        t.Span(
                                $"Prepared {payload.FilingHeader.PreparedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} UTC by {payload.FilingHeader.PreparedByUserId:D}  •  "
                            )
                            .FontSize(7);
                        t.Span("Page ").FontSize(7);
                        t.CurrentPageNumber().FontSize(7);
                        t.Span(" of ").FontSize(7);
                        t.TotalPages().FontSize(7);
                    });
            });
        });

        return doc.GeneratePdf();
    }

    private static void RenderLinesTable(IContainer container, Form41Payload payload)
    {
        if (payload.Lines.Count == 0)
        {
            container.Text("(no WHT activity in this quarter)").Italic();
            return;
        }

        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(28); // #
                c.ConstantColumn(72); // supplier TIN
                c.RelativeColumn(2); // supplier name (bilingual)
                c.ConstantColumn(75); // category
                c.ConstantColumn(40); // rate %
                c.ConstantColumn(70); // gross
                c.ConstantColumn(60); // withheld
                c.ConstantColumn(80); // SPV no.
                c.ConstantColumn(70); // SPV date
                c.ConstantColumn(80); // invoice no.
                c.ConstantColumn(95); // cert no.
            });

            table.Header(h =>
            {
                h.Cell().Text("#").Bold();
                h.Cell().Text("Supplier TIN").Bold();
                h.Cell().Text("Supplier / المورد").Bold();
                h.Cell().Text("Category").Bold();
                h.Cell().AlignRight().Text("Rate %").Bold();
                h.Cell().AlignRight().Text("Gross (EGP)").Bold();
                h.Cell().AlignRight().Text("Withheld").Bold();
                h.Cell().Text("Voucher").Bold();
                h.Cell().Text("V. date").Bold();
                h.Cell().Text("Invoice").Bold();
                h.Cell().Text("Cert").Bold();
            });

            var i = 1;
            foreach (var line in payload.Lines)
            {
                table.Cell().Text(i.ToString(CultureInfo.InvariantCulture));
                table.Cell().Text(line.SupplierTin);
                table
                    .Cell()
                    .Column(cc =>
                    {
                        cc.Item().Text(line.SupplierName.En);
                        cc.Item().Text(line.SupplierName.Ar).FontSize(8);
                    });
                table.Cell().Text(line.WhtCategoryCode);
                table
                    .Cell()
                    .AlignRight()
                    .Text(line.RateAppliedPercent.ToString("0.##", CultureInfo.InvariantCulture));
                table
                    .Cell()
                    .AlignRight()
                    .Text(line.GrossPaymentTotal.ToString("F2", CultureInfo.InvariantCulture));
                table
                    .Cell()
                    .AlignRight()
                    .Text(line.AmountWithheld.ToString("F2", CultureInfo.InvariantCulture));
                table.Cell().Text(line.SupplierPaymentVoucherNumber).FontSize(8);
                table
                    .Cell()
                    .Text(
                        line.SupplierPaymentVoucherDate.ToString(
                            "yyyy-MM-dd",
                            CultureInfo.InvariantCulture
                        )
                    )
                    .FontSize(8);
                table.Cell().Text(line.SourceInvoiceNumber).FontSize(8);
                table.Cell().Text(line.OutboundCertificateNumber ?? "—").FontSize(8);
                i++;
            }
        });
    }

    private static void RenderTotals(IContainer container, Form41Payload payload)
    {
        container.Column(col =>
        {
            col.Item().Text("Totals / الإجماليات").Bold().FontSize(11);
            col.Item().PaddingTop(4).Text($"Lines: {payload.Totals.LineCount}");
            col.Item()
                .Text(
                    $"Total gross: {payload.Totals.TotalGrossPayment.ToString("F2", CultureInfo.InvariantCulture)} EGP"
                );
            col.Item()
                .Text(
                    $"Total withheld: {payload.Totals.TotalAmountWithheld.ToString("F2", CultureInfo.InvariantCulture)} EGP"
                )
                .Bold()
                .FontSize(11);

            if (payload.Totals.ByCategory.Count > 0)
            {
                col.Item().PaddingTop(6).Text("By category:").Bold().FontSize(9);
                foreach (var cat in payload.Totals.ByCategory)
                {
                    col.Item()
                        .Text(
                            $"  {cat.WhtCategoryCode}: {cat.LineCount} line(s), {cat.AmountWithheld.ToString("F2", CultureInfo.InvariantCulture)} EGP"
                        )
                        .FontSize(9);
                }
            }
        });
    }

    private static void RenderReconciliation(IContainer container, Form41Payload payload)
    {
        container
            .AlignRight()
            .Column(col =>
            {
                col.Item().AlignRight().Text("Reconciliation / المطابقة").Bold().FontSize(11);
                col.Item()
                    .AlignRight()
                    .Text(
                        $"WHT-payable account accrual: {payload.Reconciliation.WhtPayableAccountBalanceAtPeriodEnd.ToString("F2", CultureInfo.InvariantCulture)} EGP"
                    );
                col.Item()
                    .AlignRight()
                    .Text(
                        $"Cert total: {payload.Totals.TotalAmountWithheld.ToString("F2", CultureInfo.InvariantCulture)} EGP"
                    );

                if (payload.Reconciliation.MatchesTotalAmountWithheld)
                {
                    col.Item().AlignRight().Text("Reconciled ✓").Bold();
                }
                else
                {
                    col.Item()
                        .AlignRight()
                        .Text(
                            $"DISCREPANCY: {payload.Reconciliation.DiscrepancyAmount?.ToString("F2", CultureInfo.InvariantCulture)} EGP"
                        )
                        .Bold();
                    col.Item().AlignRight().Text("MUST be resolved before MarkFiled").FontSize(8);
                }

                col.Item()
                    .PaddingTop(6)
                    .AlignRight()
                    .Text(
                        $"Audit-chain extract: indices {payload.AuditChainExtractRef.StartIndex}..{payload.AuditChainExtractRef.EndIndex}"
                    )
                    .FontSize(7);
                col.Item()
                    .AlignRight()
                    .Text($"hash {payload.AuditChainExtractRef.ExtractSha256[..16]}…")
                    .FontSize(7);
            });
    }
}
