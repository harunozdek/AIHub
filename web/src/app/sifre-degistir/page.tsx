import type { Metadata } from "next";
import { SifreFormu } from "./sifre-formu";

export const metadata: Metadata = { title: "AIHub — Şifre değiştir" };

/** Kendi şifresini değiştirme ekranı (Spec 0004, R-9). */
export default function SifreDegistirPage() {
  return (
    <section className="mx-auto max-w-sm py-6" aria-labelledby="baslik">
      <h1 id="baslik" className="mb-6 text-xl font-semibold">Şifre değiştir</h1>
      <SifreFormu />
    </section>
  );
}
