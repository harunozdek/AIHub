using System.Net;

namespace AIHub.Domain.Monitoring;

/// <summary>
/// Denetimin bağlanamayacağı adresler (Spec 0005, R-3; ADR 0003): <c>169.254.0.0/16</c> ve IPv6
/// link-local <c>fe80::/10</c>. IPv4-mapped IPv6 biçimi (<c>::ffff:169.254.x.x</c>) de engellidir.
/// Kurum içi adresler ve loopback serbesttir.
/// </summary>
public static class HealthAddressPolicy
{
    public static bool IsBlocked(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] == 169 && bytes[1] == 254;
        }

        return address.IsIPv6LinkLocal;
    }
}
