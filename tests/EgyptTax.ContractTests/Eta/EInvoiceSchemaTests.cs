using System.Text.Json.Nodes;
using EgyptTax.Application.Eta;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Eta;
using EgyptTax.SharedKernel;
using Json.Schema;

namespace EgyptTax.ContractTests.Eta;

/// <summary>
/// T077 — every generated eInvoice JSON document MUST validate against
/// <c>contracts/eta-einvoice.schema.json</c> per SC-007 ("100% schema-
/// validated"). The generator covers the three customer tax-profile
/// branches (B2BRegistered → receiver.type=B with TIN; B2BUnregistered
/// → P; B2CConsumer → P) plus mixed-rate lines so the taxableItems
/// array is exercised end-to-end.
/// </summary>
public class EInvoiceSchemaTests
{
    private static readonly JsonSchema Schema = LoadSchema();

    [Fact]
    public void GeneratedJson_ForB2BRegisteredCustomer_ValidatesAgainstContractSchema()
    {
        var (invoice, issuer, receiver, request) = BuildFixture(
            CustomerTaxProfileType.B2BRegistered
        );
        var json = new EInvoiceJsonGenerator().GenerateAsJson(request);

        var validation = ValidateAgainstSchema(json);
        validation
            .IsValid.Should()
            .BeTrue(
                because: "B2B-Registered receivers MUST yield a schema-valid invoice (with TIN-bearing receiver.id). Errors: "
                    + string.Join("; ", validation.Errors)
            );

        var node = JsonNode.Parse(json)!.AsObject();
        node["receiver"]!["type"]!.GetValue<string>().Should().Be("B");
        node["receiver"]!["id"]!.GetValue<string>().Should().Match("?????????");
    }

    [Fact]
    public void GeneratedJson_ForB2BUnregisteredCustomer_ValidatesAgainstContractSchema_AndOmitsReceiverId()
    {
        var (invoice, issuer, receiver, request) = BuildFixture(
            CustomerTaxProfileType.B2BUnregistered
        );
        var json = new EInvoiceJsonGenerator().GenerateAsJson(request);

        var validation = ValidateAgainstSchema(json);
        validation
            .IsValid.Should()
            .BeTrue(
                because: "B2B-Unregistered receivers MUST yield a schema-valid invoice with no TIN. Errors: "
                    + string.Join("; ", validation.Errors)
            );

        var node = JsonNode.Parse(json)!.AsObject();
        node["receiver"]!["type"]!.GetValue<string>().Should().Be("P");
        node["receiver"]!
            .AsObject()
            .ContainsKey("id")
            .Should()
            .BeFalse(because: "FR-040 — only B2BRegistered receivers carry a TIN");
    }

    [Fact]
    public void GeneratedJson_ForB2CConsumerCustomer_ValidatesAgainstContractSchema_AndOmitsReceiverId()
    {
        var (invoice, issuer, receiver, request) = BuildFixture(CustomerTaxProfileType.B2CConsumer);
        var json = new EInvoiceJsonGenerator().GenerateAsJson(request);

        var validation = ValidateAgainstSchema(json);
        validation
            .IsValid.Should()
            .BeTrue(
                because: "B2C consumer receivers MUST yield a schema-valid invoice. Errors: "
                    + string.Join("; ", validation.Errors)
            );

        var node = JsonNode.Parse(json)!.AsObject();
        node["receiver"]!["type"]!.GetValue<string>().Should().Be("P");
    }

    [Fact]
    public void GeneratedJson_HeaderMatchesPostedInvoiceFields()
    {
        var (invoice, issuer, _, request) = BuildFixture(CustomerTaxProfileType.B2BRegistered);
        var json = new EInvoiceJsonGenerator().GenerateAsJson(request);

        var node = JsonNode.Parse(json)!.AsObject();
        node["documentType"]!.GetValue<string>().Should().Be("I");
        node["documentTypeVersion"]!.GetValue<string>().Should().Be("1.0");
        node["internalID"]!.GetValue<string>().Should().Be(invoice.DocumentNumber);
        node["taxpayerActivityCode"]!.GetValue<string>().Should().Be(issuer.TaxpayerActivityCode);
        node["totalAmount"]!.GetValue<decimal>().Should().Be(invoice.GrandTotal.Amount);
        node["totalSalesAmount"]!.GetValue<decimal>().Should().Be(invoice.Subtotal.Amount);
        node["taxTotals"]!.AsArray().Should().HaveCount(1);
        node["taxTotals"]![0]!["taxType"]!.GetValue<string>().Should().Be("T1");
        node["taxTotals"]![0]!["amount"]!.GetValue<decimal>().Should().Be(invoice.VatTotal.Amount);
    }

    [Fact]
    public void GeneratedJson_LineCount_MatchesInvoiceLineCount()
    {
        var (invoice, _, _, request) = BuildFixture(
            CustomerTaxProfileType.B2BRegistered,
            lineCount: 3
        );
        var json = new EInvoiceJsonGenerator().GenerateAsJson(request);

        var validation = ValidateAgainstSchema(json);
        validation
            .IsValid.Should()
            .BeTrue(
                because: "multi-line invoices MUST validate. Errors: "
                    + string.Join("; ", validation.Errors)
            );

        var lines = JsonNode.Parse(json)!.AsObject()["invoiceLines"]!.AsArray();
        lines.Should().HaveCount(invoice.Lines.Count);
    }

