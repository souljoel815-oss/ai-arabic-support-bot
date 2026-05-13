using System.Globalization;
using EgyptTax.Application.Pdf;
using EgyptTax.Domain.Quotations;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EgyptTax.Infrastructure.Pdf;

/// <summary>
/// L1.5 follow-on — concrete QuestPDF-based quotation renderer.
/// Mirrors <see cref="QuestPdfInvoiceRenderer"/>'s shape (header /
/// content / footer + bilingual labels) but drops the legally-
/// required fields that don't apply to non-fiscal documents:
/// no QR seal, no Arabic-words total (operator can request that
/// later if a customer needs it), no credit-note section.
/// </summary>
public sealed class QuestPdfQuotationRenderer : IQuotationPdfRenderer
{
    private const string PrimaryFontFamily = QuestPdfFontInitializer.PrimaryFontFamily;
    private const string ArabicFallbackFamily = QuestPdfFontInitializer.ArabicFallbackFamily;

    static QuestPdfQuotationRenderer() => QuestPdfFontInitializer.EnsureRegistered();

    public byte[] Render(QuotationPdfRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var quotation = request.Quotation;

        var doc = QuestPDF.Fluent.Document.Create(c =>
        {
            c.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(t =>
                    t.FontSize(10).FontFamily(PrimaryFontFamily, ArabicFallbackFamily));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            left.Item().Text(request.Issuer.LegalName.English).Bold().FontSize(14);
                            left.Item().Text(request.Issuer.LegalName.Arabic).FontSize(12);
                            left.Item().Text($"TIN: {request.Issuer.TaxRegistrationNumber}");
                            left.Item().Text(request.Issuer.Address.DisplayEnglish);
                            left.Item().Text(request.Issuer.Address.DisplayArabic);
                        });
                        row.RelativeItem().AlignRight().Column(right =>
                        {
                            right.Item().AlignRight().Text("QUOTATION").Bold().FontSize(14);
                            right.Item().AlignRight().Text("عرض سعر").FontSize(12);
                            right.Item().AlignRight().Text(
                                $"No. {quotation.QuotationNumber ?? "(draft)"}");
                            right.Item().AlignRight().Text(
                                $"Date: {quotation.DocumentDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
                            right.Item().AlignRight().Text(
                                $"Valid until: {quotation.ValidUntilDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
                            right.Item().AlignRight().Text("Currency: EGP / جنيه مصري");
                        });
                    });
                });

                page.Content().PaddingVertical(15).Column(col =>
                {
                    col.Item().Text("Quote for / عرض سعر إلى").Bold();
                    col.Item().Text(request.Receiver.Name.English);
                    col.Item().Text(request.Receiver.Name.Arabic);
                    col.Item().Text(request.Receiver.Address.DisplayEnglish);
                    col.Item().Text(request.Receiver.Address.DisplayArabic);
                    if (quotation.CustomerTaxProfileSnapshot.TinValue is { } tin)
                    {
                        col.Item().Text($"Customer TIN: {tin}");
                    }
                    if (!string.IsNullOrWhiteSpace(request.Receiver.Phone))
                    {
                        col.Item().Text($"Phone: {request.Receiver.Phone}");
                    }

                    col.Item().PaddingTop(10).Element(e => RenderLines(e, request));

                    col.Item().PaddingTop(10).AlignRight().Column(totals =>
                    {
                        totals.Item().AlignRight().Text(
                            $"Subtotal: {quotation.Subtotal.Amount.ToString("F2", CultureInfo.InvariantCulture)} EGP");
                        totals.Item().AlignRight().Text(
                            $"VAT total: {quotation.VatTotal.Amount.ToString("F2", CultureInfo.InvariantCulture)} EGP");
                        totals.Item().AlignRight().Text(
                            $"Grand total: {quotation.GrandTotal.Amount.ToString("F2", CultureInfo.InvariantCulture)} EGP")
                            .Bold().FontSize(12);
                    });

                    if (!string.IsNullOrWhiteSpace(quotation.Notes))
                    {
                        col.Item().PaddingTop(15).Text("Notes / ملاحظات").Bold();
                        col.Item().Text(quotation.Notes);
                    }
                });

                page.Footer().AlignCenter().Column(col =>
                {
                    col.Item().Text(
                        "This is a quotation, not a tax invoice. Prices are valid until the date above; placing an order generates a tax invoice.")
                        .FontSize(8).Italic();
                    col.Item().Text(
                        "هذا عرض سعر وليس فاتورة ضريبية. الأسعار سارية حتى التاريخ المذكور أعلاه؛ تأكيد الطلب يُصدر فاتورة ضريبية.")
                        .FontSize(8).Italic();
                });
            });
        });

        return doc.GeneratePdf();
    }

    private static void RenderLines(IContainer container, QuotationPdfRequest request)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(28);  // index
                c.ConstantColumn(70);  // item code
                c.RelativeColumn();    // description
                c.ConstantColumn(40);  // qty
                c.ConstantColumn(60);  // unit price
                c.ConstantColumn(60);  // subtotal
                c.ConstantColumn(40);  // vat %
                c.ConstantColumn(60);  // vat
                c.ConstantColumn(60);  // total
            });

            table.Header(header =>
            {
                header.Cell().Text("#").Bold();
                header.Cell().Text("Item code").Bold();
                header.Cell().Text("Description / الوصف").Bold();
                header.Cell().AlignRight().Text("Qty").Bold();
                header.Cell().AlignRight().Text("Unit price").Bold();
                header.Cell().AlignRight().Text("Subtotal").Bold();
                header.Cell().AlignRight().Text("VAT %").Bold();
                header.Cell().AlignRight().Text("VAT").Bold();
                header.Cell().AlignRight().Text("Total").Bold();
            });

            var index = 1;
            foreach (var line in request.Quotation.Lines)
            {
                var info = request.Items.TryGetValue(line.ItemId, out var item)
                    ? item
                    : new ItemRenderInfo(line.ItemId.ToString("N"),
                        new SharedKernel.ArabicEnglishText("", ""));

                table.Cell().Text(index.ToString(CultureInfo.InvariantCulture));
                table.Cell().Text(info.Code);
                table.Cell().Column(c =>
                {
                    c.Item().Text(info.Name.English);
                    c.Item().Text(info.Name.Arabic).FontSize(8);
                });
                table.Cell().AlignRight().Text(line.Quantity.ToString("0.##", CultureInfo.InvariantCulture));
                table.Cell().AlignRight().Text(line.UnitPrice.Amount.ToString("F2", CultureInfo.InvariantCulture));
                table.Cell().AlignRight().Text(line.LineSubtotal.Amount.ToString("F2", CultureInfo.InvariantCulture));
                table.Cell().AlignRight().Text(line.VatRatePercent.ToString("0.##", CultureInfo.InvariantCulture));
                table.Cell().AlignRight().Text(line.LineVat.Amount.ToString("F2", CultureInfo.InvariantCulture));
                var lineTotal = line.LineNetBeforeVat.Amount + line.LineVat.Amount;
                table.Cell().AlignRight().Text(lineTotal.ToString("F2", CultureInfo.InvariantCulture));

                index++;
            }
        });
    }
}
