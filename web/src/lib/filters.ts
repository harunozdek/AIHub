/**
 * Liste filtreleri ile sayfa adresi arasındaki eşleme (Spec 0002, R-3): etkin filtreler adreste durur,
 * adres paylaşılınca aynı sonuç görünür. Burada yalnızca taşıma yapılır; doğrulama kuralları
 * Application'dadır ve mesajlar backend ProblemDetails'inden gelir.
 */
export const LOG_LEVELS = ["Info", "Warning", "Error", "Critical"] as const;

export type ListFilters = {
  service?: string;
  levels: string[];
  /** ISO 8601, saat dilimiyle (ör. 2026-10-27T09:00:00.000Z). */
  from?: string;
  to?: string;
  cursor?: string;
};

type RawParams = Record<string, string | string[] | undefined>;

function first(value: string | string[] | undefined): string | undefined {
  const v = Array.isArray(value) ? value[0] : value;
  return v ? v : undefined;
}

export function parseFilters(params: RawParams): ListFilters {
  const level = params.level;
  return {
    service: first(params.service),
    levels: (Array.isArray(level) ? level : level ? [level] : []).filter(Boolean),
    from: first(params.from),
    to: first(params.to),
    cursor: first(params.cursor),
  };
}

/** Filtrelerin sorgu dizesi; `cursor` verilmezse sayfa işareti düşer (filtre değişince ilk sayfa). */
export function toSearchParams(filters: ListFilters, options: { withCursor?: boolean } = {}): URLSearchParams {
  const query = new URLSearchParams();
  if (filters.service) query.set("service", filters.service);
  for (const level of filters.levels) query.append("level", level);
  if (filters.from) query.set("from", filters.from);
  if (filters.to) query.set("to", filters.to);
  if (options.withCursor && filters.cursor) query.set("cursor", filters.cursor);
  return query;
}

export function hasActiveFilters(filters: ListFilters): boolean {
  return Boolean(filters.service || filters.levels.length || filters.from || filters.to);
}

export function listHref(filters: ListFilters, cursor?: string): string {
  const query = toSearchParams({ ...filters, cursor }, { withCursor: true });
  const qs = query.toString();
  return qs ? `/logs?${qs}` : "/logs";
}

/** Ayrıntıdan listeye dönüşte filtreler ve sayfa korunur (AC-7). Yalnızca liste adresi kabul edilir. */
export function detailHref(id: string, listUrl: string): string {
  return `/logs/${encodeURIComponent(id)}?donus=${encodeURIComponent(listUrl)}`;
}

export function safeListUrl(value: string | undefined): string {
  return value && (value === "/logs" || value.startsWith("/logs?")) ? value : "/logs";
}