    [Fact]
    public void Generate_ForUnpostedDraft_Throws()
    {
        var (issuer, receiver, vat) = BuildIssuerReceiverVat(CustomerTaxProfileType.B2BRegistered);
        var draft = SalesInvoice.CreateDraft(
            receiver.Id,
            receiver.TaxProfile,
            new DateOnly(2026, 5, 7)
        );
        draft.AddLine(Guid.NewGuid(), 1m, MoneyEgp.From(100m), vat.Id, 14m);
        var request = new EInvoiceRenderRequest(
            draft,
            issuer,
            receiver,
            ItemCodes: new Dictionary<Guid, string>(),
            VatCategoryCodes: new Dictionary<Guid, string> { [vat.Id] = "Standard" }
        );

        var act = () => new EInvoiceJsonGenerator().GenerateAsJson(request);
        act.Should()
            .Throw<InvalidOperationException>(
                because: "the eInvoice is the post-time wire format; pre-post drafts MUST not be submitted to ETA"
            );
    }

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
            "eta-einvoice.schema.json"
        );
        if (!File.Exists(schemaPath))
        {
            throw new FileNotFoundException(
                $"Contract schema not found at {schemaPath}. The .csproj must copy it via the contracts/**/*.json glob."
            );
        }
        return JsonSchema.FromFile(schemaPath);
    }

    private static (
        SalesInvoice invoice,
        Company issuer,
        Customer receiver,
        EInvoiceRenderRequest request
    ) BuildFixture(CustomerTaxProfileType profileType, int lineCount = 1)
    {
        var (issuer, receiver, vat) = BuildIssuerReceiverVat(profileType);

        var item = new Item(
            code: "ITEM-001",
            name: new ArabicEnglishText("ساعة استشارة", "Consulting Hour"),
            defaultVatCategoryId: vat.Id
        );

        var draft = SalesInvoice.CreateDraft(
            customerId: receiver.Id,
            customerTaxProfileSnapshot: receiver.TaxProfile,
            documentDate: new DateOnly(2026, 5, 7)
        );
        for (var i = 0; i < lineCount; i++)
        {
            draft.AddLine(
                item.Id,
                quantity: 1m + i,
                unitPrice: MoneyEgp.From(500m + (100 * i)),
                vatCategoryId: vat.Id,
                vatRatePercent: vat.RatePercent
            );
        }

        // Force the invoice into Posted state for the generator.
        draft.MarkPosted(
            documentNumber: "INV-2026-000123",
            postedByUserId: Guid.NewGuid(),
            postedAtUtc: new DateTime(2026, 5, 7, 11, 0, 0, DateTimeKind.Utc),
            postingMode: DocumentPostingMode.UnapprovedDirect,
            approvalEnabled: false
        );

        var itemCodes = new Dictionary<Guid, string> { [item.Id] = item.Code };
        var vatCodes = new Dictionary<Guid, string> { [vat.Id] = vat.Code };
        var request = new EInvoiceRenderRequest(draft, issuer, receiver, itemCodes, vatCodes);
        return (draft, issuer, receiver, request);
    }

    private static (Company issuer, Customer receiver, VatCategory vat) BuildIssuerReceiverVat(
        CustomerTaxProfileType profileType
    )
    {
        var issuer = new Company(
            legalName: new ArabicEnglishText("شركة الاختبار", "Test Company SAE"),
            taxRegistrationNumber: EgyptianTin.Parse("123456789"),
            commercialRegistrationNumber: "CR-001234",
            address: PostalAddress.Create(
                display: new ArabicEnglishText("شارع التحرير ١٢", "12 Tahrir Street"),
                governorate: "Cairo",
                regionCity: "Downtown",
                street: "Tahrir",
                buildingNumber: "12",
                postalCode: "11511"
            ),
            taxpayerActivityCode: "0001"
        );

        var vat = new VatCategory(
            code: "Standard",
            name: new ArabicEnglishText("قياسي", "Standard"),
            ratePercent: 14m,
            effectiveFromDate: new DateOnly(2026, 1, 1),
            effectiveToDate: null,
            recoverableInputVat: true
        );

        var customerAddress = PostalAddress.Create(
            display: new ArabicEnglishText("شارع النيل ٤٥", "45 Nile Street"),
            governorate: "Giza",
            regionCity: "Dokki",
            street: "Nile",
            buildingNumber: "45"
        );

        var taxProfile = profileType switch
        {
            CustomerTaxProfileType.B2BRegistered => CustomerTaxProfile.B2BRegistered(
                EgyptianTin.Parse("987654321"),
                false,
                vat.Id
            ),
            CustomerTaxProfileType.B2BUnregistered => CustomerTaxProfile.B2BUnregistered(
                false,
                vat.Id
            ),
            CustomerTaxProfileType.B2CConsumer => CustomerTaxProfile.B2CConsumer(false, vat.Id),
            _ => throw new ArgumentOutOfRangeException(nameof(profileType)),
        };

        var receiver = new Customer(
            code: "CUST-001",
            name: new ArabicEnglishText("عميل تجريبي", "Test Customer LLC"),
            address: customerAddress,
            taxProfile: taxProfile
        );

        return (issuer, receiver, vat);
    }
}
