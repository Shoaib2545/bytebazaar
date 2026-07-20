import type { Metadata } from "next";
import { searchCatalog } from "@/lib/api";
import { EMPTY_SEARCH_RESULTS } from "@/lib/search";
import ActiveFilterChips from "@/components/ActiveFilterChips";
import Pagination from "@/components/Pagination";
import ProductCard from "@/components/ProductCard";
import SortSelect, { SEARCH_SORT_OPTIONS } from "@/components/SortSelect";

export const dynamic = "force-dynamic";

type SearchParams = { [key: string]: string | string[] | undefined };

interface Props {
  searchParams: Promise<SearchParams>;
}

/**
 * Flatten Next's searchParams into a simple string map (arrays -> csv), same
 * convention as app/category/[slug]/page.tsx — the search endpoint accepts the
 * identical page/pageSize/sort/brand/price/attribute-code query params.
 */
function normalizeParams(sp: SearchParams): Record<string, string> {
  const out: Record<string, string> = {};
  for (const [key, value] of Object.entries(sp)) {
    if (value === undefined) continue;
    out[key] = Array.isArray(value) ? value.join(",") : value;
  }
  return out;
}

export async function generateMetadata({
  searchParams,
}: Props): Promise<Metadata> {
  const q = normalizeParams(await searchParams).q?.trim() ?? "";
  return {
    title: q ? `Search: ${q}` : "Search",
    // Internal search results are thin/duplicate content — keep them out of
    // the index but let crawlers follow through to the products.
    robots: { index: false, follow: true },
  };
}

export default async function SearchPage({ searchParams }: Props) {
  // `urlParams` mirrors the address bar and drives the server-rendered
  // sort/chip links; `query` adds the API's paging defaults on top.
  const urlParams = normalizeParams(await searchParams);
  const q = (urlParams.q ?? "").trim();
  urlParams.q = q;
  const query = { ...urlParams };
  if (!query.page) query.page = "1";
  if (!query.pageSize) query.pageSize = "24";

  // `q` travels as its own argument; everything else is passed through so the
  // filter/sort/pagination URL conventions behave exactly as on a category.
  const filterParams = { ...query };
  delete filterParams.q;
  const results = q
    ? await searchCatalog(q, filterParams)
    : { ...EMPTY_SEARCH_RESULTS, query: q };

  const basePath = "/search";

  return (
    <div className="mx-auto max-w-7xl px-4 py-6">
      {/* min-h reserves the header row so the sort control hydrating in does
          not shove the product grid down (CLS). */}
      <div className="mb-5 flex min-h-14 flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold text-blue-950">
            {q ? (
              <>
                Search results for{" "}
                <span className="text-orange-600">&ldquo;{q}&rdquo;</span>
              </>
            ) : (
              "Search"
            )}
          </h1>
          <p className="mt-0.5 text-sm text-slate-500">
            {results.totalCount} result{results.totalCount === 1 ? "" : "s"}
          </p>
        </div>
        {q && (
          <SortSelect
            basePath={basePath}
            params={urlParams}
            options={SEARCH_SORT_OPTIONS}
          />
        )}
      </div>

      <ActiveFilterChips basePath={basePath} params={urlParams} />

      {results.items.length > 0 ? (
        <>
          <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5">
            {results.items.map((p, i) => (
              // The first row is above the fold — its image is the LCP
              // candidate, so load it eagerly rather than lazily.
              <ProductCard key={p.id} product={p} eager={i < 5} />
            ))}
          </div>
          <Pagination
            basePath={basePath}
            params={query}
            page={results.page}
            pageSize={results.pageSize}
            totalCount={results.totalCount}
          />
        </>
      ) : (
        <div className="rounded-lg border border-dashed border-slate-300 bg-white p-16 text-center">
          <p className="text-sm font-medium text-slate-600">
            {q
              ? `Nothing found for "${q}".`
              : "Type something in the search bar to find products."}
          </p>
          <p className="mt-1 text-xs text-slate-400">
            Try a different keyword, brand or model number.
          </p>
        </div>
      )}
    </div>
  );
}
