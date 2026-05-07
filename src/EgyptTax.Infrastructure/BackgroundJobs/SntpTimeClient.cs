using System.Net;
using System.Net.Sockets;

namespace EgyptTax.Infrastructure.BackgroundJobs;

/// <summary>
/// Minimal SNTPv4 client (RFC 4330). Sends a single UDP packet to the
/// configured NTP server (default <c>pool.ntp.org:123</c>) and parses
/// the 64-bit transmit timestamp out of the 48-byte response. Uses a
/// short receive timeout so a misconfigured firewall surfaces as a
/// <c>SocketException</c> rather than a hanging job.
/// </summary>
public sealed class SntpTimeClient(string ntpServer = "pool.ntp.org", int port = 123) : INtpTimeClient
{
    private const int NtpPacketSize = 48;
    private const byte LiNoWarning_VnV4_ModeClient = 0x1B;

    /// <summary>NTP epoch is 1900-01-01 UTC; .NET DateTime epoch differs.</summary>
    private static readonly DateTime NtpEpoch = new(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly string _ntpServer = ntpServer;
    private readonly int _port = port;

    public async Task<DateTime> QueryUtcNowAsync(CancellationToken cancellationToken = default)
    {
        var addresses = await Dns.GetHostAddressesAsync(_ntpServer, cancellationToken);
        if (addresses.Length == 0)
        {
            throw new InvalidOperationException($"DNS lookup for '{_ntpServer}' returned no addresses.");
        }
        var endpoint = new IPEndPoint(addresses[0], _port);

        using var udp = new UdpClient(addresses[0].AddressFamily);
        udp.Client.ReceiveTimeout = (int)TimeSpan.FromSeconds(3).TotalMilliseconds;
        udp.Client.SendTimeout = (int)TimeSpan.FromSeconds(3).TotalMilliseconds;

        var request = new byte[NtpPacketSize];
        request[0] = LiNoWarning_VnV4_ModeClient;
        await udp.SendAsync(request, NtpPacketSize, endpoint).WaitAsync(cancellationToken);

        var receive = await udp.ReceiveAsync(cancellationToken);
        if (receive.Buffer.Length < NtpPacketSize)
        {
            throw new InvalidOperationException($"NTP response too short: {receive.Buffer.Length} bytes.");
        }

        // Transmit timestamp lives at offset 40, big-endian 64-bit fixed-point.
        var integer = (uint)((receive.Buffer[40] << 24) | (receive.Buffer[41] << 16) | (receive.Buffer[42] << 8) | receive.Buffer[43]);
        var fraction = (uint)((receive.Buffer[44] << 24) | (receive.Buffer[45] << 16) | (receive.Buffer[46] << 8) | receive.Buffer[47]);
        var milliseconds = (long)(integer * 1000L) + (fraction * 1000L / 0x100000000L);
        return NtpEpoch.AddMilliseconds(milliseconds);
    }
}
