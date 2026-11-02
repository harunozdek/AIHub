import { createServer } from "node:http";

/**
 * e2e'nin sahte sağlık sunucusu (Spec 0005). Global setup ayrı bir süreç olarak başlatır; durdurma diğer
 * süreçlerle aynı yoldan yapılır (global-teardown). Testler davranışı HTTP ile ayarlar:
 * - `/__ayarla?yol=/x&kod=503&gecikme=3000`: o yolun durum kodu ve gecikmesi (ms);
 * - `/__sayi?yol=/x`: o yola gelen denetim sayısı.
 * Ayarlanmamış her yol hemen 200 döner.
 */
const port = Number(process.argv[2] ?? 0);
const ayarlar = new Map<string, { kod: number; gecikme: number }>();
const sayilar = new Map<string, number>();

const sunucu = createServer((istek, yanit) => {
  const url = new URL(istek.url ?? "/", "http://saglik.invalid");
  if (url.pathname === "/__ayarla") {
    const yol = url.searchParams.get("yol") ?? "/";
    ayarlar.set(yol, { kod: Number(url.searchParams.get("kod") ?? 200), gecikme: Number(url.searchParams.get("gecikme") ?? 0) });
    yanit.writeHead(204).end();
    return;
  }
  if (url.pathname === "/__sayi") {
    yanit.writeHead(200, { "Content-Type": "text/plain" }).end(String(sayilar.get(url.searchParams.get("yol") ?? "/") ?? 0));
    return;
  }

  sayilar.set(url.pathname, (sayilar.get(url.pathname) ?? 0) + 1);
  const ayar = ayarlar.get(url.pathname) ?? { kod: 200, gecikme: 0 };
  setTimeout(() => {
    if (!yanit.destroyed) yanit.writeHead(ayar.kod).end();
  }, ayar.gecikme);
});

sunucu.listen(port, "127.0.0.1", () => {
  const adres = sunucu.address();
  console.log(`saglik-hedefi hazir: ${typeof adres === "object" && adres ? adres.port : port}`);
});
