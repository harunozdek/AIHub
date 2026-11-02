import { cookies } from "next/headers";
import { getIronSession, unsealData, type SessionOptions } from "iron-session";

/**
 * Panel oturumu: backend'in imzaladığı token yalnızca sunucuda okunur. Çerez `httpOnly` ve şifrelidir;
 * tarayıcı JavaScript'i token'a erişemez (Spec 0002, R-1; Spec 0004, ADR 0002).
 */
export type PanelSession = {
  accessToken: string;
  /** Token'ın bitişi, epoch saniye: girişten itibaren mutlak 8 saat (Spec 0004, R-10). */
  expiresAt: number;
  name?: string;
  /** Yalnızca arayüz için (ör. kendi satırında "Pasifleştir" gösterilmez); kural backend'dedir. */
  userId?: string;
};

export const SESSION_COOKIE = "aihub_session";

function password(): string {
  const secret = process.env.SESSION_SECRET;
  if (!secret || secret.length < 32) {
    throw new Error("SESSION_SECRET en az 32 karakter olmalıdır (web/.env.local, .env.example'a bakın).");
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

/** Backend'in döndürdüğü token'ı oturuma yazar (giriş ve şifre değiştirme). */
export async function saveToken(token: string, expiresAt: string, name?: string, userId?: string): Promise<void> {
  const session = await getSession();
  session.accessToken = token;
  session.expiresAt = Math.floor(new Date(expiresAt).getTime() / 1000);
  if (name !== undefined) session.name = name;
  if (userId !== undefined) session.userId = userId;
  await session.save();
}

/** Middleware çerezi doğrudan çözer; bozuk veya eski çerez oturum yok sayılır. */
export async function unsealSession(seal: string | undefined): Promise<PanelSession | null> {
  if (!seal) {
    return null;
  }
  const data = await unsealData<Partial<PanelSession>>(seal, { password: password(), ttl: 0 });
  return data.accessToken && typeof data.expiresAt === "number" ? (data as PanelSession) : null;
}
