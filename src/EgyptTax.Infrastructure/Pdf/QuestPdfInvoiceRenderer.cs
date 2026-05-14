using System.Globalization;
using EgyptTax.Application.Pdf;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Workflow;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Localization;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EgyptTax.Infrastructure.Pdf;

/// <summary>
/// FR-033 / SC-008 — bilingual sales-invoice PDF renderer. The visual
/// design is intentionally restrained at the MVP stage: every
/// legally required field per <c>contracts/legal-invoice-fields.md</c>
/// is rendered as plain text in a predictable place so the SC-008
/// contract test (T079) can assert presence by string match against
/// the extracted PDF text. Pretty layout polish lands in a later
/// styling pass; the contract is "every field present", not "looks
/// nice". QuestPDF Community license is set once at static
/// construction (per QuestPDF's licensing requirement; the project
/// qualifies under the &lt;$1M revenue threshold).
/// </summary>
public sealed class QuestPdfInvoiceRenderer : ISalesInvoicePdfRenderer
{
    private const string PrimaryFontFamily = QuestPdfFontInitializer.PrimaryFontFamily;
    private const string ArabicFallbackFamily = QuestPdfFontInitializer.ArabicFallbackFamily;

    static QuestPdfInvoiceRenderer() => QuestPdfFontInitializer.EnsureRegistered();

    /// <summary>v4 C.9 — palette + ornament settings derived from
    /// <see cref="InvoicePdfRequest.Template"/>. Hard-coded for the
    /// three v4 variants; a designer mode would replace this with a
    /// settings-driven palette.</summary>
    private readonly record struct TemplateStyle(
        string AccentHex,
        string MutedAccentHex,
        bool HasHeaderBand,
        bool HasTotalsHighlight,
        bool DenseLayout);

    private static TemplateStyle StyleFor(EgyptTax.Domain.MasterData.InvoicePdfTemplate template) =>
        template switch
        {
            // Modern — slate-blue accent + colored header band + tinted totals row.
            EgyptTax.Domain.MasterData.InvoicePdfTemplate.Modern =>
                new TemplateStyle("#1E3A8A", "#DBE2F1", true, true, false),
            // Minimal — soft gray, no ornament, generous whitespace.
            EgyptTax.Domain.MasterData.InvoicePdfTemplate.Minimal =>
                new TemplateStyle("#374151", "#F3F4F6", false, false, false),
            // Classic (default) — black-on-white, no accents.
            _ => new TemplateStyle("#000000", "#FFFFFF", false, false, false),
        };

