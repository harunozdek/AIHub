using System.Net;
using System.Net.Sockets;
using AIHub.Application.Abstractions;

namespace AIHub.Infrastructure.Monitoring;

/// <summary>Kayıt anındaki DNS çözümü. Ad çözülemiyorsa boş liste: kayıt kabul edilir (plan 0005, risk 4).</summary>
internal sealed class DnsHostAddressResolver : IHostAddressResolver
{
    public async Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        try
        {
            return await Dns.GetHostAddressesAsync(host, cancellationToken);
        }
        catch (SocketException)
        {
            return [];
        }
    }
}
