/**
 * Removable chips for the facet filters currently in the URL.
 *
 * This is the chip half of FilterSidebar, split out for /search: the search
 * endpoint accepts the same brand/price/attribute params as the category
 * endpoints, but there is no per-query facet payload to render checkboxes
 * from. So a shopper who arrives on /search carrying filters (e.g. from a
 * category page, or a saved link) can still see and clear them.
 *
 * Server-rendered: removing a chip is just a navigation to a `force-dynamic`
 * route, which Next does not prefetch anyway, so each chip is a plain `<a>`
 * and the component ships no client JS at all.
 *
 * Keep RESERVED in sync with FilterSidebar.tsx and NON_FILTER_PARAMS in
 * lib/seo.ts.
 */
const RESERVED = new Set(["page", "pageSize", "sort", "q"]);

function label(code: string, value: string): string {
  if (code === "price") return `Price: ${value.replace("-", " – ")}`;
  if (code === "brand") return `Brand: ${value}`;
  return `${code}: ${value}`;
}

function buildHref(
  basePath: string,
  params: Record<string, string>,
  mutate: (sp: URLSearchParams) => void
): string {
  const sp = new URLSearchParams(params);
  mutate(sp);
  sp.delete("page"); // any filter change resets pagination
  const qs = sp.toString();
  return qs ? `${basePath}?${qs}` : basePath;
}

interface Props {
  basePath: string;
  /** Current URL query, normalised to a string map by the page. */
  params: Record<string, string>;
}

export default function ActiveFilterChips({ basePath, params }: Props) {
  const chips: { key: string; href: string; label: string }[] = [];

  for (const [code, raw] of Object.entries(params)) {
    if (RESERVED.has(code) || !raw?.trim()) continue;

    if (code === "price") {
      chips.push({
        key: "price",
        label: label("price", raw),
        href: buildHref(basePath, params, (sp) => sp.delete("price")),
      });
      continue;
    }

    for (const value of raw.split(",").filter(Boolean)) {
      chips.push({
        key: `${code}-${value}`,
        label: label(code, value),
        href: buildHref(basePath, params, (sp) => {
          const rest = (sp.get(code) ?? "")
            .split(",")
            .filter((v) => v && v !== value);
          if (rest.length) sp.set(code, rest.join(","));
          else sp.delete(code);
        }),
      });
    }
  }

  if (chips.length === 0) return null;

  const clearAllHref = buildHref(basePath, params, (sp) => {
    for (const key of Array.from(sp.keys())) {
      if (!RESERVED.has(key)) sp.delete(key);
    }
  });

  return (
    <div className="mb-4 rounded-lg border border-slate-200 bg-white p-3">
      <div className="mb-2 flex items-center justify-between">
        <span className="text-xs font-semibold uppercase tracking-wide text-slate-500">
          Active filters
        </span>
        <a
          href={clearAllHref}
          className="text-xs font-medium text-orange-600 hover:underline"
        >
          Clear all
        </a>
      </div>
      <div className="flex flex-wrap gap-1.5">
        {chips.map((chip) => (
          <a
            key={chip.key}
            href={chip.href}
            className="inline-flex items-center gap-1 rounded-full bg-blue-950 px-2.5 py-1 text-xs text-white transition hover:bg-blue-900"
            title="Remove filter"
          >
            {chip.label}
            <span aria-hidden className="text-orange-400">
              ×
            </span>
          </a>
        ))}
      </div>
    </div>
  );
}
