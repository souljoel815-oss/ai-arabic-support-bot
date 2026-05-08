using System.Security.Cryptography;
using EgyptTax.Application.Verification;
using EgyptTax.Infrastructure.Verification;

namespace EgyptTax.ContractTests.Verification;

/// <summary>
/// T078 — Document Verification Seal QR contract per
/// <c>contracts/verification-seal-qr.md</c>. Runs the canonical SC-013
/// scenarios: 100 random posted documents → encode → decode → verify
/// → all <c>VALID</c>; then for each, mutate one field at a time and
/// assert the verifier returns <c>TAMPERED</c> with the correct
/// mismatch. The malformed / unknown branches are also exercised.
/// </summary>
public class QrSealTests
{
    [Fact]
    public void Encode_StartsWithProtocolPrefix()
    {
        var seal = DocumentSealCodec.Encode(NewPayload(seed: 1));
        seal.Should()
            .StartWith(
                "EGT1.",
                because: "the contract mandates the EGT1.<base64url-cbor> envelope"
            );
        seal.Split('.').Should().HaveCount(2);
    }

    [Fact]
    public void OneHundredRandomDocuments_RoundTripDecode_AllPayloadsRecovered()
    {
        for (var seed = 1; seed <= 100; seed++)
        {
            var payload = NewPayload(seed);
            var seal = DocumentSealCodec.Encode(payload);
            var decoded = DocumentSealCodec.Decode(seal);
            decoded.Should().NotBeNull(because: $"seal #{seed} MUST decode cleanly");
            decoded!.DocumentType.Should().Be(payload.DocumentType);
            decoded.DocumentNumber.Should().Be(payload.DocumentNumber);
            decoded.DocumentId.Should().Be(payload.DocumentId);
            decoded.GrandTotalPiastres.Should().Be(payload.GrandTotalPiastres);
            decoded.AuditEntryHash.Should().Equal(payload.AuditEntryHash);
            decoded.AuditEntryIndex.Should().Be(payload.AuditEntryIndex);
            decoded.VerifyUrl.Should().Be(payload.VerifyUrl);
            decoded.IssuerTin.Should().Be(payload.IssuerTin);
        }
    }

    [Fact]
    public void OneHundredRandomDocuments_VerifyAgainstLiveResolver_AllValid()
    {
        for (var seed = 1; seed <= 100; seed++)
        {
            var payload = NewPayload(seed);
            var seal = DocumentSealCodec.Encode(payload);

            var resolver = ResolverFor(payload, divergence: SealDivergence.None);
            var result = DocumentSealCodec.Verify(seal, resolver);

            result
                .Outcome.Should()
                .Be(
                    SealOutcome.Valid,
                    because: $"seal #{seed} matches live resolver — verifier MUST report VALID"
                );
            result.Mismatches.Should().BeEmpty();
        }
    }

    [Theory]
    [InlineData(SealDivergence.GrandTotalPiastres, "grand_total_piastres")]
    [InlineData(SealDivergence.AuditEntryHash, "audit_entry_hash")]
    [InlineData(SealDivergence.DocumentNumber, "document_number")]
    [InlineData(SealDivergence.AuditEntryIndex, "audit_entry_index")]
    public void OneHundredTamperedDocuments_VerifyReportsTampered_WithCorrectField(
        SealDivergence divergence,
        string expectedField
    )
    {
        for (var seed = 1; seed <= 100; seed++)
        {
            var payload = NewPayload(seed);
            var seal = DocumentSealCodec.Encode(payload);

            var resolver = ResolverFor(payload, divergence);
            var result = DocumentSealCodec.Verify(seal, resolver);

            result
                .Outcome.Should()
                .Be(
                    SealOutcome.Tampered,
                    because: $"seed #{seed} mutated {divergence} on the live side — verifier MUST report TAMPERED"
                );
            result
                .Mismatches.Should()
                .Contain(
                    m => m.Field == expectedField,
                    because: $"the {divergence} mutation MUST surface as a mismatch on the {expectedField} field"
                );
        }
    }

    [Fact]
    public void Verify_DocumentNotFound_ReturnsUnknown()
    {
        var seal = DocumentSealCodec.Encode(NewPayload(seed: 7));
        var result = DocumentSealCodec.Verify(seal, _ => null);

        result
            .Outcome.Should()
            .Be(
                SealOutcome.Unknown,
                because: "when the resolver returns null, the document does not exist in this installation"
            );
    }

