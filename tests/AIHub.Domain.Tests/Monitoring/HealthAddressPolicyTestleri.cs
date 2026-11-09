using System.Net;
using AIHub.Domain.Monitoring;

namespace AIHub.Domain.Tests.Monitoring;

// Spec 0005 — R-3, AC-3, AC-5; ADR 0003: denetimin bağlanamayacağı adresler.
public class HealthAddressPolicyTestleri
{
    [Theory]
    [InlineData("169.254.169.254")]
    [InlineData("169.254.0.0")]
    [InlineData("169.254.255.255")]
    [InlineData("fe80::1")]
    [InlineData("febf:ffff::1")]
    [InlineData("::ffff:169.254.169.254")]
    // Review 1, F-07: NAT64 ve eski IPv4-compatible biçimler.
    [InlineData("64:ff9b::a9fe:a9fe")]
    [InlineData("64:ff9b::169.254.0.1")]
    [InlineData("::169.254.169.254")]
    public void Link_local_ve_metadata_adresleri_engellidir(string ip)
    {
        Assert.True(HealthAddressPolicy.IsBlocked(IPAddress.Parse(ip)));
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.5")]
    [InlineData("192.168.1.10")]
    [InlineData("172.16.0.1")]
    [InlineData("169.253.255.255")]
    [InlineData("169.255.0.1")]
    [InlineData("::1")]
    [InlineData("fec0::1")]
    [InlineData("::ffff:10.0.0.5")]
    [InlineData("64:ff9b::a00:5")]
    [InlineData("::10.0.0.5")]
    [InlineData("::")]
    public void Kurum_ici_ve_loopback_adresleri_serbesttir(string ip)
    {
        Assert.False(HealthAddressPolicy.IsBlocked(IPAddress.Parse(ip)));
    }
}
