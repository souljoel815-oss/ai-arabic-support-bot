using System.Security.Cryptography;
using System.Text;

namespace EgyptTax.Application.Referrals;

/// <summary>
/// G4.3 — deterministic referral-code derivation from a hardware
/// identifier. Same HWID always produces the same code so the
/// customer can rely on "my code is XK4M-7PQ2" surviving restarts
/// and database wipes. No DB row needed for issuance.
///
/// Format: <c>XXXX-XXXX</c> — 8 base-32 characters split by a dash
/// for readability. Excludes ambiguous glyphs (0/O, 1/I/L) to keep
/// phone-dictation reliable for the SMB market.
///
/// Vendor-side reconciliation: when a customer activates their
/// license via Issue-License.ps1, the operator can supply
/// <c>-ReferrerCode XK4M-7PQ2</c>; the vendor reverse-looks-up the
/// HWID it was minted from (their license ledger holds every
/// issued HWID) and credits both parties 30 days. The reverse
/// lookup is feasible because the alphabet is small enough to
/// brute-force a HMAC against the small number of issued HWIDs.
/// </summary>
public static class ReferralCode
{
    // Crockford-style alphabet minus ambiguous glyphs (0/O, 1/I/L).
    // 32 distinct symbols → 5 bits per char → 40 bits of entropy
    // across 8 chars. Plenty for a per-install code that's only
    // collision-checked at issuance.
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ"; // 31 chars
    private const string AlphabetPadded = Alphabet + "2";              // pad to 32

    /// <summary>HMAC-SHA256 the HWID with a stable salt, then map the
    /// first 8 base-32 nibbles into the alphabet. Salt is part of the
    /// program to keep the derivation pinned to DaftarX (so the same
    /// HWID never generates a code we'd accept from a stranger's
    /// totally different product).</summary>
    private static readonly byte[] Salt = Encoding.UTF8.GetBytes("daftarx-referral-v1");

    public static string Derive(string hwid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hwid);
        using var mac = new HMACSHA256(Salt);
        var hash = mac.ComputeHash(Encoding.UTF8.GetBytes(hwid.Trim().ToUpperInvariant()));

        var sb = new StringBuilder(9);
        for (int i = 0; i < 8; i++)
        {
            // Take 5 low-bits per character — the upper 24 bits of the
            // hash provide ample entropy across 8 chars.
            var idx = hash[i] & 0x1F;
            if (sb.Length == 4) sb.Append('-');
            sb.Append(AlphabetPadded[idx]);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Lightweight format check — does the input string LOOK like a
    /// referral code? Used to validate paste-ins on the redemption
    /// form before bothering with the vendor lookup.
    /// </summary>
    public static bool IsValidShape(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;
        var s = input.Trim().ToUpperInvariant();
        if (s.Length != 9) return false;
        if (s[4] != '-') return false;
        for (int i = 0; i < 9; i++)
        {
            if (i == 4) continue;
            if (!Alphabet.Contains(s[i])) return false;
        }
        return true;
    }

    /// <summary>Normalize a user-supplied code: trim, upper-case,
    /// strip non-alphabet chars, re-insert the dash. Lets the
    /// operator paste "xk4m 7pq2" or "XK4M7PQ2" without ceremony.</summary>
    public static string? TryNormalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var s = input.Trim().ToUpperInvariant();
        var clean = new StringBuilder(8);
        foreach (var c in s)
        {
            if (Alphabet.Contains(c)) clean.Append(c);
            if (clean.Length == 8) break;
        }
        if (clean.Length != 8) return null;
        return $"{clean.ToString()[..4]}-{clean.ToString()[4..]}";
    }
}
