using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Quotations;

namespace EgyptTax.Application.Pdf;

/// <summary>
/// L1.5 follow-on (v3 roadmap) — quotation PDF renderer. Quotations
/// are NOT fiscal documents (no ETA submission, no QR seal, no
/// legal-invoice-fields contract), so the renderer is much simpler
/// than <see cref="ISalesInvoicePdfRenderer"/>: header + line table
/// + totals + validity statement. Operator sends this PDF to the
/// customer via WhatsApp/email; if the customer accepts, the
/// operator clicks "Convert to invoice" and the regular
/// SalesInvoice PDF takes over.
/// </summary>
public interface IQuotationPdfRenderer
{
    byte[] Render(QuotationPdfRequest request);
}

public sealed record QuotationPdfRequest(
    Quotation Quotation,
    Company Issuer,
    Customer Receiver,
    IReadOnlyDictionary<Guid, ItemRenderInfo> Items
);
