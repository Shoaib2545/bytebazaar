"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import OrderStatusBadge from "@/components/OrderStatusBadge";
import { OrderDetail, getOrder } from "@/lib/account";
import { formatPrice } from "@/lib/format";

function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString("en-PK", {
    year: "numeric",
    month: "short",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit",
  });
}

export default function OrderDetailClient({
  orderNumber,
}: {
  orderNumber: string;
}) {
  const [order, setOrder] = useState<OrderDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const loading = !order && !error;

  useEffect(() => {
    let cancelled = false;
    getOrder(orderNumber)
      .then((res) => {
        if (!cancelled) setOrder(res);
      })
      .catch((err) => {
        if (!cancelled)
          setError(
            err instanceof Error ? err.message : "Could not load this order."
          );
      });
    return () => {
      cancelled = true;
    };
  }, [orderNumber]);

  if (loading) {
    return (
      <div className="rounded-lg border border-slate-200 bg-white p-10 text-center text-sm text-slate-400">
        Loading order...
      </div>
    );
  }

  if (error || !order) {
    return (
      <div>
        <div className="rounded-lg border border-red-200 bg-red-50 p-6 text-sm text-red-700">
          {error ?? "Order not found."}
        </div>
        <Link
          href="/account/orders"
          className="mt-4 inline-block text-sm font-semibold text-orange-600 hover:underline"
        >
          &larr; Back to orders
        </Link>
      </div>
    );
  }

  return (
    <div>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <Link
            href="/account/orders"
            className="text-xs font-semibold text-slate-400 hover:text-orange-600"
          >
            &larr; Back to orders
          </Link>
          <h1 className="mt-1 text-xl font-bold text-blue-950">
            Order {order.orderNumber}
          </h1>
          <p className="text-xs text-slate-400">
            Placed {formatDateTime(order.createdAt)} · {order.paymentMethod}
          </p>
        </div>
        <OrderStatusBadge status={order.status} />
      </div>

      <div className="mt-6 grid gap-6 lg:grid-cols-3">
        <div className="space-y-6 lg:col-span-2">
          {/* Items */}
          <section className="overflow-hidden rounded-lg border border-slate-200 bg-white">
            <h2 className="border-b border-slate-100 px-5 py-3 text-sm font-bold uppercase tracking-wide text-blue-950">
              Items
            </h2>
            <div className="px-5">
              {order.items.map((item) => (
                <div
                  key={item.productId}
                  className="flex items-center gap-4 border-b border-slate-100 py-4 last:border-b-0"
                >
                  <Link
                    href={`/product/${item.slug}`}
                    className="flex h-16 w-16 shrink-0 items-center justify-center overflow-hidden rounded-md border border-slate-100 bg-slate-50"
                  >
                    {item.imageUrl ? (
                      // eslint-disable-next-line @next/next/no-img-element
                      <img
                        src={item.imageUrl}
                        alt={item.name}
                        className="h-full w-full object-contain p-1"
                      />
                    ) : (
                      <span className="text-[10px] text-slate-300">No image</span>
                    )}
                  </Link>
                  <div className="min-w-0 flex-1">
                    <Link
                      href={`/product/${item.slug}`}
                      className="line-clamp-2 text-sm font-medium text-slate-800 hover:text-orange-600"
                    >
                      {item.name}
                    </Link>
                    <p className="mt-0.5 text-xs text-slate-400">
                      {formatPrice(item.unitPrice)} × {item.quantity}
                    </p>
                  </div>
                  <span className="shrink-0 text-sm font-bold text-blue-950">
                    {formatPrice(item.lineTotal)}
                  </span>
                </div>
              ))}
            </div>
          </section>

          {/* Status timeline */}
          <section className="rounded-lg border border-slate-200 bg-white p-5">
            <h2 className="text-sm font-bold uppercase tracking-wide text-blue-950">
              Order Timeline
            </h2>
            <ol className="mt-4 space-y-0">
              {order.history.map((entry, i) => (
                <li key={`${entry.status}-${entry.createdAt}`} className="relative flex gap-4 pb-6 last:pb-0">
                  {i < order.history.length - 1 && (
                    <span className="absolute left-[7px] top-4 h-full w-0.5 bg-slate-200" />
                  )}
                  <span
                    className={`relative mt-1 h-4 w-4 shrink-0 rounded-full border-2 ${
                      i === order.history.length - 1
                        ? "border-orange-500 bg-orange-500"
                        : "border-slate-300 bg-white"
                    }`}
                  />
                  <div>
                    <div className="flex items-center gap-2">
                      <OrderStatusBadge status={entry.status} />
                      <span className="text-xs text-slate-400">
                        {formatDateTime(entry.createdAt)}
                      </span>
                    </div>
                    {entry.note && (
                      <p className="mt-1 text-sm text-slate-600">{entry.note}</p>
                    )}
                  </div>
                </li>
              ))}
              {order.history.length === 0 && (
                <li className="text-sm text-slate-400">No status updates yet.</li>
              )}
            </ol>
          </section>
        </div>

        <div className="space-y-6">
          {/* Totals */}
          <section className="rounded-lg border border-slate-200 bg-white p-5">
            <h2 className="text-sm font-bold uppercase tracking-wide text-blue-950">
              Totals
            </h2>
            <dl className="mt-4 space-y-2 text-sm">
              <div className="flex justify-between">
                <dt className="text-slate-500">Subtotal</dt>
                <dd className="font-semibold text-slate-800">
                  {formatPrice(order.subtotal)}
                </dd>
              </div>
              <div className="flex justify-between">
                <dt className="text-slate-500">Shipping</dt>
                <dd className="font-semibold text-slate-800">
                  {formatPrice(order.shippingFee)}
                </dd>
              </div>
              <div className="flex justify-between border-t border-slate-100 pt-2 text-base">
                <dt className="font-bold text-blue-950">Total</dt>
                <dd className="font-extrabold text-blue-950">
                  {formatPrice(order.total)}
                </dd>
              </div>
            </dl>
          </section>

          {/* Shipping address */}
          <section className="rounded-lg border border-slate-200 bg-white p-5">
            <h2 className="text-sm font-bold uppercase tracking-wide text-blue-950">
              Shipping Address
            </h2>
            <address className="mt-3 space-y-1 text-sm not-italic text-slate-600">
              <p className="font-semibold text-slate-800">
                {order.shippingAddress.fullName}
              </p>
              <p>{order.shippingAddress.addressLine}</p>
              <p>
                {order.shippingAddress.city}, {order.shippingAddress.region}
              </p>
              <p>{order.shippingAddress.phone}</p>
              <p>{order.shippingAddress.email}</p>
            </address>
          </section>
        </div>
      </div>
    </div>
  );
}
