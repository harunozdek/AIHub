import { cookies } from "next/headers";
import { getIronSession, sealData, unsealData, type SessionOptions } from "iron-session";

/**
 * Panel oturumu: token'lar yalnızca sunucuda okunur. Çerez `httpOnly` ve şifrelidir; tarayıcı
 * JavaScript'i token'a erişemez (Spec 0002, R-1; plan 0002, Amendment A).
 */
export type PanelSession = {
  accessToken: string;
  refreshToken?: string;
  idToken?: string;
  /** Erişim token'ının bitişi, epoch saniye. */
  expiresAt: number;
  name?: string;
};

/** Giriş akışı sürerken geri dönüşü doğrulayan tek kullanımlık değerler. */
export type LoginFlow = {
  state: string;
  nonce: string;
  codeVerifier: string;
  returnTo: string;
};

export const SESSION_COOKIE = "aihub_session";
export const LOGIN_FLOW_COOKIE = "aihub_login";
const LOGIN_FLOW_TTL_SECONDS = 10 * 60;

function password(): string {
  const secret = process.env.SESSION_SECRET;
  if (!secret || secret.length < 32) {
    throw new Error("SESSION_SECRET en az 32 karakter olmalıdır.");
  }
  return secret;
}

const secure = process.env.NODE_ENV === "production";

function sessionOptions(): SessionOptions {
  return {
    password: password(),
    cookieName: SESSION_COOKIE,
    // ttl 0: çerez tarayıcı oturumuyla yaşar; geçerliliği token'ın bitişi belirler.
    ttl: 0,
    cookieOptions: { httpOnly: true, secure, sameSite: "lax", path: "/" },
  };
}

export async function getSession() {
  return getIronSession<Partial<PanelSession>>(await cookies(), sessionOptions());
}

export function sealSession(session: PanelSession): Promise<string> {
  return sealData(session, { password: password(), ttl: 0 });
}

/** Middleware çerezi doğrudan çözer; bozuk veya eski çerez oturum yok sayılır. */
export async function unsealSession(seal: string | undefined): Promise<PanelSession | null> {
  if (!seal) {
    return null;
  }
  const data = await unsealData<Partial<PanelSession>>(seal, { password: password(), ttl: 0 });
  return data.accessToken && typeof data.expiresAt === "number" ? (data as PanelSession) : null;
}

export const sessionCookieOptions = { httpOnly: true, secure, sameSite: "lax", path: "/" } as const;

export async function getLoginFlow() {
  return getIronSession<Partial<LoginFlow>>(await cookies(), {
    password: password(),
    cookieName: LOGIN_FLOW_COOKIE,
    ttl: LOGIN_FLOW_TTL_SECONDS,
    cookieOptions: { httpOnly: true, secure, sameSite: "lax", path: "/auth" },
  });
}
