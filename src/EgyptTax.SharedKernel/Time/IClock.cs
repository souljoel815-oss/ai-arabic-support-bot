namespace EgyptTax.SharedKernel.Time;

/// <summary>
/// Abstraction over <see cref="DateTime.UtcNow"/> so that domain logic can be
/// tested deterministically. Per research.md R-23, the application treats
/// server local time as authoritative and verifies NTP skew separately via
/// the Hangfire NtpHealthCheckJob.
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
