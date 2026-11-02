/**
 * Girişten sonra dönülecek adres yalnızca panelin kendi yolu olabilir: ayrıştırılmış URL aynı origin'de
 * kalmalı, yol `//` ile başlamamalı, kontrol karakteri ve ters eğik çizgi taşımamalı (Spec 0002 açık
 * yönlendirme koruması, F-01, R2-01; Spec 0004, AC-2). Karşılaştırma sabit bir taban üzerinden yapılır:
 * sonuç her zaman göreli bir yoldur.
 */
const BASE = new URL("http://aihub.invalid/");

export function safeReturnTo(value: string | null | undefined): string {
  const fallback = "/logs";
  if (!value || !value.startsWith("/") || /[\u0000-\u001F\u007F\\]/.test(value)) {
    return fallback;
  }
  const url = URL.canParse(value, BASE) ? new URL(value, BASE) : null;
  if (!url || url.origin !== BASE.origin || url.pathname.startsWith("//")) {
    return fallback;
  }
  const result = url.pathname + url.search;
  if (new URL(result, BASE).origin !== BASE.origin || result.startsWith("/giris")) {
    return fallback;
  }
  return result;
}