    public byte[] Render(InvoicePdfRequest request)
    {
        // Scattered honeypot — block PDF generation on unlicensed installs.
        LicenseSentry.EnsureLicensed("QuestPdfInvoiceRenderer.Render");
        ArgumentNullException.ThrowIfNull(request);
        var invoice = request.Invoice;
        if (invoice.State != DocumentState.Posted)
        {
            throw new InvalidOperationException(
                "PDF rendering is only valid for posted sales invoices."
            );
        }

        var labelEn = DocumentTypeLabelEnglish(invoice, request.Receiver);
        var labelAr = DocumentTypeLabelArabic(invoice, request.Receiver);
        var style = StyleFor(request.Template);
        var qrPng = RenderQrPng(request.SealQrPayload);
        // v4 A.5 — second QR for the customer-portal magic link,
        // rendered only when a portal URL was supplied. Operator
        // may not have generated a link for this customer yet.
        var portalQrPng = string.IsNullOrWhiteSpace(request.PortalUrl)
            ? null
            : RenderQrPng(request.PortalUrl);
        // FR-014 Arabic-words converter is non-negative-only by spec;
        // for credit notes (which carry negative totals) we render the
        // absolute value with a "(credit)" prefix so the PDF reads
        // sensibly without blowing up the converter.
        var grandTotalArabicWords = invoice.IsCreditNote
            ? "(credit) "
                + ArabicWordsConverter.FromEgyptianPounds(
                    EgyptTax.SharedKernel.MoneyEgp.From(Math.Abs(invoice.GrandTotal.Amount))
                )
            : ArabicWordsConverter.FromEgyptianPounds(invoice.GrandTotal);

        var doc = QuestPDF.Fluent.Document.Create(c =>
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
                        // v4 C.9 — Modern variant lays a thin accent
                        // band above the issuer block. Classic +
                        // Minimal skip it (the Container is invisible
                        // when no background is applied).
                        if (style.HasHeaderBand)
                        {
                            col.Item()
                                .Background(style.AccentHex)
                                .Padding(6)
                                .Row(band =>
                                {
                                    band.RelativeItem()
                                        .Text(request.Issuer.LegalName.English)
                                        .FontColor("#FFFFFF").Bold().FontSize(11);
                                    band.RelativeItem().AlignRight()
                                        .Text(labelEn)
                                        .FontColor("#FFFFFF").Bold().FontSize(11);
                                });
                        }
                        col.Item()
                            .PaddingTop(style.HasHeaderBand ? 6 : 0)
                            .Row(row =>
                            {
                                row.RelativeItem()
                                    .Column(left =>
                                    {
                                        left.Item()
                                            .Text(request.Issuer.LegalName.English)
                                            .Bold()
                                            .FontColor(style.AccentHex)
                                            .FontSize(14);
                                        left.Item()
                                            .Text(request.Issuer.LegalName.Arabic)
                                            .FontSize(12);
                                        left.Item()
                                            .Text($"TIN: {request.Issuer.TaxRegistrationNumber}");
                                        left.Item()
                                            .Text(
                                                $"CR: {request.Issuer.CommercialRegistrationNumber}"
                                            );
                                        left.Item().Text(request.Issuer.Address.DisplayEnglish);
                                        left.Item().Text(request.Issuer.Address.DisplayArabic);
                                    });
                                row.RelativeItem()
                                    .AlignRight()
                                    .Column(right =>
                                    {
                                        right.Item().AlignRight().Text(labelEn).Bold()
                                            .FontColor(style.AccentHex).FontSize(14);
                                        right.Item().AlignRight().Text(labelAr).FontSize(12);
                                        right
                                            .Item()
                                            .AlignRight()
                                            .Text($"No. {invoice.DocumentNumber}");
                                        right
                                            .Item()
                                            .AlignRight()
                                            .Text(
                                                $"Date: {invoice.DocumentDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}"
                                            );
                                        right.Item().AlignRight().Text("Currency: EGP / جنيه مصري");
                                    });
                            });
                    });

