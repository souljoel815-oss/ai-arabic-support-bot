using System.Buffers;
using System.Buffers.Text;
using System.Formats.Cbor;
using System.Globalization;
using EgyptTax.Application.Verification;

namespace EgyptTax.Infrastructure.Verification;

/// <summary>
/// FR-044 / SC-013 — codec for the QR-encoded Document Verification
/// Seal. Format on the wire (per <c>contracts/verification-seal-qr.md</c>):
/// <c>EGT1.&lt;base64url-no-padding(deterministic-cbor(payload))&gt;</c>.
/// CBOR is preferred over JSON because the verifier targets a single
/// QR at typical print scaling (≤ 300 bytes); CBOR's binary integer
/// encoding plus the compact map-key shape (uint 1..8) keeps the seal
/// well under that ceiling for typical invoices.
/// </summary>
public static class DocumentSealCodec
{
    private const string ProtocolPrefix = "EGT1.";

    /// <summary>
    /// Resolver supplied by the verifier surface — given a document
    /// id, returns the live row's canonical fields, or <c>null</c> if
    /// the document does not exist in this installation
    /// (<see cref="SealOutcome.Unknown"/>).
    /// </summary>
    public delegate ResolvedDocument? LiveDocumentResolver(Guid documentId);

    public static string Encode(DocumentSealPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var writer = new CborWriter(
            CborConformanceMode.Ctap2Canonical,
            convertIndefiniteLengthEncodings: true
        );
        writer.WriteStartMap(8);

        writer.WriteUInt32(1);
        writer.WriteUInt32((uint)payload.DocumentType);

        writer.WriteUInt32(2);
        writer.WriteTextString(payload.DocumentNumber);

        writer.WriteUInt32(3);
        writer.WriteTextString(payload.DocumentId.ToString("D"));

        writer.WriteUInt32(4);
        writer.WriteInt64(payload.GrandTotalPiastres);

        writer.WriteUInt32(5);
        writer.WriteByteString(payload.AuditEntryHash);

        writer.WriteUInt32(6);
        writer.WriteInt64(payload.AuditEntryIndex);

        writer.WriteUInt32(7);
        writer.WriteTextString(payload.VerifyUrl);

        writer.WriteUInt32(8);
        writer.WriteTextString(payload.IssuerTin);

        writer.WriteEndMap();

        var bytes = writer.Encode();
        return ProtocolPrefix + Base64Url.Encode(bytes);
    }

