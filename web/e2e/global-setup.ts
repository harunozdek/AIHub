import { spawn, type ChildProcess } from "node:child_process";
import { randomBytes } from "node:crypto";
import net from "node:net";
import path from "node:path";
import { GenericContainer, Wait } from "testcontainers";
import { running, stopAll } from "./global-teardown";

const webDir = path.resolve(__dirname, "..");
const apiDll = path.resolve(webDir, "../src/AIHub.Api/bin/Debug/net10.0/AIHub.Api.dll");

// Kimlik sağlayıcı: etkileşimli giriş formu (çıkıştan sonra kendiliğinden yeniden giriş olmaz).
// Token'lar hem panelin client id'sini (OIDC doğrulaması) hem backend'in audience'ını taşır.
const mockOidcConfig = JSON.stringify({
  interactiveLogin: true,
  tokenCallbacks: [
    {
      issuerId: "aihub",
      tokenExpiry: 3600,
      requestMappings: [
        { requestParam: "grant_type", match: "*", claims: { aud: ["aihub-panel", "aihub-api"], azp: "aihub-panel", name: "E2E Kullanıcısı" } },
      ],
    },
  ],
});

/**
 * PostgreSQL 17, Redis 7 ve mock OIDC container'larını, backend'i (derlenmiş dll, migration açık) ve
 * `next start`'ı boş portlarda başlatır. Backend `dotnet run` yerine dll'den başlatılır: tek süreç olur
 * ve durdurulunca arkada alt süreç kalmaz. Herhangi bir adım başarısız olursa başlatılan her şey durdurulur.
 */
export default async function globalSetup() {
  const { processes, containers } = running();
  try {
    const [postgres, redis, oidc] = await Promise.all([
      new GenericContainer("postgres:17-alpine")
        .withEnvironment({ POSTGRES_USER: "aihub", POSTGRES_PASSWORD: "aihub-e2e", POSTGRES_DB: "aihub" })
        .withExposedPorts(5432)
        .withWaitStrategy(Wait.forLogMessage(/database system is ready to accept connections/, 2))
        .start(),
      new GenericContainer("redis:7-alpine").withExposedPorts(6379).withWaitStrategy(Wait.forLogMessage(/Ready to accept connections/)).start(),
      new GenericContainer("ghcr.io/navikt/mock-oauth2-server:6.1.2")
        .withEnvironment({ JSON_CONFIG: mockOidcConfig })
        .withExposedPorts(8080)
        .withWaitStrategy(Wait.forHttp("/aihub/.well-known/openid-configuration", 8080))
        .start(),
    ]);
    containers.push(postgres, redis, oidc);

    // Issuer, token'ı isteyen adresten türer: tarayıcı, panel ve backend aynı "localhost" adını kullanır.
    const issuer = `http://localhost:${oidc.getMappedPort(8080)}/aihub`;
    const apiUrl = `http://localhost:${await freePort()}`;
    const webPort = await freePort();
    const appUrl = `http://localhost:${webPort}`;
    const sessionSecret = randomBytes(32).toString("base64url");

    const api = spawn("dotnet", [apiDll], {
      cwd: path.dirname(apiDll),
      env: {
        ...process.env,
        ASPNETCORE_ENVIRONMENT: "Development",
        ASPNETCORE_URLS: apiUrl,
        ConnectionStrings__Postgres: `Host=localhost;Port=${postgres.getMappedPort(5432)};Username=aihub;Password=aihub-e2e;Database=aihub`,
        ConnectionStrings__Redis: `localhost:${redis.getMappedPort(6379)}`,
        Database__MigrateOnStartup: "true",
        Panel__Authority: issuer,
        Panel__Audience: "aihub-api",
      },
      stdio: ["ignore", "pipe", "pipe"],
    });
    processes.push(api);
    const apiLog = collect(api);
    await waitFor(`${apiUrl}/api/panel/services`, 401, api, apiLog, "backend");

    const nextBin = path.join(webDir, "node_modules/next/dist/bin/next");
    const web = spawn(process.execPath, [nextBin, "start", "-p", String(webPort), "-H", "localhost"], {
      cwd: webDir,
      env: {
        ...process.env,
        API_BASE_URL: apiUrl,
        APP_URL: appUrl,
        OIDC_ISSUER: issuer,
        OIDC_CLIENT_ID: "aihub-panel",
        OIDC_CLIENT_SECRET: "e2e-client-secret",
        OIDC_SCOPE: "openid profile offline_access",
        OIDC_ALLOW_HTTP: "true",
        SESSION_SECRET: sessionSecret,
      },
      stdio: ["ignore", "pipe", "pipe"],
    });
    processes.push(web);
    const webLog = collect(web);
    await waitFor(`${appUrl}/giris-hatasi`, 200, web, webLog, "panel");

    // Testler (worker süreçleri) bu değerleri ortamdan okur.
    process.env.E2E_APP_URL = appUrl;
    process.env.E2E_PG_CONTAINER = postgres.getId();
    process.env.E2E_SESSION_SECRET = sessionSecret;
    process.env.E2E_OIDC_ORIGIN = new URL(issuer).origin;
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

function collect(child: ChildProcess): string[] {
  const lines: string[] = [];
  const push = (chunk: Buffer) => {
    lines.push(chunk.toString());
    if (lines.length > 200) lines.shift();
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
