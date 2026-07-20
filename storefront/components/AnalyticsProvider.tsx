"use client";

// Fires a $pageview on every app-router navigation. Entirely inert when
// NEXT_PUBLIC_POSTHOG_KEY is unset (see lib/analytics.ts).

import { usePathname } from "next/navigation";
import { useEffect } from "react";
import { analyticsEnabled, trackPageView } from "@/lib/analytics";

export default function AnalyticsProvider() {
  const pathname = usePathname();

  useEffect(() => {
    if (!analyticsEnabled || !pathname) return;
    trackPageView(pathname);
  }, [pathname]);

  return null;
}
