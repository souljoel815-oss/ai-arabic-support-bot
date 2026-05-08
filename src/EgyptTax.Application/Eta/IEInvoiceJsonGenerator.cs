using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;

namespace EgyptTax.Application.Eta;

/// <summary>
/// FR-035 / SC-007 — port over the ETA eInvoice JSON generator. The
/// implementation in Infrastructure produces JSON conforming to
/// <c>contracts/eta-einvoice.schema.json</c>; the contract test
/// (T077) asserts the produced JSON validates against that schema
/// for every customer tax-profile permutation.
/// </summary>
public interface IEInvoiceJsonGenerator
{
    /// <summary>
    /// Render the eInvoice JSON document for a posted sales invoice.
    /// The generator captures the issuer block from the supplied
    /// <see cref="Company"/> row and the receiver block from the
    /// invoice's <c>CustomerTaxProfileSnapshot</c> + the live
    /// <see cref="Customer"/> row, and emits the line + tax totals
    /// from the invoice itself.
    /// </summary>
    string GenerateAsJson(EInvoiceRenderRequest request);
}

public sealed record EInvoiceRenderRequest(
    SalesInvoice Invoice,
    Company Issuer,
    Customer Receiver,
    IReadOnlyDictionary<Guid, string> ItemCodes,
    IReadOnlyDictionary<Guid, string> VatCategoryCodes
);
