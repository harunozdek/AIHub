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
  | { ok: false; kind: "unavailable" };

/**
 * @param path backend yolu, sorgu dizesiyle (ör. `/api/panel/logs?service=x`)
 * @param returnTo token reddedilirse yeniden girişten sonra dönülecek panel adresi
 */
async function get<T>(path: string, returnTo: string): Promise<ApiResult<T>> {
  const session = await getSession();
  if (!session.accessToken) {
    redirect(`/auth/giris?returnTo=${encodeURIComponent(returnTo)}`);
  }

  let response: Response;
  try {
    response = await fetch(new URL(path, process.env.API_BASE_URL), {
      headers: { Authorization: `Bearer ${session.accessToken}`, Accept: "application/json" },
      cache: "no-store",
    });
  } catch {
    return { ok: false, kind: "unavailable" };
  }

  if (response.ok) {
    return { ok: true, data: (await response.json()) as T };
  }
  if (response.status === 401) {
    // Token middleware'den sonra geçersizleşti (ör. sağlayıcıda iptal): yeniden giriş, aynı adrese dönüş.
    redirect(`/auth/giris?returnTo=${encodeURIComponent(returnTo)}`);
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
