using System.Globalization;
using EgyptTax.Application.Wht;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EgyptTax.Infrastructure.Pdf;

/// <summary>
/// FR-045 / US7 / T207 — bilingual WHT certificate (شهادة خصم)
/// renderer. Reads the same <see cref="WhtCertificatePayload"/>
/// the contract test (T198) validates against
/// <c>contracts/wht-certificate.schema.json</c>, so any field a
/// regulator inspector reads on the PDF is also present in the
/// JSON shape — no per-channel drift.
///
/// Layout (one A4 page):
///   * Title band: bilingual title + certificate number.
///   * Header band: issuer (left, the company that withholds) +
///     direction badge + counterparty (right, the party from whom
///     tax was withheld).
///   * Source-document panel: source invoice + source voucher
///     numbers + dates so an inspector can pivot to the
///     originating documents.
///   * Withholding details: category code + bilingual category
///     name + rate + gross + amount withheld + net.
///   * Signature block + currency / language footer.
/// </summary>
public sealed class WhtCertificatePdfRenderer
{
    private const string PrimaryFontFamily = QuestPdfFontInitializer.PrimaryFontFamily;
    private const string ArabicFallbackFamily = QuestPdfFontInitializer.ArabicFallbackFamily;

    static WhtCertificatePdfRenderer() => QuestPdfFontInitializer.EnsureRegistered();

    public static byte[] Render(WhtCertificatePayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var doc = Document.Create(c =>
        {
            c.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(t =>
                    t.FontSize(10).FontFamily(PrimaryFontFamily, ArabicFallbackFamily)
                );

                page.Header()
                    .Column(col =>
                    {
                        col.Item()
                            .AlignCenter()
                            .Text("WHT Certificate / شهادة خصم")
                            .Bold()
                            .FontSize(16);
                        col.Item()
                            .AlignCenter()
                            .Text(
                                $"No. {payload.CertificateNumber}  •  {DirectionLabelEn(payload.Direction)}"
                            )
                            .FontSize(10);
                        col.Item().PaddingTop(6).LineHorizontal(0.75f);
                    });

                page.Content()
                    .PaddingVertical(10)
                    .Column(col =>
                    {
                        // Issuer + counterparty band.
                        col.Item()
                            .Row(row =>
                            {
                                row.RelativeItem()
                                    .Column(left =>
                                    {
                                        left.Item().Text("Issuer / المُصدِر").Bold().FontSize(11);
                                        left.Item().Text(payload.IssuerCompanyName.En);
                                        left.Item().Text(payload.IssuerCompanyName.Ar).FontSize(9);
                                        left.Item().Text($"TIN: {payload.IssuerCompanyTin}");
                                    });
                                row.RelativeItem()
                                    .AlignRight()
                                    .Column(right =>
                                    {
                                        right
                                            .Item()
                                            .AlignRight()
                                            .Text("Counterparty / الطرف الآخر")
                                            .Bold()
                                            .FontSize(11);
                                        right.Item().AlignRight().Text(payload.CounterpartyName.En);
                                        right
                                            .Item()
                                            .AlignRight()
                                            .Text(payload.CounterpartyName.Ar)
                                            .FontSize(9);
                                        right
                                            .Item()
                                            .AlignRight()
                                            .Text($"TIN: {payload.CounterpartyTin}");
                                    });
                            });

                        col.Item().PaddingTop(12).LineHorizontal(0.25f);

                        // Source documents.
                        col.Item()
                            .PaddingTop(10)
                            .Text("Source documents / المستندات الأصلية")
                            .Bold()
                            .FontSize(11);
                        col.Item()
                            .Row(row =>
                            {
                                row.RelativeItem()
                                    .Column(left =>
                                    {
                                        left.Item()
                                            .Text($"Invoice no.: {payload.SourceInvoiceNumber}");
                                        left.Item()
                                            .Text(
                                                $"Invoice date: {payload.SourceInvoiceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}"
                                            );
                                    });
                                row.RelativeItem()
                                    .AlignRight()
                                    .Column(right =>
                                    {
                                        right
                                            .Item()
                                            .AlignRight()
                                            .Text($"Voucher no.: {payload.SourceVoucherNumber}");
                                        right
                                            .Item()
                                            .AlignRight()
                                            .Text(
                                                $"Voucher date: {payload.SourceVoucherDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}"
                                            );
                                    });
                            });

                        col.Item().PaddingTop(12).LineHorizontal(0.25f);

                        // Withholding details.
                        col.Item()
                            .PaddingTop(10)
                            .Text("Withholding details / تفاصيل الخصم")
                            .Bold()
                            .FontSize(11);
                        col.Item().PaddingTop(4).Element(e => RenderDetailsTable(e, payload));

                        // Footer band.
                        col.Item()
                            .PaddingTop(20)
                            .Row(row =>
                            {
                                row.RelativeItem()
                                    .Column(left =>
                                    {
                                        left.Item()
                                            .Text(
                                                $"Issued at (UTC): {payload.IssuedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}"
                                            )
                                            .FontSize(8);
                                        left.Item()
                                            .Text(
                                                $"Currency: {payload.Currency}  •  Language: {payload.Language}"
                                            )
                                            .FontSize(8);
                                    });
                                row.ConstantItem(180)
                                    .AlignRight()
                                    .Column(right =>
                                    {
                                        right
                                            .Item()
                                            .AlignRight()
                                            .Text("Authorised signature / توقيع معتمد")
                                            .FontSize(9);
                                        right.Item().PaddingTop(20).LineHorizontal(0.5f);
                                    });
                            });
                    });

                page.Footer()
                    .AlignCenter()
                    .Text(t =>
                    {
                        t.Span("FR-045 ").FontSize(7);
                        t.Span("•").FontSize(7);
                        t.Span($" Cert {payload.CertificateNumber}").FontSize(7);
                    });
            });
        });

        return doc.GeneratePdf();
    }

    private static void RenderDetailsTable(IContainer container, WhtCertificatePayload p)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(2);
                c.RelativeColumn(3);
            });

            void Row(string label, string value, bool bold = false)
            {
                table.Cell().Padding(2).Text(label).Bold();
                var cell = table.Cell().Padding(2);
                if (bold)
                    cell.Text(value).Bold().FontSize(11);
                else
                    cell.Text(value);
            }

            Row("Category code / كود الفئة", p.WhtCategoryCode);
            Row("Category name / اسم الفئة", $"{p.WhtCategoryName.En} ({p.WhtCategoryName.Ar})");
            Row(
                "Rate applied / النسبة",
                $"{p.RateAppliedPercent.ToString("0.##", CultureInfo.InvariantCulture)} %"
            );
            if (p.GrossPayment is not null)
            {
                Row(
                    "Gross payment / إجمالي المبلغ",
                    $"{p.GrossPayment.Value.ToString("F2", CultureInfo.InvariantCulture)} EGP"
                );
            }
            Row(
                "Amount withheld / المبلغ المخصوم",
                $"{p.AmountWithheld.ToString("F2", CultureInfo.InvariantCulture)} EGP",
                bold: true
            );
            if (p.NetPayment is not null)
            {
                Row(
                    "Net payment / صافي المدفوع",
                    $"{p.NetPayment.Value.ToString("F2", CultureInfo.InvariantCulture)} EGP"
                );
            }
        });
    }

    private static string DirectionLabelEn(string direction) =>
        direction switch
        {
            "OutboundToSupplier" => "Outbound — to supplier",
            "InboundFromCustomer" => "Inbound — from customer",
            _ => direction,
        };
}
