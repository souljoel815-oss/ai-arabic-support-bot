namespace EgyptTax.Web.Licensing.Shamir;

/// <summary>
/// Arithmetic over GF(2^8) with reduction polynomial 0x11B (x^8 + x^4
/// + x^3 + x + 1) — the same field AES uses for its S-box. Pre-computed
/// log/antilog tables make multiplication, division, and inverse all
/// constant-time table lookups; addition and subtraction are XOR.
/// </summary>
internal static class Gf256
{
    private static readonly byte[] LogTable = new byte[256];
    private static readonly byte[] AntilogTable = new byte[256];

    static Gf256()
    {
        // Generator g = 0x03 (smallest primitive element for the AES
        // polynomial). Build the antilog table by repeated multiply,
        // then invert it for the log table.
        byte x = 1;
        for (var i = 0; i < 255; i++)
        {
            AntilogTable[i] = x;
            LogTable[x] = (byte)i;
            // x = x * g
            int next = (x << 1) ^ ((x & 0x80) != 0 ? 0x1B : 0); // 0x11B mod
            x = (byte)(next & 0xFF);
        }
        AntilogTable[255] = AntilogTable[0]; // wrap so log(1) cycle works
    }

    public static byte Add(byte a, byte b) => (byte)(a ^ b);
    public static byte Sub(byte a, byte b) => (byte)(a ^ b);

    public static byte Mul(byte a, byte b)
    {
        if (a == 0 || b == 0) return 0;
        var s = LogTable[a] + LogTable[b];
        if (s >= 255) s -= 255;
        return AntilogTable[s];
    }

    public static byte Div(byte a, byte b)
    {
        if (a == 0) return 0;
        if (b == 0) throw new DivideByZeroException("GF(256) divide by zero.");
        var s = LogTable[a] - LogTable[b];
        if (s < 0) s += 255;
        return AntilogTable[s];
    }
}
