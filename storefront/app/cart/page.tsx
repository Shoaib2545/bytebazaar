"use client";

import Link from "next/link";
import { FormEvent, useState } from "react";
import { useCart } from "@/components/Providers";
import { CartItem } from "@/lib/cart";
import { formatPrice } from "@/lib/format";

function CartRow({ item }: { item: CartItem }) {
  const { updateItem, removeItem } = useCart();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function setQuantity(quantity: number) {
    if (busy) return;
    setBusy(true);
    setError(null);
    try {
      await updateItem(item.productId, quantity);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not update item.");
    } finally {
      setBusy(false);
    }
  }

  async function onRemove() {
    if (busy) return;
    setBusy(true);
    setError(null);
    try {
      await removeItem(item.productId);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not remove item.");
      setBusy(false);
    }
  }

  return (
    <div className="flex gap-4 border-b border-slate-100 py-4 last:border-b-0">
      <Link
        href={`/product/${item.slug}`}
        className="flex h-20 w-20 shrink-0 items-center justify-center overflow-hidden rounded-md border border-slate-100 bg-slate-50"
      >
        {item.imageUrl ? (
          // eslint-disable-next-line @next/next/no-img-element
          <img
            src={item.imageUrl}
            alt={item.name}
            className="h-full w-full object-contain p-1"
          />
        ) : (
          <span className="text-xs text-slate-300">No image</span>
        )}
      </Link>

      <div className="flex flex-1 flex-col">
        <div className="flex items-start justify-between gap-3">
          <Link
            href={`/product/${item.slug}`}
            className="line-clamp-2 text-sm font-medium text-slate-800 hover:text-orange-600"
          >
            {item.name}
          </Link>
          <button
            type="button"
            onClick={onRemove}
            disabled={busy}
            aria-label={`Remove ${item.name}`}
            className="shrink-0 text-slate-400 transition hover:text-red-500 disabled:opacity-50"
          >
            <svg
              xmlns="http://www.w3.org/2000/svg"
              fill="none"
              viewBox="0 0 24 24"
              strokeWidth={1.5}
              stroke="currentColor"
              className="h-5 w-5"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="m14.74 9-.346 9m-4.788 0L9.26 9m9.968-3.21c.342.052.682.107 1.022.166m-1.022-.165L18.16 19.673a2.25 2.25 0 0 1-2.244 2.077H8.084a2.25 2.25 0 0 1-2.244-2.077L4.772 5.79m14.456 0a48.108 48.108 0 0 0-3.478-.397m-12 .562c.34-.059.68-.114 1.022-.165m0 0a48.11 48.11 0 0 1 3.478-.397m7.5 0v-.916c0-1.18-.91-2.164-2.09-2.201a51.964 51.964 0 0 0-3.32 0c-1.18.037-2.09 1.022-2.09 2.201v.916m7.5 0a48.667 48.667 0 0 0-7.5 0"
              />
            </svg>
          </button>
        </div>

        <p className="mt-1 text-xs text-slate-400">
          {formatPrice(item.unitPrice)} each
          {item.stock <= 5 && item.stock > 0 && (
            <span className="ml-2 font-medium text-orange-600">
              Only {item.stock} left
            </span>
          )}
        </p>

        <div className="mt-auto flex items-center justify-between pt-2">
          <div className="flex items-center rounded-md border border-slate-300">
            <button
              type="button"
              aria-label="Decrease quantity"
              onClick={() => setQuantity(item.quantity - 1)}
              disabled={busy}
              className="px-2.5 py-1 text-base leading-none text-slate-600 transition hover:bg-slate-100 disabled:opacity-50"
            >
              −
            </button>
            <span className="min-w-9 border-x border-slate-300 px-2 py-1 text-center text-sm font-semibold">
              {item.quantity}
            </span>
            <button
              type="button"
              aria-label="Increase quantity"
              onClick={() => setQuantity(item.quantity + 1)}
              disabled={busy || item.quantity >= item.stock}
              className="px-2.5 py-1 text-base leading-none text-slate-600 transition hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-50"
            >
              +
            </button>
          </div>
          <span className="text-sm font-bold text-blue-950">
            {formatPrice(item.lineTotal)}
          </span>
        </div>

        {error && (
          <p className="mt-1.5 text-xs font-medium text-red-600">{error}</p>
        )}
      </div>
    </div>
  );
}

