import type { MetadataRoute } from "next";
import { SITE_URL } from "@/lib/seo";

export default function robots(): MetadataRoute.Robots {
  return {
    rules: [
      {
        userAgent: "*",
        allow: "/",
        disallow: [
          "/api/",
          "/cart",
          "/checkout",
          "/account",
          "/account/",
          "/login",
          "/register",
          "/order-confirmation/",
          // Search result pages are thin/duplicate — keep them out of the index.
          "/search",
          // NOTE: facet permutations are deliberately *not* disallowed here.
          // Blocking them by robots.txt would stop crawlers from ever reading
          // the rel=canonical / noindex the category page emits. Consolidation
          // is handled in app/category/[slug]/page.tsx via lib/seo.ts.
        ],
      },
    ],
    sitemap: `${SITE_URL}/sitemap.xml`,
    host: SITE_URL,
  };
}
