"use client";

import Link from "next/link";
import { useCallback, useMemo, useSyncExternalStore } from "react";
import { useAuth } from "@/lib/auth-client";
import { StoredOrderSummary, orderSummaryKey } from "@/lib/checkout";
import { formatPrice } from "@/lib/format";

const NEXT_STEPS = [
  {
    title: "Order confirmation call",
    text: "Our team will call you shortly to confirm your order and delivery address.",
  },
  {
    title: "Packing & dispatch",
    text: "Once confirmed, we carefully pack your items and hand them to our courier partner.",
  },
  {
    title: "Delivery & payment",
    text: "Your order arrives at your doorstep — pay the rider in cash (COD). Please keep the exact amount ready.",
  },
];

export default function ConfirmationClient({
  orderNumber,
}: {
  orderNumber: string;
}) {
  const { status } = useAuth();

  // sessionStorage is a client-only external store; read it without effects.
  const getSnapshot = useCallback((): string | null => {
    try {
      return window.sessionStorage.getItem(orderSummaryKey(orderNumber));
    } catch {
      return null;
    }
  }, [orderNumber]);

  const raw = useSyncExternalStore(
    useCallback(() => () => {}, []),
    getSnapshot,
    () => null
  );

  const summary = useMemo<StoredOrderSummary | null>(() => {
    if (!raw) return null;
    try {
      return JSON.parse(raw) as StoredOrderSummary;
    } catch {
      return null;
    }
  }, [raw]);

  return (
    <div className="mx-auto max-w-2xl px-4 py-12">
      <div className="rounded-xl border border-slate-200 bg-white p-8 text-center shadow-sm">
        <div className="mx-auto flex h-16 w-16 items-center justify-center rounded-full bg-green-100">
          <svg
            xmlns="http://www.w3.org/2000/svg"
            fill="none"
            viewBox="0 0 24 24"
            strokeWidth={2}
            stroke="currentColor"
            className="h-8 w-8 text-green-600"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              d="m4.5 12.75 6 6 9-13.5"
            />
          </svg>
        </div>

        <h1 className="mt-4 text-2xl font-bold text-blue-950">
          Thank you for your order!
        </h1>
        <p className="mt-2 text-sm text-slate-500">
          Your order has been placed successfully.
        </p>

        <div className="mt-6 rounded-lg bg-slate-50 px-6 py-4">
          <p className="text-xs uppercase tracking-wide text-slate-400">
            Order number
          </p>
          <p className="mt-1 text-2xl font-extrabold tracking-wider text-blue-950">
            {orderNumber}
          </p>
          {summary && (
            <div className="mt-3 space-y-1 text-sm text-slate-600">
              <p>
                Total (incl. shipping):{" "}
                <span className="font-bold text-blue-950">
                  {formatPrice(summary.total)}
                </span>{" "}
                — Cash on Delivery
              </p>
              {summary.email && (
                <p>
                  A confirmation will be sent to{" "}
                  <span className="font-medium">{summary.email}</span>.
                </p>
              )}
            </div>
          )}
        </div>

        <p className="mt-4 text-xs text-slate-400">
          Please note your order number — you&apos;ll need it for any queries.
        </p>
      </div>

      <div className="mt-6 rounded-xl border border-slate-200 bg-white p-8">
        <h2 className="text-sm font-bold uppercase tracking-wide text-blue-950">
          What happens next
        </h2>
        <ol className="mt-4 space-y-4">
          {NEXT_STEPS.map((step, i) => (
            <li key={step.title} className="flex gap-4">
              <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-orange-100 text-sm font-bold text-orange-600">
                {i + 1}
              </span>
              <div>
                <p className="text-sm font-semibold text-slate-800">
                  {step.title}
                </p>
                <p className="mt-0.5 text-sm text-slate-500">{step.text}</p>
              </div>
            </li>
          ))}
        </ol>
      </div>

      <div className="mt-6 flex flex-col items-center gap-3 sm:flex-row sm:justify-center">
        {status === "authenticated" && (
          <Link
            href={`/account/orders/${encodeURIComponent(orderNumber)}`}
            className="w-full rounded-md bg-blue-950 px-6 py-2.5 text-center text-sm font-semibold text-white transition hover:bg-blue-900 sm:w-auto"
          >
            Track this order
          </Link>
        )}
        <Link
          href="/"
          className="w-full rounded-md border border-slate-300 px-6 py-2.5 text-center text-sm font-semibold text-slate-700 transition hover:bg-slate-50 sm:w-auto"
        >
          Continue Shopping
        </Link>
      </div>
    </div>
  );
}
