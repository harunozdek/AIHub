import { spawn, type ChildProcess } from "node:child_process";
import { randomBytes } from "node:crypto";
import { appendFileSync, mkdtempSync } from "node:fs";
import net from "node:net";
import os from "node:os";
import path from "node:path";
import { GenericContainer, Wait } from "testcontainers";
import { running, stopAll } from "./global-teardown";
import { E2E_KULLANICI, E2E_SIFRE } from "./kimlik";

const webDir = path.resolve(__dirname, "..");
const apiDll = path.resolve(webDir, "../src/AIHub.Api/bin/Debug/net10.0/AIHub.Api.dll");

/**
 * PostgreSQL 17 ve Redis 7 container'larını, backend'i (derlenmiş dll, migration açık, ilk yönetici
 * ayarlı) ve `next start`'ı boş portlarda başlatır. Backend `dotnet run` yerine dll'den başlatılır: tek
 * süreç olur ve durdurulunca arkada alt süreç kalmaz. Herhangi bir adım başarısız olursa başlatılan her şey
 * durdurulur. Kimlik sağlayıcısı yoktur: giriş AIHub'ın kendi hesaplarıyla yapılır (ADR 0002).
 */
export default async function globalSetup() {
  const { processes, containers, dirs } = running();
  try {
    // Panel süreçlerinin çıktısı testlerin okuyabileceği dosyalara da yazılır (giriş hatası logları).
    const logDir = mkdtempSync(path.join(os.tmpdir(), "aihub-e2e-"));
    dirs.push(logDir);
    // allSettled: biri başarısız olsa da öteki başlatmalar beklenir ve başlayan her container sahiplik
    // listesine girer; böylece hata yolunda stopAll() hepsini durdurur (review 1, F-10).
    const sonuclar = await Promise.allSettled([
      new GenericContainer("postgres:17-alpine")
        .withEnvironment({ POSTGRES_USER: "aihub", POSTGRES_PASSWORD: "aihub-e2e", POSTGRES_DB: "aihub" })
        .withExposedPorts(5432)
        .withWaitStrategy(Wait.forLogMessage(/database system is ready to accept connections/, 2))
        .start(),
      new GenericContainer("redis:7-alpine").withExposedPorts(6379).withWaitStrategy(Wait.forLogMessage(/Ready to accept connections/)).start(),
    ]);
    for (const sonuc of sonuclar) {
      if (sonuc.status === "fulfilled") containers.push(sonuc.value);
    }
    const hata = sonuclar.find((s) => s.status === "rejected");
    if (hata) throw hata.reason;
    const [postgres, redis] = containers;

    const apiUrl = `http://localhost:${await freePort()}`;
    const webPort = await freePort();
    const appUrl = `http://localhost:${webPort}`;
    const sessionSecret = randomBytes(32).toString("base64url");

    // Sahte sağlık sunucusu (Spec 0005): ayrı süreç, diğer süreçlerle birlikte durdurulur.
    const saglikPort = await freePort();
    const saglik = spawn(process.execPath, ["--disable-warning=MODULE_TYPELESS_PACKAGE_JSON", path.join(webDir, "e2e/saglik-hedefi.ts"), String(saglikPort)], {
      cwd: webDir,
      stdio: ["ignore", "pipe", "pipe"],
    });
    processes.push(saglik);
    const saglikUrl = `http://127.0.0.1:${saglikPort}`;
    await waitFor(`${saglikUrl}/__sayi`, 200, saglik, collect(saglik), "sahte sağlık sunucusu");

    const api = spawn("dotnet", [apiDll], {
      cwd: path.dirname(apiDll),
      env: {
        ...process.env,
        ASPNETCORE_ENVIRONMENT: "Development",
        ASPNETCORE_URLS: apiUrl,
        ConnectionStrings__Postgres: `Host=localhost;Port=${postgres.getMappedPort(5432)};Username=aihub;Password=aihub-e2e;Database=aihub`,
        ConnectionStrings__Redis: `localhost:${redis.getMappedPort(6379)}`,
        Database__MigrateOnStartup: "true",
        Auth__TokenSigningKey: randomBytes(32).toString("base64url"),
        Auth__BootstrapAdmin__UserName: E2E_KULLANICI,
        Auth__BootstrapAdmin__DisplayName: "E2E Kullanıcısı",
        Auth__BootstrapAdmin__Password: E2E_SIFRE,
        // Hız için düşük tur; üretim değeri Api testinde doğrulanır (plan 0004, risk 6).
        Auth__PasswordHash__Iterations: "1000",
        // Sağlık denetimi kısa aralıkla: 3 denetimde Down, yaklaşık 3 sn. Spec değerleri (30 sn, 5 sn)
        // Domain ve Api testlerinde doğrulanır (plan 0005, risk 1).
        Monitoring__TickInterval: "00:00:00.200",
        Monitoring__CheckInterval: "00:00:01",
        Monitoring__Timeout: "00:00:01",
        // Kendi kaydı (Spec 0008) e2e'de açılışta eklenmez: diğer testler listeyi boş varsayar. Kendi kayıtları
        // saglik.spec.ts'te gerçek adreslerle eklenir (plan 0008, amendment).
        SelfMonitoring__ApiHealthUrl: "",
        SelfMonitoring__PanelHealthUrl: "",
      },
      stdio: ["ignore", "pipe", "pipe"],
    });
    processes.push(api);
    const apiLog = collect(api);
    await waitFor(`${apiUrl}/api/panel/services`, 401, api, apiLog, "backend");

    const nextBin = path.join(webDir, "node_modules/next/dist/bin/next");
    const web = spawn(process.execPath, [nextBin, "start", "-p", String(webPort), "-H", "localhost"], {
      cwd: webDir,
      env: { ...process.env, API_BASE_URL: apiUrl, SESSION_SECRET: sessionSecret },
      stdio: ["ignore", "pipe", "pipe"],
    });
    processes.push(web);
    const webLogFile = path.join(logDir, "panel.log");
    const webLog = collect(web, webLogFile);
    await waitFor(`${appUrl}/giris`, 200, web, webLog, "panel");

    // Ayarları eksik panel: API_BASE_URL boş. Silinmez, boş dize verilir: Next tanımsız değişkenleri
    // web/.env.local'dan doldurur, tanımlı boş değeri ezmez (0003 review 1, R1-01).
    const ayarsizUrl = `http://localhost:${await freePort()}`;
    const ayarsiz = spawn(process.execPath, [nextBin, "start", "-p", new URL(ayarsizUrl).port, "-H", "localhost"], {
      cwd: webDir,
      env: { ...process.env, API_BASE_URL: "", SESSION_SECRET: sessionSecret },
      stdio: ["ignore", "pipe", "pipe"],
    });
    processes.push(ayarsiz);
    const ayarsizLogFile = path.join(logDir, "ayarsiz.log");
    const ayarsizLog = collect(ayarsiz, ayarsizLogFile);
    await waitFor(`${ayarsizUrl}/giris`, 200, ayarsiz, ayarsizLog, "ayarsız panel");

    // Testler (worker süreçleri) bu değerleri ortamdan okur.
    process.env.E2E_WEB_LOG = webLogFile;
    process.env.E2E_AYARSIZ_URL = ayarsizUrl;
    process.env.E2E_AYARSIZ_LOG = ayarsizLogFile;
    process.env.E2E_APP_URL = appUrl;
    process.env.E2E_PG_CONTAINER = postgres.getId();
    // Spec 0008: backend'in kendi sağlık adresi ve AC-9 için duraklatılan kuyruk.
    process.env.E2E_API_URL = apiUrl;
    process.env.E2E_REDIS_CONTAINER = redis.getId();
    process.env.E2E_PG_URL = `postgres://aihub:aihub-e2e@localhost:${postgres.getMappedPort(5432)}/aihub`;
    process.env.E2E_SESSION_SECRET = sessionSecret;
    process.env.E2E_SAGLIK_URL = saglikUrl;
  } catch (error) {
    await stopAll();
    throw error;
  }
}

