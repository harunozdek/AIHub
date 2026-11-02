import { NextResponse } from "next/server";
import { getSession } from "@/lib/auth/session";
import type { MonitoredApp } from "@/lib/api-client";

export type DownApp = { serviceName: string; downSince: string | null };

/**
 * Down uyarısının yokladığı uç (Spec 0005, R-7, AC-8). Tarayıcı backend'i çağırmaz: istek panelin
 * sunucusunda, oturum çerezindeki token'la yapılır (BFF). Sayfalardan farklı olarak yönlendirme yapmaz;
 * oturum düşmüşse 401, backend okunamazsa 503 döner ve uyarı son bilinen durumu gösterir. Liste
 * backend'den Down'lar önce gelir; yalnızca Down olanlar süzülür (plan 0005, risk 7).
 */
export async function GET() {
  const session = await getSession();
  if (!session.accessToken) {
    return NextResponse.json({ error: "oturum yok" }, { status: 401 });
  }

  let response: Response;
  try {
    response = await fetch(new URL("/api/monitored-apps", process.env.API_BASE_URL), {
      headers: { Authorization: `Bearer ${session.accessToken}`, Accept: "application/json" },
      cache: "no-store",
    });
  } catch {
    return NextResponse.json({ error: "durum okunamadı" }, { status: 503 });
  }

  if (!response.ok) {
    const status = response.status === 401 ? 401 : 503;
    return NextResponse.json({ error: "durum okunamadı" }, { status });
  }

  const apps = (await response.json()) as MonitoredApp[];
  const down: DownApp[] = apps.filter((a) => a.status === "Down").map((a) => ({ serviceName: a.serviceName, downSince: a.downSince }));
  return NextResponse.json({ down }, { headers: { "Cache-Control": "no-store" } });
}
