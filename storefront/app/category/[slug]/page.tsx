import type { Metadata } from "next";
import Link from "next/link";
import { getCategoryFilters, getCategoryProducts } from "@/lib/api";
import FilterSidebar from "@/components/FilterSidebar";
import Pagination from "@/components/Pagination";
import ProductCard from "@/components/ProductCard";
import SortSelect from "@/components/SortSelect";

export const dynamic = "force-dynamic";

type SearchParams = { [key: string]: string | string[] | undefined };

interface Props {
  params: Promise<{ slug: string }>;
  searchParams: Promise<SearchParams>;
}

/** Flatten Next's searchParams into a simple string map (arrays -> csv). */
function normalizeParams(sp: SearchParams): Record<string, string> {
  const out: Record<string, string> = {};
  for (const [key, value] of Object.entries(sp)) {
    if (value === undefined) continue;
    out[key] = Array.isArray(value) ? value.join(",") : value;
  }
  return out;
}

function titleFromSlug(slug: string): string {
  return slug
    .split("-")
    .map((w) => (w ? w[0].toUpperCase() + w.slice(1) : w))
    .join(" ");
}

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const { slug } = await params;
  return {
    title: titleFromSlug(slug),
    description: `Shop ${titleFromSlug(slug)} at ByteBazaar — best PKR prices on PC hardware and electronics.`,
  };
}

export default async function CategoryPage({ params, searchParams }: Props) {
  const { slug } = await params;
  const query = normalizeParams(await searchParams);
  if (!query.page) query.page = "1";
  if (!query.pageSize) query.pageSize = "24";

  const [filters, products] = await Promise.all([
    getCategoryFilters(slug),
    getCategoryProducts(slug, query),
  ]);

  const basePath = `/category/${slug}`;
  const title = titleFromSlug(slug);

  return (
    <div className="mx-auto max-w-7xl px-4 py-6">
      <nav className="mb-4 text-xs text-slate-500" aria-label="Breadcrumb">
        <Link href="/" className="hover:text-orange-600">
          Home
        </Link>{" "}
        / <span className="text-slate-700">{title}</span>
      </nav>

      <div className="mb-5 flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold text-blue-950">{title}</h1>
          <p className="mt-0.5 text-sm text-slate-500">
            {products.totalCount} product{products.totalCount === 1 ? "" : "s"}
          </p>
        </div>
        <SortSelect basePath={basePath} />
      </div>

      <div className="flex flex-col gap-6 lg:flex-row">
        <FilterSidebar basePath={basePath} filters={filters} />

        <div className="min-w-0 flex-1">
          {products.items.length > 0 ? (
            <>
              <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 xl:grid-cols-4">
                {products.items.map((p) => (
                  <ProductCard key={p.id} product={p} />
                ))}
              </div>
              <Pagination
                basePath={basePath}
                params={query}
                page={products.page}
                pageSize={products.pageSize}
                totalCount={products.totalCount}
              />
            </>
          ) : (
            <div className="rounded-lg border border-dashed border-slate-300 bg-white p-16 text-center">
              <p className="text-sm font-medium text-slate-600">
                No products found.
              </p>
              <p className="mt-1 text-xs text-slate-400">
                Try removing some filters or check back later.
              </p>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
