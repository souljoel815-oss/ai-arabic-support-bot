using System.Security.Cryptography;

namespace EgyptTax.Web.Licensing.Shamir;

/// <summary>
/// Shamir's Secret Sharing over GF(2^8). Each byte of the secret is
/// split independently — same scheme as <c>ssss-split</c>, the
/// HashiCorp Vault unseal-keys generator, and most other byte-
/// granularity SSS implementations.
///
/// We use a 2-of-3 threshold: any 2 of the 3 shares reconstruct the
/// secret, but no single share leaks anything. The third share is
/// "free" in the sense that the install can recover from a corrupted
/// registry entry as long as the DPAPI file + HWID material are
/// intact.
///
/// Wire format per share:
///   byte[0]      = x-coordinate (1, 2 or 3) — the "share id"
///   byte[1..]    = y-coordinates per byte of the secret
/// Total share length = 1 + secret.Length.
/// </summary>
public static class ShamirSecretSharing
{
    public const int TotalShares = 3;
    public const int Threshold   = 2;

    /// <summary>
    /// Split a secret into <see cref="TotalShares"/> shares. Each
    /// share is independently meaningless; any
    /// <see cref="Threshold"/> reconstruct the secret exactly.
    /// </summary>
    public static byte[][] Split(byte[] secret)
    {
        ArgumentNullException.ThrowIfNull(secret);
        if (secret.Length == 0) throw new ArgumentException("Secret must be non-empty.", nameof(secret));

        // For threshold k: random polynomial of degree k-1.
        // For k=2: f(x) = secret_byte + a1*x   (a1 random per byte)
        var shares = new byte[TotalShares][];
        for (var s = 0; s < TotalShares; s++)
        {
            shares[s] = new byte[1 + secret.Length];
            shares[s][0] = (byte)(s + 1); // x = 1, 2, 3 (NOT zero — f(0) = secret)
        }

        // Per byte: pick a random a1, then evaluate f(x) for x = 1..3.
        var rnd = new byte[secret.Length];
        RandomNumberGenerator.Fill(rnd);

        for (var i = 0; i < secret.Length; i++)
        {
            var s0 = secret[i];
            var a1 = rnd[i];
            for (var s = 0; s < TotalShares; s++)
            {
                var x = (byte)(s + 1);
                // f(x) = s0 + a1 * x in GF(256)
                shares[s][1 + i] = Gf256.Add(s0, Gf256.Mul(a1, x));
            }
        }
        return shares;
    }

    /// <summary>
    /// Reconstruct the secret from any <see cref="Threshold"/> shares.
    /// Throws when fewer than the threshold are supplied or when the
    /// supplied shares are inconsistent (different lengths / duplicate
    /// x-coordinates).
    /// </summary>
    public static byte[] Reconstruct(IReadOnlyList<byte[]> shares)
    {
        ArgumentNullException.ThrowIfNull(shares);
        if (shares.Count < Threshold)
            throw new ArgumentException($"Need at least {Threshold} shares; got {shares.Count}.", nameof(shares));

        // De-duplicate by x and clip to threshold.
        var picked = new List<byte[]>(Threshold);
        var seen = new HashSet<byte>();
        foreach (var s in shares)
        {
            if (s is null || s.Length < 2) continue;
            if (!seen.Add(s[0])) continue;
            picked.Add(s);
            if (picked.Count == Threshold) break;
        }
        if (picked.Count < Threshold)
            throw new ArgumentException("Insufficient distinct shares for reconstruction.", nameof(shares));

        var len = picked[0].Length - 1;
        foreach (var s in picked)
        {
            if (s.Length - 1 != len)
                throw new ArgumentException("Share lengths inconsistent.", nameof(shares));
        }

        // Lagrange interpolation at x=0 over GF(256).
        var secret = new byte[len];
        for (var i = 0; i < len; i++)
        {
            byte acc = 0;
            for (var j = 0; j < picked.Count; j++)
            {
                byte num = 1;
                byte den = 1;
                var xj = picked[j][0];
                var yj = picked[j][1 + i];
                for (var m = 0; m < picked.Count; m++)
                {
                    if (m == j) continue;
                    var xm = picked[m][0];
                    // L_j(0) = ∏ (-x_m) / (x_j - x_m)
                    // In GF(256), addition == subtraction == XOR.
                    num = Gf256.Mul(num, xm);
                    den = Gf256.Mul(den, Gf256.Sub(xj, xm));
                }
                var lj = Gf256.Div(num, den);
                acc = Gf256.Add(acc, Gf256.Mul(yj, lj));
            }
            secret[i] = acc;
        }
        return secret;
    }
}
