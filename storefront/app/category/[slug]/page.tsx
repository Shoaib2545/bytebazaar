import type { Metadata } from "next";
import Link from "next/link";
import {
  getCategoryFilters,
  getCategoryProducts,
  getCategoryTree,
} from "@/lib/api";
import {
  SITE_NAME,
  breadcrumbJsonLd,
  findCategory,
  listingSeo,
  titleFromSlug,
} from "@/lib/seo";
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

/**
 * The live category node, or null when the API is down / the slug is unknown.
 * `getCategoryTree` is React-cached, so the metadata and page calls share one
 * upstream request.
 */
async function categoryNode(slug: string) {
  return findCategory(await getCategoryTree(), slug);
}

/** Real category name from the live tree, falling back to the slug. */
async function categoryName(slug: string): Promise<string> {
  return (await categoryNode(slug))?.name ?? titleFromSlug(slug);
}

/** Trimmed non-empty string, or null. Guards against "" / whitespace-only. */
function text(value: string | null | undefined): string | null {
  const trimmed = value?.trim();
  return trimmed ? trimmed : null;
}

export async function generateMetadata({
  params,
  searchParams,
}: Props): Promise<Metadata> {
  const { slug } = await params;
  const sp = await searchParams;
  const node = await categoryNode(slug);
  const name = node?.name ?? titleFromSlug(slug);

  // Canonical always points at the unfiltered category; deep filter
  // permutations get noindex,follow. See lib/seo.ts for the full policy.
  const { canonical, robots } = listingSeo(`/category/${slug}`, sp);

  // Admin-authored SEO overrides are optional on the DTO — older API builds
  // omit the fields entirely — so treat absent, null and blank identically and
  // fall back to the templated copy.
  const metaTitle = text(node?.metaTitle);
  const metaDescription = text(node?.metaDescription);

  const description =
    metaDescription ??
    `Shop ${name} at ${SITE_NAME} — best PKR prices on PC hardware and electronics. Genuine stock, nationwide delivery.`;

  return {
    // An override replaces the whole title, bypassing the "%s | ByteBazaar"
    // template so admins get exactly the string they typed.
    title: metaTitle ? { absolute: metaTitle } : name,
    description,
    alternates: { canonical },
    robots,
    openGraph: {
      type: "website",
      title: metaTitle ?? `${name} | ${SITE_NAME}`,
      description,
      url: canonical,
      siteName: SITE_NAME,
    },
    twitter: {
      card: "summary_large_image",
      title: metaTitle ?? `${name} | ${SITE_NAME}`,
      description,
    },
  };
}

export default async function CategoryPage({ params, searchParams }: Props) {
  const { slug } = await params;
  // `urlParams` is exactly what is in the address bar — it is what the
  // server-rendered filter/sort links build their hrefs from, so a link never
  // invents a `page=1&pageSize=24` that the shopper did not ask for.
  // `query` adds the API's paging defaults on top.
  const urlParams = normalizeParams(await searchParams);
  const query = { ...urlParams };
  if (!query.page) query.page = "1";
  if (!query.pageSize) query.pageSize = "24";

  const [filters, products, title] = await Promise.all([
    getCategoryFilters(slug),
    getCategoryProducts(slug, query),
    categoryName(slug),
  ]);

  const basePath = `/category/${slug}`;

  const jsonLd = breadcrumbJsonLd([
    { name: "Home", path: "/" },
    { name: title, path: basePath },
  ]);

  return (
    <div className="mx-auto max-w-7xl px-4 py-6">
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }}
      />

      <nav className="mb-4 text-xs text-slate-500" aria-label="Breadcrumb">
        <Link href="/" className="hover:text-orange-600">
          Home
        </Link>{" "}
        / <span className="text-slate-700">{title}</span>
      </nav>

      {/* min-h reserves the header row so the sort control hydrating in does
          not shove the product grid down (CLS). */}
      <div className="mb-5 flex min-h-14 flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold text-blue-950">{title}</h1>
          <p className="mt-0.5 text-sm text-slate-500">
            {products.totalCount} product{products.totalCount === 1 ? "" : "s"}
          </p>
        </div>
        <SortSelect basePath={basePath} params={urlParams} />
      </div>

      <div className="flex flex-col gap-6 lg:flex-row">
        <FilterSidebar
          basePath={basePath}
          filters={filters}
          params={urlParams}
        />

        <div className="min-w-0 flex-1">
          {products.items.length > 0 ? (
            <>
              <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 xl:grid-cols-4">
                {products.items.map((p, i) => (
                  // The first row is above the fold on mobile — its image is
                  // the LCP candidate, so load it eagerly rather than lazily.
                  // The mobile grid is 2-up, so that row is exactly 2 cards:
                  // marking more than that just puts extra high-priority
                  // requests ahead of the render-blocking assets.
                  <ProductCard key={p.id} product={p} eager={i < 2} />
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
