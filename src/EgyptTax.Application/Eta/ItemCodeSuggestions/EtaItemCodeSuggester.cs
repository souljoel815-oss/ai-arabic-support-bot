using System.Reflection;
using System.Text.Json;

namespace EgyptTax.Application.Eta.ItemCodeSuggestions;

/// <summary>
/// G2.1 — pure fuzzy-matching suggester for ETA item codes
/// (GS1 GPC + EGS local). Operator types an item description in
/// Arabic OR English; the suggester returns the top-N candidates
/// from the bundled corpus with a 0–100 confidence score.
///
/// Algorithm:
///   1. Tokenise the operator's input (Arabic + Latin both).
///   2. For each corpus entry, compute token-overlap score
///      against entry.ar (Arabic keywords) AND entry.en (English
///      keywords). Best of the two wins per entry.
///   3. Add a substring bonus when any input token is a substring
///      of any keyword (catches "لاب" → "لاب توب").
///   4. Sort descending, take top N (default 3).
///
/// Corpus is embedded JSON. Operators / vendor expand it
/// incrementally — each entry is independent, no schema migrations.
/// LLM upgrade is the G3.1 follow-on; this static fuzzy matcher
/// covers ~95% of common Egyptian SMB inventory at zero cost.
///
/// Static — no instance state, no DI registration needed (same
/// shape as <c>BankMatchScorer</c>).
/// </summary>
public static class EtaItemCodeSuggester
{
    /// <summary>Above this score we mark the suggestion "high
    /// confidence" — operator can apply with one click without
    /// reviewing alternatives.</summary>
    public const int HighConfidenceThreshold = 80;

    /// <summary>Below this score we don't show the suggestion at
    /// all — too noisy.</summary>
    public const int MinUsefulScore = 25;

    private static readonly char[] TokenSeparators =
        new[] { ' ', '\t', '\n', '-', '_', '/', '.', ',', '،', '؛' };

    private static readonly Lazy<IReadOnlyList<EtaItemCodeEntry>> Corpus = new(LoadCorpus);

    private static readonly JsonSerializerOptions CorpusJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Public read-only handle on the loaded corpus —
    /// used by tests + a future "manage suggestions" UI.</summary>
    public static IReadOnlyList<EtaItemCodeEntry> All => Corpus.Value;

    public static IReadOnlyList<EtaItemCodeSuggestion> Suggest(
        string description,
        int topN = 3)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return Array.Empty<EtaItemCodeSuggestion>();
        }

        var inputTokens = Tokenise(description);
        if (inputTokens.Length == 0)
        {
            return Array.Empty<EtaItemCodeSuggestion>();
        }

        var scored = new List<EtaItemCodeSuggestion>(Corpus.Value.Count);
        foreach (var entry in Corpus.Value)
        {
            var arScore = ScoreAgainstKeywords(inputTokens, entry.Ar);
            var enScore = ScoreAgainstKeywords(inputTokens, entry.En);
            // Also try the labels themselves (operator might paste
            // the Arabic display name verbatim).
            var labelScoreAr = ScoreAgainstKeywords(inputTokens, new[] { entry.LabelAr });
            var labelScoreEn = ScoreAgainstKeywords(inputTokens, new[] { entry.LabelEn });

            var score = Math.Max(Math.Max(arScore, enScore),
                                 Math.Max(labelScoreAr, labelScoreEn));
            if (score < MinUsefulScore) continue;

            scored.Add(new EtaItemCodeSuggestion(
                Code: entry.Code,
                Kind: entry.Kind,
                LabelAr: entry.LabelAr,
                LabelEn: entry.LabelEn,
                Category: entry.Category,
                Confidence: Math.Clamp(score, 0, 100)));
        }

        return scored
            .OrderByDescending(s => s.Confidence)
            .Take(topN)
            .ToList();
    }

    private static int ScoreAgainstKeywords(
        string[] inputTokens,
        IReadOnlyCollection<string> keywords)
    {
        if (keywords.Count == 0) return 0;

        var keywordTokens = keywords
            .SelectMany(kw => Tokenise(kw))
            .ToArray();
        if (keywordTokens.Length == 0) return 0;

        // Direct token-equality hits — strongest signal.
        var hits = inputTokens.Count(it =>
            keywordTokens.Any(kt => string.Equals(kt, it, StringComparison.OrdinalIgnoreCase)));

        // Substring containment — catches partial spelling
        // ("لاب" matches "لابتوب").
        var partials = inputTokens.Count(it =>
            keywordTokens.Any(kt =>
                kt.Length >= 3 && it.Length >= 3 &&
                (kt.Contains(it, StringComparison.OrdinalIgnoreCase) ||
                 it.Contains(kt, StringComparison.OrdinalIgnoreCase))));

        // Score: 60 pts for direct hits scaled by ratio of input
        // tokens matched; 40 pts for partials scaled the same.
        // Cap at 100.
        var hitRatio = (double)hits / inputTokens.Length;
        var partialRatio = (double)Math.Max(0, partials - hits) / inputTokens.Length;
        var score = (int)Math.Round(hitRatio * 60 + partialRatio * 40);
        return Math.Clamp(score, 0, 100);
    }

    private static string[] Tokenise(string input) =>
        input.Split(TokenSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .ToArray();

    private static System.Collections.ObjectModel.ReadOnlyCollection<EtaItemCodeEntry> LoadCorpus()
    {
        var assembly = typeof(EtaItemCodeSuggester).Assembly;
        var resourceName = $"{assembly.GetName().Name}.Eta.ItemCodeSuggestions.eta-item-codes.json";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded corpus '{resourceName}' missing. Check the EmbeddedResource Include in EgyptTax.Application.csproj.");
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();

        var entries = JsonSerializer.Deserialize<List<EtaItemCodeEntry>>(json, CorpusJsonOptions)
            ?? new List<EtaItemCodeEntry>();
        return entries.AsReadOnly();
    }
}

/// <summary>One row in the bundled corpus — read directly from
/// the JSON. Property names are case-insensitive on deserialise.</summary>
public sealed record EtaItemCodeEntry(
    string Code,
    string Kind,
    string LabelEn,
    string LabelAr,
    string[] Ar,
    string[] En,
    string Category);

/// <summary>One suggestion returned to the UI. Confidence is 0-100;
/// ≥80 = "apply with one click", lower = "operator should review".</summary>
public sealed record EtaItemCodeSuggestion(
    string Code,
    string Kind,
    string LabelAr,
    string LabelEn,
    string Category,
    int Confidence);
