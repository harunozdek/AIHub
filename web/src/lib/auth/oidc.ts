import * as client from "openid-client";
import type { LoginFlow, PanelSession } from "./session";

/**
 * Standart OIDC, sağlayıcıdan bağımsız: issuer, client ve kapsamlar konfigürasyondan gelir
 * (plan 0002, risk 1). Authorization code + PKCE, state ve nonce ile.
 */
let configuration: Promise<client.Configuration> | undefined;

function required(name: string): string {
  const value = process.env[name];
  if (!value) {
    throw new Error(`${name} tanımlı değil.`);
  }
  return value;
}

export function appUrl(path = "/"): URL {
  return new URL(path, required("APP_URL"));
}

function config(): Promise<client.Configuration> {
  configuration ??= (async () => {
    const issuer = new URL(required("OIDC_ISSUER"));
    // Düz HTTP sağlayıcı (mock OIDC) yalnızca geliştirmede veya OIDC_ALLOW_HTTP=true ile açıkça istenince
    // kabul edilir; e2e `next start` (production) ile koşar. Gerçek ortamlarda HTTPS zorunlu kalır.
    const insecure = issuer.protocol === "http:" && (process.env.NODE_ENV !== "production" || process.env.OIDC_ALLOW_HTTP === "true");
    return client.discovery(
      issuer,
      required("OIDC_CLIENT_ID"),
      process.env.OIDC_CLIENT_SECRET,
      undefined,
      insecure ? { execute: [client.allowInsecureRequests] } : undefined,
    );
  })().catch((error: unknown) => {
    configuration = undefined; // sağlayıcı geçici erişilemezse bir sonraki istek yeniden dener
    throw error;
  });
  return configuration;
}

/**
 * Yalnızca panel içi yollar dönüş adresi olabilir; başka siteye yönlendirme yapılmaz. Metin öneki
 * yetmez: URL ayrıştırıcısı tab ve satır sonlarını siler ("/\t/site" → "//site"). Bu yüzden kontrol
 * karakteri reddedilir ve değer ayrıştırılıp origin karşılaştırılır (review 1, F-01).
 * Ayrıştırma dot-segment'leri de normalize eder ("/.//site" → yol "//site"); bu yüzden dönen değerin
 * kendisi de yeniden ayrıştırılıp aynı origin'de kaldığı doğrulanır (review 2, R2-01).
 */
export function safeReturnTo(value: string | null | undefined): string {
  const fallback = "/logs";
  if (!value || !value.startsWith("/") || /[\u0000-\u001F\u007F\\]/.test(value)) {
    return fallback;
  }
  const base = appUrl("/");
  const url = URL.canParse(value, base) ? new URL(value, base) : null;
  if (!url || url.origin !== base.origin || url.pathname.startsWith("//")) {
    return fallback;
  }
  const result = url.pathname + url.search;
  return new URL(result, base).origin === base.origin ? result : fallback;
}

export async function beginLogin(returnTo: string): Promise<{ url: URL; flow: LoginFlow }> {
  const cfg = await config();
  const codeVerifier = client.randomPKCECodeVerifier();
  const flow: LoginFlow = { state: client.randomState(), nonce: client.randomNonce(), codeVerifier, returnTo: safeReturnTo(returnTo) };
  const url = client.buildAuthorizationUrl(cfg, {
    redirect_uri: appUrl("/auth/geri-donus").href,
    scope: process.env.OIDC_SCOPE ?? "openid profile offline_access",
    code_challenge: await client.calculatePKCECodeChallenge(codeVerifier),
    code_challenge_method: "S256",
    state: flow.state,
    nonce: flow.nonce,
  });
  return { url, flow };
}

function toSession(tokens: client.TokenEndpointResponse & client.TokenEndpointResponseHelpers, previous?: PanelSession): PanelSession {
  const claims = tokens.claims();
  return {
    accessToken: tokens.access_token,
    refreshToken: tokens.refresh_token ?? previous?.refreshToken,
    idToken: tokens.id_token ?? previous?.idToken,
    expiresAt: Math.floor(Date.now() / 1000) + (tokens.expires_in ?? 300),
    name: typeof claims?.name === "string" ? claims.name : previous?.name,
  };
}

/** Sağlayıcıdan dönen adresi doğrular ve kodu token'larla değiştirir. */
export async function completeLogin(currentUrl: URL, flow: LoginFlow): Promise<PanelSession> {
  const cfg = await config();
  const tokens = await client.authorizationCodeGrant(cfg, currentUrl, {
    pkceCodeVerifier: flow.codeVerifier,
    expectedState: flow.state,
    expectedNonce: flow.nonce,
    idTokenExpected: true,
  });
  return toSession(tokens);
}

export async function refresh(session: PanelSession): Promise<PanelSession | null> {
  if (!session.refreshToken) {
    return null;
  }
  try {
    return toSession(await client.refreshTokenGrant(await config(), session.refreshToken), session);
  } catch {
    return null; // yenilenemeyen oturum yeniden girişe yönlenir
  }
}

/** Sağlayıcı oturumunu da kapatan adres; sağlayıcı desteklemiyorsa doğrudan giriş sayfası. */
export async function logoutUrl(idToken: string | undefined): Promise<URL> {
  const loggedOut = appUrl("/");
  try {
    const cfg = await config();
    if (!cfg.serverMetadata().end_session_endpoint) {
      return loggedOut;
    }
    return client.buildEndSessionUrl(cfg, {
      post_logout_redirect_uri: loggedOut.href,
      ...(idToken ? { id_token_hint: idToken } : {}),
    });
  } catch {
    return loggedOut;
  }
}
