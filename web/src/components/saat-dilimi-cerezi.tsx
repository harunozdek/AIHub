"use client";

import { useRouter } from "next/navigation";
import { useEffect } from "react";
import { SAAT_DILIMI_CEREZI } from "@/lib/dashboard-pencere";

/**
 * Tarayıcının saat dilimini (IANA adı, ör. Europe/Istanbul) bir çereze yazar: sunucu, serbest tarih aralığının yerel gün
 * sınırlarını bununla hesaplar (Spec 0011, CB-7; plan 0011, risk 3). Çerez sır değildir. `yenile` verildiyse sunucu
 * çerez olmadan aralığı hesaplayamamıştır; çerez yazıldıktan sonra sayfa bir kez yenilenir.
 */
export function SaatDilimiCerezi({ mevcut, yenile }: { mevcut: string | null; yenile: boolean }) {
  const router = useRouter();

  useEffect(() => {
    const dilim = Intl.DateTimeFormat().resolvedOptions().timeZone;
    if (dilim && dilim !== mevcut) {
      document.cookie = `${SAAT_DILIMI_CEREZI}=${encodeURIComponent(dilim)}; path=/; max-age=31536000; samesite=lax`;
      // Çerezler engellenmişse yenileme döngüye girmesin: yalnızca çerez gerçekten yazıldıysa.
      if (yenile && document.cookie.split("; ").some((c) => c.startsWith(`${SAAT_DILIMI_CEREZI}=`))) router.refresh();
    }
  }, [mevcut, yenile, router]);

  return null;
}
