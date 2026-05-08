using System.Text.Json;

namespace EgyptTax.UnitTests.SharedKernel.Localization;

/// <summary>
/// T060 — R-19 mandates an Egyptian-tax terminology dictionary that
/// is **bilingually complete**: every key present in one locale's
/// JSON file MUST be present in the other. The CI gate is this test:
/// it parses both files, computes the symmetric difference of their
/// keys, and fails with the specific list of missing keys per locale.
/// A drift-by-one-key in the future therefore points at the exact
/// term that was added in one file and forgotten in the other.
/// </summary>
public class TerminologyKeyParityTests
{
    [Fact]
    public void TerminologyArabic_AndEnglish_HaveIdenticalKeySets()
    {
        var arabic = LoadKeys("terminology.ar.json");
        var english = LoadKeys("terminology.en.json");

        var missingFromArabic = english.Except(arabic).Order().ToArray();
        var missingFromEnglish = arabic.Except(english).Order().ToArray();

        var failures = new List<string>(2);
        if (missingFromArabic.Length > 0)
        {
            failures.Add(
                $"Keys present in terminology.en.json but missing from terminology.ar.json: {string.Join(", ", missingFromArabic)}"
            );
        }
        if (missingFromEnglish.Length > 0)
        {
            failures.Add(
                $"Keys present in terminology.ar.json but missing from terminology.en.json: {string.Join(", ", missingFromEnglish)}"
            );
        }

        failures
            .Should()
            .BeEmpty(
                because: "R-19 — both terminology files MUST cover the same key set so the bilingual UI never falls back to a missing translation"
            );
    }

    [Fact]
    public void TerminologyFiles_AreNonEmpty()
    {
        // Sanity check — a future regression that emits empty JSON
        // would silently pass the parity test (∅ == ∅) but break the
        // app. Catch that here.
        LoadKeys("terminology.ar.json").Should().NotBeEmpty();
        LoadKeys("terminology.en.json").Should().NotBeEmpty();
    }

    [Fact]
    public void Terminology_RequiredCoreKeys_ArePresent()
    {
        // The MVP UI surface (master data + invoice editor) needs
        // these terms. Adding them as a hard floor catches accidental
        // deletion as well as missing-key drift.
        var requiredKeys = new[]
        {
            "vat",
            "salesInvoice",
            "creditNote",
            "tin",
            "customer",
            "supplier",
            "invoice",
            "post",
            "draft",
            "submitted",
            "approved",
            "posted",
            "voided",
        };

        var arabic = LoadKeys("terminology.ar.json");
        foreach (var key in requiredKeys)
        {
            arabic
                .Should()
                .Contain(key, because: $"required core key '{key}' MUST be in terminology.ar.json");
        }
    }

    private static HashSet<string> LoadKeys(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Localization", fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Terminology file not found at {path}. The .csproj must copy it via the Localization/*.json glob."
            );
        }
        using var stream = File.OpenRead(path);
        using var doc = JsonDocument.Parse(stream);
        return doc
            .RootElement.EnumerateObject()
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);
    }
}
