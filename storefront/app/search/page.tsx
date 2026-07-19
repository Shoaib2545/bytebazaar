import type { Metadata } from "next";
import { searchProducts, EMPTY_PAGED } from "@/lib/api";
import Pagination from "@/components/Pagination";
import ProductCard from "@/components/ProductCard";
import SortSelect from "@/components/SortSelect";

export const dynamic = "force-dynamic";

type SearchParams = { [key: string]: string | string[] | undefined };

interface Props {
  searchParams: Promise<SearchParams>;
}

function first(value: string | string[] | undefined): string {
  if (Array.isArray(value)) return value[0] ?? "";
  return value ?? "";
}

export async function generateMetadata({
  searchParams,
}: Props): Promise<Metadata> {
  const q = first((await searchParams).q);
  return { title: q ? `Search: ${q}` : "Search" };
}

export default async function SearchPage({ searchParams }: Props) {
  const sp = await searchParams;
  const q = first(sp.q).trim();
  const page = first(sp.page) || "1";
  const sort = first(sp.sort);

  const extra: Record<string, string> = { page, pageSize: "24" };
  if (sort) extra.sort = sort;

  const results = q ? await searchProducts(q, extra) : EMPTY_PAGED;

  const urlParams: Record<string, string> = { q, page };
  if (sort) urlParams.sort = sort;

  return (
    <div className="mx-auto max-w-7xl px-4 py-6">
      <div className="mb-5 flex flex-wrap items-center justify-between gap-3">
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
        <SortSelect basePath="/search" />
      </div>

      {results.items.length > 0 ? (
        <>
          <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5">
            {results.items.map((p) => (
              <ProductCard key={p.id} product={p} />
            ))}
          </div>
          <Pagination
            basePath="/search"
            params={urlParams}
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
