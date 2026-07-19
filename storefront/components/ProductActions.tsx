"use client";

import { useState } from "react";
import AddToCartButton from "./AddToCartButton";
import WishlistButton from "./WishlistButton";

interface Props {
  productId: string;
  stock: number;
}

/** Quantity stepper + add-to-cart + wishlist heart for the product page. */
export default function ProductActions({ productId, stock }: Props) {
  const [quantity, setQuantity] = useState(1);
  const inStock = stock > 0;
  const max = Math.max(stock, 1);

  function clamp(value: number) {
    return Math.min(Math.max(value, 1), max);
  }

  return (
    <div className="mt-4">
      {inStock && (
        <div className="mb-3 flex items-center gap-3">
          <span className="text-sm font-medium text-slate-600">Quantity</span>
          <div className="flex items-center rounded-md border border-slate-300">
            <button
              type="button"
              aria-label="Decrease quantity"
              onClick={() => setQuantity((q) => clamp(q - 1))}
              disabled={quantity <= 1}
              className="px-3 py-1.5 text-lg leading-none text-slate-600 transition hover:bg-slate-100 disabled:cursor-not-allowed disabled:text-slate-300"
            >
              −
            </button>
            <span className="min-w-10 border-x border-slate-300 px-2 py-1.5 text-center text-sm font-semibold">
              {quantity}
            </span>
            <button
              type="button"
              aria-label="Increase quantity"
              onClick={() => setQuantity((q) => clamp(q + 1))}
              disabled={quantity >= max}
              className="px-3 py-1.5 text-lg leading-none text-slate-600 transition hover:bg-slate-100 disabled:cursor-not-allowed disabled:text-slate-300"
            >
              +
            </button>
          </div>
          <span className="text-xs text-slate-400">max {stock}</span>
        </div>
      )}

      <div className="flex items-start gap-3">
        <AddToCartButton
          productId={productId}
          stock={stock}
          quantity={quantity}
          className="flex-1"
        />
        <WishlistButton
          productId={productId}
          className="mt-0.5 h-11 w-11 shrink-0 border border-slate-200"
        />
      </div>
    </div>
  );
}
