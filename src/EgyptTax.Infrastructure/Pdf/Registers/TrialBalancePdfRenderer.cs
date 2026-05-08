using EgyptTax.Application.Reports;
using EgyptTax.Domain.MasterData;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EgyptTax.Infrastructure.Pdf.Registers;

/// <summary>
/// FR-024 / FR-048 / T233 — trial balance PDF. Wraps the existing
/// <see cref="TrialBalanceReport"/> (computed by
/// <c>SqlTrialBalanceReportQuery</c>) in a renderable form so the
/// inspector sees the same numbers the in-app report does, but in
/// a self-contained PDF that lives inside the inspection bundle.
/// The "Balanced?" indicator surfaces the bedrock invariant
/// SUM(debits) == SUM(credits); a "NO" reading is the canonical
/// sign of GL tampering and would also fail the audit-chain replay.
/// </summary>
public static class TrialBalancePdfRenderer
{
    public static byte[] Render(Company company, DateTime generatedAtUtc, TrialBalanceReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return RegisterPageShell.Render(
            titleEn: "Trial Balance",
            titleAr: "ميزان المراجعة",
            company: company,
            periodStart: report.PeriodStart,
            periodEnd: report.PeriodEnd,
            generatedAtUtc: generatedAtUtc,
            renderBody: c => RenderBody(c, report)
        );
    }

    private static void RenderBody(IContainer container, TrialBalanceReport report)
    {
        if (report.Rows.Count == 0)
        {
            container.AlignCenter().Text("No accounts had activity in this period.").Italic();
            return;
        }

        container.Column(col =>
        {
            col.Item().Element(c => RenderTable(c, report));
            col.Item().PaddingTop(8).Element(c => RenderTotals(c, report));
        });
    }

    private static void RenderTable(IContainer container, TrialBalanceReport report)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(28); // #
                c.ConstantColumn(120); // account code
                c.RelativeColumn(); // (filler)
                c.ConstantColumn(80); // debit
                c.ConstantColumn(80); // credit
                c.ConstantColumn(80); // net
            });

            table.Header(h =>
            {
                h.Cell().Text("#").Bold();
                h.Cell().Text("Account code").Bold();
                h.Cell().Text("");
                h.Cell().AlignRight().Text("Debit").Bold();
                h.Cell().AlignRight().Text("Credit").Bold();
                h.Cell().AlignRight().Text("Net").Bold();
            });

            var i = 1;
            foreach (var r in report.Rows)
            {
                table.Cell().Text(i.ToString(System.Globalization.CultureInfo.InvariantCulture));
                table.Cell().Text(r.AccountCode);
                table.Cell().Text("");
                table.Cell().AlignRight().Text(RegisterPageShell.Money(r.TotalDebit.Amount));
                table.Cell().AlignRight().Text(RegisterPageShell.Money(r.TotalCredit.Amount));
                table.Cell().AlignRight().Text(RegisterPageShell.Money(r.NetBalance.Amount));
                i++;
            }
        });
    }

    private static void RenderTotals(IContainer container, TrialBalanceReport report)
    {
        container
            .AlignRight()
            .Column(col =>
            {
                col.Item()
                    .AlignRight()
                    .Text($"Total debits: {RegisterPageShell.Money(report.TotalDebits.Amount)} EGP")
                    .Bold();
                col.Item()
                    .AlignRight()
                    .Text(
                        $"Total credits: {RegisterPageShell.Money(report.TotalCredits.Amount)} EGP"
                    )
                    .Bold();
                col.Item()
                    .AlignRight()
                    .Text($"Balanced: {(report.IsBalanced ? "YES" : "NO — INVESTIGATE")}")
                    .Bold()
                    .FontSize(11);
                col.Item()
                    .AlignRight()
                    .Text($"Accounts with activity: {report.Rows.Count}")
                    .FontSize(9);
            });
    }
}
