import { redirect } from "next/navigation";
import { getSession } from "@/lib/auth/session";

/**
 * Backend panel okuma yüzeyinin sunucu tarafı istemcisi. Bearer token oturumdan eklenir ve tarayıcıya
 * hiç gitmez. Hatalar iç ayrıntı taşımayan bir türe çevrilir (Spec 0002, R-9).
 */
export type LogSummary = {
  id: string;
  receivedAt: string;
  serviceName: string;
  level: string;
  messagePreview: string;
  messageTruncated: boolean;
  exceptionType: string | null;
};

export type LogPage = {
  items: LogSummary[];
  older: string | null;
  newer: string | null;
  from: string;
  to: string;
};

export type LogDetail = {
  id: string;
  serviceName: string;
  level: string;
  message: string;
  exceptionType: string | null;
  stackTrace: string | null;
  metadata: unknown;
  occurredAt: string | null;
  receivedAt: string;
};

/** Alan adına göre doğrulama mesajları; alan adları backend'in ProblemDetails `errors` anahtarlarıdır. */
export type FieldErrors = Record<string, string[]>;

export type ApiResult<T> =
  | { ok: true; data: T }
  | { ok: false; kind: "validation"; errors: FieldErrors }
  | { ok: false; kind: "notFound" }
  | { ok: false; kind: "conflict"; message: string }
  | { ok: false; kind: "unavailable" };

/**
 * @param path backend yolu, sorgu dizesiyle (ör. `/api/panel/logs?service=x`)
 * @param returnTo token reddedilirse yeniden girişten sonra dönülecek panel adresi
 */
function get<T>(path: string, returnTo: string): Promise<ApiResult<T>> {
  return request<T>("GET", path, returnTo);
}

async function request<T>(method: "GET" | "POST", path: string, returnTo: string, body?: unknown): Promise<ApiResult<T>> {
  const session = await getSession();
  if (!session.accessToken) {
    redirect(`/giris?returnTo=${encodeURIComponent(returnTo)}`);
  }

  let response: Response;
  try {
    response = await fetch(new URL(path, process.env.API_BASE_URL), {
      method,
      headers: {
        Authorization: `Bearer ${session.accessToken}`,
        Accept: "application/json",
        ...(body === undefined ? {} : { "Content-Type": "application/json" }),
      },
      body: body === undefined ? undefined : JSON.stringify(body),
      cache: "no-store",
    });
  } catch {
    return { ok: false, kind: "unavailable" };
  }

  if (response.ok) {
    const text = await response.text();
    return { ok: true, data: (text ? JSON.parse(text) : null) as T };
  }
  if (response.status === 409) {
    const problem = (await response.json().catch(() => null)) as { title?: string } | null;
    return { ok: false, kind: "conflict", message: problem?.title ?? "İşlem yapılamadı." };
  }
  if (response.status === 401) {
    // Token middleware'den sonra geçersizleşti (ör. kullanıcı pasifleştirildi veya şifresi değişti): yeniden
    // giriş, aynı adrese dönüş (Spec 0004, R-8, R-9). Çerez önce silinir (review 1, F-06).
    redirect(`/auth/oturum-dustu?returnTo=${encodeURIComponent(returnTo)}`);
  }
  if (response.status === 404) {
    return { ok: false, kind: "notFound" };
  }
  if (response.status === 400) {
    const problem = (await response.json().catch(() => null)) as { errors?: FieldErrors } | null;
    if (problem?.errors) {
      return { ok: false, kind: "validation", errors: problem.errors };
    }
  }
  return { ok: false, kind: "unavailable" };
}

export function listLogs(query: URLSearchParams, returnTo: string) {
  const qs = query.toString();
  return get<LogPage>(`/api/panel/logs${qs ? `?${qs}` : ""}`, returnTo);
}

export function listServices(returnTo: string) {
  return get<{ services: string[] }>("/api/panel/services", returnTo);
}

export function getLog(id: string, returnTo: string) {
  return get<LogDetail>(`/api/panel/logs/${encodeURIComponent(id)}`, returnTo);
}

// --- Spec 0004: yerel giriş ve kullanıcı yönetimi -------------------------------------------------

export type UserRow = {
  id: string;
  userName: string;
  displayName: string;
  isActive: boolean;
  createdAt: string;
  createdBy: string | null;
  lastLoginAt: string | null;
  deactivatedAt: string | null;
  deactivatedBy: string | null;
};

export type IssuedToken = { token: string; expiresAt: string };

export type LoginOutcome =
  | { ok: true; token: string; expiresAt: string; displayName: string; userId: string }
  | { ok: false; kind: "invalid" }
  | { ok: false; kind: "unavailable"; reason: unknown };

/**
 * Giriş: oturum gerekmez. Her başarısızlık backend'de aynı 401'dir (R-2); kullanıcı adı ve şifre bu
 * fonksiyonun dışına, loga ya da hata nesnesine hiç çıkmaz.
 */
export async function login(userName: string, password: string): Promise<LoginOutcome> {
  if (!process.env.API_BASE_URL) {
    return { ok: false, kind: "unavailable", reason: new Error("API_BASE_URL tanımlı değil.") };
  }

  let response: Response;
  try {
    response = await fetch(new URL("/api/auth/login", process.env.API_BASE_URL), {
      method: "POST",
      headers: { "Content-Type": "application/json", Accept: "application/json" },
      body: JSON.stringify({ userName, password }),
      cache: "no-store",
    });
  } catch (error) {
    return { ok: false, kind: "unavailable", reason: error };
  }

  if (response.ok) {
    const body = (await response.json()) as { token: string; expiresAt: string; displayName: string; userId: string };
    return { ok: true, ...body };
  }
  if (response.status === 401) {
    return { ok: false, kind: "invalid" };
  }
  return { ok: false, kind: "unavailable", reason: `backend yanıtı ${response.status}` };
}

export function listUsers(returnTo: string) {
  return get<UserRow[]>("/api/users", returnTo);
}

export function createUser(input: { userName: string; displayName: string; password: string }, returnTo: string) {
  return request<{ id: string }>("POST", "/api/users", returnTo, input);
}

export function setUserActive(id: string, active: boolean, returnTo: string) {
  return request<null>("POST", `/api/users/${encodeURIComponent(id)}/${active ? "activate" : "deactivate"}`, returnTo);
}

export function changePassword(input: { currentPassword: string; newPassword: string }, returnTo: string) {
  return request<IssuedToken>("POST", "/api/me/password", returnTo, input);
}