function freePort(): Promise<number> {
  return new Promise((resolve, reject) => {
    const server = net.createServer();
    server.once("error", reject);
    server.listen(0, "localhost", () => {
      const { port } = server.address() as net.AddressInfo;
      server.close(() => resolve(port));
    });
  });
}

function collect(child: ChildProcess, file?: string): string[] {
  const lines: string[] = [];
  const push = (chunk: Buffer) => {
    lines.push(chunk.toString());
    if (lines.length > 200) lines.shift();
    if (file) {
      try {
        appendFileSync(file, chunk);
      } catch {
        // Teardown klasörü sildikten sonra gelen son parça: Playwright ana sürecini düşürmesin (R1-05).
      }
    }
  };
  child.stdout?.on("data", push);
  child.stderr?.on("data", push);
  return lines;
}

async function waitFor(url: string, status: number, child: ChildProcess, log: string[], name: string) {
  const deadline = Date.now() + 90_000;
  while (Date.now() < deadline) {
    if (child.exitCode !== null) {
      throw new Error(`${name} başlarken kapandı (çıkış kodu ${child.exitCode}):\n${log.join("")}`);
    }
    try {
      const response = await fetch(url, { redirect: "manual" });
      if (response.status === status) return;
    } catch {
      // henüz dinlemiyor
    }
    await new Promise((resolve) => setTimeout(resolve, 250));
  }
  throw new Error(`${name} 90 sn içinde hazır olmadı (${url}):\n${log.join("")}`);
}