    [Theory]
    [InlineData("ABCDEF.12345")] // Wrong protocol
    [InlineData("EGT1.")] // Empty payload
    [InlineData("EGT1.not-base64url-!!!")] // Bad base64
    [InlineData("plain garbage")] // No dot
    [InlineData("EGT1.AAAA")] // Valid base64 but truncated CBOR
    public void Verify_MalformedSeal_ReturnsMalformed(string seal)
    {
        var result = DocumentSealCodec.Verify(
            seal,
            _ =>
                throw new InvalidOperationException(
                    "resolver MUST NOT be called for malformed seals"
                )
        );

        result
            .Outcome.Should()
            .Be(
                SealOutcome.Malformed,
                because: "verifier MUST short-circuit before calling the resolver when the seal cannot decode"
            );
    }

    private static DocumentSealPayload NewPayload(int seed)
    {
        // Seeded RNG so this test fixture is reproducible across runs.
        var rng = new Random(seed);
        var hash = new byte[32];
        rng.NextBytes(hash);

        var documentType =
            (seed % 7 == 0) ? SealedDocumentType.CreditNote : SealedDocumentType.SalesInvoice;
        var prefix = documentType == SealedDocumentType.SalesInvoice ? "INV" : "CN";
        var totalPiastres = (long)(rng.NextDouble() * 5_000_000); // up to ~50,000 EGP

        return new DocumentSealPayload(
            DocumentType: documentType,
            DocumentNumber: $"{prefix}-2026-{seed:D6}",
            DocumentId: NewSeededGuid(seed),
            GrandTotalPiastres: totalPiastres,
            AuditEntryHash: hash,
            AuditEntryIndex: seed * 10L,
            VerifyUrl: "/verify",
            IssuerTin: "123456789"
        );
    }

    private static Guid NewSeededGuid(int seed)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(seed).CopyTo(bytes, 0);
        return new Guid(bytes);
    }

    private static DocumentSealCodec.LiveDocumentResolver ResolverFor(
        DocumentSealPayload original,
        SealDivergence divergence
    ) =>
        documentId =>
        {
            if (documentId != original.DocumentId)
            {
                return null;
            }
            return divergence switch
            {
                SealDivergence.None => new ResolvedDocument(
                    DocumentNumber: original.DocumentNumber,
                    GrandTotalPiastres: original.GrandTotalPiastres,
                    AuditEntryHash: original.AuditEntryHash,
                    AuditEntryIndex: original.AuditEntryIndex
                ),
                SealDivergence.GrandTotalPiastres => new ResolvedDocument(
                    DocumentNumber: original.DocumentNumber,
                    GrandTotalPiastres: original.GrandTotalPiastres + 1,
                    AuditEntryHash: original.AuditEntryHash,
                    AuditEntryIndex: original.AuditEntryIndex
                ),
                SealDivergence.AuditEntryHash => new ResolvedDocument(
                    DocumentNumber: original.DocumentNumber,
                    GrandTotalPiastres: original.GrandTotalPiastres,
                    AuditEntryHash: FlipFirstByte(original.AuditEntryHash),
                    AuditEntryIndex: original.AuditEntryIndex
                ),
                SealDivergence.DocumentNumber => new ResolvedDocument(
                    DocumentNumber: original.DocumentNumber + "X",
                    GrandTotalPiastres: original.GrandTotalPiastres,
                    AuditEntryHash: original.AuditEntryHash,
                    AuditEntryIndex: original.AuditEntryIndex
                ),
                SealDivergence.AuditEntryIndex => new ResolvedDocument(
                    DocumentNumber: original.DocumentNumber,
                    GrandTotalPiastres: original.GrandTotalPiastres,
                    AuditEntryHash: original.AuditEntryHash,
                    AuditEntryIndex: original.AuditEntryIndex + 1
                ),
                _ => throw new ArgumentOutOfRangeException(nameof(divergence)),
            };
        };

    private static byte[] FlipFirstByte(byte[] source)
    {
        var copy = (byte[])source.Clone();
        copy[0] ^= 0xFF;
        return copy;
    }

    public enum SealDivergence
    {
        None,
        GrandTotalPiastres,
        AuditEntryHash,
        DocumentNumber,
        AuditEntryIndex,
    }
}
