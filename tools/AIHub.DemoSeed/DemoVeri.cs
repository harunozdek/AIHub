using System.Text.Json.Nodes;
using AIHub.Application.Ingestion;
using AIHub.Domain.Logs;

namespace AIHub.DemoSeed;

/// <summary>
/// Örnek kayıtların dağılımı (Spec 0006, R-10). Sabit tohumlu rastgelelik: her koşu aynı kurala göre, o anki
/// zamana göre son 24 saate yayılmış kayıtlar üretir. İlk dört kayıt her dereceyi en az bir kez içerir.
/// </summary>
public static class DemoVeri
{
    public const int RecordCount = 480;

    public static readonly IReadOnlyList<string> Services =
        ["demo-odeme", "demo-stok", "demo-siparis", "demo-kargo", "demo-kimlik", "demo-bildirim"];

    private static readonly string[] ExceptionTypes =
    [
        "System.TimeoutException",
        "System.Net.Http.HttpRequestException",
        "Npgsql.NpgsqlException",
        "System.InvalidOperationException",
        "System.NullReferenceException",
        "System.UnauthorizedAccessException",
        "System.Text.Json.JsonException",
    ];

    private static readonly Dictionary<LogLevel, string[]> Messages = new()
    {
        [LogLevel.Info] = ["Sipariş oluşturuldu", "Ödeme onaylandı", "Stok güncellendi", "Kargo etiketi basıldı", "Kullanıcı oturum açtı"],
        [LogLevel.Warning] = ["Yanıt süresi eşiği aştı", "Yeniden deneme yapılıyor", "Önbellek ıskaladı", "Kuyruk derinliği artıyor"],
        [LogLevel.Error] = ["Ödeme sağlayıcısı yanıt vermedi", "Veritabanı bağlantısı kurulamadı: Host=db;password=Demo123!", "Stok rezervasyonu başarısız"],
        [LogLevel.Critical] = ["Servis başlatılamadı", "Kimlik doğrulama sağlayıcısına ulaşılamıyor: Bearer eyJhbGciOi.demo.token"],
    };

    public static IEnumerable<(DateTimeOffset ReceivedAt, IngestLogCommand Command)> Create(DateTimeOffset now)
    {
        var random = new Random(2026);
        var step = TimeSpan.FromHours(23.5) / RecordCount;

        for (var i = 0; i < RecordCount; i++)
        {
            var level = i < 4 ? (LogLevel)(3 - i) : PickLevel(random);
            var service = Services[random.Next(Services.Count)];
            var receivedAt = now - step * i - TimeSpan.FromMilliseconds(random.Next(1, 1000));

            string? exceptionType = null;
            string? stackTrace = null;
            if (level is LogLevel.Error or LogLevel.Critical || (level == LogLevel.Warning && random.Next(5) == 0))
            {
                // İki çekilişin küçüğü: ilk türler daha sık görülür, "sık exception'lar" sırası anlamlı olur.
                exceptionType = ExceptionTypes[Math.Min(random.Next(ExceptionTypes.Length), random.Next(ExceptionTypes.Length))];
                stackTrace = $"{exceptionType}: örnek hata\n   at {ToNamespace(service)}.Islem.CalistirAsync()\n   at {ToNamespace(service)}.Program.Main()";
            }

            var messages = Messages[level];
            var metadata = new JsonObject { ["ortam"] = "demo", ["istekId"] = Guid.NewGuid().ToString("N") };
            yield return (receivedAt, new IngestLogCommand(
                service, level, messages[random.Next(messages.Length)], exceptionType, stackTrace, metadata, receivedAt.AddMilliseconds(-50)));
        }
    }

    private static LogLevel PickLevel(Random random) => random.Next(100) switch
    {
        < 55 => LogLevel.Info,
        < 80 => LogLevel.Warning,
        < 95 => LogLevel.Error,
        _ => LogLevel.Critical,
    };

    private static string ToNamespace(string service) =>
        "Demo." + string.Concat(service["demo-".Length..].Select((c, i) => i == 0 ? char.ToUpperInvariant(c) : c));
}
