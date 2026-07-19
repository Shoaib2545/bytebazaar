"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import OrderStatusBadge from "@/components/OrderStatusBadge";
import { PagedOrders, getOrders } from "@/lib/account";
import { formatPrice } from "@/lib/format";

const PAGE_SIZE = 10;

export default function OrdersPage() {
  const [page, setPage] = useState(1);
  const [result, setResult] = useState<
    | { page: number; data: PagedOrders }
    | { page: number; error: string }
    | null
  >(null);

  useEffect(() => {
    let cancelled = false;
    getOrders(page, PAGE_SIZE)
      .then((res) => {
        if (!cancelled) setResult({ page, data: res });
      })
      .catch((err) => {
        if (!cancelled)
          setResult({
            page,
            error:
              err instanceof Error
                ? err.message
                : "Could not load your orders.",
          });
      });
    return () => {
      cancelled = true;
    };
  }, [page]);

  const current = result?.page === page ? result : null;
  const data = current && "data" in current ? current.data : null;
  const error = current && "error" in current ? current.error : null;
  const loading = !current;

  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / PAGE_SIZE)) : 1;

  return (
    <div>
      <h1 className="text-xl font-bold text-blue-950">My Orders</h1>

      {loading ? (
        <div className="mt-6 rounded-lg border border-slate-200 bg-white p-10 text-center text-sm text-slate-400">
          Loading orders...
        </div>
      ) : error ? (
        <div className="mt-6 rounded-lg border border-red-200 bg-red-50 p-6 text-sm text-red-700">
          {error}
        </div>
      ) : !data || data.items.length === 0 ? (
        <div className="mt-6 rounded-lg border border-slate-200 bg-white p-10 text-center">
          <p className="text-sm font-semibold text-slate-700">No orders yet</p>
          <p className="mt-1 text-sm text-slate-500">
            When you place an order, it will show up here.
          </p>
          <Link
            href="/"
            className="mt-5 inline-block rounded-md bg-orange-500 px-6 py-2.5 text-sm font-semibold text-white transition hover:bg-orange-600"
          >
            Start Shopping
          </Link>
        </div>
      ) : (
        <>
          <div className="mt-6 overflow-hidden rounded-lg border border-slate-200 bg-white">
            <table className="w-full text-sm">
              <thead className="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
                <tr>
                  <th className="px-4 py-3">Order</th>
                  <th className="hidden px-4 py-3 sm:table-cell">Date</th>
                  <th className="px-4 py-3">Status</th>
                  <th className="hidden px-4 py-3 sm:table-cell">Items</th>
                  <th className="px-4 py-3 text-right">Total</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((order) => (
                  <tr
                    key={order.orderNumber}
                    className="border-t border-slate-100 transition hover:bg-slate-50"
                  >
                    <td className="px-4 py-3">
                      <Link
                        href={`/account/orders/${encodeURIComponent(order.orderNumber)}`}
                        className="font-semibold text-blue-950 hover:text-orange-600"
                      >
                        {order.orderNumber}
                      </Link>
                    </td>
                    <td className="hidden px-4 py-3 text-slate-500 sm:table-cell">
                      {new Date(order.createdAt).toLocaleDateString("en-PK", {
                        year: "numeric",
                        month: "short",
                        day: "numeric",
                      })}
                    </td>
                    <td className="px-4 py-3">
                      <OrderStatusBadge status={order.status} />
                    </td>
                    <td className="hidden px-4 py-3 text-slate-500 sm:table-cell">
                      {order.itemCount}
                    </td>
                    <td className="px-4 py-3 text-right font-semibold text-slate-800">
                      {formatPrice(order.total)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {totalPages > 1 && (
            <div className="mt-4 flex items-center justify-center gap-3 text-sm">
              <button
                type="button"
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={page <= 1}
                className="rounded-md border border-slate-300 px-3 py-1.5 transition hover:bg-white disabled:cursor-not-allowed disabled:opacity-50"
              >
                Previous
              </button>
              <span className="text-slate-500">
                Page {page} of {totalPages}
              </span>
              <button
                type="button"
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                disabled={page >= totalPages}
                className="rounded-md border border-slate-300 px-3 py-1.5 transition hover:bg-white disabled:cursor-not-allowed disabled:opacity-50"
              >
                Next
              </button>
            </div>
          )}
        </>
      )}
    </div>
  );
}
