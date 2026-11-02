using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using AIHub.Application.Abstractions;
using AIHub.Domain.Monitoring;
using Microsoft.Extensions.Options;

namespace AIHub.Infrastructure.Monitoring;

/// <summary>
/// Sağlık adresini bir kez dener (Spec 0005, R-4, R-6; ADR 0003):
/// - Ad bağlantı anında çözülür. Çözülen adreslerden biri engelliyse bağlanılmaz. Bağlantı doğrulanan
///   IP'ye kurulur, böylece DNS sonradan değişse de engel aşılamaz.
/// - Yönlendirme izlenmez, proxy kullanılmaz, çerez tutulmaz.
/// - Yalnızca yanıt başlıkları beklenir; gövde okunmadan yanıt kapatılır.
/// Beklenen her hata bir <see cref="ProbeResult"/> olarak döner; exception yalnızca iptal içindir.
/// </summary>
internal sealed class HttpHealthProbe : IHealthProbe, IDisposable
{
    private readonly HttpClient _client;
    private readonly TimeSpan _timeout;

    public HttpHealthProbe(IOptions<MonitoringOptions> options)
    {
        _timeout = options.Value.Timeout;
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            UseCookies = false,
            ConnectTimeout = _timeout,
            ConnectCallback = ConnectAsync,
            // Uzun ömürlü bağlantı eski bir DNS çözümünü taşımasın.
            PooledConnectionLifetime = TimeSpan.FromMinutes(1),
        };
        _client = new HttpClient(handler) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("AIHub-HealthCheck/1.0");
    }

    public async Task<ProbeResult> ProbeAsync(Uri url, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            var code = (int)response.StatusCode;
            return code switch
            {
                >= 200 and < 300 => ProbeResult.Success,
                >= 300 and < 400 => ProbeResult.Failure(HealthFailureReason.Redirect),
                _ => ProbeResult.Failure(HealthFailureReason.HttpStatus, code),
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ProbeResult.Failure(HealthFailureReason.Timeout);
        }
        catch (HttpRequestException ex) when (Find<BlockedAddressException>(ex) is not null)
        {
            return ProbeResult.Failure(HealthFailureReason.BlockedAddress);
        }
        catch (HttpRequestException ex) when (Find<AuthenticationException>(ex) is not null || ex.HttpRequestError == HttpRequestError.SecureConnectionError)
        {
            return ProbeResult.Failure(HealthFailureReason.CertificateError);
        }
        catch (HttpRequestException ex) when (Find<TimeoutException>(ex) is not null)
        {
            return ProbeResult.Failure(HealthFailureReason.Timeout);
        }
        catch (HttpRequestException)
        {
            return ProbeResult.Failure(HealthFailureReason.ConnectionFailed);
        }
    }

    public void Dispose() => _client.Dispose();

    private static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var host = context.DnsEndPoint.Host.Trim('[', ']');
        var addresses = IPAddress.TryParse(host, out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(host, cancellationToken);
        if (addresses.Length == 0)
        {
            throw new SocketException((int)SocketError.HostNotFound);
        }

        if (addresses.Any(HealthAddressPolicy.IsBlocked))
        {
            throw new BlockedAddressException();
        }

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private static T? Find<T>(Exception ex)
        where T : Exception
    {
        for (Exception? current = ex; current is not null; current = current.InnerException)
        {
            if (current is T match)
            {
                return match;
            }
        }

        return null;
    }

    private sealed class BlockedAddressException() : Exception("Adres engelli bir IP'ye çözülüyor.");
}
