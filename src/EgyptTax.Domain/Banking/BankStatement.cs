using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.Banking;

/// <summary>
/// P3.2 — one bank-statement period for a specific
/// <see cref="EgyptTax.Domain.MasterData.CashAccount"/>. Imported by
/// the operator (manual entry today; PDF parsers come per-bank in
/// later pushes).
///
/// The statement is the source-of-truth ledger from the bank's side.
/// Each <see cref="BankStatementLine"/> starts <c>Unmatched</c>; the
/// operator (or the future auto-match engine — P3.4) ties each line
/// to a <see cref="EgyptTax.Domain.Documents.SupplierPaymentVoucher"/>
/// or <see cref="EgyptTax.Domain.Documents.CustomerReceiptVoucher"/>
/// to drive the bank-recon view.
/// </summary>
public sealed class BankStatement
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid CashAccountId { get; init; }
    public DateOnly PeriodStart { get; init; }
    public DateOnly PeriodEnd { get; init; }
    public MoneyEgp OpeningBalance { get; init; }
    public MoneyEgp ClosingBalance { get; init; }
    public string SourceFileName { get; init; } = "";
    public DateTime ImportedAtUtc { get; init; }

    private readonly List<BankStatementLine> _lines = new();
    public IReadOnlyCollection<BankStatementLine> Lines => _lines;

    private BankStatement() { }

    public BankStatement(
        Guid cashAccountId,
        DateOnly periodStart,
        DateOnly periodEnd,
        MoneyEgp openingBalance,
        MoneyEgp closingBalance,
        string sourceFileName,
        DateTime importedAtUtc)
    {
        if (cashAccountId == Guid.Empty)
        {
            throw new ArgumentException("CashAccountId is required.", nameof(cashAccountId));
        }
        if (periodStart > periodEnd)
        {
            throw new ArgumentException("Period start cannot be after period end.", nameof(periodStart));
        }

        CashAccountId = cashAccountId;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        OpeningBalance = openingBalance;
        ClosingBalance = closingBalance;
        SourceFileName = sourceFileName ?? "";
        ImportedAtUtc = importedAtUtc;
    }

    public BankStatementLine AddLine(
        DateOnly transactionDate,
        string description,
        MoneyEgp debit,
        MoneyEgp credit,
        MoneyEgp runningBalance,
        string? bankReference)
    {
        if (transactionDate < PeriodStart || transactionDate > PeriodEnd)
        {
            throw new ArgumentOutOfRangeException(
                nameof(transactionDate),
                $"Transaction {transactionDate} falls outside the statement period {PeriodStart}–{PeriodEnd}.");
        }
        if (debit.Amount > 0m && credit.Amount > 0m)
        {
            throw new ArgumentException(
                "A bank statement line cannot have both debit AND credit > 0 (same row is one direction or the other).");
        }
        var line = new BankStatementLine(
            statementId: Id,
            transactionDate: transactionDate,
            description: description ?? "",
            debit: debit,
            credit: credit,
            runningBalance: runningBalance,
            bankReference: bankReference);
        _lines.Add(line);
        return line;
    }

    /// <summary>
    /// Self-check: sum of credits − sum of debits + opening = closing.
    /// Operator runs this from the UI before walking away from a
    /// freshly-imported statement.
    /// </summary>
    public bool BalanceMatchesLines()
    {
        var creditSum = _lines.Sum(l => l.Credit.Amount);
        var debitSum = _lines.Sum(l => l.Debit.Amount);
        var implied = OpeningBalance.Amount + creditSum - debitSum;
        return Math.Abs(implied - ClosingBalance.Amount) < 0.01m;
    }
}

