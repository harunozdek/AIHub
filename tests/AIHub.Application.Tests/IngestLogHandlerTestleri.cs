using System.Text.Json.Nodes;
using AIHub.Application.Ingestion;
using AIHub.Domain.Logs;

namespace AIHub.Application.Tests;

// Spec 0001: AC-3 (ServiceName anahtardan), AC-7 (kuyruğa giden içerik maskeli), AC-10 (kuyruk hatası).
public class IngestLogHandlerTestleri
{
    private static readonly DateTimeOffset Simdi = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    private static IngestLogCommand Komut(
        string message = "Tahsilat başarısız",
        string? stackTrace = null,
        JsonObject? metadata = null,
        string servis = "odeme-servisi") =>
        new(servis, LogLevel.Error, message, "System.InvalidOperationException", stackTrace, metadata, null);

    [Fact]
    public async Task Gecerli_gonderim_kuyruga_yazilir_ve_kayit_kimligi_doner()
    {
        var kuyruk = new FakeLogQueue();
        var handler = new IngestLogHandler(kuyruk, new FakeClock(Simdi));

        var sonuc = await handler.HandleAsync(Komut(), CancellationToken.None);

        Assert.True(sonuc.IsSuccess);
        var kayit = Assert.Single(kuyruk.Enqueued);
        Assert.Equal(kayit.Id, sonuc.Value);
        Assert.Equal(7, kayit.Id.Version);
        Assert.Equal(Simdi, kayit.ReceivedAt);
    }

    [Fact]
    public async Task ServiceName_govdeden_degil_kimligi_dogrulanmis_anahtardan_gelir()
    {
        // Komutta gövdeden gelen bir ServiceName alanı hiç yoktur; tek kaynak kimlik doğrulamasıdır.
        var adAlanlari = typeof(IngestLogCommand).GetProperties()
            .Where(p => p.Name.Contains("ServiceName", StringComparison.Ordinal))
            .Select(p => p.Name);
        Assert.Equal(["AuthenticatedServiceName"], adAlanlari);

        var kuyruk = new FakeLogQueue();
        await new IngestLogHandler(kuyruk, new FakeClock(Simdi))
            .HandleAsync(Komut(servis: "odeme-servisi"), CancellationToken.None);

        Assert.Equal("odeme-servisi", Assert.Single(kuyruk.Enqueued).ServiceName);
    }

    [Fact]
    public async Task Kuyruga_yazilan_icerik_maskelenmistir_ham_hassas_deger_kuyruga_ulasmaz()
    {
        var kuyruk = new FakeLogQueue();
        var metadata = new JsonObject
        {
            ["musteri"] = new JsonObject { ["eposta"] = "ali@ornek.com", ["tckn"] = 10000000146 },
            ["etiketler"] = new JsonArray("normal", "Bearer abcDEF123"),
            ["api_key"] = "sk_live_123",
            ["siparisNo"] = 12345678901,
        };

        await new IngestLogHandler(kuyruk, new FakeClock(Simdi)).HandleAsync(
            Komut(message: "ali@ornek.com için ödeme", stackTrace: "at Db.Open(Host=db01;Password=Gizli1;)", metadata: metadata),
            CancellationToken.None);

        var kayit = Assert.Single(kuyruk.Enqueued);
        Assert.Equal("[MASKED:email] için ödeme", kayit.Message);
        Assert.Equal("at Db.Open(Host=db01;Password=[MASKED:password];)", kayit.StackTrace);
        var saklanan = JsonNode.Parse(kayit.Metadata!)!.AsObject();
        Assert.Equal("[MASKED:email]", saklanan["musteri"]!["eposta"]!.GetValue<string>());
        Assert.Equal("[MASKED:tckn]", saklanan["musteri"]!["tckn"]!.GetValue<string>());
        Assert.Equal("Bearer [MASKED:bearer-token]", saklanan["etiketler"]![1]!.GetValue<string>());
        Assert.Equal("[MASKED:api-key]", saklanan["api_key"]!.GetValue<string>());
        Assert.Equal(12345678901, saklanan["siparisNo"]!.GetValue<long>());
        foreach (var ham in new[] { "ali@ornek.com", "10000000146", "abcDEF123", "sk_live_123", "Gizli1" })
        {
            Assert.DoesNotContain(ham, kayit.Message + kayit.StackTrace + kayit.Metadata);
        }
    }

