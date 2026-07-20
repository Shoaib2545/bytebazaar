import type { NextConfig } from "next";

/**
 * Hosts allowed through the Next image optimizer. Product/banner/category
 * images come from whatever URLs admins enter, so the list is configurable:
 *
 *   NEXT_PUBLIC_IMAGE_HOSTS=cdn.bytebazaar.pk,images.example.com
 *
 * The same variable is read by lib/images.ts, which falls back to an
 * unoptimized image for any host that is NOT on this list — an unknown host
 * degrades to a plain image instead of a 400 from /_next/image.
 */
const imageHosts = (
  process.env.NEXT_PUBLIC_IMAGE_HOSTS ?? "placehold.co,localhost,127.0.0.1"
)
  .split(",")
  .map((h) => h.trim())
  .filter(Boolean);

const nextConfig: NextConfig = {
  experimental: {
    // Ship the stylesheet as an inline <style> instead of a <link>.
    //
    // The storefront's CSS is Tailwind — one atomic sheet, ~9 KiB over the
    // wire, that barely grows with the app. As a separate <link> it is
    // render-blocking and, being discovered only after the document parses,
    // costs a full extra round trip before anything can paint (Lighthouse
    // measured ~730 ms of render-blocking time on the category page).
    // Inlining removes that request from the critical path entirely.
    //
    // Trade-off: returning visitors no longer reuse a cached stylesheet.
    // That is the right call here — catalog pages are `force-dynamic`, so the
    // HTML is never cached anyway, and first-time visitors are the ones whose
    // Core Web Vitals we are held to.
    inlineCss: true,
  },
  images: {
    remotePatterns: imageHosts.flatMap((hostname) =>
      (["https", "http"] as const).map((protocol) => ({ protocol, hostname }))
    ),
    formats: ["image/avif", "image/webp"],
    // Product grids render at most ~300px wide on mobile; trimming the ladder
    // avoids generating variants nothing requests.
    imageSizes: [64, 96, 128, 200, 256, 384],
  },
};

export default nextConfig;