public sealed class BankStatementLine
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid StatementId { get; init; }
    public DateOnly TransactionDate { get; init; }
    public string Description { get; init; } = "";
    public MoneyEgp Debit { get; init; }
    public MoneyEgp Credit { get; init; }
    public MoneyEgp RunningBalance { get; init; }
    public string? BankReference { get; init; }

    public BankStatementLineStatus Status { get; private set; }
        = BankStatementLineStatus.Unmatched;
    public Guid? MatchedSupplierPaymentVoucherId { get; private set; }
    public Guid? MatchedCustomerReceiptVoucherId { get; private set; }
    public DateTime? MatchedAtUtc { get; private set; }
    public Guid? MatchedByUserId { get; private set; }

    /// <summary>
    /// P3.4 — auto-match scorer's confidence in the suggested
    /// counterparty (0–100). Populated when <see cref="Status"/> is
    /// <see cref="BankStatementLineStatus.SuggestedMatch"/> or
    /// <see cref="BankStatementLineStatus.Matched"/> via auto-match.
    /// Null for human-driven matches and untouched lines.
    /// </summary>
    public int? MatchConfidenceScore { get; private set; }
    public Guid? SuggestedSupplierPaymentVoucherId { get; private set; }
    public Guid? SuggestedCustomerReceiptVoucherId { get; private set; }
    public DateTime? SuggestedAtUtc { get; private set; }

    private BankStatementLine() { }

    internal BankStatementLine(
        Guid statementId,
        DateOnly transactionDate,
        string description,
        MoneyEgp debit,
        MoneyEgp credit,
        MoneyEgp runningBalance,
        string? bankReference)
    {
        StatementId = statementId;
        TransactionDate = transactionDate;
        Description = description;
        Debit = debit;
        Credit = credit;
        RunningBalance = runningBalance;
        BankReference = bankReference;
    }

    /// <summary>
    /// Tie this statement line to a supplier-payment voucher (outflow).
    /// </summary>
    public void MatchToSupplierPayment(Guid supplierPaymentVoucherId, Guid byUserId, DateTime nowUtc)
    {
        if (Status == BankStatementLineStatus.Matched)
        {
            throw new InvalidOperationException($"Line {Id} already matched.");
        }
        if (supplierPaymentVoucherId == Guid.Empty)
        {
            throw new ArgumentException("SupplierPaymentVoucherId required.", nameof(supplierPaymentVoucherId));
        }
        Status = BankStatementLineStatus.Matched;
        MatchedSupplierPaymentVoucherId = supplierPaymentVoucherId;
        MatchedCustomerReceiptVoucherId = null;
        MatchedAtUtc = nowUtc;
        MatchedByUserId = byUserId;
    }

    /// <summary>
    /// Tie this statement line to a customer-receipt voucher (inflow).
    /// </summary>
    public void MatchToCustomerReceipt(Guid customerReceiptVoucherId, Guid byUserId, DateTime nowUtc)
    {
        if (Status == BankStatementLineStatus.Matched)
        {
            throw new InvalidOperationException($"Line {Id} already matched.");
        }
        if (customerReceiptVoucherId == Guid.Empty)
        {
            throw new ArgumentException("CustomerReceiptVoucherId required.", nameof(customerReceiptVoucherId));
        }
        Status = BankStatementLineStatus.Matched;
        MatchedCustomerReceiptVoucherId = customerReceiptVoucherId;
        MatchedSupplierPaymentVoucherId = null;
        MatchedAtUtc = nowUtc;
        MatchedByUserId = byUserId;
    }

    /// <summary>
    /// Operator dismisses the line (e.g., interbank transfer, bank fee
    /// captured elsewhere). Won't appear in the unmatched queue but
    /// stays in the statement for audit.
    /// </summary>
    public void Ignore(Guid byUserId, DateTime nowUtc)
    {
        Status = BankStatementLineStatus.Ignored;
        MatchedAtUtc = nowUtc;
        MatchedByUserId = byUserId;
    }

    public void Unmatch()
    {
        Status = BankStatementLineStatus.Unmatched;
        MatchedSupplierPaymentVoucherId = null;
        MatchedCustomerReceiptVoucherId = null;
        MatchedAtUtc = null;
        MatchedByUserId = null;
        MatchConfidenceScore = null;
        SuggestedSupplierPaymentVoucherId = null;
        SuggestedCustomerReceiptVoucherId = null;
        SuggestedAtUtc = null;
    }

    /// <summary>
    /// P3.4 auto-match — record a high-confidence match without
    /// committing it. Human reviewer accepts via
    /// <see cref="AcceptSuggestion"/> or rejects via
    /// <see cref="RejectSuggestion"/>. Either both voucher refs are
    /// null and we exit early (no candidate found) or exactly one
    /// must be set (the scorer never proposes both directions).
    /// </summary>
    public void SuggestSupplierPayment(Guid supplierPaymentVoucherId, int score, DateTime nowUtc)
    {
        EnsureSuggestable();
        if (supplierPaymentVoucherId == Guid.Empty)
        {
            throw new ArgumentException("SupplierPaymentVoucherId required.", nameof(supplierPaymentVoucherId));
        }
        ValidateScore(score);
        Status = BankStatementLineStatus.SuggestedMatch;
        SuggestedSupplierPaymentVoucherId = supplierPaymentVoucherId;
        SuggestedCustomerReceiptVoucherId = null;
        MatchConfidenceScore = score;
        SuggestedAtUtc = nowUtc;
    }

    public void SuggestCustomerReceipt(Guid customerReceiptVoucherId, int score, DateTime nowUtc)
    {
        EnsureSuggestable();
        if (customerReceiptVoucherId == Guid.Empty)
        {
            throw new ArgumentException("CustomerReceiptVoucherId required.", nameof(customerReceiptVoucherId));
        }
        ValidateScore(score);
        Status = BankStatementLineStatus.SuggestedMatch;
        SuggestedCustomerReceiptVoucherId = customerReceiptVoucherId;
        SuggestedSupplierPaymentVoucherId = null;
        MatchConfidenceScore = score;
        SuggestedAtUtc = nowUtc;
    }

    /// <summary>
    /// P3.4 auto-match — promote the pending suggestion to a real
    /// match. Used by the auto-match Hangfire job for ≥95% scores
    /// and by the human reviewer's "Approve" button.
    /// </summary>
    public void AcceptSuggestion(Guid byUserId, DateTime nowUtc)
    {
        if (Status != BankStatementLineStatus.SuggestedMatch)
        {
            throw new InvalidOperationException(
                $"Line {Id} has no pending suggestion (state {Status}).");
        }
        if (SuggestedSupplierPaymentVoucherId is { } spvId)
        {
            Status = BankStatementLineStatus.Matched;
            MatchedSupplierPaymentVoucherId = spvId;
            MatchedCustomerReceiptVoucherId = null;
        }
        else if (SuggestedCustomerReceiptVoucherId is { } crvId)
        {
            Status = BankStatementLineStatus.Matched;
            MatchedCustomerReceiptVoucherId = crvId;
            MatchedSupplierPaymentVoucherId = null;
        }
        else
        {
            throw new InvalidOperationException(
                $"Line {Id} is in SuggestedMatch but has no suggested voucher ref.");
        }
        MatchedAtUtc = nowUtc;
        MatchedByUserId = byUserId;
        SuggestedSupplierPaymentVoucherId = null;
        SuggestedCustomerReceiptVoucherId = null;
        SuggestedAtUtc = null;
    }

    /// <summary>
    /// P3.4 auto-match — reviewer rejected the suggestion. Returns
    /// the line to <see cref="BankStatementLineStatus.Unmatched"/>
    /// so it surfaces in the manual queue again, and clears the
    /// scorer fields so the next auto-match pass doesn't re-suggest
    /// the same voucher (the application layer is responsible for
    /// remembering rejections if we need that later).
    /// </summary>
    public void RejectSuggestion()
    {
        if (Status != BankStatementLineStatus.SuggestedMatch)
        {
            throw new InvalidOperationException(
                $"Line {Id} has no pending suggestion (state {Status}).");
        }
        Status = BankStatementLineStatus.Unmatched;
        SuggestedSupplierPaymentVoucherId = null;
        SuggestedCustomerReceiptVoucherId = null;
        SuggestedAtUtc = null;
        MatchConfidenceScore = null;
    }

    private void EnsureSuggestable()
    {
        if (Status == BankStatementLineStatus.Matched)
        {
            throw new InvalidOperationException($"Line {Id} already matched — cannot overwrite with a suggestion.");
        }
        if (Status == BankStatementLineStatus.Ignored)
        {
            throw new InvalidOperationException($"Line {Id} is ignored — cannot suggest a match.");
        }
    }

    private static void ValidateScore(int score)
    {
        if (score is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(score), $"Score must be 0–100, got {score}.");
        }
    }
}

public enum BankStatementLineStatus
{
    Unmatched,
    Matched,
    Ignored,
    /// <summary>
    /// P3.4 — scorer found a candidate above the suggestion threshold
    /// (≥70%) but below the auto-match threshold (≥95%). Pending
    /// human approval in the unmatched queue.
    /// </summary>
    SuggestedMatch,
}
