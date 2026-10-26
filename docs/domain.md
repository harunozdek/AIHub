# Domain

> **Bootstrap'te dolduruldu (2026-10-26).** İş, insanlar ve ajanlar arasındaki ortak dil.
> Bir terim burada yoksa, AI onun anlamını kendisi uydurur.

## Ubiquitous language

Bu terimler **kodda da aynı (İngilizce) biçimleriyle** geçer, böylece sözlük ile kod birebir
örtüşür — genel kod dili Türkçe olsa bile (`docs/conventions.md`, Language).

| Term | Meaning | Notes / not to be confused with |
|---|---|---|
| Provider | AIHub'ın konuştuğu dış AI sağlayıcı kurumu (Anthropic, OpenAI …) | Model ile karıştırılmaz: Provider kurumdur, Model üründür |
| Model | Bir Provider'ın somut, adlandırılmış modeli (ör. `claude-opus-5`) | Provider'ın kendisi değil; bir Provider birden çok Model sunar |
| Route | Bir Request'i hangi Model'e göndereceğimizi belirleyen kural | Yük dengeleme veya yeniden deneme değil — karar kuralı |
| Request | İstemciden AIHub'a gelen tek çağrı | AIHub'ın Provider'a yaptığı dış HTTP çağrısı değil |
| Usage | Bir Request'in ölçümü: token sayısı, maliyet, gecikme, Model, Provider, sonuç durumu | İçerik değil — prompt/yanıt metni Usage'a girmez |
| Quota | Bir Tenant'ın tüketim üst sınırı | Rate limit ile aynı şey değil: Quota birikimli tüketimdir |
| Tenant | Kotalanan ve faturalanan müşteri birimi | Kullanıcı değil; bir Tenant'ın birden çok kullanıcısı olabilir |

## Business rules

- **BR-1:** Başarılı veya başarısız **her** Request için bir Usage kaydı oluşur (token, maliyet,
  gecikme, Model, Provider, sonuç durumu). Ölçülemeyen çağrı ne faturalanabilir ne hata ayıklanabilir.

> Henüz karara bağlanmayan BR adayları (bağlayıcı **değil**, ilk spec'te veya bir ADR ile karara
> bağlanmalı): Quota aşımında isteğin hiç gönderilmemesi (429) · Provider hatalarının AIHub'ın
> kendi hata sözleşmesine çevrilmesi.

## Key domain invariants

- Bir Usage kaydı prompt veya yanıt **içeriği** barındırmaz; yalnızca ölçüm taşır
  (`docs/security.md`, Loglama).
- Provider kimlik bilgileri hiçbir Request yanıtında, hata mesajında veya logda görünmez.
- Terim ile kod aynı kelimeyi kullanır: yukarıdaki tablodaki bir terim koda farklı bir adla girmez.
