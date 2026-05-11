using EgyptTax.Domain.Banking;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.MasterData;

namespace EgyptTax.Application.Banking;

/// <summary>
/// P3.4 — bank-recon matching engine. Given an unmatched
/// <see cref="BankStatementLine"/> + the universe of posted
/// payment/receipt vouchers + their counterparties, returns the
/// best candidate plus a 0–100 confidence score.
///
/// Scoring composition (higher = stronger):
///   • Amount proximity — 50 pts max
///       Exact (delta &lt; 0.01) ⇒ 50; sliding linearly to 0 at ±2%.
///       Outside ±2% the candidate is dropped.
///   • Date proximity — 25 pts max
///       Same day ⇒ 25; sliding linearly to 0 at ±7 days.
///       Outside ±7 days the candidate is dropped.
///   • Counterparty / reference fuzzy match — 25 pts max
///       Direct payment-reference hit ⇒ 25.
///       Counterparty name token overlap ratio scaled to 25.
///       Phone-number trailing-digit hit ⇒ +10 (clamped to 25).
///
/// Caller passes pre-filtered candidate lists (typically last 200
/// posted vouchers) so the scorer is pure / O(n) / trivially
/// unit-testable. Static — no instance state, no DI registration
/// needed.
/// </summary>
public static class BankMatchScorer
{
    public const int AutoMatchThreshold = 95;
    public const int SuggestionThreshold = 70;

    private static readonly char[] NameTokenSeparators =
        new[] { ' ', '\t', '-', '_', '/', '.', ',' };

    public static ScoredCandidate? ScoreOutflow(
        BankStatementLine line,
        IReadOnlyCollection<SupplierPaymentVoucher> spvCandidates,
        IReadOnlyDictionary<Guid, Supplier> suppliers)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(spvCandidates);
        ArgumentNullException.ThrowIfNull(suppliers);
        if (line.Debit.Amount <= 0m) return null;

        ScoredCandidate? best = null;
        foreach (var v in spvCandidates)
        {
            var amountScore = ScoreAmount(line.Debit.Amount, v.NetCashPaid.Amount);
            if (amountScore is null) continue;
            var dateScore = ScoreDate(line.TransactionDate, v.PaymentDate);
            if (dateScore is null) continue;
            var supplier = suppliers.GetValueOrDefault(v.SupplierId);
            var counterpartyScore = ScoreCounterpartyCore(
                line.Description,
                line.BankReference,
                paymentReference: v.PaymentReference,
                nameAr: supplier?.Name.Arabic,
                nameEn: supplier?.Name.English,
                phone: supplier?.Phone);
            var total = amountScore.Value + dateScore.Value + counterpartyScore;
            if (best is null || total > best.Score)
            {
                best = new ScoredCandidate(
                    SupplierPaymentVoucherId: v.Id,
                    CustomerReceiptVoucherId: null,
                    Score: total);
            }
        }
        return best;
    }

    public static ScoredCandidate? ScoreInflow(
        BankStatementLine line,
        IReadOnlyCollection<CustomerReceiptVoucher> crvCandidates,
        IReadOnlyDictionary<Guid, Customer> customers)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(crvCandidates);
        ArgumentNullException.ThrowIfNull(customers);
        if (line.Credit.Amount <= 0m) return null;

        ScoredCandidate? best = null;
        foreach (var v in crvCandidates)
        {
            var amountScore = ScoreAmount(line.Credit.Amount, v.NetCashReceived.Amount);
            if (amountScore is null) continue;
            var dateScore = ScoreDate(line.TransactionDate, v.ReceiptDate);
            if (dateScore is null) continue;
            var customer = customers.GetValueOrDefault(v.CustomerId);
            var counterpartyScore = ScoreCounterpartyCore(
                line.Description,
                line.BankReference,
                paymentReference: v.PaymentReference,
                nameAr: customer?.Name.Arabic,
                nameEn: customer?.Name.English,
                phone: customer?.Phone);
            var total = amountScore.Value + dateScore.Value + counterpartyScore;
            if (best is null || total > best.Score)
            {
                best = new ScoredCandidate(
                    SupplierPaymentVoucherId: null,
                    CustomerReceiptVoucherId: v.Id,
                    Score: total);
            }
        }
        return best;
    }

    private static int? ScoreAmount(decimal lineAmount, decimal voucherAmount)
    {
        if (lineAmount <= 0m || voucherAmount <= 0m) return null;
        var delta = Math.Abs(lineAmount - voucherAmount);
        if (delta < 0.01m) return 50;
        var tolerance = lineAmount * 0.02m;
        if (delta > tolerance) return null;
        var ratio = (double)(delta / tolerance);
        return (int)Math.Round(50 * (1 - ratio));
    }

    private static int? ScoreDate(DateOnly lineDate, DateOnly voucherDate)
    {
        var days = Math.Abs(lineDate.DayNumber - voucherDate.DayNumber);
        if (days > 7) return null;
        return (int)Math.Round(25 * (1 - days / 7d));
    }

    private static int ScoreCounterpartyCore(
        string description,
        string? bankReference,
        string? paymentReference,
        string? nameAr,
        string? nameEn,
        string? phone)
    {
        var haystack = $"{description} {bankReference}".Trim();
        if (haystack.Length == 0) return 0;

        var score = 0;

        // Exact payment-reference appears in the bank line description.
        if (!string.IsNullOrWhiteSpace(paymentReference)
            && haystack.Contains(paymentReference.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            score = 25;
        }
        else
        {
            score = Math.Max(
                NameOverlapScore(haystack, nameAr),
                NameOverlapScore(haystack, nameEn));
        }

        if (!string.IsNullOrWhiteSpace(phone))
        {
            var digits = new string(phone.Where(char.IsDigit).ToArray());
            if (digits.Length >= 4)
            {
                var tail = digits[^4..];
                var haystackDigits = new string(haystack.Where(char.IsDigit).ToArray());
                if (haystackDigits.Contains(tail, StringComparison.Ordinal))
                {
                    score = Math.Min(25, score + 10);
                }
            }
        }

        return score;
    }

    private static int NameOverlapScore(string haystack, string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return 0;
        var tokens = name
            .Split(NameTokenSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 3)
            .ToArray();
        if (tokens.Length == 0) return 0;
        var hits = tokens.Count(t => haystack.Contains(t, StringComparison.OrdinalIgnoreCase));
        if (hits == 0) return 0;
        return (int)Math.Round(25d * hits / tokens.Length);
    }

    public sealed record ScoredCandidate(
        Guid? SupplierPaymentVoucherId,
        Guid? CustomerReceiptVoucherId,
        int Score);
}
