using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Workflow;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EgyptTax.Infrastructure.Pdf.Registers;

/// <summary>
/// FR-024 / FR-048 / T233 — general-journal listing. One block
/// per <c>JournalEntry</c>, showing the source document
/// (number + type) plus every debit/credit line so an inspector
/// can trace each posted invoice/expense down to its accounting
/// effect. The journal-entry factory enforces SUM(debit) ==
/// SUM(credit) at construction; we re-display the per-entry totals
/// so a reviewer can spot any out-of-band tampering instantly.
/// </summary>
public static class GeneralJournalListingPdfRenderer
{
    public sealed record EntryRow(
        DateTime PostedAtUtc,
        string SourceDocumentNumber,
        DocumentType SourceDocumentType,
        IReadOnlyList<LineRow> Lines
    );

    public sealed record LineRow(
        string AccountCode,
        decimal Debit,
        decimal Credit,
        string Description
    );

    public static byte[] Render(
        Company company,
        DateOnly periodStart,
        DateOnly periodEnd,
        DateTime generatedAtUtc,
        IReadOnlyList<EntryRow> entries
    )
    {
        ArgumentNullException.ThrowIfNull(entries);
        return RegisterPageShell.Render(
            titleEn: "General Journal Listing",
            titleAr: "سجل اليومية العامة",
            company: company,
            periodStart: periodStart,
            periodEnd: periodEnd,
            generatedAtUtc: generatedAtUtc,
            renderBody: c => RenderBody(c, entries)
        );
    }

    private static void RenderBody(IContainer container, IReadOnlyList<EntryRow> entries)
    {
        if (entries.Count == 0)
        {
            container.AlignCenter().Text("No journal entries posted in this period.").Italic();
            return;
        }

        container.Column(col =>
        {
            foreach (var e in entries)
            {
                col.Item().PaddingTop(6).Element(c => RenderEntry(c, e));
            }
            col.Item().PaddingTop(10).Element(c => RenderGrandTotals(c, entries));
        });
    }

    private static void RenderEntry(IContainer container, EntryRow e)
    {
        container.Column(col =>
        {
            col.Item()
                .Row(row =>
                {
                    row.RelativeItem()
                        .Text($"{e.SourceDocumentType} • {e.SourceDocumentNumber}")
                        .Bold();
                    row.RelativeItem()
                        .AlignRight()
                        .Text(
                            $"Posted (UTC): {e.PostedAtUtc.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)}"
                        )
                        .FontSize(8);
                });

            col.Item()
                .Element(c =>
                    c.Table(table =>
                    {
                        table.ColumnsDefinition(cd =>
                        {
                            cd.ConstantColumn(80); // account
                            cd.RelativeColumn(); // description
                            cd.ConstantColumn(70); // debit
                            cd.ConstantColumn(70); // credit
                        });

                        table.Header(h =>
                        {
                            h.Cell().Text("Account").Bold().FontSize(8);
                            h.Cell().Text("Description").Bold().FontSize(8);
                            h.Cell().AlignRight().Text("Debit").Bold().FontSize(8);
                            h.Cell().AlignRight().Text("Credit").Bold().FontSize(8);
                        });

                        foreach (var l in e.Lines)
                        {
                            table.Cell().Text(l.AccountCode);
                            table.Cell().Text(l.Description).FontSize(8);
                            table
                                .Cell()
                                .AlignRight()
                                .Text(l.Debit > 0m ? RegisterPageShell.Money(l.Debit) : "");
                            table
                                .Cell()
                                .AlignRight()
                                .Text(l.Credit > 0m ? RegisterPageShell.Money(l.Credit) : "");
                        }
                    })
                );

            var debits = e.Lines.Sum(l => l.Debit);
            var credits = e.Lines.Sum(l => l.Credit);
            col.Item()
                .AlignRight()
                .Text(
                    $"Σ Debit {RegisterPageShell.Money(debits)} • Σ Credit {RegisterPageShell.Money(credits)}"
                )
                .FontSize(8);
            col.Item().LineHorizontal(0.25f);
        });
    }

    private static void RenderGrandTotals(IContainer container, IReadOnlyList<EntryRow> entries)
    {
        var debits = entries.SelectMany(e => e.Lines).Sum(l => l.Debit);
        var credits = entries.SelectMany(e => e.Lines).Sum(l => l.Credit);
        container
            .AlignRight()
            .Column(col =>
            {
                col.Item()
                    .AlignRight()
                    .Text($"Total debits: {RegisterPageShell.Money(debits)} EGP")
                    .Bold();
                col.Item()
                    .AlignRight()
                    .Text($"Total credits: {RegisterPageShell.Money(credits)} EGP")
                    .Bold();
                col.Item()
                    .AlignRight()
                    .Text($"Balanced: {(debits == credits ? "YES" : "NO — INVESTIGATE")}")
                    .Bold()
                    .FontSize(11);
                col.Item().AlignRight().Text($"Entries: {entries.Count}").FontSize(9);
            });
    }
}
