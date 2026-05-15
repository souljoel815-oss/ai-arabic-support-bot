using System.Globalization;
using EgyptTax.Application.Audit;
using EgyptTax.Application.Purchases;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Purchases;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Purchases;

/// <summary>
/// v5 E.3 — purchase credit-note correction path. Mirrors
/// <c>IssueCreditNoteHandler</c> on the sales side. Creates a Draft
/// credit-note PurchaseInvoice that references the supplied posted
/// PurchaseInvoice, copying the supplier + tax-profile snapshot and
/// attaching the supplied lines (each with a negative quantity). The
/// draft is not posted here — operator posts it through the regular
/// flow (the post handler will allocate a number from the appropriate
/// series and emit the JE: DR Accounts Payable / CR Stock Interim).
/// </summary>
public sealed class IssuePurchaseCreditNoteHandler
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditLogStore _auditLog;

    public IssuePurchaseCreditNoteHandler(AppDbContext db, IClock clock, IAuditLogStore auditLog)
    {
        _db = db;
        _clock = clock;
        _auditLog = auditLog;
    }

    public async Task<PurchaseInvoice> HandleAsync(
        IssuePurchaseCreditNoteCommand command,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.SupplierCreditNoteNumber);
        if (command.Lines.Count == 0)
        {
            throw new ArgumentException(
                "A credit note MUST carry at least one line — there's nothing to credit otherwise.",
                nameof(command)
            );
        }

        var original =
            await _db.Set<PurchaseInvoice>()
                .Include(p => p.Lines)
                .FirstOrDefaultAsync(p => p.Id == command.OriginalPurchaseInvoiceId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Source purchase invoice {command.OriginalPurchaseInvoiceId} not found."
            );

        // Factory enforces (a) source is Posted, (b) source isn't already a credit note.
        var creditNote = PurchaseInvoice.CreateCreditNoteFor(
            originalInvoice: original,
            reason: command.Reason,
            supplierCreditNoteNumber: command.SupplierCreditNoteNumber,
            dateReceived: command.DateReceived
        );

        foreach (var line in command.Lines)
        {
            creditNote.AddLine(
                itemId: line.ItemId,
                expenseCategoryId: line.ExpenseCategoryId,
                quantity: line.Quantity,
                unitPrice: line.UnitPrice,
                vatCategoryId: line.VatCategoryId,
                vatRatePercent: line.VatRatePercent,
                deductibleFlag: line.DeductibleFlag,
                costCenterId: line.CostCenterId
            );
        }

        _db.Add(creditNote);
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.AppendAsync(
            new AuditLogPayload(
                Kind: "purchase_credit_note.issued",
                ActorUserId: null,
                ActorFirmName: null,
                CompanyId: Guid.Empty,
                PayloadJson: BuildAuditPayload(creditNote, original)
            ),
            cancellationToken
        );

        return creditNote;
    }

    private static string BuildAuditPayload(PurchaseInvoice creditNote, PurchaseInvoice original) =>
        $$"""{"purchase_credit_note_id":"{{creditNote.Id:D}}","original_purchase_invoice_id":"{{original.Id:D}}","original_document_number":"{{original.DocumentNumber}}","supplier_id":"{{creditNote.SupplierId:D}}","reason":{{Quote(creditNote.CreditNoteReason)}},"subtotal_egp":{{creditNote.Subtotal.Amount.ToString("F2", CultureInfo.InvariantCulture)}},"vat_total_egp":{{creditNote.VatTotal.Amount.ToString("F2", CultureInfo.InvariantCulture)}},"grand_total_egp":{{creditNote.GrandTotal.Amount.ToString("F2", CultureInfo.InvariantCulture)}},"line_count":{{creditNote.Lines.Count}}}""";

    private static string Quote(string? value) =>
        value is null
            ? "null"
            : "\""
                + value
                    .Replace("\\", "\\\\", StringComparison.Ordinal)
                    .Replace("\"", "\\\"", StringComparison.Ordinal)
                + "\"";
}
