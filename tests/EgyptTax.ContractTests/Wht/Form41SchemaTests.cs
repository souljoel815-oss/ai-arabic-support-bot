using System.Text.Json.Nodes;
using EgyptTax.Application.Wht;
using Json.Schema;

namespace EgyptTax.ContractTests.Wht;

/// <summary>
/// T199 / FR-046 / US7 — every Form 41 filing JSON MUST validate
/// against <c>contracts/form41.schema.json</c>. The PDF renderer
/// (batch 4b) consumes the same payload, so any drift between the
/// schema + the in-process payload would silently produce
/// regulator-rejected filings. Catches that drift in CI.
/// </summary>
public class Form41SchemaTests
{
    private static readonly JsonSchema Schema = LoadSchema();

    [Fact]
    public void FullyHydratedPayload_ValidatesAgainstContractSchema()
    {
        var payload = SamplePayload();
        var json = Form41JsonSerializer.Serialize(payload);

        var outcome = ValidateAgainstSchema(json);
        outcome
            .IsValid.Should()
            .BeTrue(
                because: "Form 41 payload MUST be schema-valid. Errors: "
                    + string.Join("; ", outcome.Errors)
            );

        // Spot-check the regulator's must-have fields.
        var node = JsonNode.Parse(json)!.AsObject();
        node["filingHeader"]!["fiscalYear"]!.GetValue<int>().Should().Be(2026);
        node["filingHeader"]!["quarter"]!.GetValue<int>().Should().Be(2);
        node["filingHeader"]!["companyName"]!["ar"]!.GetValue<string>().Should().NotBeNullOrEmpty();
        node["totals"]!["lineCount"]!.GetValue<int>().Should().Be(2);
        node["totals"]!["totalAmountWithheld"]!.GetValue<decimal>().Should().Be(750m);
        node["reconciliation"]!["matchesTotalAmountWithheld"]!.GetValue<bool>().Should().BeTrue();
    }

    [Fact]
    public void EmptyQuarterPayload_StillValidates()
    {
        // Quarters with no WHT activity still produce a Form 41
        // filing (an inspector wants to see we considered the
        // period, not silence). lines = []; totals all zero.
        var payload = SamplePayload() with
        {
            Lines = Array.Empty<Form41Line>(),
            Totals = new Form41Totals(
                LineCount: 0,
                TotalGrossPayment: 0m,
                TotalAmountWithheld: 0m,
                ByCategory: Array.Empty<Form41ByCategoryRow>()
            ),
            Reconciliation = new Form41Reconciliation(
                WhtPayableAccountBalanceAtPeriodEnd: 0m,
                MatchesTotalAmountWithheld: true,
                DiscrepancyAmount: null
            ),
        };
        var json = Form41JsonSerializer.Serialize(payload);

        var outcome = ValidateAgainstSchema(json);
        outcome
            .IsValid.Should()
            .BeTrue(
                because: "empty-quarter payload MUST still validate (lines: minItems isn't enforced). Errors: "
                    + string.Join("; ", outcome.Errors)
            );
    }

    [Fact]
    public void Reconciliation_WithDiscrepancy_StillValidates_AndExposesAmount()
    {
        // discrepancyAmount is optional in the schema — but when
        // present should be a number. Pin both shapes here.
        var payload = SamplePayload() with
        {
            Reconciliation = new Form41Reconciliation(
                WhtPayableAccountBalanceAtPeriodEnd: 800m,
                MatchesTotalAmountWithheld: false,
                DiscrepancyAmount: 50m
            ),
        };
        var json = Form41JsonSerializer.Serialize(payload);

        var outcome = ValidateAgainstSchema(json);
        outcome
            .IsValid.Should()
            .BeTrue(
                because: "discrepancyAmount=50 + matchesTotalAmountWithheld=false MUST validate (the cleanliness check is FR-046 application logic, not a schema constraint). Errors: "
                    + string.Join("; ", outcome.Errors)
            );

        var node = JsonNode.Parse(json)!.AsObject();
        node["reconciliation"]!["matchesTotalAmountWithheld"]!.GetValue<bool>().Should().BeFalse();
        node["reconciliation"]!["discrepancyAmount"]!.GetValue<decimal>().Should().Be(50m);
    }

    [Fact]
    public void Payload_WithMalformedCompanyTin_FailsSchemaValidation()
    {
        var payload = SamplePayload();
        var bad = payload with { FilingHeader = payload.FilingHeader with { CompanyTin = "BAD" } };
        var json = Form41JsonSerializer.Serialize(bad);

        var outcome = ValidateAgainstSchema(json);
        outcome
            .IsValid.Should()
            .BeFalse(because: "schema TIN pattern ^[0-9]{9}$ MUST reject non-9-digit values");
    }

    private static Form41Payload SamplePayload() =>
        new(
            FilingHeader: new Form41Header(
                CompanyTin: "123456789",
                CompanyName: new BilingualText("شركة الاختبار", "Test Company SAE"),
                FiscalYear: 2026,
                Quarter: 2,
                FillingPeriodStart: new DateOnly(2026, 4, 1),
                FillingPeriodEnd: new DateOnly(2026, 6, 30),
                PreparedAt: new DateTime(2026, 7, 5, 10, 0, 0, DateTimeKind.Utc),
                PreparedByUserId: Guid.NewGuid()
            ),
            Lines: new[]
            {
                new Form41Line(
                    SupplierTin: "987654321",
                    SupplierName: new BilingualText("مورد", "Acme Suppliers LLC"),
                    WhtCategoryCode: "Services",
                    RateAppliedPercent: 5m,
                    GrossPaymentTotal: 10_000m,
                    AmountWithheld: 500m,
                    SupplierPaymentVoucherNumber: "SPV-2026-000001",
                    SupplierPaymentVoucherDate: new DateOnly(2026, 5, 10),
                    SourceInvoiceNumber: "PI-2026-000123",
                    OutboundCertificateNumber: "WHT-SPV-2026-000001"
                ),
                new Form41Line(
                    SupplierTin: "987654322",
                    SupplierName: new BilingualText("مورد آخر", "Beta Suppliers LLC"),
                    WhtCategoryCode: "Professional",
                    RateAppliedPercent: 10m,
                    GrossPaymentTotal: 2_500m,
                    AmountWithheld: 250m,
                    SupplierPaymentVoucherNumber: "SPV-2026-000002",
                    SupplierPaymentVoucherDate: new DateOnly(2026, 6, 20),
                    SourceInvoiceNumber: "PI-2026-000200",
                    OutboundCertificateNumber: "WHT-SPV-2026-000002"
                ),
            },
            Totals: new Form41Totals(
                LineCount: 2,
                TotalGrossPayment: 12_500m,
                TotalAmountWithheld: 750m,
                ByCategory: new[]
                {
                    new Form41ByCategoryRow("Professional", 1, 250m),
                    new Form41ByCategoryRow("Services", 1, 500m),
                }
            ),
            Reconciliation: new Form41Reconciliation(
                WhtPayableAccountBalanceAtPeriodEnd: 750m,
                MatchesTotalAmountWithheld: true,
                DiscrepancyAmount: null
            ),
            AuditChainExtractRef: new Form41AuditChainExtractRef(
                StartIndex: 100,
                EndIndex: 250,
                ExtractSha256: "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
            )
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
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "contracts", "form41.schema.json");
        if (!File.Exists(schemaPath))
        {
            throw new FileNotFoundException(
                $"Contract schema not found at {schemaPath}. The .csproj must copy it via the contracts/**/*.json glob."
            );
        }
        return JsonSchema.FromFile(schemaPath);
    }
}
