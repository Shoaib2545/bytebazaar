import { CategoryFilters, FilterAttribute } from "@/lib/api";
import PriceFilter from "./PriceFilter";

/**
 * Faceted filter sidebar — rendered **entirely on the server**.
 *
 * Filter state lives in the URL, so every checkbox is really just "go to this
 * URL with one value toggled". Expressing that as a `<Link>` means the whole
 * sidebar — by far the biggest widget on a category page, one control per
 * brand plus one per option of every filterable attribute — ships zero client
 * JS and costs nothing to hydrate. It also makes the facets crawlable and
 * keeps them working before (and without) JS.
 *
 * The only genuinely stateful control is the price min/max pair, which has to
 * hold un-submitted input; that stays a small client island (`PriceFilter`).
 *
 * Note the plain `<a>` rather than `next/link`. A category page is
 * `force-dynamic`, so Next does not prefetch it and a filter click costs a
 * server round trip either way — but `Link` is a Client Component, so using it
 * here would put ~50 client references in the RSC payload and hydrate ~50
 * components for no navigational benefit. Plain anchors keep the facet list
 * pure markup.
 *
 * Params arrive as a plain map already resolved from the page's `searchParams`,
 * so nothing here needs `useSearchParams` (which would force it client-side).
 */

/** Params that are not attribute/brand/price filters. */
const RESERVED = new Set(["page", "pageSize", "sort", "q"]);

interface Props {
  basePath: string;
  filters: CategoryFilters;
  /** Current URL query, normalised to a string map by the page. */
  params: Record<string, string>;
}

function parseValues(param: string | null | undefined): string[] {
  return param ? param.split(",").filter(Boolean) : [];
}

/**
 * Build a link target from the current params with `mutate` applied.
 * Any facet change resets pagination, exactly as the old click handler did.
 */
function buildHref(
  basePath: string,
  params: Record<string, string>,
  mutate: (sp: URLSearchParams) => void
): string {
  const sp = new URLSearchParams(params);
  mutate(sp);
  sp.delete("page");
  const qs = sp.toString();
  return qs ? `${basePath}?${qs}` : basePath;
}

function toggleHref(
  basePath: string,
  params: Record<string, string>,
  code: string,
  value: string,
  single: boolean
): string {
  return buildHref(basePath, params, (sp) => {
    let values = parseValues(sp.get(code));
    if (single) {
      values = values.includes(value) ? [] : [value];
    } else if (values.includes(value)) {
      values = values.filter((v) => v !== value);
    } else {
      values = [...values, value];
    }
    if (values.length) sp.set(code, values.join(","));
    else sp.delete(code);
  });
}

function removeHref(
  basePath: string,
  params: Record<string, string>,
  code: string,
  value?: string
): string {
  return buildHref(basePath, params, (sp) => {
    if (value === undefined) {
      sp.delete(code);
      return;
    }
    const values = parseValues(sp.get(code)).filter((v) => v !== value);
    if (values.length) sp.set(code, values.join(","));
    else sp.delete(code);
  });
}

function clearAllHref(
  basePath: string,
  params: Record<string, string>
): string {
  return buildHref(basePath, params, (sp) => {
    for (const key of Array.from(sp.keys())) {
      if (!RESERVED.has(key)) sp.delete(key);
    }
  });
}

/**
 * Check indicator standing in for the old `accent-orange-500` inputs.
 *
 * Deliberately a single element with a text glyph rather than a nested SVG:
 * a category page renders one of these per brand and per attribute option
 * (~45 of them here), and every extra DOM node is paid for three times — in
 * the RSC payload, in React's hydration pass, and in layout.
 */
function CheckMark({ checked, round }: { checked: boolean; round?: boolean }) {
  return (
    <span
      aria-hidden
      className={`flex h-4 w-4 shrink-0 items-center justify-center text-[10px] leading-none border ${
        round ? "rounded-full" : "rounded-sm"
      } ${
        checked
          ? "border-orange-500 bg-orange-500 text-white"
          : "border-slate-300 bg-white"
      }`}
    >
      {checked ? (round ? "●" : "✓") : ""}
    </span>
  );
}

