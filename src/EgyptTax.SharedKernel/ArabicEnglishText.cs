namespace EgyptTax.SharedKernel;

public readonly record struct ArabicEnglishText(string Arabic, string English)
{
    public static ArabicEnglishText Empty { get; } = new(string.Empty, string.Empty);

    /// <summary>
    /// Pick the rendering for a preferred language. If the preferred side is
    /// empty, fall back to the other language so display surfaces never show
    /// a blank string when at least one side has content.
    /// </summary>
    public string Display(Language preferred) => preferred switch
    {
        Language.Ar when !string.IsNullOrEmpty(Arabic) => Arabic,
        Language.Ar => English,
        Language.En when !string.IsNullOrEmpty(English) => English,
        Language.En => Arabic,
        _ => Arabic,
    };
}
