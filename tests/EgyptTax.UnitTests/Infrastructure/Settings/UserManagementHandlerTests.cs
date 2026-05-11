using EgyptTax.Infrastructure.Settings;

namespace EgyptTax.UnitTests.Infrastructure.Settings;

/// <summary>
/// Gux.13 Tab 6 — covers the pure helpers on
/// <see cref="UserManagementHandler"/>. The DB-touching methods
/// (Add / Disable / Reactivate / Reset / Delete / List) are
/// exercised in the integration suite where the AppDbContext
/// fixture lives.
/// </summary>
public class UserManagementHandlerTests
{
    [Fact]
    public void GenerateTempPassword_Has12Characters()
    {
        var pw = UserManagementHandler.GenerateTempPassword();
        pw.Should().HaveLength(12);
    }

    [Fact]
    public void GenerateTempPassword_DifferentEachCall()
    {
        // 100 calls collision-free is overwhelmingly likely (alphabet
        // is 60 chars, 12 positions → ~10^21 combinations).
        var seen = new HashSet<string>();
        for (var i = 0; i < 100; i++)
        {
            seen.Add(UserManagementHandler.GenerateTempPassword());
        }
        seen.Should().HaveCount(100);
    }

    [Fact]
    public void GenerateTempPassword_OnlyAllowedCharacters()
    {
        const string alphabet = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789!@#$";
        for (var i = 0; i < 50; i++)
        {
            var pw = UserManagementHandler.GenerateTempPassword();
            foreach (var c in pw)
            {
                alphabet.Should().Contain(c.ToString(),
                    because: $"generated char '{c}' must be in the allowed alphabet");
            }
        }
    }

    [Fact]
    public void GenerateTempPassword_ExcludesAmbiguousGlyphs()
    {
        // Same dictation-friendly philosophy as ReferralCode.Derive —
        // the alphabet excludes 0/O/1/I/L/o/i/l so verbal handoff to
        // the new user works.
        for (var i = 0; i < 100; i++)
        {
            var pw = UserManagementHandler.GenerateTempPassword();
            pw.Should().NotContainAny("0", "O", "1", "I", "L", "o", "i", "l");
        }
    }
}
