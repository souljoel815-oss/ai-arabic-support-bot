using System.Text.Json.Nodes;
using EgyptTax.Application.Wht;
using Json.Schema;

namespace EgyptTax.ContractTests.Wht;

/// <summary>
/// T198 / FR-045 / US7 — every WHT-certificate JSON shape MUST
/// validate against <c>contracts/wht-certificate.schema.json</c>.
/// The PDF renderer (T207) consumes the same payload, so any
/// drift between the schema + the in-process payload would silently
/// produce inspector-rejected certificates. Catches that drift in
/// CI before it ships.
/// </summary>
public class WhtCertificateSchemaTests
{
    private static readonly JsonSchema Schema = LoadSchema();

    [Fact]
    public void OutboundToSupplier_FullyHydratedPayload_ValidatesAgainstContractSchema()
    {
        var payload = SamplePayload(direction: "OutboundToSupplier");
        var json = WhtCertificateJsonSerializer.Serialize(payload);

        var outcome = ValidateAgainstSchema(json);
        outcome
            .IsValid.Should()
            .BeTrue(
                because: "Outbound-to-supplier payload MUST be schema-valid. Errors: "
                    + string.Join("; ", outcome.Errors)
            );

        // Spot-check a couple of must-have fields.
        var node = JsonNode.Parse(json)!.AsObject();
        node["direction"]!.GetValue<string>().Should().Be("OutboundToSupplier");
        node["currency"]!.GetValue<string>().Should().Be("EGP");
        node["language"]!.GetValue<string>().Should().Be("ar+en");
        node["issuerCompanyName"]!["ar"]!.GetValue<string>().Should().NotBeNullOrEmpty();
        node["issuerCompanyName"]!["en"]!.GetValue<string>().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void InboundFromCustomer_FullyHydratedPayload_ValidatesAgainstContractSchema()
    {
        var payload = SamplePayload(direction: "InboundFromCustomer");
        var json = WhtCertificateJsonSerializer.Serialize(payload);

        var outcome = ValidateAgainstSchema(json);
        outcome
            .IsValid.Should()
            .BeTrue(
                because: "Inbound-from-customer payload MUST be schema-valid. Errors: "
                    + string.Join("; ", outcome.Errors)
            );

        var node = JsonNode.Parse(json)!.AsObject();
        node["direction"]!.GetValue<string>().Should().Be("InboundFromCustomer");
    }

    [Fact]
    public void Payload_OmittingOptionalGrossAndNet_StillValidates()
    {
        // Schema marks `grossPayment` + `netPayment` as optional —
        // only `amountWithheld` is required. A minimal payload
        // (e.g., for a future inbound cert that arrived without
        // gross/net context) MUST still validate.
        var payload = SamplePayload(direction: "InboundFromCustomer") with
        {
            GrossPayment = null,
            NetPayment = null,
        };
        var json = WhtCertificateJsonSerializer.Serialize(payload);

        var outcome = ValidateAgainstSchema(json);
        outcome
            .IsValid.Should()
            .BeTrue(
                because: "grossPayment + netPayment are optional. Errors: "
                    + string.Join("; ", outcome.Errors)
            );

        // Make sure the optional fields actually got OMITTED (not
        // serialised as null) — the schema would reject a null on
        // a number-typed field.
        var node = JsonNode.Parse(json)!.AsObject();
        node.ContainsKey("grossPayment").Should().BeFalse();
        node.ContainsKey("netPayment").Should().BeFalse();
    }

    [Fact]
    public void Payload_WithMalformedTin_FailsSchemaValidation()
    {
        // Sanity check the schema's pattern guards (^[0-9]{9}$ for
        // both TINs) actually fire — catches a future regression
        // where the TIN type accepts non-9-digit junk.
        var payload = SamplePayload(direction: "OutboundToSupplier") with
        {
            CounterpartyTin = "ABC", // not 9 digits
        };
        var json = WhtCertificateJsonSerializer.Serialize(payload);

        var outcome = ValidateAgainstSchema(json);
        outcome
            .IsValid.Should()
            .BeFalse(because: "the schema's TIN pattern MUST reject non-9-digit values");
    }

    private static WhtCertificatePayload SamplePayload(string direction) =>
        new(
            CertificateNumber: direction == "OutboundToSupplier"
                ? "WHT-SPV-2026-000001"
                : "CUST-CERT-2026-001",
            Direction: direction,
            IssuedAt: new DateTime(2026, 5, 10, 11, 0, 0, DateTimeKind.Utc),
            IssuerCompanyTin: "123456789",
            IssuerCompanyName: new BilingualText("شركة الاختبار", "Test Company SAE"),
            CounterpartyTin: "987654321",
            CounterpartyName: new BilingualText("الطرف الآخر", "Counterparty LLC"),
            SourceInvoiceNumber: direction == "OutboundToSupplier"
                ? "PI-2026-000123"
                : "INV-2026-000123",
            SourceInvoiceDate: new DateOnly(2026, 5, 9),
            SourceVoucherNumber: direction == "OutboundToSupplier"
                ? "SPV-2026-000001"
                : "CRV-2026-000001",
            SourceVoucherDate: new DateOnly(2026, 5, 10),
            WhtCategoryCode: "Services",
            WhtCategoryName: new BilingualText("خدمات", "Services"),
            RateAppliedPercent: 5m,
            AmountWithheld: 500m,
            GrossPayment: 10_000m,
            NetPayment: 9_500m,
            Currency: "EGP",
            Language: "ar+en",
            AuditChainHash: "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
        );

    private static SchemaValidationOutcome ValidateAgainstSchema(string json)
    {
        var node = JsonNode.Parse(json);
        var results = Schema.Evaluate(
            node,
            new EvaluationOptions
            {
                OutputFormat = OutputFormat.List,
                EvaluateAs = SpecVersion.Draft202012,
            }
        );
        if (results.IsValid)
        {
            return new SchemaValidationOutcome(true, Array.Empty<string>());
        }
        var errors = results
            .Details.Where(d => d.HasErrors)
            .SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Key}={e.Value}"))
            .ToArray();
        return new SchemaValidationOutcome(false, errors);
    }

    private sealed record SchemaValidationOutcome(bool IsValid, IReadOnlyList<string> Errors);

    private static JsonSchema LoadSchema()
    {
        var schemaPath = Path.Combine(
            AppContext.BaseDirectory,
            "contracts",
            "wht-certificate.schema.json"
        );
        if (!File.Exists(schemaPath))
        {
            throw new FileNotFoundException(
                $"Contract schema not found at {schemaPath}. The .csproj must copy it via the contracts/**/*.json glob."
            );
        }
        return JsonSchema.FromFile(schemaPath);
    }
}
