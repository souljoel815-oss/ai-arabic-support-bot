namespace EgyptTax.Web.Licensing;

/// <summary>
/// Gux.13 — server-side edition gating. Every handler / page that
/// implements an edition-restricted feature calls
/// <see cref="Require"/> with the matching <see cref="Feature"/>
/// constant. The gate throws <see cref="LicenseRestrictionException"/>
/// when the current install's edition doesn't include the feature.
///
/// Reads from <see cref="LicenseStatus"/> on every check (no caching
/// inside the gate) so that an in-session license upgrade
/// immediately unlocks features without an app restart.
///
/// Trial mode unlocks all Enterprise features (NOT Firm Portal) so
/// prospects can experience the full Enterprise experience before
/// they decide which edition to buy.
///
/// Distinct from the boot-time <see cref="LicenseGate"/> which
/// validates the license envelope itself (signature, expiry, HWID
/// match). EditionGate runs after activation succeeds.
/// </summary>
public static class EditionGate
{
    /// <summary>
    /// Throw if the current install's edition doesn't include
    /// <paramref name="featureKey"/>. Returns silently when allowed.
    /// </summary>
    /// <param name="featureKey">One of the constants in <see cref="Feature"/>.</param>
    public static void Require(string featureKey)
    {
        if (Allows(featureKey)) return;

        var currentEdition = CurrentEdition();
        var minimumEdition = Feature.MinimumEditionFor(featureKey);
        throw new LicenseRestrictionException(featureKey, currentEdition, minimumEdition);
    }

    /// <summary>
    /// Boolean form for UI hide/disable decisions. Identical logic
    /// to <see cref="Require"/> minus the throw — Razor pages use
    /// this to gray out buttons before the user clicks.
    /// </summary>
    public static bool Allows(string featureKey)
    {
        // Unlicensed installs have NO features — they can only view
        // existing data + hit the License tab to activate. Trial
        // mode is treated as licensed.
        if (!LicenseStatus.IsLicensed) return false;

        // The current payload's Features[] takes precedence when
        // present. When the array is empty/null (pre-Gux.13 token),
        // fall back to the edition's default feature set.
        var features = ActiveFeatures();
        return features.Contains(featureKey, StringComparer.Ordinal);
    }

    /// <summary>The edition this install is currently running.
    /// Trial state always returns <see cref="LicenseEdition.Trial"/>;
    /// active state returns whatever the payload encoded.</summary>
    public static LicenseEdition CurrentEdition()
    {
        if (LicenseStatus.State == LicenseState.Trial) return LicenseEdition.Trial;
        return LicenseEditionExtensions.ParseOrSolo(LicenseStatus.EditionWireString);
    }

    /// <summary>The features currently active on this install.
    /// Reads <c>LicenseStatus.PayloadFeatures</c> when present;
    /// falls back to <c>Feature.DefaultsFor(CurrentEdition())</c>
    /// when the payload omitted the array (pre-Gux.13 token or
    /// trial mode without explicit feature list).</summary>
    public static IReadOnlyList<string> ActiveFeatures()
    {
        var explicitFeatures = LicenseStatus.PayloadFeatures;
        if (explicitFeatures is { Count: > 0 }) return explicitFeatures;
        return Feature.DefaultsFor(CurrentEdition());
    }

    /// <summary>How many users this install is allowed to have. UI
    /// uses this to disable the "Add User" button on Solo and to
    /// display "3 / 5 users" style indicators.</summary>
    public static int MaxUsers()
    {
        var explicitMax = LicenseStatus.PayloadMaxUsers;
        if (explicitMax is > 0) return explicitMax.Value;
        return CurrentEdition().DefaultMaxUsers();
    }

    /// <summary>How many companies this install is allowed to
    /// register. Used by the Firm Portal company switcher.</summary>
    public static int MaxCompanies()
    {
        var explicitMax = LicenseStatus.PayloadMaxCompanies;
        if (explicitMax is > 0) return explicitMax.Value;
        return CurrentEdition().DefaultMaxCompanies();
    }
}