function CouponBox() {
  const { cart, applyCoupon, removeCoupon } = useCart();
  const [code, setCode] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function onApply(e: FormEvent) {
    e.preventDefault();
    const trimmed = code.trim();
    if (!trimmed || busy) return;
    setBusy(true);
    setError(null);
    try {
      await applyCoupon(trimmed);
      setCode("");
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Could not apply the coupon."
      );
    } finally {
      setBusy(false);
    }
  }

  async function onRemove() {
    if (busy) return;
    setBusy(true);
    setError(null);
    try {
      await removeCoupon();
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Could not remove the coupon."
      );
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="mt-4 border-t border-slate-100 pt-4">
      <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">
        Coupon
      </p>
      {cart.couponCode ? (
        <div className="mt-2 flex items-center justify-between gap-2">
          <span className="inline-flex items-center gap-1.5 rounded-full bg-green-50 px-3 py-1 text-xs font-bold uppercase tracking-wide text-green-700 ring-1 ring-inset ring-green-200">
            <svg
              xmlns="http://www.w3.org/2000/svg"
              fill="none"
              viewBox="0 0 24 24"
              strokeWidth={2}
              stroke="currentColor"
              className="h-3.5 w-3.5"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="m4.5 12.75 6 6 9-13.5"
              />
            </svg>
            {cart.couponCode}
          </span>
          <button
            type="button"
            onClick={onRemove}
            disabled={busy}
            className="text-xs font-medium text-slate-400 transition hover:text-red-500 disabled:opacity-50"
          >
            Remove
          </button>
        </div>
      ) : (
        <form onSubmit={onApply} className="mt-2 flex gap-2">
          <input
            type="text"
            value={code}
            onChange={(e) => setCode(e.target.value)}
            placeholder="Coupon code"
            aria-label="Coupon code"
            className="w-full min-w-0 rounded-md border border-slate-300 px-3 py-2 text-sm uppercase placeholder:normal-case focus:border-orange-500 focus:outline-none focus:ring-1 focus:ring-orange-500"
          />
          <button
            type="submit"
            disabled={busy || !code.trim()}
            className="shrink-0 rounded-md border border-blue-950 px-4 py-2 text-sm font-semibold text-blue-950 transition hover:bg-blue-950 hover:text-white disabled:cursor-not-allowed disabled:opacity-50"
          >
            {busy ? "..." : "Apply"}
          </button>
        </form>
      )}
      {error && (
        <p className="mt-1.5 text-xs font-medium text-red-600">{error}</p>
      )}
    </div>
  );
}

export default function CartPage() {
  const { cart, loading } = useCart();

  return (
    <div className="mx-auto max-w-4xl px-4 py-8">
      <h1 className="text-2xl font-bold text-blue-950">Shopping Cart</h1>

      {loading ? (
        <div className="mt-8 rounded-lg border border-slate-200 bg-white p-10 text-center text-sm text-slate-400">
          Loading your cart...
        </div>
      ) : cart.items.length === 0 ? (
        <div className="mt-8 rounded-lg border border-slate-200 bg-white p-10 text-center">
          <p className="text-lg font-semibold text-slate-700">
            Your cart is empty
          </p>
          <p className="mt-1 text-sm text-slate-500">
            Browse the store and add products you like.
          </p>
          <Link
            href="/"
            className="mt-5 inline-block rounded-md bg-orange-500 px-6 py-2.5 text-sm font-semibold text-white transition hover:bg-orange-600"
          >
            Continue Shopping
          </Link>
        </div>
      ) : (
        <div className="mt-6 grid gap-6 lg:grid-cols-3">
          <div className="rounded-lg border border-slate-200 bg-white px-5 py-1 lg:col-span-2">
            {cart.items.map((item) => (
              <CartRow key={item.productId} item={item} />
            ))}
          </div>

          <div className="h-fit rounded-lg border border-slate-200 bg-white p-5">
            <h2 className="text-sm font-bold uppercase tracking-wide text-blue-950">
              Order Summary
            </h2>
            <dl className="mt-4 space-y-2 text-sm">
              <div className="flex justify-between">
                <dt className="text-slate-500">
                  Subtotal ({cart.itemCount}{" "}
                  {cart.itemCount === 1 ? "item" : "items"})
                </dt>
                <dd className="font-semibold text-slate-800">
                  {formatPrice(cart.subtotal)}
                </dd>
              </div>
              {cart.discount > 0 && (
                <div className="flex justify-between">
                  <dt className="text-slate-500">
                    Discount
                    {cart.couponCode ? ` (${cart.couponCode})` : ""}
                  </dt>
                  <dd className="font-semibold text-green-600">
                    −{formatPrice(cart.discount)}
                  </dd>
                </div>
              )}
              <div className="flex justify-between">
                <dt className="text-slate-500">Shipping</dt>
                <dd className="text-slate-400">Calculated at checkout</dd>
              </div>
              <div className="flex justify-between border-t border-slate-100 pt-2 text-base">
                <dt className="font-bold text-blue-950">Total</dt>
                <dd className="font-extrabold text-blue-950">
                  {formatPrice(cart.total)}
                </dd>
              </div>
            </dl>

            <CouponBox />

            <Link
              href="/checkout"
              className="mt-5 block w-full rounded-md bg-orange-500 py-3 text-center text-sm font-semibold text-white transition hover:bg-orange-600"
            >
              Proceed to Checkout
            </Link>
            <Link
              href="/"
              className="mt-2 block text-center text-xs text-slate-500 hover:text-orange-600"
            >
              Continue shopping
            </Link>
          </div>
        </div>
      )}
    </div>
  );
}