    [Fact]
    public async Task Sinirdaki_Message_maskeleme_onu_uzatsa_da_kabul_edilir()
    {
        var kuyruk = new FakeLogQueue();
        var ham = new string('m', LogEntryLimits.MessageMaxLength - 12) + " 10000000146";

        var sonuc = await new IngestLogHandler(kuyruk, new FakeClock(Simdi)).HandleAsync(Komut(message: ham), CancellationToken.None);

        Assert.True(sonuc.IsSuccess);
        var kayit = Assert.Single(kuyruk.Enqueued);
        Assert.EndsWith(" [MASKED:tckn]", kayit.Message);
        Assert.True(kayit.Message.Length > LogEntryLimits.MessageMaxLength);
    }

    [Fact]
    public async Task Metadata_anahtar_adlarindaki_hassas_veri_maskelenir_cakisan_anahtarlar_korunur()
    {
        var kuyruk = new FakeLogQueue();
        var metadata = new JsonObject
        {
            ["ali@ornek.com"] = new JsonObject { ["sepet"] = 3 },
            ["veli@ornek.com"] = new JsonObject { ["sepet"] = 5 },
            ["10000000146"] = true,
            ["db_password"] = "hunter2",
        };

        await new IngestLogHandler(kuyruk, new FakeClock(Simdi)).HandleAsync(Komut(metadata: metadata), CancellationToken.None);

        var saklanan = JsonNode.Parse(Assert.Single(kuyruk.Enqueued).Metadata!)!.AsObject();
        Assert.Equal(3, saklanan["[MASKED:email]"]!["sepet"]!.GetValue<int>());
        Assert.Equal(5, saklanan["[MASKED:email]#2"]!["sepet"]!.GetValue<int>());
        Assert.True(saklanan["[MASKED:tckn]"]!.GetValue<bool>());
        Assert.Equal("[MASKED:password]", saklanan["db_password"]!.GetValue<string>());
        var json = saklanan.ToJsonString();
        foreach (var ham in new[] { "ali@ornek.com", "veli@ornek.com", "10000000146", "hunter2" })
        {
            Assert.DoesNotContain(ham, json);
        }
    }

    [Fact]
    public async Task Cagiranin_metadata_nesnesi_degistirilmez()
    {
        var metadata = new JsonObject { ["eposta"] = "ali@ornek.com" };

        await new IngestLogHandler(new FakeLogQueue(), new FakeClock(Simdi))
            .HandleAsync(Komut(metadata: metadata), CancellationToken.None);

        Assert.Equal("ali@ornek.com", metadata["eposta"]!.GetValue<string>());
    }

    [Fact]
    public async Task Kuyruk_erisilemezse_QueueUnavailable_sonucu_doner()
    {
        var kuyruk = new FakeLogQueue { Unavailable = true };

        var sonuc = await new IngestLogHandler(kuyruk, new FakeClock(Simdi)).HandleAsync(Komut(), CancellationToken.None);

        Assert.False(sonuc.IsSuccess);
        Assert.Equal(IngestionErrors.QueueUnavailable, Assert.Single(sonuc.Errors));
    }

    [Fact]
    public async Task Gecersiz_gonderim_kuyruga_hic_yazilmaz()
    {
        var kuyruk = new FakeLogQueue();

        var sonuc = await new IngestLogHandler(kuyruk, new FakeClock(Simdi)).HandleAsync(Komut(message: " "), CancellationToken.None);

        Assert.False(sonuc.IsSuccess);
        Assert.Empty(kuyruk.Enqueued);
    }
}
