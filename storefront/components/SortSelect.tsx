"use client";

import { useRouter } from "next/navigation";

export interface SortOption {
  value: string;
  label: string;
}

/** Category listings default to newest-first. */
const CATALOG_OPTIONS: SortOption[] = [
  { value: "newest", label: "Newest" },
  { value: "price_asc", label: "Price: Low to High" },
  { value: "price_desc", label: "Price: High to Low" },
];

/** Search results default to relevance — the engine's own ranking. */
export const SEARCH_SORT_OPTIONS: SortOption[] = [
  { value: "relevance", label: "Relevance" },
  { value: "price_asc", label: "Price: Low to High" },
  { value: "price_desc", label: "Price: High to Low" },
];

interface Props {
  basePath: string;
  /**
   * Current URL query, resolved server-side and passed down. Reading it as a
   * prop instead of via `useSearchParams` keeps this island free of a Suspense
   * boundary and of a router-state subscription.
   */
  params: Record<string, string>;
  /** Defaults to the catalog set; pass SEARCH_SORT_OPTIONS on /search. */
  options?: SortOption[];
}

export default function SortSelect({
  basePath,
  params,
  options = CATALOG_OPTIONS,
}: Props) {
  const router = useRouter();
  // The first option is the implicit default and is never written to the URL,
  // so the unsorted URL stays canonical.
  const fallback = options[0]?.value ?? "newest";
  const current = params.sort || fallback;

  function onChange(value: string) {
    const sp = new URLSearchParams(params);
    if (value === fallback) sp.delete("sort");
    else sp.set("sort", value);
    sp.delete("page");
    const qs = sp.toString();
    router.push(qs ? `${basePath}?${qs}` : basePath);
  }

  return (
    <label className="flex items-center gap-2 text-sm text-slate-600">
      <span className="hidden sm:inline">Sort by</span>
      <select
        value={current}
        onChange={(e) => onChange(e.target.value)}
        className="rounded border border-slate-300 bg-white px-2 py-1.5 text-sm focus:border-orange-500 focus:outline-none"
      >
        {options.map((o) => (
          <option key={o.value} value={o.value}>
            {o.label}
          </option>
        ))}
      </select>
    </label>
  );
}
