using System.Collections.Concurrent;

namespace EgyptTax.Infrastructure.Api;

/// <summary>
/// v4 B.3 — per-key rate limiter for the public REST API. In-memory
/// fixed-window counter (60 requests / minute by default; settable
/// via <see cref="WithLimit"/>). Single-instance portable: replace
/// with Redis only if we ever scale-out (the v4 spec explicitly
/// scopes against that).
///
/// Behaviour: each key has a current 60-second window with a
/// counter; once it reaches the limit, <see cref="TryConsume"/>
/// returns false + the seconds remaining in the window so the
/// caller can emit a <c>Retry-After</c> header.
///
/// Why a static counter (not RAII / token bucket): one counter
/// reset per minute is fine for the volumes we expect (a runaway
/// integration burns 60 calls then sleeps 60s, predictable to
/// the developer reading the 429s). Bucket smoothing buys us
/// nothing at this scale.
/// </summary>
public sealed class ApiKeyRateLimiter
{
    public const int DefaultLimitPerMinute = 60;

    private readonly TimeProvider _time;
    private readonly int _limit;
    private readonly ConcurrentDictionary<Guid, WindowState> _windows = new();

    public ApiKeyRateLimiter() : this(TimeProvider.System, DefaultLimitPerMinute) { }

    public ApiKeyRateLimiter(TimeProvider time, int limitPerMinute = DefaultLimitPerMinute)
    {
        ArgumentNullException.ThrowIfNull(time);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limitPerMinute);
        _time = time;
        _limit = limitPerMinute;
    }

    /// <summary>Limit applied per key per 60-second window.</summary>
    public int Limit => _limit;

    public sealed record Outcome(bool Allowed, int RetryAfterSeconds, int RemainingInWindow);

    /// <summary>Try to consume a request slot for the key. When
    /// returning false, <see cref="Outcome.RetryAfterSeconds"/>
    /// gives the seconds until the window rolls over.</summary>
    public Outcome TryConsume(Guid keyId)
    {
        var nowUtc = _time.GetUtcNow().UtcDateTime;
        while (true)
        {
            var current = _windows.GetOrAdd(keyId, _ => new WindowState(nowUtc, 0));
            // Roll the window if it's older than 60 seconds.
            if ((nowUtc - current.WindowStartUtc).TotalSeconds >= 60d)
            {
                var fresh = new WindowState(nowUtc, 1);
                if (_windows.TryUpdate(keyId, fresh, current))
                {
                    return new Outcome(true, 0, _limit - 1);
                }
                continue; // race — retry
            }

            if (current.Count >= _limit)
            {
                var elapsed = (nowUtc - current.WindowStartUtc).TotalSeconds;
                var remaining = (int)Math.Ceiling(60d - elapsed);
                return new Outcome(false, Math.Max(1, remaining), 0);
            }

            var bumped = current with { Count = current.Count + 1 };
            if (_windows.TryUpdate(keyId, bumped, current))
            {
                return new Outcome(true, 0, _limit - bumped.Count);
            }
            // CAS lost — retry.
        }
    }

    private sealed record WindowState(DateTime WindowStartUtc, int Count);
}
