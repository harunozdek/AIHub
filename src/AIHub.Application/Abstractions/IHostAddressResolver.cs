using System.Net;

namespace AIHub.Application.Abstractions;

/// <summary>Kayıt anında adresin çözüldüğü IP'ler. Ad çözülemiyorsa boş liste döner (plan 0005, risk 4).</summary>
public interface IHostAddressResolver
{
    Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken);
}
