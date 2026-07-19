import Link from "next/link";
import { ProductListItem } from "@/lib/api";
import { formatPrice } from "@/lib/format";

export default function ProductCard({ product }: { product: ProductListItem }) {
  const onSale =
    product.salePrice != null && product.salePrice < product.price;
  const outOfStock = product.stock <= 0;

  return (
    <Link
      href={`/product/${product.slug}`}
      className="group relative flex flex-col overflow-hidden rounded-lg border border-slate-200 bg-white transition hover:border-orange-400 hover:shadow-lg"
    >
      {onSale && !outOfStock && (
        <span className="absolute left-2 top-2 z-10 rounded bg-orange-500 px-2 py-0.5 text-xs font-bold text-white">
          SALE
        </span>
      )}
      {outOfStock && (
        <span className="absolute left-2 top-2 z-10 rounded bg-slate-700 px-2 py-0.5 text-xs font-bold text-white">
          Out of stock
        </span>
      )}

      <div className="flex aspect-square items-center justify-center overflow-hidden bg-slate-50 p-4">
        {product.imageUrl ? (
          // eslint-disable-next-line @next/next/no-img-element
          <img
            src={product.imageUrl}
            alt={product.name}
            className={`h-full w-full object-contain transition duration-200 group-hover:scale-105 ${
              outOfStock ? "opacity-50 grayscale" : ""
            }`}
            loading="lazy"
          />
        ) : (
          <div className="flex h-full w-full items-center justify-center text-slate-300">
            <svg
              xmlns="http://www.w3.org/2000/svg"
              fill="none"
              viewBox="0 0 24 24"
              strokeWidth={1}
              stroke="currentColor"
              className="h-16 w-16"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="m2.25 15.75 5.159-5.159a2.25 2.25 0 0 1 3.182 0l5.159 5.159m-1.5-1.5 1.409-1.409a2.25 2.25 0 0 1 3.182 0l2.909 2.909M3.75 21h16.5A1.5 1.5 0 0 0 21.75 19.5V4.5A1.5 1.5 0 0 0 20.25 3H3.75A1.5 1.5 0 0 0 2.25 4.5v15A1.5 1.5 0 0 0 3.75 21Z"
              />
            </svg>
          </div>
        )}
      </div>

      <div className="flex flex-1 flex-col gap-1 p-3">
        {product.brandName && (
          <span className="text-xs font-medium uppercase tracking-wide text-slate-400">
            {product.brandName}
          </span>
        )}
        <h3 className="line-clamp-2 text-sm font-medium text-slate-800 group-hover:text-blue-900">
          {product.name}
        </h3>
        <div className="mt-auto flex items-baseline gap-2 pt-2">
          {onSale ? (
            <>
              <span className="text-base font-bold text-orange-600">
                {formatPrice(product.salePrice!)}
              </span>
              <span className="text-xs text-slate-400 line-through">
                {formatPrice(product.price)}
              </span>
            </>
          ) : (
            <span className="text-base font-bold text-blue-950">
              {formatPrice(product.price)}
            </span>
          )}
        </div>
      </div>
    </Link>
  );
}
