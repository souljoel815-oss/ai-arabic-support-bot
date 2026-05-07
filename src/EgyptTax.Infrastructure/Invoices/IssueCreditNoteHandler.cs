using System.Globalization;
using EgyptTax.Application.Audit;
using EgyptTax.Application.Invoices;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Invoices;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Invoices;

/// <summary>
/// FR-013 — credit-note correction path. Creates a Draft credit
/// note that references the supplied posted SalesInvoice, copying
/// the customer + tax-profile snapshot and attaching the supplied
/// lines (each with a negative quantity per the negate-original
/// rule, but partial credit allowed — caller picks the per-line
/// quantity). The draft is **not posted** here — the caller posts
/// it through the regular <see cref="PostSalesInvoiceHandler"/>
/// flow which allocates the document number from the CN series
/// (the post handler derives the document type from
/// <see cref="SalesInvoice.IsCreditNote"/>). This separation keeps
/// the issue-vs-post audit trail clean: issuing is captured here as
/// <c>credit_note.issued</c>, posting fires the canonical
/// <c>sales_invoice.posted</c> event.
/// </summary>
public sealed class IssueCreditNoteHandler
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditLogStore _auditLog;

    public IssueCreditNoteHandler(AppDbContext db, IClock clock, IAuditLogStore auditLog)
    {
        _db = db;
        _clock = clock;
        _auditLog = auditLog;
    }

    public async Task<SalesInvoice> HandleAsync(
        IssueCreditNoteCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Reason);
        if (command.Lines.Count == 0)
        {
            throw new ArgumentException(
                "A credit note MUST carry at least one line — there's nothing to credit otherwise.",
                nameof(command));
        }

        var original = await _db.Set<SalesInvoice>()
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == command.OriginalSalesInvoiceId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Source sales invoice {command.OriginalSalesInvoiceId} not found.");

        // The factory enforces (a) source is Posted, (b) source isn't
        // already a credit note. Belt-and-braces: any other rule
        // changes here go on the factory side, not the handler.
        var creditNote = SalesInvoice.CreateCreditNoteFor(
            originalInvoice: original,
            reason: command.Reason,
            documentDate: command.DocumentDate);

        foreach (var line in command.Lines)
        {
            creditNote.AddLine(
                itemId: line.ItemId,
                quantity: line.Quantity,
                unitPrice: line.UnitPrice,
                vatCategoryId: line.VatCategoryId,
                vatRatePercent: line.VatRatePercent);
        }

        _db.Add(creditNote);
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.AppendAsync(new AuditLogPayload(
            Kind: "credit_note.issued",
            ActorUserId: null,
            ActorFirmName: null,
            CompanyId: Guid.Empty,
            PayloadJson: BuildAuditPayload(creditNote, original)),
            cancellationToken);

        return creditNote;
    }

    private static string BuildAuditPayload(SalesInvoice creditNote, SalesInvoice original) =>
        $$"""{"credit_note_id":"{{creditNote.Id:D}}","original_invoice_id":"{{original.Id:D}}","original_document_number":"{{original.DocumentNumber}}","customer_id":"{{creditNote.CustomerId:D}}","reason":{{Quote(creditNote.CreditNoteReason)}},"subtotal_egp":{{creditNote.Subtotal.Amount.ToString("F2", CultureInfo.InvariantCulture)}},"vat_total_egp":{{creditNote.VatTotal.Amount.ToString("F2", CultureInfo.InvariantCulture)}},"grand_total_egp":{{creditNote.GrandTotal.Amount.ToString("F2", CultureInfo.InvariantCulture)}},"line_count":{{creditNote.Lines.Count}}}""";

    private static string Quote(string? value) =>
        value is null
            ? "null"
            : "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal)
                          .Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
}
