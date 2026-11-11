/**
 * Panelin sağlık adresi (Spec 0008, R-2, R-3): yalnızca panelin kendisinin çalıştığını söyler; backend'e erişimi
 * denetlemez, backend arızası bu adresi başarısız yapmaz. Girişsizdir (middleware muaf tutar); yanıt yalnızca
 * durum metnidir ve önbelleğe alınmaz. Her istekte çalışır: derlemede önceden üretilmiş bir yanıt, panel
 * durmuşken önbellekten "Healthy" dönerdi.
 */
export const dynamic = "force-dynamic";

export function GET() {
  return new Response("Healthy", {
    headers: { "Content-Type": "text/plain; charset=utf-8", "Cache-Control": "no-store, no-cache" },
  });
}
