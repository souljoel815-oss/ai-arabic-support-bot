using EgyptTax.Infrastructure.Identity;

namespace EgyptTax.UnitTests.Infrastructure.Identity;

public class Argon2idPasswordHasherTests
{
    [Fact]
    public void Hash_RoundtripsThroughVerify()
    {
        var sut = new Argon2idPasswordHasher();

        var hash = sut.Hash("Tr0ub4dor!ARightBig0ne");

        sut.Verify("Tr0ub4dor!ARightBig0ne", hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_WithWrongPassword_ReturnsFalse()
    {
        var sut = new Argon2idPasswordHasher();
        var hash = sut.Hash("correct horse battery staple");

        sut.Verify("incorrect horse battery staple", hash).Should().BeFalse();
    }

    [Fact]
    public void Hash_ProducesUniqueHashesForSameInput()
    {
        // The salt is per-call random, so two hashes of the same input
        // MUST differ. (They both verify against the input.)
        var sut = new Argon2idPasswordHasher();

        var a = sut.Hash("hello world");
        var b = sut.Hash("hello world");

        a.Should().NotBe(b);
        sut.Verify("hello world", a).Should().BeTrue();
        sut.Verify("hello world", b).Should().BeTrue();
    }

    [Fact]
    public void Hash_FormatStartsWithAlgorithmIdentifier()
    {
        // Per R-08 the stored format is `{algo}${params}${salt}${hash}`.
        // The algo identifier carries forward across parameter bumps so a
        // future hasher can decide whether to re-hash on login.
        var sut = new Argon2idPasswordHasher();

        var hash = sut.Hash("anything");

        hash.Should().StartWith("argon2id$");
    }

    [Fact]
    public void Verify_OnMalformedHash_ReturnsFalse()
    {
        var sut = new Argon2idPasswordHasher();

        sut.Verify("password", "not-a-hash").Should().BeFalse();
        sut.Verify("password", "argon2id$broken").Should().BeFalse();
        sut.Verify("password", string.Empty).Should().BeFalse();
    }
}
