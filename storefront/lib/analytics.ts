"use client";

// Lightweight, env-gated PostHog client for the storefront funnel.
//
// Deliberately dependency-free: `posthog-js` would ship ~50KB of autocapture
// into every route, which fights the Core Web Vitals goal, and it has to be
// dynamically imported to stay gated anyway. We only need explicit funnel
// events, so we post them to PostHog's public capture endpoint directly.
//
// Gating contract: when NEXT_PUBLIC_POSTHOG_KEY is absent this module is a
// complete no-op — no network requests, no storage writes, no console output.

const POSTHOG_KEY = process.env.NEXT_PUBLIC_POSTHOG_KEY;
const POSTHOG_HOST = (
  process.env.NEXT_PUBLIC_POSTHOG_HOST || "https://us.i.posthog.com"
).replace(/\/+$/, "");

/** True only when a project key is configured. */
export const analyticsEnabled = Boolean(POSTHOG_KEY);

const DISTINCT_ID_KEY = "bb.analytics.did";

function randomId(): string {
  try {
    if (typeof crypto !== "undefined" && "randomUUID" in crypto) {
      return crypto.randomUUID();
    }
  } catch {
    // fall through
  }
  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
}

/**
 * Stable anonymous id. Only created once analytics is enabled, so a keyless
 * deployment writes nothing to localStorage.
 */
function distinctId(): string {
  try {
    const existing = window.localStorage.getItem(DISTINCT_ID_KEY);
    if (existing) return existing;
    const fresh = randomId();
    window.localStorage.setItem(DISTINCT_ID_KEY, fresh);
    return fresh;
  } catch {
    // Private mode / storage disabled — fall back to a per-page-load id.
    return randomId();
  }
}

type Props = Record<string, unknown>;

function send(event: string, props: Props = {}): void {
  if (!analyticsEnabled || typeof window === "undefined") return;

  const payload = JSON.stringify({
    api_key: POSTHOG_KEY,
    event,
    distinct_id: distinctId(),
    timestamp: new Date().toISOString(),
    properties: {
      ...props,
      $current_url: window.location.href,
      $pathname: window.location.pathname,
      $host: window.location.host,
      $lib: "bytebazaar-storefront",
    },
  });

  const url = `${POSTHOG_HOST}/e/`;

  try {
    // sendBeacon survives navigation (matters for checkout_started / purchase).
    if (typeof navigator !== "undefined" && navigator.sendBeacon) {
      const blob = new Blob([payload], { type: "application/json" });
      if (navigator.sendBeacon(url, blob)) return;
    }
    void fetch(url, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: payload,
      keepalive: true,
      // Analytics must never affect auth state or block the UI.
      credentials: "omit",
      mode: "no-cors",
    }).catch(() => {});
  } catch {
    // Analytics is best-effort — never surface an error to the shopper.
  }
}

// ---------------- funnel events ----------------

export interface ProductRef {
  id: string;
  name?: string;
  slug?: string;
  price?: number;
  brand?: string | null;
  category?: string | null;
}

export function trackPageView(pathname: string): void {
  send("$pageview", { path: pathname });
}

/** Funnel step 1 — a product detail page was viewed. */
export function trackProductViewed(product: ProductRef): void {
  send("product_viewed", {
    product_id: product.id,
    product_name: product.name,
    product_slug: product.slug,
    price: product.price,
    brand: product.brand ?? undefined,
    category: product.category ?? undefined,
  });
}

/** Funnel step 2 — an item was successfully added to the cart. */
export function trackAddToCart(
  product: ProductRef,
  quantity: number,
  source: "product_page" | "product_card" = "product_card"
): void {
  send("add_to_cart", {
    product_id: product.id,
    product_name: product.name,
    price: product.price,
    quantity,
    value: product.price != null ? product.price * quantity : undefined,
    currency: "PKR",
    source,
  });
}

/** Funnel step 3 — the checkout page was reached with a non-empty cart. */
export function trackCheckoutStarted(input: {
  itemCount: number;
  value: number;
  couponCode?: string | null;
}): void {
  send("checkout_started", {
    item_count: input.itemCount,
    value: input.value,
    currency: "PKR",
    coupon_code: input.couponCode ?? undefined,
  });
}

/** Funnel step 4 — the order was placed. */
export function trackPurchase(input: {
  orderNumber: string;
  value: number;
  itemCount: number;
  paymentMethod: string;
  couponCode?: string | null;
  discount?: number;
}): void {
  send("purchase", {
    order_number: input.orderNumber,
    value: input.value,
    currency: "PKR",
    item_count: input.itemCount,
    payment_method: input.paymentMethod,
    coupon_code: input.couponCode ?? undefined,
    discount: input.discount,
  });
}
