using EgyptTax.Domain.Workflow;
using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.Expenses;

/// <summary>
/// C3 — expense document per FR-014 / FR-015 / FR-016. Simpler than
/// PurchaseInvoice: header-only (no lines), single category +
/// amount + deductible flag + description. Attachments live in the
/// shared <c>Attachment</c> entity, keyed by <c>(document_id,
/// document_type = Expense)</c>.
///
/// FR-014 invariant: depreciation expenses are NOT entered through
/// this aggregate — they are produced by FR-017 (fixed-asset
/// depreciation runs) in Phase 4. There is no field-level guard
/// here; the spec relies on category curation (the operator simply
/// doesn't create a "Depreciation" expense category).
///
/// State machine reused from <see cref="DocumentStateMachine"/>;
/// FR-016 enforcement (deductible → requires attachment) lives in
/// the application-layer <c>PostExpenseHandler</c> mirror of
/// PostPurchaseInvoiceHandler.
/// </summary>
public sealed class Expense
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateOnly DocumentDate { get; private set; }
    public Guid CategoryId { get; private set; }
    public MoneyEgp Amount { get; private set; } = MoneyEgp.Zero;
    public bool DeductibleFlag { get; private set; }
    public ArabicEnglishText Description { get; private set; }
    public DocumentState State { get; private set; } = DocumentState.Draft;

    public string? DocumentNumber { get; private set; }
    public DateTime? PostedAtUtc { get; private set; }
    public Guid? PostedByUserId { get; private set; }
    public DocumentPostingMode? PostingMode { get; private set; }

    private Expense() { }

    private Expense(
        DateOnly documentDate,
        Guid categoryId,
        MoneyEgp amount,
        bool deductibleFlag,
        ArabicEnglishText description)
    {
        DocumentDate = documentDate;
        CategoryId = categoryId;
        Amount = amount;
        DeductibleFlag = deductibleFlag;
        Description = description;
    }

    public static Expense CreateDraft(
        DateOnly documentDate,
        Guid categoryId,
        MoneyEgp amount,
        bool deductibleFlag,
        ArabicEnglishText description)
    {
        if (categoryId == Guid.Empty)
        {
            throw new ArgumentException("CategoryId is required.", nameof(categoryId));
        }
        if (amount.Amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount),
                "Expense amount must be positive.");
        }
        return new Expense(documentDate, categoryId, amount, deductibleFlag, description);
    }

    public void UpdateCategory(Guid categoryId)
    {
        ThrowIfNotDraft(nameof(UpdateCategory));
        if (categoryId == Guid.Empty)
        {
            throw new ArgumentException("CategoryId is required.", nameof(categoryId));
        }
        CategoryId = categoryId;
    }

    public void UpdateAmount(MoneyEgp amount)
    {
        ThrowIfNotDraft(nameof(UpdateAmount));
        if (amount.Amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount),
                "Expense amount must be positive.");
        }
        Amount = amount;
    }

    public void UpdateDeductibleFlag(bool deductible)
    {
        ThrowIfNotDraft(nameof(UpdateDeductibleFlag));
        DeductibleFlag = deductible;
    }

    public void UpdateDescription(ArabicEnglishText description)
    {
        ThrowIfNotDraft(nameof(UpdateDescription));
        Description = description;
    }

    public void UpdateDocumentDate(DateOnly documentDate)
    {
        ThrowIfNotDraft(nameof(UpdateDocumentDate));
        DocumentDate = documentDate;
    }

    public void MarkPosted(
        string documentNumber,
        Guid postedByUserId,
        DateTime postedAtUtc,
        DocumentPostingMode postingMode,
        bool approvalEnabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentNumber);
        var nextState = DocumentStateMachine.Transition(State, DocumentState.Posted, approvalEnabled);
        DocumentNumber = documentNumber;
        PostedByUserId = postedByUserId;
        PostedAtUtc = postedAtUtc;
        PostingMode = postingMode;
        State = nextState;
    }

    private void ThrowIfNotDraft(string operation)
    {
        if (State != DocumentState.Draft)
        {
            throw new InvalidOperationException(
                $"Cannot {operation} on expense {Id}: current state {State} is not Draft.");
        }
    }
}
