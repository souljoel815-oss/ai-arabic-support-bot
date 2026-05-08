using System.Globalization;
using System.Text;
using System.Text.Json;
using EgyptTax.Application.Eta;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Workflow;

namespace EgyptTax.Infrastructure.Eta;

/// <summary>
/// FR-035 / SC-007 — produces the canonical ETA eInvoice JSON document
/// for a posted sales invoice. The shape is governed by
/// <c>contracts/eta-einvoice.schema.json</c>; this implementation
/// hand-emits the JSON via <see cref="Utf8JsonWriter"/> so the
/// banker's-rounded monetary values stay exact (no decimal-to-double
/// drift) and the field order is stable across rebuilds (helps with
/// payload diff review during regulator alignment).
/// </summary>
public sealed class EInvoiceJsonGenerator : IEInvoiceJsonGenerator
{
    private const string DocumentTypeVersion = "1.0";
    private const string IssuerType = "B";
    private const string TaxTypeVat = "T1";
    private const string DefaultUnitType = "EA";

    public string GenerateAsJson(EInvoiceRenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var invoice = request.Invoice;
        if (invoice.State != DocumentState.Posted)
        {
            throw new InvalidOperationException(
                "eInvoice JSON can only be rendered for a posted sales invoice."
            );
        }

        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            w.WriteStartObject();

            // Header
            w.WriteString(
                "documentType",
                invoice.PostingMode is null
                    ? "I" // Default to invoice; credit-note path supplied via document-type discrimination once CreditNote ships.
                    : "I"
            );
            w.WriteString("documentTypeVersion", DocumentTypeVersion);
            w.WriteString("dateTimeIssued", FormatPostingTimestampIso(invoice));
            w.WriteString("taxpayerActivityCode", request.Issuer.TaxpayerActivityCode);
            w.WriteString("internalID", invoice.DocumentNumber!);

            // Issuer
            w.WritePropertyName("issuer");
            WriteParty(
                w,
                request.Issuer.LegalName.English,
                request.Issuer.Address,
                IssuerType,
                tin: request.Issuer.TaxRegistrationNumber
            );

            // Receiver
            w.WritePropertyName("receiver");
            WriteReceiver(w, request.Receiver, invoice.CustomerTaxProfileSnapshot);

            // Lines
            w.WriteStartArray("invoiceLines");
            foreach (var line in invoice.Lines)
            {
                WriteLine(w, line, request);
            }
            w.WriteEndArray();

            // Totals
            WriteMoneyProperty(w, "totalDiscountAmount", 0m);
            WriteMoneyProperty(w, "totalSalesAmount", invoice.Subtotal.Amount);
            WriteMoneyProperty(w, "netAmount", invoice.Subtotal.Amount);
            WriteMoneyProperty(w, "totalAmount", invoice.GrandTotal.Amount);
            WriteMoneyProperty(w, "extraDiscountAmount", 0m);
            WriteMoneyProperty(w, "totalItemsDiscountAmount", 0m);

            // Tax totals — single VAT entry summing all line VATs at MVP scope.
            w.WriteStartArray("taxTotals");
            w.WriteStartObject();
            w.WriteString("taxType", TaxTypeVat);
            WriteMoneyProperty(w, "amount", invoice.VatTotal.Amount);
            w.WriteEndObject();
            w.WriteEndArray();

            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteParty(
        Utf8JsonWriter w,
        string name,
        PostalAddress address,
        string type,
        string? tin
    )
    {
        w.WriteStartObject();
        WriteAddress(w, address);
        w.WriteString("type", type);
        if (tin is not null)
        {
            w.WriteString("id", tin);
        }
        w.WriteString("name", name);
        w.WriteEndObject();
    }

    private static void WriteReceiver(
        Utf8JsonWriter w,
        Customer customer,
        CustomerTaxProfile snapshot
    )
    {
        w.WriteStartObject();
        WriteAddress(w, customer.Address);

        // type per schema: B = B2BRegistered, P = B2C/Person, F = Foreign.
        // The MVP customer model has B2BRegistered / B2BUnregistered /
        // B2CConsumer; B2BUnregistered also maps to "P" for ETA shape.
        var type = snapshot.ProfileType switch
        {
            CustomerTaxProfileType.B2BRegistered => "B",
            CustomerTaxProfileType.B2BUnregistered => "P",
            CustomerTaxProfileType.B2CConsumer => "P",
            _ => "P",
        };
        w.WriteString("type", type);
        if (
            snapshot.ProfileType == CustomerTaxProfileType.B2BRegistered
            && snapshot.TinValue is not null
        )
        {
            w.WriteString("id", snapshot.TinValue);
        }
        w.WriteString("name", customer.Name.English);
        w.WriteEndObject();
    }

    private static void WriteAddress(Utf8JsonWriter w, PostalAddress address)
    {
        w.WritePropertyName("address");
        w.WriteStartObject();
        w.WriteString("country", address.Country);
        w.WriteString("governate", address.Governorate);
        w.WriteString("regionCity", address.RegionCity);
        w.WriteString("street", address.Street);
        w.WriteString("buildingNumber", address.BuildingNumber);
        if (!string.IsNullOrWhiteSpace(address.PostalCode))
        {
            w.WriteString("postalCode", address.PostalCode);
        }
        w.WriteEndObject();
    }

    private static void WriteLine(
        Utf8JsonWriter w,
        SalesInvoiceLine line,
        EInvoiceRenderRequest request
    )
    {
        var itemCode = request.ItemCodes.TryGetValue(line.ItemId, out var code)
            ? code
            : line.ItemId.ToString("N");
        var vatCategoryCode = request.VatCategoryCodes.TryGetValue(line.VatCategoryId, out var vc)
            ? vc
            : "Standard";

        w.WriteStartObject();
        w.WriteString("description", itemCode);
        w.WriteString("itemType", "EGS");
        w.WriteString("itemCode", itemCode);
        w.WriteString("unitType", DefaultUnitType);
        w.WriteNumber("quantity", line.Quantity);
        w.WriteString("internalCode", itemCode);

        WriteMoneyProperty(w, "salesTotal", line.LineSubtotal.Amount);
        WriteMoneyProperty(w, "total", line.LineTotal.Amount);
        WriteMoneyProperty(w, "valueDifference", 0m);
        WriteMoneyProperty(w, "totalTaxableFees", 0m);
        WriteMoneyProperty(w, "netTotal", line.LineSubtotal.Amount);
        WriteMoneyProperty(w, "itemsDiscount", 0m);

        w.WritePropertyName("unitValue");
        w.WriteStartObject();
        w.WriteString("currencySold", "EGP");
        WriteMoneyProperty(w, "amountEGP", line.UnitPrice.Amount);
        w.WriteEndObject();

        w.WritePropertyName("discount");
        w.WriteStartObject();
        w.WriteNumber("rate", 0);
        WriteMoneyProperty(w, "amount", 0m);
        w.WriteEndObject();

        w.WriteStartArray("taxableItems");
        w.WriteStartObject();
        w.WriteString("taxType", TaxTypeVat);
        WriteMoneyProperty(w, "amount", line.LineVat.Amount);
        w.WriteString("subType", vatCategoryCode);
        w.WriteNumber("rate", line.VatRatePercent);
        w.WriteEndObject();
        w.WriteEndArray();

        w.WriteEndObject();
    }

    private static void WriteMoneyProperty(Utf8JsonWriter w, string name, decimal amount)
    {
        // MoneyEgp.AmountRoundedToCents semantics — banker's rounding to 2 dp.
        var rounded = decimal.Round(amount, 2, MidpointRounding.ToEven);
        w.WriteNumber(name, rounded);
    }

    private static string FormatPostingTimestampIso(SalesInvoice invoice)
    {
        var ts = invoice.PostedAtUtc ?? DateTime.UtcNow;
        return ts.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
    }
}
