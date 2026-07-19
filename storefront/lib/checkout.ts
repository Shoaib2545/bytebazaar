"use client";

// Typed client for checkout — shipping options + placing a COD order.

import { apiFetch, readErrorMessage } from "./auth-client";

export interface ShippingOption {
  code: string;
  name: string;
  fee: number;
}

export interface CheckoutRequest {
  fullName: string;
  phone: string;
  email: string;
  addressLine: string;
  city: string;
  region: string;
  shippingCode: string;
  paymentMethod: "COD";
  notes?: string;
}

export interface CheckoutResult {
  orderId: string;
  orderNumber: string;
  total: number;
  status: string;
  /** Coupon applied to the order, if any. */
  couponCode: string | null;
  /** Discount amount applied to the order; 0 when none. */
  discount: number;
}

export async function getShippingOptions(): Promise<ShippingOption[]> {
  const res = await apiFetch("/api/checkout/shipping-options");
  if (!res.ok) {
    throw new Error(
      await readErrorMessage(res, "Could not load shipping options.")
    );
  }
  return (await res.json()) as ShippingOption[];
}

export async function placeOrder(
  request: CheckoutRequest
): Promise<CheckoutResult> {
  const res = await apiFetch("/api/checkout", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
  });
  if (!res.ok) {
    throw new Error(
      await readErrorMessage(res, "Could not place the order. Please try again.")
    );
  }
  return (await res.json()) as CheckoutResult;
}

// ---------- order-confirmation hand-off (sessionStorage) ----------

export interface StoredOrderSummary {
  orderNumber: string;
  total: number;
  status: string;
  email: string;
  fullName: string;
  placedAt: string;
  /** Coupon applied to the order (older stored entries may omit these). */
  couponCode?: string | null;
  discount?: number;
}

export function orderSummaryKey(orderNumber: string): string {
  return `bb.order.${orderNumber}`;
}

export function storeOrderSummary(summary: StoredOrderSummary) {
  try {
    window.sessionStorage.setItem(
      orderSummaryKey(summary.orderNumber),
      JSON.stringify(summary)
    );
  } catch {
    // sessionStorage unavailable — confirmation page degrades gracefully
  }
}
