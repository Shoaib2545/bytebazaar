"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import ProductCard from "@/components/ProductCard";
import { useWishlist } from "@/components/Providers";
import { getWishlist, removeFromWishlist } from "@/lib/account";
import type { ProductListItem } from "@/lib/api";

export default function WishlistPage() {
  const { refresh } = useWishlist();
  const [items, setItems] = useState<ProductListItem[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    getWishlist()
      .then((res) => {
        if (!cancelled) setItems(res);
      })
      .catch((err) => {
        if (!cancelled)
          setError(
            err instanceof Error ? err.message : "Could not load your wishlist."
          );
      });
    return () => {
      cancelled = true;
    };
  }, []);

  async function onRemove(productId: string) {
    try {
      await removeFromWishlist(productId);
      setItems((prev) => prev?.filter((p) => p.id !== productId) ?? prev);
      refresh(); // keep the shared heart state in sync
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Could not remove the item."
      );
    }
  }

  return (
    <div>
      <h1 className="text-xl font-bold text-blue-950">My Wishlist</h1>

      {error && (
        <div className="mt-4 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {error}
        </div>
      )}

      {items === null && !error ? (
        <div className="mt-6 rounded-lg border border-slate-200 bg-white p-10 text-center text-sm text-slate-400">
          Loading wishlist...
        </div>
      ) : items && items.length === 0 ? (
        <div className="mt-6 rounded-lg border border-slate-200 bg-white p-10 text-center">
          <p className="text-sm font-semibold text-slate-700">
            Your wishlist is empty
          </p>
          <p className="mt-1 text-sm text-slate-500">
            Tap the heart on any product to save it for later.
          </p>
          <Link
            href="/"
            className="mt-5 inline-block rounded-md bg-orange-500 px-6 py-2.5 text-sm font-semibold text-white transition hover:bg-orange-600"
          >
            Browse Products
          </Link>
        </div>
      ) : (
        <div className="mt-6 grid grid-cols-2 gap-4 md:grid-cols-3 xl:grid-cols-4">
          {(items ?? []).map((product) => (
            <div key={product.id} className="flex flex-col gap-2">
              <ProductCard product={product} />
              <button
                type="button"
                onClick={() => onRemove(product.id)}
                className="rounded-md border border-slate-300 py-1.5 text-xs font-semibold text-slate-500 transition hover:border-red-300 hover:text-red-600"
              >
                Remove from Wishlist
              </button>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
