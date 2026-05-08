using System.Security.Cryptography;
using System.Text;
using EgyptTax.SharedKernel.Audit;

namespace EgyptTax.Domain.Audit;

/// <summary>
/// SHA-256 over <c>canonicalize(payload_json) || prev_hash</c>. The audit
/// emitter (server-side append) and the verifier (potentially running on a
/// clean inspector machine) MUST share this implementation so the same
/// logical inputs always yield the same hash bytes.
/// </summary>
public static class AuditChainHasher
{
    /// <summary>32-byte zero hash representing the genesis (no previous entry).</summary>
    public static readonly byte[] GenesisHash = new byte[32];

    /// <summary>
    /// Compute <c>SHA-256( JCS-canonicalize(payloadJson) || prevHash )</c>.
    /// </summary>
    public static byte[] ComputeHash(string payloadJson, byte[] prevHash)
    {
        ArgumentNullException.ThrowIfNull(payloadJson);
        ArgumentNullException.ThrowIfNull(prevHash);
        if (prevHash.Length != 32)
        {
            throw new ArgumentException(
                "prevHash must be exactly 32 bytes (SHA-256).",
                nameof(prevHash)
            );
        }

        var canonical = JsonCanonicalizer.Canonicalize(payloadJson);
        var canonicalBytes = Encoding.UTF8.GetBytes(canonical);

        var combined = new byte[canonicalBytes.Length + prevHash.Length];
        Buffer.BlockCopy(canonicalBytes, 0, combined, 0, canonicalBytes.Length);
        Buffer.BlockCopy(prevHash, 0, combined, canonicalBytes.Length, prevHash.Length);

        return SHA256.HashData(combined);
    }
}
