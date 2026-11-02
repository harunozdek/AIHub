using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AIHub.Api.Tests.Altyapi;

/// <summary>
/// Testlerin yönettiği sahte sağlık sunucusu (Spec 0005). Her yol için durum kodu ve gecikme ayarlanır,
/// gelen istekler yol başına sayılır. <c>/yonlendir</c> 302 ile <c>/hedef</c>'e yönlendirir;
/// <c>/buyuk-govde</c> başlıkları hemen gönderir, gövdeyi yavaş ve sonsuz akıtır. Ayrıca kendinden
/// imzalı sertifikalı bir https ucu vardır.
/// </summary>
public sealed class SaglikHedefi : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly ConcurrentDictionary<string, int> _durumlar = new();
    private readonly ConcurrentDictionary<string, TimeSpan> _gecikmeler = new();
    private readonly ConcurrentDictionary<string, int> _sayilar = new();

    private SaglikHedefi()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(k =>
        {
            k.Listen(IPAddress.Loopback, 0);
            k.Listen(IPAddress.Loopback, 0, o => o.UseHttps(KendindenImzaliSertifika()));
        });
        _app = builder.Build();
        _app.Run(YanitlaAsync);
    }

    public string HttpTaban { get; private set; } = "";

    public string HttpsTaban { get; private set; } = "";

    public static async Task<SaglikHedefi> BaslatAsync()
    {
        var hedef = new SaglikHedefi();
        await hedef._app.StartAsync();
        var adresler = hedef._app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses;
        hedef.HttpTaban = adresler.Single(a => a.StartsWith("http://", StringComparison.Ordinal));
        hedef.HttpsTaban = adresler.Single(a => a.StartsWith("https://", StringComparison.Ordinal));
        return hedef;
    }

    public string Http(string yol) => HttpTaban + yol;

    public void DurumAyarla(string yol, int durum) => _durumlar[yol] = durum;

    public void GecikmeAyarla(string yol, TimeSpan gecikme) => _gecikmeler[yol] = gecikme;

    public int Sayi(string yol) => _sayilar.TryGetValue(yol, out var sayi) ? sayi : 0;

    public ValueTask DisposeAsync() => _app.DisposeAsync();

    private async Task YanitlaAsync(HttpContext context)
    {
        var yol = context.Request.Path.Value ?? "/";
        _sayilar.AddOrUpdate(yol, 1, (_, n) => n + 1);
        if (_gecikmeler.TryGetValue(yol, out var gecikme))
        {
            await Task.Delay(gecikme, context.RequestAborted);
        }

        if (yol == "/yonlendir")
        {
            context.Response.Redirect("/hedef");
            return;
        }

        if (yol == "/buyuk-govde")
        {
            context.Response.StatusCode = 200;
            await context.Response.Body.FlushAsync(context.RequestAborted);
            var parca = new byte[64 * 1024];
            while (!context.RequestAborted.IsCancellationRequested)
            {
                await context.Response.Body.WriteAsync(parca, context.RequestAborted);
                await Task.Delay(100, context.RequestAborted);
            }

            return;
        }

        context.Response.StatusCode = _durumlar.TryGetValue(yol, out var durum) ? durum : 200;
    }

    private static X509Certificate2 KendindenImzaliSertifika()
    {
        using var anahtar = RSA.Create(2048);
        var istek = new CertificateRequest("CN=localhost", anahtar, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var sertifika = istek.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        return X509CertificateLoader.LoadPkcs12(sertifika.Export(X509ContentType.Pfx), null);
    }
}
