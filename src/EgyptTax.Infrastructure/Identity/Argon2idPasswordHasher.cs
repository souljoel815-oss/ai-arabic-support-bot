using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using EgyptTax.Application.Identity;
using Konscious.Security.Cryptography;

namespace EgyptTax.Infrastructure.Identity;

/// <summary>
/// FR-002 / R-08 — Argon2id password hasher with OWASP-recommended
/// parameters (memory 64 MiB, iterations 3, parallelism 4). Stored
/// format: <c>argon2id$m={memKiB},t={iter},p={par}${base64-salt}${base64-hash}</c>
/// so a future bumped-parameter version can be detected at verify time
/// and the caller can decide to re-hash.
/// </summary>
public sealed class Argon2idPasswordHasher : IPasswordHasher
{
    private const string Algorithm = "argon2id";
    private const int DefaultMemoryKiB = 64 * 1024; // 64 MiB
    private const int DefaultIterations = 3;
    private const int DefaultParallelism = 4;
    private const int SaltLengthBytes = 16;
    private const int HashLengthBytes = 32;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        var salt = RandomNumberGenerator.GetBytes(SaltLengthBytes);
        var hash = ComputeHash(password, salt, DefaultMemoryKiB, DefaultIterations, DefaultParallelism);

        var paramsSegment = string.Create(CultureInfo.InvariantCulture,
            $"m={DefaultMemoryKiB},t={DefaultIterations},p={DefaultParallelism}");

        return string.Join('$',
            Algorithm,
            paramsSegment,
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    public bool Verify(string password, string hash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash))
        {
            return false;
        }

        var parts = hash.Split('$');
        if (parts.Length != 4 || parts[0] != Algorithm)
        {
            return false;
        }

        if (!TryParseParams(parts[1], out var memKiB, out var iter, out var par))
        {
            return false;
        }

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = ComputeHash(password, salt, memKiB, iter, par);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static byte[] ComputeHash(string password, byte[] salt, int memoryKiB, int iterations, int parallelism)
    {
        using var argon = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = parallelism,
            MemorySize = memoryKiB,
            Iterations = iterations,
        };
        return argon.GetBytes(HashLengthBytes);
    }

    private static bool TryParseParams(string paramSegment, out int memKiB, out int iterations, out int parallelism)
    {
        memKiB = iterations = parallelism = 0;
        var values = paramSegment.Split(',');
        if (values.Length != 3) return false;

        return TryParseKv(values[0], "m", out memKiB)
            && TryParseKv(values[1], "t", out iterations)
            && TryParseKv(values[2], "p", out parallelism);
    }

    private static bool TryParseKv(string segment, string expectedKey, out int value)
    {
        value = 0;
        var eq = segment.IndexOf('=', StringComparison.Ordinal);
        if (eq < 1) return false;
        var key = segment[..eq];
        var raw = segment[(eq + 1)..];
        return key == expectedKey && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
