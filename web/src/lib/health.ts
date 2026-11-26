import type { HealthFailureReason, HealthStatus, MonitoredApp } from "@/lib/api-client";

/** Durum metinle yazılır, yalnızca renkle anlatılmaz (Spec 0005, Erişilebilirlik). */
export function statusText(status: HealthStatus): string {
  return status === "Up" ? "Ayakta" : status === "Down" ? "Down" : "Bilinmiyor";
}

/** Son başarısızlığın anlaşılır nedeni (Spec 0005, R-6). Yanıt gövdesi ya da iç ayrıntı gösterilmez. */
export function failureText(reason: HealthFailureReason | null, statusCode: number | null): string {
  switch (reason) {
    case null:
      return "—";
    case "Timeout":
      return "Zaman aşımı";
    case "ConnectionFailed":
      return "Bağlantı kurulamadı";
    case "CertificateError":
      return "Sertifika hatası";
    case "HttpStatus":
      return statusCode ? `HTTP ${statusCode}` : "HTTP hatası";
    case "Redirect":
      return "Yönlendirme";
    case "BlockedAddress":
      return "Engellenen adres";
  }
}

/**
 * "İzlenen uygulamalar" ekranının gösterdiği alanlar (Spec 0012, CB-3). Ekleyen ve son değiştiren ekranda gösterilmez;
 * ekrana ve 10 sn'lik yoklamaya hiç taşınmaz. Sunucunun ilk çizimi ve `/durum/uygulamalar` aynı biçimi kullanır.
 */
export type EkranUygulamasi = Pick<
  MonitoredApp,
  "id" | "serviceName" | "healthUrl" | "status" | "lastCheckedAt" | "lastFailureReason" | "lastFailureStatusCode" | "downSince"
>;

export function ekranUygulamasi(a: MonitoredApp): EkranUygulamasi {
  return {
    id: a.id,
    serviceName: a.serviceName,
    healthUrl: a.healthUrl,
    status: a.status,
    lastCheckedAt: a.lastCheckedAt,
    lastFailureReason: a.lastFailureReason,
    lastFailureStatusCode: a.lastFailureStatusCode,
    downSince: a.downSince,
  };
}