function FacetOption({
  href,
  label,
  count,
  checked,
  round,
}: {
  href: string;
  label: string;
  count: number;
  checked: boolean;
  round?: boolean;
}) {
  return (
    <li>
      <a
        href={href}
        aria-label={`${checked ? "Remove filter" : "Filter by"} ${label}`}
        className="flex cursor-pointer items-center gap-2 text-sm text-slate-700 hover:text-orange-600"
      >
        <CheckMark checked={checked} round={round} />
        <span className="flex-1">{label}</span>
        <span className="text-xs text-slate-400">({count})</span>
      </a>
    </li>
  );
}

export default function FilterSidebar({ basePath, filters, params }: Props) {
  // Active-filter chips, derived from URL state on the server.
  const chips: { href: string; key: string; label: string }[] = [];
  for (const [key, raw] of Object.entries(params)) {
    if (RESERVED.has(key) || !raw?.trim()) continue;
    if (key === "price") {
      chips.push({
        key: "price",
        href: removeHref(basePath, params, "price"),
        label: `Price: ${raw.replace("-", " – ")}`,
      });
      continue;
    }
    const attr = filters.attributes.find((a) => a.code === key);
    const isBrand = key === "brand";
    for (const value of parseValues(raw)) {
      let label = value;
      if (isBrand) {
        const brand = filters.brands.find((b) => b.slug === value);
        label = `Brand: ${brand?.name ?? value}`;
      } else if (attr) {
        label = `${attr.name}: ${value}`;
      }
      chips.push({
        key: `${key}-${value}`,
        href: removeHref(basePath, params, key, value),
        label,
      });
    }
  }

  return (
    <aside className="w-full shrink-0 lg:w-64">
      {chips.length > 0 && (
        <div className="mb-4 rounded-lg border border-slate-200 bg-white p-3">
          <div className="mb-2 flex items-center justify-between">
            <span className="text-xs font-semibold uppercase tracking-wide text-slate-500">
              Active filters
            </span>
            <a
              href={clearAllHref(basePath, params)}
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
      )}

      <div className="space-y-4">
        <PriceFilter
          basePath={basePath}
          params={params}
          range={filters.priceRange}
        />

        {filters.brands.length > 0 && (
          <section className="rounded-lg border border-slate-200 bg-white p-4">
            <h3 className="mb-3 text-sm font-semibold text-blue-950">Brand</h3>
            <ul className="max-h-56 space-y-1.5 overflow-y-auto">
              {filters.brands.map((brand) => (
                <FacetOption
                  key={brand.id}
                  href={toggleHref(
                    basePath,
                    params,
                    "brand",
                    brand.slug,
                    false
                  )}
                  label={brand.name}
                  count={brand.count}
                  checked={parseValues(params.brand).includes(brand.slug)}
                />
              ))}
            </ul>
          </section>
        )}

        {filters.attributes.map((attr) => (
          <AttributeGroup
            key={attr.code}
            attr={attr}
            basePath={basePath}
            params={params}
          />
        ))}
      </div>
    </aside>
  );
}

function AttributeGroup({
  attr,
  basePath,
  params,
}: {
  attr: FilterAttribute;
  basePath: string;
  params: Record<string, string>;
}) {
  if (!attr.options.length) return null;
  const single = attr.widget === "Radio";
  const selected = parseValues(params[attr.code]);

  return (
    <section className="rounded-lg border border-slate-200 bg-white p-4">
      <h3 className="mb-3 text-sm font-semibold text-blue-950">{attr.name}</h3>
      <ul className="max-h-56 space-y-1.5 overflow-y-auto">
        {attr.options.map((opt) => (
          <FacetOption
            key={opt.value}
            href={toggleHref(basePath, params, attr.code, opt.value, single)}
            label={opt.value}
            count={opt.count}
            checked={selected.includes(opt.value)}
            round={single}
          />
        ))}
      </ul>
    </section>
  );
}
