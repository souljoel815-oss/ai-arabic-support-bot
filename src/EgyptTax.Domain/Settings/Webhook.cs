namespace EgyptTax.Domain.Settings;

/// <summary>
/// v4 B.3 — outbound webhook subscription. The operator registers
/// a URL the system POSTs to whenever an event in
/// <see cref="EventMask"/> fires (currently <c>invoice.posted</c>
/// + <c>payment.received</c>).
///
/// The <see cref="Secret"/> is HMAC-signed with each payload + sent
/// as the <c>X-Daftarx-Signature: sha256=hex</c> header so the
/// receiver can verify the request actually came from this system
/// (and isn't replay or forgery).
///
/// Failure handling is best-effort: the dispatcher retries x3 with
/// exponential backoff in-process; persistent failures are logged
/// but never block the originating handler. We intentionally do
/// NOT queue undelivered events — webhooks are advisory; receivers
/// that want guaranteed delivery should use the GET endpoints to
/// reconcile state on a schedule.
/// </summary>
public sealed class Webhook
{
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Operator-given label, e.g. "Slack #orders channel"
    /// or "Zapier post-invoice flow".</summary>
    public string Name { get; private set; } = "";

    /// <summary>Target URL the dispatcher POSTs to. Required HTTPS
    /// for production data; HTTP allowed for localhost dev.</summary>
    public string Url { get; private set; } = "";

    /// <summary>Shared secret used to HMAC-sign each payload.
    /// Generated at create-time; the operator can roll via
    /// <see cref="RollSecret"/>.</summary>
    public string Secret { get; private set; } = "";

    /// <summary>Comma-separated list of subscribed event names.
    /// Empty = subscribed to all events. Stored as a string so
    /// adding a new event doesn't require a migration.</summary>
    public string EventMask { get; private set; } = "";

    public bool Enabled { get; private set; } = true;
    public DateTime CreatedAtUtc { get; init; }
    public Guid? CreatedByUserId { get; init; }
    public DateTime? LastDispatchAtUtc { get; private set; }
    public string? LastDispatchStatus { get; private set; }

    private Webhook() { }

    public Webhook(
        string name,
        string url,
        string secret,
        string eventMask,
        DateTime createdAtUtc,
        Guid? createdByUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed)
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                $"Webhook URL must be an absolute http(s) URL; got '{url}'.", nameof(url));
        }
        Name = name.Trim();
        Url = url.Trim();
        Secret = secret;
        EventMask = (eventMask ?? "").Trim();
        CreatedAtUtc = createdAtUtc;
        CreatedByUserId = createdByUserId;
    }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    public void UpdateEventMask(string mask) => EventMask = (mask ?? "").Trim();
    public void Enable() => Enabled = true;
    public void Disable() => Enabled = false;
    public void RollSecret(string newSecret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newSecret);
        Secret = newSecret;
    }

    public void RecordDispatch(DateTime nowUtc, string status)
    {
        LastDispatchAtUtc = nowUtc;
        LastDispatchStatus = status;
    }

    /// <summary>True iff this webhook should fire for
    /// <paramref name="eventName"/>. Empty mask = subscribed to
    /// everything; otherwise comma-separated explicit list.</summary>
    public bool MatchesEvent(string eventName)
    {
        if (string.IsNullOrWhiteSpace(EventMask)) return true;
        return EventMask.Split(',', StringSplitOptions.RemoveEmptyEntries
            | StringSplitOptions.TrimEntries)
            .Any(m => string.Equals(m, eventName, StringComparison.OrdinalIgnoreCase));
    }
}
