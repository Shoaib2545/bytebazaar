import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { getProduct } from "@/lib/api";
import { formatPrice } from "@/lib/format";
import { SITE_NAME, absoluteUrl, breadcrumbJsonLd } from "@/lib/seo";
import ProductGallery from "@/components/ProductGallery";
import ProductActions from "@/components/ProductActions";
import ProductViewTracker from "@/components/ProductViewTracker";

export const dynamic = "force-dynamic";

interface Props {
  params: Promise<{ slug: string }>;
}

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const { slug } = await params;
  const product = await getProduct(slug);

  // The API may be unreachable (force-dynamic + graceful fallbacks) — never
  // let a missing product leak a half-built canonical into the index.
  if (!product) {
    return { title: "Product not found", robots: { index: false, follow: false } };
  }

  const title = product.metaTitle || product.name;
  const description =
    product.metaDescription ||
    product.description?.slice(0, 160) ||
    `Buy ${product.name} at ${SITE_NAME} — genuine stock, best PKR price, nationwide delivery.`;
  const canonical = absoluteUrl(`/product/${product.slug}`);

  return {
    title,
    description,
    alternates: { canonical },
    robots: { index: true, follow: true },
    openGraph: {
      // "product" isn't in Next's OpenGraphType union; "website" is the
      // closest valid value, and the schema.org Product JSON-LD below is what
      // search engines actually read for commerce data.
      type: "website",
      title,
      description,
      url: canonical,
      siteName: SITE_NAME,
      images: product.images.length
        ? product.images.slice(0, 4).map((url) => ({ url, alt: product.name }))
        : undefined,
    },
    twitter: {
      card: "summary_large_image",
      title,
      description,
      images: product.images.slice(0, 1),
    },
  };
}

export default async function ProductPage({ params }: Props) {
  const { slug } = await params;
  const product = await getProduct(slug);
  if (!product) notFound();

  const onSale =
    product.salePrice != null && product.salePrice < product.price;
  const effectivePrice = onSale ? product.salePrice! : product.price;
  const inStock = product.stock > 0;

  const canonical = absoluteUrl(`/product/${product.slug}`);

  const productJsonLd = {
    "@context": "https://schema.org",
    "@type": "Product",
    name: product.name,
    description: product.metaDescription || product.description || undefined,
    image: product.images,
    sku: product.id,
    url: canonical,
    category: product.categoryName ?? undefined,
    brand: product.brandName
      ? { "@type": "Brand", name: product.brandName }
      : undefined,
    additionalProperty: product.attributes.length
      ? product.attributes.map((a) => ({
          "@type": "PropertyValue",
          name: a.name,
          value: a.value,
        }))
      : undefined,
    offers: {
      "@type": "Offer",
      priceCurrency: "PKR",
      price: effectivePrice,
      availability: inStock
        ? "https://schema.org/InStock"
        : "https://schema.org/OutOfStock",
      itemCondition: "https://schema.org/NewCondition",
      // Rich results require an absolute offer URL.
      url: canonical,
      seller: { "@type": "Organization", name: SITE_NAME },
    },
  };

  const breadcrumbs = [{ name: "Home", path: "/" }];
  if (product.categorySlug) {
    breadcrumbs.push({
      name: product.categoryName ?? product.categorySlug,
      path: `/category/${product.categorySlug}`,
    });
  }
  breadcrumbs.push({ name: product.name, path: `/product/${product.slug}` });

  return (
    <div className="mx-auto max-w-7xl px-4 py-6">
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(productJsonLd) }}
      />
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{
          __html: JSON.stringify(breadcrumbJsonLd(breadcrumbs)),
        }}
      />
      {/* Funnel step 1 — no-op unless NEXT_PUBLIC_POSTHOG_KEY is set. */}
      <ProductViewTracker
        product={{
          id: product.id,
          name: product.name,
          slug: product.slug,
          price: effectivePrice,
          brand: product.brandName,
          category: product.categoryName,
        }}
      />

      <nav className="mb-4 text-xs text-slate-500" aria-label="Breadcrumb">
        <Link href="/" className="hover:text-orange-600">
          Home
        </Link>
        {product.categorySlug && (
          <>
            {" / "}
            <Link
              href={`/category/${product.categorySlug}`}
              className="hover:text-orange-600"
            >
              {product.categoryName ?? product.categorySlug}
            </Link>
          </>
        )}
        {" / "}
        <span className="text-slate-700">{product.name}</span>
      </nav>

      <div className="grid gap-8 lg:grid-cols-2">
        <ProductGallery images={product.images} name={product.name} />

        <div>
          {product.brandName && (
            <p className="text-sm font-semibold uppercase tracking-wide text-orange-600">
              {product.brandName}
            </p>
          )}
          <h1 className="mt-1 text-2xl font-bold text-blue-950 sm:text-3xl">
            {product.name}
          </h1>

          <div className="mt-4 flex items-center gap-3">
            {inStock ? (
              <span className="inline-flex items-center gap-1.5 rounded-full bg-green-100 px-3 py-1 text-xs font-semibold text-green-700">
                <span className="h-1.5 w-1.5 rounded-full bg-green-500" />
                In stock ({product.stock} available)
              </span>
            ) : (
              <span className="inline-flex items-center gap-1.5 rounded-full bg-slate-200 px-3 py-1 text-xs font-semibold text-slate-600">
                <span className="h-1.5 w-1.5 rounded-full bg-slate-400" />
                Out of stock
              </span>
            )}
          </div>

          <div className="mt-5 rounded-lg border border-slate-200 bg-white p-5">
            <div className="flex items-baseline gap-3">
              <span className="text-3xl font-extrabold text-blue-950">
                {formatPrice(effectivePrice)}
              </span>
              {onSale && (
                <>
                  <span className="text-lg text-slate-400 line-through">
                    {formatPrice(product.price)}
                  </span>
                  <span className="rounded bg-orange-500 px-2 py-0.5 text-xs font-bold text-white">
                    SAVE {formatPrice(product.price - effectivePrice)}
                  </span>
                </>
              )}
            </div>
            <ProductActions
              productId={product.id}
              productName={product.name}
              price={effectivePrice}
              stock={product.stock}
            />
          </div>

          {product.attributes.length > 0 && (
            <div className="mt-6">
              <h2 className="mb-2 text-sm font-bold uppercase tracking-wide text-blue-950">
                Specifications
              </h2>
              <table className="w-full overflow-hidden rounded-lg border border-slate-200 text-sm">
                <tbody>
                  {product.attributes.map((attr, i) => (
                    <tr
                      key={`${attr.name}-${i}`}
                      className={i % 2 === 0 ? "bg-white" : "bg-slate-50"}
                    >
                      <th className="w-1/3 border-b border-slate-100 px-4 py-2.5 text-left font-medium text-slate-500">
                        {attr.name}
                      </th>
                      <td className="border-b border-slate-100 px-4 py-2.5 text-slate-800">
                        {attr.value}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>

      {product.description && (
        <section className="mt-10 rounded-lg border border-slate-200 bg-white p-6">
          <h2 className="mb-3 text-lg font-bold text-blue-950">Description</h2>
          <div className="whitespace-pre-line text-sm leading-relaxed text-slate-700">
            {product.description}
          </div>
        </section>
      )}
    </div>
  );
}
