namespace EgyptTax.Infrastructure.Persistence.Mobile;

/// <summary>
/// T026 per specs/009-android-app/tasks.md + data-model.md §5.
///
/// Binds an FCM device token to a (user, device) pair so the
/// notification publishers can fan out push messages to the right
/// phones. Per FR-006 the app POSTs to /api/v1/notifications/register
/// on sign-in (creates or refreshes a row) and DELETEs on sign-out
/// (soft-delete: <see cref="RevokedAtUtc"/> populated, row kept for
/// audit per the existing FR-028 audit pattern).
///
/// Plain POCO with init-only properties — no domain invariants here
/// since this is a transport/infrastructure entity, not a business
/// aggregate. The endpoint handler in
/// <c>src/EgyptTax.Web/Pages/Api/Mobile/NotificationRegisterEndpoint.cs</c>
/// (T071) owns the upsert logic.
/// </summary>
public sealed class MobilePushRegistration
{
    /// <summary>Surrogate primary key.</summary>
    public Guid Id { get; init; }

    /// <summary>FK to the existing <c>users.id</c>.</summary>
    public Guid UserId { get; init; }

    /// <summary>Stable UUID the app generates on first launch and
    /// persists in EncryptedSharedPreferences. Survives FCM-token
    /// rotation; the (UserId, DeviceId) pair is the natural key.</summary>
    public string DeviceId { get; init; } = "";

    /// <summary>Current FCM device token. NEVER logged to audit_log or
    /// Crashlytics; treated as a credential per FR-017.</summary>
    public string FcmToken { get; set; } = "";

    /// <summary><c>"android"</c> for now; <c>"ios"</c> reserved.</summary>
    public string Platform { get; init; } = "android";

    /// <summary>The mobile app's <c>versionName</c> at last registration
    /// (e.g. <c>"1.0.0"</c>). Lets the vendor spot customers stuck on
    /// an outdated client.</summary>
    public string AppVersion { get; set; } = "";

    /// <summary>First-time registration timestamp.</summary>
    public DateTime CreatedAtUtc { get; init; }

    /// <summary>Updated on every <c>/register</c> POST.</summary>
    public DateTime LastSeenAtUtc { get; set; }

    /// <summary>Soft-delete marker. <c>null</c> while active; populated
    /// on <c>/register</c> DELETE so the audit-log row is preserved.</summary>
    public DateTime? RevokedAtUtc { get; set; }
}
