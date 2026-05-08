using System.Globalization;
using EgyptTax.Domain.MasterData;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EgyptTax.Infrastructure.Pdf.Registers;

/// <summary>
/// Shared shell for the five US9 register PDFs (sales-invoice
/// register, purchase+expense register, credit-note+reversal
/// register, general-journal listing, trial balance). Each
/// register supplies its own bilingual title + body builder; the
/// shell handles the page setup, the company/TIN/period banner,
/// and the page-number footer so the five PDFs render with one
/// consistent look the inspector recognises across registers.
/// </summary>
internal static class RegisterPageShell
{
    public static byte[] Render(
        string titleEn,
        string titleAr,
        Company company,
        DateOnly periodStart,
        DateOnly periodEnd,
        DateTime generatedAtUtc,
        Action<IContainer> renderBody)
    {
        QuestPdfFontInitializer.EnsureRegistered();

        var doc = Document.Create(c =>
        {
            c.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(t => t
                    .FontSize(9)
                    .FontFamily(QuestPdfFontInitializer.PrimaryFontFamily,
                                QuestPdfFontInitializer.ArabicFallbackFamily));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            left.Item().Text(company.LegalName.English).Bold().FontSize(13);
                            left.Item().Text(company.LegalName.Arabic).FontSize(11);
                            left.Item().Text($"TIN: {company.TaxRegistrationNumber}").FontSize(9);
                        });
                        row.RelativeItem().AlignRight().Column(right =>
                        {
                            right.Item().AlignRight().Text(titleEn).Bold().FontSize(13);
                            right.Item().AlignRight().Text(titleAr).FontSize(11);
                            right.Item().AlignRight().Text(
                                $"Period: {periodStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} → {periodEnd.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}")
                                .FontSize(9);
                            right.Item().AlignRight().Text(
                                $"Generated (UTC): {generatedAtUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}")
                                .FontSize(8);
                        });
                    });
                    col.Item().PaddingTop(6).LineHorizontal(0.5f);
                });

                page.Content().PaddingVertical(10).Element(renderBody);

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Page ").FontSize(8);
                    t.CurrentPageNumber().FontSize(8);
                    t.Span(" of ").FontSize(8);
                    t.TotalPages().FontSize(8);
                });
            });
        });

        return doc.GeneratePdf();
    }

    public static string Money(decimal amount) =>
        amount.ToString("F2", CultureInfo.InvariantCulture);

    public static string Date(DateOnly d) =>
        d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