                page.Content()
                    .PaddingVertical(15)
                    .Column(col =>
                    {
                        col.Item().Text("Bill to / فاتورة إلى").Bold();
                        col.Item().Text(request.Receiver.Name.English);
                        col.Item().Text(request.Receiver.Name.Arabic);
                        col.Item().Text(request.Receiver.Address.DisplayEnglish);
                        col.Item().Text(request.Receiver.Address.DisplayArabic);

                        if (
                            invoice.CustomerTaxProfileSnapshot.ProfileType
                                == CustomerTaxProfileType.B2BRegistered
                            && invoice.CustomerTaxProfileSnapshot.TinValue is { } tin
                        )
                        {
                            col.Item().Text($"Customer TIN: {tin}");
                        }
                        if (!string.IsNullOrWhiteSpace(request.Receiver.Phone))
                        {
                            col.Item().Text($"Phone: {request.Receiver.Phone}");
                        }

                        // FR-013 / legal-invoice-fields.md section G —
                        // credit-note specifics. Reference to the
                        // original invoice number + date + reason. Only
                        // rendered when the document is a credit note.
                        if (invoice.IsCreditNote && request.OriginalInvoiceReference is { } orig)
                        {
                            col.Item()
                                .PaddingTop(10)
                                .Text("Credit note details / تفاصيل إشعار الخصم")
                                .Bold();
                            col.Item().Text($"Original invoice number: {orig.DocumentNumber}");
                            col.Item()
                                .Text(
                                    $"Original invoice date: {orig.DocumentDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}"
                                );
                            if (!string.IsNullOrWhiteSpace(invoice.CreditNoteReason))
                            {
                                col.Item().Text($"Reason: {invoice.CreditNoteReason}");
                            }
                        }

                        col.Item().PaddingTop(10).Element(c => RenderLines(c, request, style));

                        col.Item()
                            .PaddingTop(10)
                            .AlignRight()
                            .Column(totals =>
                            {
                                totals
                                    .Item()
                                    .AlignRight()
                                    .Text(
                                        $"Subtotal: {invoice.Subtotal.Amount.ToString("F2", CultureInfo.InvariantCulture)} EGP"
                                    );
                                totals
                                    .Item()
                                    .AlignRight()
                                    .Text(
                                        $"VAT total: {invoice.VatTotal.Amount.ToString("F2", CultureInfo.InvariantCulture)} EGP"
                                    );
                                // v4 C.9 — Modern variant tints the
                                // grand-total line to draw the eye.
                                var grandItem = totals.Item();
                                if (style.HasTotalsHighlight)
                                {
                                    grandItem = grandItem
                                        .Background(style.MutedAccentHex)
                                        .PaddingHorizontal(8)
                                        .PaddingVertical(4);
                                }
                                grandItem
                                    .AlignRight()
                                    .Text(
                                        $"Grand total: {invoice.GrandTotal.Amount.ToString("F2", CultureInfo.InvariantCulture)} EGP"
                                    )
                                    .Bold()
                                    .FontColor(style.AccentHex)
                                    .FontSize(12);
                            });

                        col.Item().PaddingTop(8).Text($"In words: {grandTotalArabicWords}");

                        col.Item()
                            .PaddingTop(20)
                            .Row(row =>
                            {
                                row.RelativeItem()
                                    .Column(left =>
                                    {
                                        left.Item()
                                            .Text($"Posted by: {request.PostedByUserDisplayName}");
                                        var postedAt = invoice.PostedAtUtc ?? DateTime.UtcNow;
                                        left.Item()
                                            .Text(
                                                $"Posted at (UTC): {postedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}"
                                            );
                                        left.Item()
                                            .Text(
                                                $"Posted at (Cairo): {ToCairoTime(postedAt).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}"
                                            );
                                        left.Item()
                                            .Text(
                                                "Amounts rounded to two decimal places / الأرقام مقربة لأقرب قرشين"
                                            )
                                            .FontSize(8);
                                    });
                                row.ConstantItem(120)
                                    .AlignRight()
                                    .Column(right =>
                                    {
                                        right.Item().AlignRight().Image(qrPng).FitArea();
                                        right
                                            .Item()
                                            .AlignRight()
                                            .Text("Verification seal")
                                            .FontSize(8);
                                    });
                                if (portalQrPng is not null)
                                {
                                    row.ConstantItem(120)
                                        .AlignRight()
                                        .Column(right =>
                                        {
                                            right.Item().AlignRight().Image(portalQrPng).FitArea();
                                            right
                                                .Item()
                                                .AlignRight()
                                                .Text("امسح للوصول لكشفك / Scan for your statement")
                                                .FontSize(7);
                                        });
                                }
                            });
                    });