    /// <summary>
    /// Decode a wire-format seal back into its payload. Returns
    /// <c>null</c> on malformed inputs (wrong prefix, broken base64,
    /// truncated CBOR, missing required keys, etc.) — the verifier
    /// then surfaces a <see cref="SealOutcome.Malformed"/> outcome.
    /// </summary>
    public static DocumentSealPayload? Decode(string seal)
    {
        if (string.IsNullOrEmpty(seal))
        {
            return null;
        }

        if (!seal.StartsWith(ProtocolPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var encoded = seal.AsSpan(ProtocolPrefix.Length);
        if (encoded.IsEmpty)
        {
            return null;
        }

        if (!Base64Url.TryDecode(encoded, out var raw))
        {
            return null;
        }

        try
        {
            var reader = new CborReader(raw, CborConformanceMode.Ctap2Canonical);
            if (reader.PeekState() != CborReaderState.StartMap)
            {
                return null;
            }
            var count = reader.ReadStartMap();
            if (count is not 8)
            {
                return null;
            }

            uint? docType = null;
            string? docNumber = null;
            string? docIdRaw = null;
            long? totalPiastres = null;
            byte[]? entryHash = null;
            long? entryIndex = null;
            string? verifyUrl = null;
            string? issuerTin = null;

            for (var i = 0; i < count; i++)
            {
                var key = reader.ReadUInt32();
                switch (key)
                {
                    case 1:
                        docType = reader.ReadUInt32();
                        break;
                    case 2:
                        docNumber = reader.ReadTextString();
                        break;
                    case 3:
                        docIdRaw = reader.ReadTextString();
                        break;
                    case 4:
                        totalPiastres = reader.ReadInt64();
                        break;
                    case 5:
                        entryHash = reader.ReadByteString();
                        break;
                    case 6:
                        entryIndex = reader.ReadInt64();
                        break;
                    case 7:
                        verifyUrl = reader.ReadTextString();
                        break;
                    case 8:
                        issuerTin = reader.ReadTextString();
                        break;
                    default:
                        reader.SkipValue();
                        break;
                }
            }
            reader.ReadEndMap();

            if (
                docType is null
                || docNumber is null
                || docIdRaw is null
                || totalPiastres is null
                || entryHash is null
                || entryIndex is null
                || verifyUrl is null
                || issuerTin is null
            )
            {
                return null;
            }

            if (!Guid.TryParse(docIdRaw, out var docId))
            {
                return null;
            }

            if (!Enum.IsDefined(typeof(SealedDocumentType), (int)docType.Value))
            {
                return null;
            }

            return new DocumentSealPayload(
                DocumentType: (SealedDocumentType)docType.Value,
                DocumentNumber: docNumber,
                DocumentId: docId,
                GrandTotalPiastres: totalPiastres.Value,
                AuditEntryHash: entryHash,
                AuditEntryIndex: entryIndex.Value,
                VerifyUrl: verifyUrl,
                IssuerTin: issuerTin
            );
        }
        catch (Exception ex)
            when (ex is CborContentException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    public static SealVerificationResult Verify(string seal, LiveDocumentResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);

        var payload = Decode(seal);
        if (payload is null)
        {
            return new SealVerificationResult(
                SealOutcome.Malformed,
                null,
                null,
                Array.Empty<SealMismatch>()
            );
        }

        var live = resolver(payload.DocumentId);
        if (live is null)
        {
            return new SealVerificationResult(
                SealOutcome.Unknown,
                payload.DocumentNumber,
                payload.DocumentType.ToString(),
                Array.Empty<SealMismatch>()
            );
        }

        var mismatches = new List<SealMismatch>(4);
        if (!string.Equals(live.DocumentNumber, payload.DocumentNumber, StringComparison.Ordinal))
        {
            mismatches.Add(
                new SealMismatch("document_number", payload.DocumentNumber, live.DocumentNumber)
            );
        }
        if (live.GrandTotalPiastres != payload.GrandTotalPiastres)
        {
            mismatches.Add(
                new SealMismatch(
                    "grand_total_piastres",
                    payload.GrandTotalPiastres.ToString(CultureInfo.InvariantCulture),
                    live.GrandTotalPiastres.ToString(CultureInfo.InvariantCulture)
                )
            );
        }
        if (!live.AuditEntryHash.AsSpan().SequenceEqual(payload.AuditEntryHash))
        {
            mismatches.Add(
                new SealMismatch(
                    "audit_entry_hash",
                    Convert.ToHexString(payload.AuditEntryHash),
                    Convert.ToHexString(live.AuditEntryHash)
                )
            );
        }
        if (live.AuditEntryIndex != payload.AuditEntryIndex)
        {
            mismatches.Add(
                new SealMismatch(
                    "audit_entry_index",
                    payload.AuditEntryIndex.ToString(CultureInfo.InvariantCulture),
                    live.AuditEntryIndex.ToString(CultureInfo.InvariantCulture)
                )
            );
        }

        var outcome = mismatches.Count == 0 ? SealOutcome.Valid : SealOutcome.Tampered;
        return new SealVerificationResult(
            outcome,
            payload.DocumentNumber,
            payload.DocumentType.ToString(),
            mismatches
        );
    }
}

/// <summary>
/// Base64url (RFC 4648 §5) encoder/decoder without padding. Stays
/// internal to the codec so the contract surface only exposes the
/// composed wire form.
/// </summary>
internal static class Base64Url
{
    public static string Encode(ReadOnlySpan<byte> bytes)
    {
        var standard = Convert.ToBase64String(bytes);
        return standard.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static bool TryDecode(ReadOnlySpan<char> input, out byte[] result)
    {
        // Re-pad and reverse alphabet substitutions, then run through
        // the framework decoder.
        var padded = ArrayPool<char>.Shared.Rent(input.Length + 4);
        try
        {
            for (var i = 0; i < input.Length; i++)
            {
                padded[i] = input[i] switch
                {
                    '-' => '+',
                    '_' => '/',
                    _ => input[i],
                };
            }
            var len = input.Length;
            var pad = (4 - (len & 3)) & 3;
            for (var i = 0; i < pad; i++)
            {
                padded[len + i] = '=';
            }

            var span = padded.AsSpan(0, len + pad);
            var buffer = new byte[((len + pad) / 4) * 3];
            if (!Convert.TryFromBase64Chars(span, buffer, out var written))
            {
                result = Array.Empty<byte>();
                return false;
            }
            result = buffer.AsSpan(0, written).ToArray();
            return true;
        }
        finally
        {
            ArrayPool<char>.Shared.Return(padded);
        }
    }
}
