import Link from "next/link";
import {
  firstLeafCategory,
  getBanners,
  getCategoryProducts,
  getCategoryTree,
  getFeaturedProducts,
} from "@/lib/api";
import HeroCarousel from "@/components/HeroCarousel";
import ProductCard from "@/components/ProductCard";

export const dynamic = "force-dynamic";

export default async function HomePage() {
  const [tree, banners, featured] = await Promise.all([
    getCategoryTree(),
    getBanners(),
    getFeaturedProducts(8),
  ]);
  const leaf = firstLeafCategory(tree);
  const latest = leaf
    ? await getCategoryProducts(leaf.slug, {
        sort: "newest",
        page: "1",
        pageSize: "8",
      })
    : null;

  const heroBanners = banners
    .filter((b) => b.placement === "Hero")
    .sort((a, b) => a.sortOrder - b.sortOrder);
  const stripBanners = banners
    .filter((b) => b.placement === "Strip")
    .sort((a, b) => a.sortOrder - b.sortOrder);

  return (
    <div className="mx-auto max-w-7xl px-4 py-8">
      {/* Hero — real banners when available, static fallback otherwise */}
      {heroBanners.length > 0 ? (
        <HeroCarousel banners={heroBanners} />
      ) : (
        <section className="relative overflow-hidden rounded-2xl bg-gradient-to-r from-blue-950 via-blue-900 to-blue-950 px-8 py-14 text-white sm:px-12">
          <div className="absolute -right-20 -top-20 h-64 w-64 rounded-full bg-orange-500/20 blur-3xl" />
          <div className="absolute -bottom-24 right-32 h-48 w-48 rounded-full bg-orange-500/10 blur-2xl" />
          <p className="text-sm font-semibold uppercase tracking-widest text-orange-400">
            Build your dream rig
          </p>
          <h1 className="mt-2 max-w-xl text-3xl font-extrabold leading-tight sm:text-5xl">
            PC Hardware &amp; Electronics at{" "}
            <span className="text-orange-500">Bazaar</span> Prices
          </h1>
          <p className="mt-4 max-w-lg text-sm text-white/70 sm:text-base">
            Graphics cards, processors, laptops, peripherals and more — genuine
            stock, nationwide delivery, prices in PKR.
          </p>
          <div className="mt-6 flex flex-wrap gap-3">
            <Link
              href={leaf ? `/category/${leaf.slug}` : "/search?q=pc"}
              className="rounded-md bg-orange-500 px-6 py-3 text-sm font-semibold text-white transition hover:bg-orange-600"
            >
              Shop Now
            </Link>
            <Link
              href="/register"
              className="rounded-md border border-white/30 px-6 py-3 text-sm font-semibold text-white/90 transition hover:bg-white/10"
            >
              Create Account
            </Link>
          </div>
        </section>
      )}

      {/* Strip banners */}
      {stripBanners.length > 0 && (
        <section className="mt-4 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {stripBanners.map((banner) => {
            const inner = (
              <>
                {banner.imageUrl && (
                  // eslint-disable-next-line @next/next/no-img-element
                  <img
                    src={banner.imageUrl}
                    alt={banner.title}
                    className="absolute inset-0 h-full w-full object-cover"
                    loading="lazy"
                  />
                )}
                <div className="absolute inset-0 bg-blue-950/60" />
                <div className="relative z-10 px-5 py-4">
                  <p className="text-sm font-bold text-white">{banner.title}</p>
                  {banner.subtitle && (
                    <p className="mt-0.5 line-clamp-1 text-xs text-white/75">
                      {banner.subtitle}
                    </p>
                  )}
                </div>
              </>
            );
            const cls =
              "relative block h-20 overflow-hidden rounded-lg bg-blue-950 transition hover:opacity-90";
            return banner.linkUrl ? (
              <Link key={banner.id} href={banner.linkUrl} className={cls}>
                {inner}
              </Link>
            ) : (
              <div key={banner.id} className={cls}>
                {inner}
              </div>
            );
          })}
        </section>
      )}

      {/* Category tiles */}
      {tree.length > 0 && (
        <section className="mt-12">
          <h2 className="mb-4 text-xl font-bold text-blue-950">
            Shop by Category
          </h2>
          <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-6">
            {tree.slice(0, 12).map((cat) => (
              <Link
                key={cat.id}
                href={`/category/${cat.slug}`}
                className="group flex flex-col items-center gap-3 rounded-lg border border-slate-200 bg-white p-5 text-center transition hover:border-orange-400 hover:shadow-md"
              >
                <div className="flex h-16 w-16 items-center justify-center overflow-hidden rounded-full bg-slate-100">
                  {cat.imageUrl ? (
                    // eslint-disable-next-line @next/next/no-img-element
                    <img
                      src={cat.imageUrl}
                      alt={cat.name}
                      className="h-full w-full object-cover"
                      loading="lazy"
                    />
                  ) : (
                    <svg
                      xmlns="http://www.w3.org/2000/svg"
                      fill="none"
                      viewBox="0 0 24 24"
                      strokeWidth={1.5}
                      stroke="currentColor"
                      className="h-8 w-8 text-blue-950/60 transition group-hover:text-orange-500"
                    >
                      <path
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        d="M9 17.25v1.007a3 3 0 0 1-.879 2.122L7.5 21h9l-.621-.621A3 3 0 0 1 15 18.257V17.25m6-12V15a2.25 2.25 0 0 1-2.25 2.25H5.25A2.25 2.25 0 0 1 3 15V5.25m18 0A2.25 2.25 0 0 0 18.75 3H5.25A2.25 2.25 0 0 0 3 5.25m18 0V12a2.25 2.25 0 0 1-2.25 2.25H5.25A2.25 2.25 0 0 1 3 12V5.25"
                      />
                    </svg>
                  )}
                </div>
                <span className="text-sm font-medium text-slate-800 group-hover:text-orange-600">
                  {cat.name}
                </span>
              </Link>
            ))}
          </div>
        </section>
      )}

      {/* Featured products */}
      {featured.length > 0 && (
        <section className="mt-12">
          <h2 className="mb-4 text-xl font-bold text-blue-950">
            Featured Products
          </h2>
          <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4">
            {featured.map((p) => (
              <ProductCard key={p.id} product={p} />
            ))}
          </div>
        </section>
      )}

      {/* Latest products */}
      <section className="mt-12">
        <div className="mb-4 flex items-center justify-between">
          <h2 className="text-xl font-bold text-blue-950">Latest Products</h2>
          {leaf && (
            <Link
              href={`/category/${leaf.slug}?sort=newest`}
              className="text-sm font-medium text-orange-600 hover:underline"
            >
              View all ›
            </Link>
          )}
        </div>
        {latest && latest.items.length > 0 ? (
          <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4">
            {latest.items.map((p) => (
              <ProductCard key={p.id} product={p} />
            ))}
          </div>
        ) : (
          <div className="rounded-lg border border-dashed border-slate-300 bg-white p-10 text-center text-sm text-slate-500">
            Products are on their way — check back soon.
          </div>
        )}
      </section>
    </div>
  );
}
