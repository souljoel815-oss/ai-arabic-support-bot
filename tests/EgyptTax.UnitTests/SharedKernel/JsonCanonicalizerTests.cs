using EgyptTax.SharedKernel.Audit;

namespace EgyptTax.UnitTests.SharedKernel;

/// <summary>
/// Vectors derived from RFC 8785 (JSON Canonicalization Scheme — JCS).
/// The audit-chain hashing in FR-028 requires a deterministic JSON
/// representation so that the verifier (running on a clean machine) can
/// recompute the same hash from the same logical payload.
/// </summary>
public class JsonCanonicalizerTests
{
    [Fact]
    public void EmptyObject_RoundtripsAsEmptyObject()
    {
        JsonCanonicalizer.Canonicalize("{}").Should().Be("{}");
    }

    [Fact]
    public void EmptyArray_RoundtripsAsEmptyArray()
    {
        JsonCanonicalizer.Canonicalize("[]").Should().Be("[]");
    }

    [Fact]
    public void ObjectKeys_AreSortedLexicographically()
    {
        const string input = """{"b": 1, "a": 2, "c": 3}""";
        const string expected = """{"a":2,"b":1,"c":3}""";

        JsonCanonicalizer.Canonicalize(input).Should().Be(expected);
    }

    [Fact]
    public void NestedObjectKeys_AreSortedRecursively()
    {
        const string input = """{"outer": {"z": 1, "a": 2}, "alpha": 3}""";
        const string expected = """{"alpha":3,"outer":{"a":2,"z":1}}""";

        JsonCanonicalizer.Canonicalize(input).Should().Be(expected);
    }

    [Fact]
    public void Arrays_PreserveOrder()
    {
        const string input = """[3, 1, 2]""";
        const string expected = """[3,1,2]""";

        JsonCanonicalizer.Canonicalize(input).Should().Be(expected);
    }

    [Fact]
    public void Whitespace_IsStripped()
    {
        var input = "{\n  \"a\"  :  1  ,\n  \"b\"  :  2\n}";
        const string expected = """{"a":1,"b":2}""";

        JsonCanonicalizer.Canonicalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("\"hello\"", "\"hello\"")]
    [InlineData("\"a\\nb\"", "\"a\\nb\"")]
    [InlineData("\"a\\tb\"", "\"a\\tb\"")]
    [InlineData("\"\\\"\"", "\"\\\"\"")]
    public void Strings_PreserveStandardEscaping(string input, string expected)
    {
        JsonCanonicalizer.Canonicalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("true", "true")]
    [InlineData("false", "false")]
    [InlineData("null", "null")]
    public void Primitives_AreEmittedAsIs(string input, string expected)
    {
        JsonCanonicalizer.Canonicalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("0", "0")]
    [InlineData("1", "1")]
    [InlineData("-1", "-1")]
    [InlineData("1.5", "1.5")]
    [InlineData("1234567890", "1234567890")]
    public void Numbers_AreEmittedInShortestForm(string input, string expected)
    {
        JsonCanonicalizer.Canonicalize(input).Should().Be(expected);
    }

    [Fact]
    public void Canonicalize_IsIdempotent()
    {
        const string input = """{"b":1,"a":2}""";
        var first = JsonCanonicalizer.Canonicalize(input);
        var second = JsonCanonicalizer.Canonicalize(first);

        first.Should().Be(second);
    }

    [Fact]
    public void Canonicalize_ProducesIdenticalOutputForLogicallyEqualInputs()
    {
        // Two JSON strings that differ only in key order and whitespace.
        const string input1 = """{"a": 1, "b": 2}""";
        var input2 = "{\n    \"b\": 2,\n    \"a\": 1\n}";

        JsonCanonicalizer.Canonicalize(input1)
            .Should().Be(JsonCanonicalizer.Canonicalize(input2));
    }
}
