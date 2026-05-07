using EgyptTax.Application.Eta;
using EgyptTax.Application.Pdf;
using EgyptTax.Application.Verification;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.Infrastructure.Verification;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Invoices;

/// <summary>
/// Shared loader that the PDF + eInvoice JSON download routes use to
/// hydrate everything the renderers need from a single sales-invoice
/// id. The renderers consume an `InvoicePdfRequest` /
/// `EInvoiceRenderRequest` shape that includes the issuer, receiver,
/// item codes, and VAT category info; this helper builds those
/// dictionaries in one DB roundtrip per table so the auto-submit
/// orchestrator + the PDF / eInvoice JSON download routes all share
/// the same hydration path.
/// </summary>
public static class InvoiceRenderingPipeline
{
    public sealed record Bundle(
        SalesInvoice Invoice,
        Company Issuer,
        Customer Receiver,
        InvoicePdfRequest PdfRequest,
        EInvoiceRenderRequest EInvoiceRequest);

    public static async Task<Bundle?> LoadAsync(
        AppDbContext db,
        Guid invoiceId,
        CancellationToken cancellationToken)
    {
        var invoice = await db.Set<SalesInvoice>()
            .Include(i => i.Lines)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);
        if (invoice is null || invoice.State != DocumentState.Posted)
        {
            return null;
        }

        var issuer = await db.Set<Company>().AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        if (issuer is null)
        {
            return null;
        }

        var receiver = await db.Set<Customer>().AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == invoice.CustomerId, cancellationToken);
        if (receiver is null)
        {
            return null;
        }

        var itemIds = invoice.Lines.Select(l => l.ItemId).Distinct().ToArray();
        var items = await db.Set<Item>().AsNoTracking()
            .Where(i => itemIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, cancellationToken);

        var vatIds = invoice.Lines.Select(l => l.VatCategoryId).Distinct().ToArray();
        var vatCategories = await db.Set<VatCategory>().AsNoTracking()
            .Where(v => vatIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, cancellationToken);

        var itemRender = items.ToDictionary(
            kv => kv.Key,
            kv => new ItemRenderInfo(kv.Value.Code, kv.Value.Name));
        var vatRender = vatCategories.ToDictionary(
            kv => kv.Key,
            kv => new VatCategoryRenderInfo(kv.Value.Code, kv.Value.Name, kv.Value.RatePercent));

        var sealPayload = DocumentSealCodec.Encode(new DocumentSealPayload(
            DocumentType: SealedDocumentType.SalesInvoice,
            DocumentNumber: invoice.DocumentNumber!,
            DocumentId: invoice.Id,
            GrandTotalPiastres: (long)(invoice.GrandTotal.Amount * 100m),
            AuditEntryHash: new byte[32],
            AuditEntryIndex: 1L,
            VerifyUrl: "/api/v1/verify",
            IssuerTin: issuer.TaxRegistrationNumber));

        var pdfRequest = new InvoicePdfRequest(
            Invoice: invoice,
            Issuer: issuer,
            Receiver: receiver,
            Items: itemRender,
            VatCategories: vatRender,
            PostedByUserDisplayName: "(unknown)",
            SealQrPayload: sealPayload);

        var eInvoiceRequest = new EInvoiceRenderRequest(
            Invoice: invoice,
            Issuer: issuer,
            Receiver: receiver,
            ItemCodes: items.ToDictionary(kv => kv.Key, kv => kv.Value.Code),
            VatCategoryCodes: vatCategories.ToDictionary(kv => kv.Key, kv => kv.Value.Code));

        return new Bundle(invoice, issuer, receiver, pdfRequest, eInvoiceRequest);
    }
}
