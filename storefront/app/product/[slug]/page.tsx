import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { getProduct } from "@/lib/api";
import { formatPrice } from "@/lib/format";
import ProductGallery from "@/components/ProductGallery";

export const dynamic = "force-dynamic";

interface Props {
  params: Promise<{ slug: string }>;
}

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const { slug } = await params;
  const product = await getProduct(slug);
  if (!product) return { title: "Product not found" };
  return {
    title: product.metaTitle || product.name,
    description:
      product.metaDescription ||
      product.description?.slice(0, 160) ||
      `Buy ${product.name} at ByteBazaar.`,
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

  const jsonLd = {
    "@context": "https://schema.org",
    "@type": "Product",
    name: product.name,
    description: product.metaDescription || product.description || undefined,
    image: product.images,
    sku: product.id,
    brand: product.brandName
      ? { "@type": "Brand", name: product.brandName }
      : undefined,
    offers: {
      "@type": "Offer",
      priceCurrency: "PKR",
      price: effectivePrice,
      availability: inStock
        ? "https://schema.org/InStock"
        : "https://schema.org/OutOfStock",
      url: `/product/${product.slug}`,
    },
  };

  return (
    <div className="mx-auto max-w-7xl px-4 py-6">
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }}
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
            <button
              type="button"
              disabled={!inStock}
              className="mt-4 w-full rounded-md bg-orange-500 py-3 text-sm font-semibold text-white transition hover:bg-orange-600 disabled:cursor-not-allowed disabled:bg-slate-300"
              title={inStock ? "Cart coming soon" : "Out of stock"}
            >
              {inStock ? "Add to Cart" : "Out of Stock"}
            </button>
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
