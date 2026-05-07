using System.Text;
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
/// T103 — golden-file regression test for the canonical sales-invoice
/// PDF. Renders a deterministic fixture (pinned GUIDs, fixed dates,
/// fixed totals), extracts text via PdfPig, normalizes ligatures +
/// whitespace, and compares against a checked-in golden text snapshot
/// at <c>Pdf/golden-invoice-b2bregistered.txt</c>.
///
/// Any unintended change to the renderer — a renamed label, a moved
/// totals row, an accidental locale shift — will surface here as a
/// diff between the rendered text and the snapshot. Intentional
/// changes are accepted by re-running with the env var
/// <c>UPDATE_GOLDEN_PDF=1</c>, which rewrites the snapshot under the
/// source tree (developer commits the diff alongside the renderer
/// change).
///
/// The golden file is text, not bytes — PDF byte-comparison is
/// brittle (timestamps + font subsetting + object ordering) but the
/// extracted text content is what an inspector and the eInvoice
/// JSON consumer actually rely on, so that's where the regression
/// signal lives.
/// </summary>
[Collection(PdfRenderingCollection.Name)]
public class InvoicePdfGoldenTests
{
    private const string GoldenFileName = "golden-invoice-b2bregistered.txt";

    [Fact]
    public void Canonical_B2BRegistered_Invoice_Matches_GoldenSnapshot()
    {
        var rendered = RenderCanonicalInvoiceText();
        var goldenPath = ResolveGoldenPath();

        if (Environment.GetEnvironmentVariable("UPDATE_GOLDEN_PDF") == "1")
        {
            File.WriteAllText(goldenPath, rendered, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return;
        }

        File.Exists(goldenPath).Should().BeTrue(
            because: $"the golden snapshot at {goldenPath} MUST be checked in. Run with UPDATE_GOLDEN_PDF=1 to bootstrap it after an intentional change to the renderer.");

        var golden = File.ReadAllText(goldenPath);

        // Cross-platform newline normalization so a developer on
        // Windows committing the file with CRLF doesn't break a
        // Linux CI run that reads the source with LF preserved.
        var normalizedGolden = golden.Replace("\r\n", "\n", StringComparison.Ordinal);
        var normalizedActual = rendered.Replace("\r\n", "\n", StringComparison.Ordinal);

        normalizedActual.Should().Be(normalizedGolden,
            because: "the rendered PDF text MUST match the checked-in golden snapshot exactly. " +
                     "If this is an intentional renderer change, re-run with UPDATE_GOLDEN_PDF=1 and commit the diff.");
    }

    private static string RenderCanonicalInvoiceText()
    {
        var fixture = BuildPinnedFixture();
        var pdf = new QuestPdfInvoiceRenderer().Render(fixture.Request);

        using var doc = PdfDocument.Open(pdf);
        var raw = string.Concat(doc.GetPages().Select(p => p.Text));

        return Normalize(raw);
    }

    /// <summary>
    /// Strip ligature substitutions and collapse runs of whitespace so
    /// minor PDF-encoder choices (font subsetting, kerning passes) do
    /// not invalidate the snapshot. The snapshot still captures the
    /// document-wide token stream — the regression signal we want.
    /// </summary>
    private static string Normalize(string raw)
    {
        // PdfPig occasionally emits U+0000 between shaped Arabic
        // glyph runs as a stream-boundary marker. Strip those so the
        // golden file is readable and diffable; the test still
        // catches semantic regressions because the surrounding glyph
        // codepoints and ordering are preserved.
        var nulStripped = Regex.Replace(raw, "\u0000", "");

        var ligatures = nulStripped
            .Replace("ﬀ", "ff", StringComparison.Ordinal)
            .Replace("ﬁ", "fi", StringComparison.Ordinal)
            .Replace("ﬂ", "fl", StringComparison.Ordinal)
            .Replace("ﬃ", "ffi", StringComparison.Ordinal)
            .Replace("ﬄ", "ffl", StringComparison.Ordinal)
            .Replace("ﬅ", "st", StringComparison.Ordinal)
            .Replace("ﬆ", "st", StringComparison.Ordinal);

        // Collapse runs of whitespace — including the non-breaking
        // spaces QuestPDF inserts at column boundaries — to a single
        // space, then trim each line.
        var collapsed = Regex.Replace(ligatures, @"[ \t ]+", " ");
        // The seal QR's verify-URL fallback prints the document_id as
        // human-readable text alongside the QR image (so an OCR tool
        // can read it when the QR is illegible). The document_id is
        // generated per-run via Guid.NewGuid() because the SalesInvoice
        // factory does not accept a caller-supplied Id; redact GUIDs
        // from the snapshot so it remains stable.
        var redacted = Regex.Replace(collapsed,
            @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
            "<GUID>");

        var lines = redacted.Split('\n', StringSplitOptions.None)
            .Select(l => l.TrimEnd())
            .ToArray();
        return string.Join("\n", lines);
    }

    private static string ResolveGoldenPath()
    {
        // The test runs from bin/Debug/net8.0 — walk back up to the
        // source tree's tests/EgyptTax.ContractTests/Pdf/ folder so
        // UPDATE_GOLDEN_PDF rewrites the source-tree file (the
        // developer commits this change), not the bin-folder copy.
        // Source-tree path is checked FIRST on every iteration; the
        // bin-folder sibling is only used as a final fallback when
        // no source tree is reachable (e.g. running the test DLL
        // outside its repo, on a CI runner that didn't clone the
        // tests/ tree).
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir, "tests", "EgyptTax.ContractTests", "Pdf", GoldenFileName);
            if (File.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        // Fallback: the bin-folder sibling that CopyToOutputDirectory
        // produces. Used only when no source-tree path resolves.
        var binSibling = Path.Combine(AppContext.BaseDirectory, "Pdf", GoldenFileName);
        if (File.Exists(binSibling)) return binSibling;
        // Last-resort bootstrap path so UPDATE_GOLDEN_PDF=1 can write
        // a brand-new file even when the file does not exist anywhere.
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "Pdf", GoldenFileName));
    }

    private static Fixture BuildPinnedFixture()
    {
        // Pinned GUIDs so the rendered PDF (and any text extraction
        // from it) is byte-deterministic across runs / machines.
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
                postalCode: "11511"),
            taxpayerActivityCode: "0001");

        var vatId = new Guid("11111111-1111-1111-1111-111111111111");
        var vat = new VatCategory(
            code: "Standard",
            name: new ArabicEnglishText("قياسي", "Standard"),
            ratePercent: 14m,
            effectiveFromDate: new DateOnly(2026, 1, 1),
            effectiveToDate: null,
            recoverableInputVat: true)
        { Id = vatId };

        var itemId = new Guid("22222222-2222-2222-2222-222222222222");
        var item = new Item(
            code: "ITEM-001",
            name: new ArabicEnglishText("ساعة استشارة", "Consulting Hour"),
            defaultVatCategoryId: vatId)
        { Id = itemId };

        var taxProfile = CustomerTaxProfile.B2BRegistered(
            EgyptianTin.Parse("987654321"), false, vatId);
        var receiver = new Customer(
            code: "CUST-001",
            name: new ArabicEnglishText("عميل تجريبي", "Test Customer LLC"),
            address: PostalAddress.Create(
                display: new ArabicEnglishText("شارع النيل ٤٥", "45 Nile Street"),
                governorate: "Giza", regionCity: "Dokki",
                street: "Nile", buildingNumber: "45"),
            taxProfile: taxProfile,
            phone: "+20 10 1234 5678")
        { Id = new Guid("33333333-3333-3333-3333-333333333333") };

        var draft = SalesInvoice.CreateDraft(receiver.Id, receiver.TaxProfile, new DateOnly(2026, 5, 7));
        draft.AddLine(item.Id, 1m, MoneyEgp.From(1_000m), vatId, vat.RatePercent);
        draft.MarkPosted(
            documentNumber: "INV-2026-000123",
            postedByUserId: new Guid("44444444-4444-4444-4444-444444444444"),
            postedAtUtc: new DateTime(2026, 5, 7, 11, 0, 0, DateTimeKind.Utc),
            postingMode: DocumentPostingMode.UnapprovedDirect,
            approvalEnabled: false);

        // The seal payload is binary inside the QR image, not text on
        // the page; pinning the document_id keeps the QR pixel-stable
        // even though it doesn't affect the extracted text.
        var sealPayload = DocumentSealCodec.Encode(new DocumentSealPayload(
            DocumentType: SealedDocumentType.SalesInvoice,
            DocumentNumber: draft.DocumentNumber!,
            DocumentId: draft.Id,
            GrandTotalPiastres: (long)(draft.GrandTotal.Amount * 100m),
            AuditEntryHash: new byte[32],
            AuditEntryIndex: 1L,
            VerifyUrl: "/verify",
            IssuerTin: issuer.TaxRegistrationNumber));

        var items = new Dictionary<Guid, ItemRenderInfo> { [item.Id] = new(item.Code, item.Name) };
        var vats = new Dictionary<Guid, VatCategoryRenderInfo>
        {
            [vat.Id] = new(vat.Code, vat.Name, vat.RatePercent),
        };
        var request = new InvoicePdfRequest(
            Invoice: draft,
            Issuer: issuer,
            Receiver: receiver,
            Items: items,
            VatCategories: vats,
            PostedByUserDisplayName: "Test Operator",
            SealQrPayload: sealPayload);

        return new Fixture(draft, issuer, receiver, request);
    }

    private sealed record Fixture(SalesInvoice Invoice, Company Issuer, Customer Receiver, InvoicePdfRequest Request);
}
