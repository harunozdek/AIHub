using System.Net;

namespace AIHub.Domain.Monitoring;

/// <summary>
/// Denetimin bağlanamayacağı adresler (Spec 0005, R-3; ADR 0003): <c>169.254.0.0/16</c> ve IPv6
/// link-local <c>fe80::/10</c>. IPv4'ü IPv6 içine gömen biçimler de engel denetiminden geçer: IPv4-mapped
/// (<c>::ffff:169.254.x.x</c>), NAT64 (<c>64:ff9b::/96</c>) ve eski IPv4-compatible (<c>::169.254.x.x</c>)
/// (review 1, F-07). Kurum içi adresler ve loopback serbesttir.
/// </summary>
public static class HealthAddressPolicy
{
    private static readonly byte[] Nat64Prefix = [0x00, 0x64, 0xff, 0x9b, 0, 0, 0, 0, 0, 0, 0, 0];

    public static bool IsBlocked(IPAddress address)
    {
        if (EmbeddedIPv4(address) is { } embedded)
        {
            address = embedded;
        }

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] == 169 && bytes[1] == 254;
        }

        return address.IsIPv6LinkLocal;
    }

    private static IPAddress? EmbeddedIPv4(IPAddress address)
    {
        if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            return null;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            return address.MapToIPv4();
        }

        var bytes = address.GetAddressBytes();
        var nat64 = bytes.AsSpan(0, 12).SequenceEqual(Nat64Prefix);
        // IPv4-compatible: ilk 12 bayt sıfır; ::, ::1 gibi özel adresler son 4 bayttan ayırt edilir.
        var compatible = bytes.AsSpan(0, 12).IndexOfAnyExcept((byte)0) < 0 && (bytes[12] | bytes[13]) != 0;
        return nat64 || compatible ? new IPAddress(bytes.AsSpan(12, 4)) : null;
    }
}
