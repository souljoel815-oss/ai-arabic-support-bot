namespace EgyptTax.Web.Licensing;

/// <summary>
/// Gux.13 — thrown by <see cref="EditionGate.Require"/> when the
/// current install's edition doesn't include the requested feature.
/// The Blazor pages catch this and render the upgrade prompt
/// (current-edition vs minimum-required-edition + "Upgrade now"
/// CTA) instead of a 500 page.
///
/// The Arabic + English messages are pre-rendered here so the
/// catching UI doesn't have to know the feature → label mapping.
/// </summary>
public sealed class LicenseRestrictionException : Exception
{
    /// <summary>The feature that was attempted (one of the constants
    /// in <see cref="Feature"/>).</summary>
    public string FeatureKey { get; }

    /// <summary>The minimum edition that includes this feature —
    /// used by the upgrade prompt to show "you need SMB or higher"
    /// vs "you need Enterprise or higher".</summary>
    public LicenseEdition MinimumEdition { get; }

    /// <summary>The edition the install is currently running.</summary>
    public LicenseEdition CurrentEdition { get; }

    public string ArabicMessage { get; }
    public string EnglishMessage { get; }

    public LicenseRestrictionException(
        string featureKey,
        LicenseEdition currentEdition,
        LicenseEdition minimumEdition)
        : base($"Feature '{featureKey}' requires edition {minimumEdition} (current: {currentEdition}).")
    {
        FeatureKey = featureKey;
        CurrentEdition = currentEdition;
        MinimumEdition = minimumEdition;

        ArabicMessage =
            $"هذه الميزة متاحة في خطة {minimumEdition.ArabicLabel()} فأعلى. " +
            $"خطتك الحالية: {currentEdition.ArabicLabel()}.";
        EnglishMessage =
            $"This feature requires the {minimumEdition} edition or higher. " +
            $"You're currently on {currentEdition}.";
    }
}
