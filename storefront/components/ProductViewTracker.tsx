"use client";

// Funnel step 1: emits `product_viewed` once per product detail page mount.
// Rendered from the (server) product page, which owns the product data.

import { useEffect, useRef } from "react";
import { analyticsEnabled, trackProductViewed } from "@/lib/analytics";
import type { ProductRef } from "@/lib/analytics";

export default function ProductViewTracker({ product }: { product: ProductRef }) {
  const sent = useRef<string | null>(null);

  useEffect(() => {
    if (!analyticsEnabled) return;
    if (sent.current === product.id) return;
    sent.current = product.id;
    trackProductViewed(product);
  }, [product]);

  return null;
}