                page.Footer()
                    .AlignCenter()
                    .Text(t =>
                    {
                        t.Span("EGT/").FontSize(8);
                        t.Span(invoice.Id.ToString("D")).FontSize(8);
                    });
            });
        });

        return doc.GeneratePdf();
    }

    private static void RenderLines(IContainer container, InvoicePdfRequest request, TemplateStyle style)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(28); // index
                c.ConstantColumn(70); // item code
                c.RelativeColumn(); // description
                c.ConstantColumn(40); // qty
                c.ConstantColumn(60); // unit price
                c.ConstantColumn(60); // line subtotal
                c.ConstantColumn(40); // vat %
                c.ConstantColumn(60); // line vat
                c.ConstantColumn(60); // line total
            });

            // v4 C.9 — Modern paints the header row in the muted
            // accent for visual rhythm. Classic + Minimal leave it
            // bare so the SC-008 contract test still finds plain
            // header text.
            void HeaderCell(IContainer c, string text, bool alignRight = false)
            {
                var cell = c;
                if (style.HasTotalsHighlight)
                {
                    cell = cell.Background(style.MutedAccentHex).Padding(2);
                }
                if (alignRight)
                {
                    cell.AlignRight().Text(text).Bold().FontColor(style.AccentHex);
                }
                else
                {
                    cell.Text(text).Bold().FontColor(style.AccentHex);
                }
            }

            table.Header(header =>
            {
                HeaderCell(header.Cell(), "#");
                HeaderCell(header.Cell(), "Item code");
                HeaderCell(header.Cell(), "Description / الوصف");
                HeaderCell(header.Cell(), "Qty", alignRight: true);
                HeaderCell(header.Cell(), "Unit price", alignRight: true);
                HeaderCell(header.Cell(), "Subtotal", alignRight: true);
                HeaderCell(header.Cell(), "VAT %", alignRight: true);
                HeaderCell(header.Cell(), "VAT", alignRight: true);
                HeaderCell(header.Cell(), "Total", alignRight: true);
            });

            var index = 1;
            foreach (var line in request.Invoice.Lines)
            {
                var itemCode = request.Items.TryGetValue(line.ItemId, out var item)
                    ? item.Code
                    : line.ItemId.ToString("N");
                var itemNameEn = request.Items.TryGetValue(line.ItemId, out item)
                    ? item.Name.English
                    : itemCode;
                var itemNameAr = request.Items.TryGetValue(line.ItemId, out item)
                    ? item.Name.Arabic
                    : "";

                table.Cell().Text(index.ToString(CultureInfo.InvariantCulture));
                table.Cell().Text(itemCode);
                table
                    .Cell()
                    .Column(c =>
                    {
                        c.Item().Text(itemNameEn);
                        c.Item().Text(itemNameAr).FontSize(8);
                    });
                table
                    .Cell()
                    .AlignRight()
                    .Text(line.Quantity.ToString("0.##", CultureInfo.InvariantCulture));
                table
                    .Cell()
                    .AlignRight()
                    .Text(line.UnitPrice.Amount.ToString("F2", CultureInfo.InvariantCulture));
                table
                    .Cell()
                    .AlignRight()
                    .Text(line.LineSubtotal.Amount.ToString("F2", CultureInfo.InvariantCulture));
                table
                    .Cell()
                    .AlignRight()
                    .Text(line.VatRatePercent.ToString("0.##", CultureInfo.InvariantCulture));
                table
                    .Cell()
                    .AlignRight()
                    .Text(line.LineVat.Amount.ToString("F2", CultureInfo.InvariantCulture));
                table
                    .Cell()
                    .AlignRight()
                    .Text(line.LineTotal.Amount.ToString("F2", CultureInfo.InvariantCulture));

                index++;
            }
        });
    }

    private static string DocumentTypeLabelEnglish(SalesInvoice invoice, Customer receiver)
    {
        if (invoice.IsCreditNote)
        {
            return "Credit Note";
        }
        return invoice.CustomerTaxProfileSnapshot.ProfileType == CustomerTaxProfileType.B2CConsumer
            ? "Simplified Tax Invoice"
            : "Tax Invoice";
    }

    private static string DocumentTypeLabelArabic(SalesInvoice invoice, Customer receiver)
    {
        if (invoice.IsCreditNote)
        {
            return "إشعار خصم";
        }
        return invoice.CustomerTaxProfileSnapshot.ProfileType == CustomerTaxProfileType.B2CConsumer
            ? "فاتورة ضريبية مبسطة"
            : "فاتورة ضريبية";
    }

    private static byte[] RenderQrPng(string payload)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.L);
        var pngQr = new PngByteQRCode(qrData);
        return pngQr.GetGraphic(pixelsPerModule: 6);
    }

    private static DateTime ToCairoTime(DateTime utc)
    {
        // Egypt observes UTC+2 year-round (no DST since 2014, briefly resumed
        // in 2023 then suspended again — UTC+2 is the safe MVP default; the
        // operator config can override later via TimeZoneInfo lookup).
        return utc.AddHours(2);
    }
}
