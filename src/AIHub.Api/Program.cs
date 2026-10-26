// AIHub.Api — uygulama girişi.
// FD-4 (docs/architecture.md): bu katman iş mantığı içermez; yalnızca HTTP eşleme, istek
// doğrulama ve DI kablolaması yapar. Her karar Application katmanında kalır.

var olusturucu = WebApplication.CreateBuilder(args);

// Canlılık kontrolü (spec 0001, AC-7). Bilinçli olarak hiçbir bağımlılık kontrolü KAYDEDİLMEZ:
// bu uç nokta "süreç ayakta ve DI kablolaması çalışıyor" sorusunu cevaplar, PostgreSQL/Redis
// hazır mı sorusunu değil — readiness kontrolü spec 0003'ün işi.
olusturucu.Services.AddHealthChecks();

var uygulama = olusturucu.Build();

// Kimlik doğrulaması gerektirmez (spec 0001, AC-7). Yanıt gövdesi sabittir: ortam, sürüm veya
// bağımlılık durumu bilgisi sızdırmaz (docs/security.md, Input & output).
uygulama.MapHealthChecks("/health");

uygulama.Run();

// WebApplicationFactory<Program> bu tipe erişebilsin diye: üst düzey deyimlerin ürettiği Program
// sınıfı varsayılan olarak internal'dır (tests/AIHub.Api.Tests).
public partial class Program;
