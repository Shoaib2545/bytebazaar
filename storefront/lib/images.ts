// Decides whether a given image URL can go through the Next image optimizer.
//
// Must stay in sync with `images.remotePatterns` in next.config.ts — both read
// NEXT_PUBLIC_IMAGE_HOSTS. Any host not on the list is rendered `unoptimized`
// so an admin pasting an image URL from an unconfigured CDN produces a working
// (just unoptimized) image rather than a 400 from /_next/image.

const ALLOWED_HOSTS = new Set(
  (process.env.NEXT_PUBLIC_IMAGE_HOSTS ?? "placehold.co,localhost,127.0.0.1")
    .split(",")
    .map((h) => h.trim().toLowerCase())
    .filter(Boolean)
);

export function canOptimize(src: string): boolean {
  if (!src) return false;
  // Same-origin / public folder assets are always optimizable.
  if (src.startsWith("/") && !src.startsWith("//")) return true;
  try {
    return ALLOWED_HOSTS.has(new URL(src).hostname.toLowerCase());
  } catch {
    return false;
  }
}
