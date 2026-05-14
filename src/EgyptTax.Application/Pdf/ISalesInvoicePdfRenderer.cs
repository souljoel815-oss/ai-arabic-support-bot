using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.SharedKernel;

namespace EgyptTax.Application.Pdf;

/// <summary>
/// FR-033 / SC-008 — port over the bilingual sales-invoice PDF
/// renderer. The contract surface is governed by
/// <c>contracts/legal-invoice-fields.md</c>: every legally required
/// field MUST be emitted on every PDF, with a Document Verification
/// Seal QR (FR-044) embedded bottom-right. The contract test (T079)
/// asserts field presence by extracting text + images from the
/// rendered byte[].
/// </summary>
public interface ISalesInvoicePdfRenderer
{
    /// <summary>
    /// Render the full PDF for a posted sales invoice. Returns the
    /// PDF as a byte array — callers persist it via the attachment
    /// store under the document id.
    /// </summary>
    byte[] Render(InvoicePdfRequest request);
}

public sealed record InvoicePdfRequest(
    SalesInvoice Invoice,
    Company Issuer,
    Customer Receiver,
    IReadOnlyDictionary<Guid, ItemRenderInfo> Items,
    IReadOnlyDictionary<Guid, VatCategoryRenderInfo> VatCategories,
    string PostedByUserDisplayName,
    string SealQrPayload,
    OriginalInvoiceReference? OriginalInvoiceReference = null,
    /// <summary>v4 A.5 — optional customer-portal magic-link URL.
    /// When populated, a second QR is rendered next to the FR-044
    /// seal QR encoding this URL so the customer can scan it from
    /// the printed invoice to land on their statement page.
    /// Null when the operator hasn't generated a portal link for
    /// the customer yet.</summary>
    string? PortalUrl = null
);

/// <summary>
/// FR-013 — populated only when the rendered document is a credit
/// note (the renderer's section G). Carries the source invoice's
/// canonical document number + posting date so the credit note's
/// PDF clearly identifies which invoice it corrects.
/// </summary>
public sealed record OriginalInvoiceReference(string DocumentNumber, DateOnly DocumentDate);

public sealed record ItemRenderInfo(string Code, ArabicEnglishText Name);

public sealed record VatCategoryRenderInfo(
    string Code,
    ArabicEnglishText Name,
    decimal RatePercent
);
