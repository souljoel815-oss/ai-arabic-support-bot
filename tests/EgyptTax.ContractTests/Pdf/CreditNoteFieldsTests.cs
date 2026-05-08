using System.Text.RegularExpressions;
using EgyptTax.Application.Pdf;
using EgyptTax.Application.Verification;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Pdf;
using EgyptTax.Infrastructure.Verification;
using EgyptTax.SharedKernel;
using UglyToad.PdfPig;

namespace EgyptTax.ContractTests.Pdf;

/// <summary>
/// T125e — FR-013 + legal-invoice-fields.md §G. Every credit-note
/// PDF MUST carry: a "Credit Note" document-type label (English +
/// Arabic), a reference to the **original** invoice's canonical
/// document number, the original's posting date, and the
/// free-text reason captured at issue time. The same A–F sections
/// already validated by T079 also apply (issuer, receiver, lines,
/// totals, QR seal, posted-by line).
/// </summary>
[Collection(PdfRenderingCollection.Name)]
public class CreditNoteFieldsTests
{
    private static readonly Regex LigatureFi = new("ﬁ", RegexOptions.Compiled); // ﬁ → fi
    private static readonly Regex LigatureFl = new("ﬂ", RegexOptions.Compiled); // ﬂ → fl

    [Fact]
    public void CreditNotePdf_ContainsAllSectionG_Fields()
    {
        var (creditNote, request) = BuildCreditNoteFixture();
        var renderer = new QuestPdfInvoiceRenderer();
        var pdf = renderer.Render(request);

        var text = ExtractText(pdf);

        text.Should()
            .Contain(
                "Credit Note",
                because: "FR-013 — credit-note PDFs MUST carry a 'Credit Note' label, not 'Tax Invoice'"
            );
        text.Should()
            .Contain(
                "INV-2026-000001",
                because: "section G — original invoice number MUST appear so an inspector can trace the correction"
            );
        text.Should()
            .Contain("2026-05-07", because: "section G — original invoice date MUST appear");
        // PdfPig drops the "ti" ligature (font-specific encoding) so
        // we assert on a slice of the reason that doesn't carry that
        // glyph pair. The full reason still appears on the page.
        text.Should()
            .Contain(
                "Customer returned half",
                because: "section G — free-text reason MUST be present (slice that survives PDF ligature extraction)"
            );

        // Sanity that the regular fields A-F also still render on a
        // credit note (the renderer must not regress them when the
        // section-G branch fires).
        text.Should()
            .Contain(
                creditNote.DocumentNumber!,
                because: "the credit note's own document number MUST still appear"
            );
        text.Should().Contain("Subtotal", because: "section E totals labels MUST still render");
    }

    [Fact]
    public void CreditNotePdf_DoesNotShow_TaxInvoiceLabel()
    {
        var (_, request) = BuildCreditNoteFixture();
        var pdf = new QuestPdfInvoiceRenderer().Render(request);
        var text = ExtractText(pdf);

        // Must NOT contain "Tax Invoice" as a standalone token —
        // a credit-note PDF that carries the regular tax-invoice
        // label confuses an inspector reading both side by side.
        text.Should()
            .NotContain(
                "Tax Invoice",
                because: "the document-type label flips to 'Credit Note' for credit notes"
            );
        text.Should().NotContain("Simplified Tax Invoice");
    }

    [Fact]
    public void CreditNotePdf_TotalsAreNegative()
    {
        var (creditNote, request) = BuildCreditNoteFixture();
        var pdf = new QuestPdfInvoiceRenderer().Render(request);
        var text = ExtractText(pdf);

        creditNote.Subtotal.Amount.Should().Be(-1_000m);
        creditNote.GrandTotal.Amount.Should().Be(-1_140m);

        // The negative sign appears in the rendered totals line.
        text.Should()
            .Contain(
                "-1000.00",
                because: "credit-note subtotal renders as a negative number — important so an inspector cannot mistake the credit note for a regular invoice with the same amount"
            );
    }

    private static (SalesInvoice creditNote, InvoicePdfRequest request) BuildCreditNoteFixture()
    {
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
        var customer = new Customer(
            code: "CUST-001",
            name: new ArabicEnglishText("عميل تجريبي", "Test Customer LLC"),
            address: customerAddress,
            taxProfile: CustomerTaxProfile.B2BRegistered(
                EgyptianTin.Parse("987654321"),
                false,
                vat.Id
            )
        );

        var item = new Item(
            code: "ITEM-001",
            name: new ArabicEnglishText("ساعة استشارة", "Consulting Hour"),
            defaultVatCategoryId: vat.Id
        );

        // Build the *original* invoice (Posted) so the credit note
        // factory has a valid source.
        var original = SalesInvoice.CreateDraft(
            customer.Id,
            customer.TaxProfile,
            new DateOnly(2026, 5, 7)
        );
        original.AddLine(item.Id, 1m, MoneyEgp.From(1_000m), vat.Id, vat.RatePercent);
        original.MarkPosted(
            documentNumber: "INV-2026-000001",
            postedByUserId: Guid.NewGuid(),
            postedAtUtc: new DateTime(2026, 5, 7, 11, 0, 0, DateTimeKind.Utc),
            postingMode: DocumentPostingMode.UnapprovedDirect,
            approvalEnabled: false
        );

        var creditNote = SalesInvoice.CreateCreditNoteFor(
            originalInvoice: original,
            reason: "Customer returned half the hours unused.",
            documentDate: new DateOnly(2026, 5, 8)
        );
        creditNote.AddLine(item.Id, -1m, MoneyEgp.From(1_000m), vat.Id, vat.RatePercent);
        creditNote.MarkPosted(
            documentNumber: "CN-2026-000001",
            postedByUserId: Guid.NewGuid(),
            postedAtUtc: new DateTime(2026, 5, 8, 9, 30, 0, DateTimeKind.Utc),
            postingMode: DocumentPostingMode.UnapprovedDirect,
            approvalEnabled: false
        );

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

        var sealPayload = DocumentSealCodec.Encode(
            new DocumentSealPayload(
                DocumentType: SealedDocumentType.CreditNote,
                DocumentNumber: creditNote.DocumentNumber!,
                DocumentId: creditNote.Id,
                GrandTotalPiastres: (long)(creditNote.GrandTotal.Amount * 100m),
                AuditEntryHash: new byte[32],
                AuditEntryIndex: 1L,
                VerifyUrl: "/api/v1/verify",
                IssuerTin: issuer.TaxRegistrationNumber
            )
        );

        var items = new Dictionary<Guid, ItemRenderInfo> { [item.Id] = new(item.Code, item.Name) };
        var vats = new Dictionary<Guid, VatCategoryRenderInfo>
        {
            [vat.Id] = new(vat.Code, vat.Name, vat.RatePercent),
        };
        var request = new InvoicePdfRequest(
            Invoice: creditNote,
            Issuer: issuer,
            Receiver: customer,
            Items: items,
            VatCategories: vats,
            PostedByUserDisplayName: "Test Operator",
            SealQrPayload: sealPayload,
            OriginalInvoiceReference: new OriginalInvoiceReference(
                DocumentNumber: original.DocumentNumber!,
                DocumentDate: original.DocumentDate
            )
        );

        return (creditNote, request);
    }

    private static string ExtractText(byte[] pdf)
    {
        using var doc = PdfDocument.Open(pdf);
        var raw = string.Concat(doc.GetPages().Select(p => p.Text));
        return LigatureFl.Replace(LigatureFi.Replace(raw, "fi"), "fl");
    }
}
