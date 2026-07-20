"use client";

import { useEffect, useRef, useState } from "react";
import { trackAddToCart } from "@/lib/analytics";
import { useCart } from "./Providers";

interface Props {
  productId: string;
  stock: number;
  quantity?: number;
  /** Smaller styling for product cards. */
  compact?: boolean;
  className?: string;
  /** Analytics-only metadata (funnel step 2). Safe to omit. */
  productName?: string;
  price?: number;
  source?: "product_page" | "product_card";
}

/**
 * Real add-to-cart button with inline feedback.
 * Disabled when out of stock; shows "Added ✓" briefly on success and the
 * API error message (e.g. exceeding stock) on failure.
 */
export default function AddToCartButton({
  productId,
  stock,
  quantity = 1,
  compact = false,
  className = "",
  productName,
  price,
  source = "product_card",
}: Props) {
  const { addItem } = useCart();
  const [busy, setBusy] = useState(false);
  const [added, setAdded] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    return () => {
      if (timer.current) clearTimeout(timer.current);
    };
  }, []);

  const outOfStock = stock <= 0;

  async function onClick(e: React.MouseEvent) {
    // Cards render this inside/near a Link — never navigate.
    e.preventDefault();
    e.stopPropagation();
    if (busy || outOfStock) return;
    setBusy(true);
    setError(null);
    try {
      await addItem(productId, quantity);
      // Funnel step 2 — only on a confirmed success, never on a failed add.
      trackAddToCart(
        { id: productId, name: productName, price },
        quantity,
        source
      );
      setAdded(true);
      if (timer.current) clearTimeout(timer.current);
      timer.current = setTimeout(() => setAdded(false), 1800);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not add to cart.");
      if (timer.current) clearTimeout(timer.current);
      timer.current = setTimeout(() => setError(null), 3500);
    } finally {
      setBusy(false);
    }
  }

  const base = compact
    ? "w-full rounded-md py-2 text-xs font-semibold transition"
    : "w-full rounded-md py-3 text-sm font-semibold transition";

  const label = outOfStock
    ? "Out of Stock"
    : busy
      ? "Adding..."
      : added
        ? "Added ✓"
        : "Add to Cart";

  return (
    <div className={className}>
      <button
        type="button"
        disabled={outOfStock || busy}
        onClick={onClick}
        className={`${base} ${
          added
            ? "bg-green-600 text-white"
            : "bg-orange-500 text-white hover:bg-orange-600"
        } disabled:cursor-not-allowed ${
          outOfStock ? "disabled:bg-slate-300" : "disabled:opacity-70"
        }`}
      >
        {label}
      </button>
      {error && (
        <p className="mt-1.5 text-xs font-medium text-red-600">{error}</p>
      )}
    </div>
  );
}
