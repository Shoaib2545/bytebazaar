// SEO helpers shared by the storefront app router pages.
//
// Catalog pages are `force-dynamic` (the API may be down at build time), so
// everything here must be safe to run per-request and must never throw.

import type { Metadata } from "next";
import type { CategoryNode } from "./api";

/** Public origin of the storefront, used for canonical/OG/sitemap URLs. */
export const SITE_URL = (
  process.env.NEXT_PUBLIC_SITE_URL || "http://localhost:3000"
).replace(/\/+$/, "");

export const SITE_NAME = "ByteBazaar";

/** Turn a site-relative path into an absolute URL. */
export function absoluteUrl(path: string): string {
  return `${SITE_URL}${path.startsWith("/") ? path : `/${path}`}`;
}

/**
 * Search params that are *not* facet filters. Mirrors the RESERVED set in
 * components/FilterSidebar.tsx — keep the two in sync.
 */
export const NON_FILTER_PARAMS = new Set(["page", "pageSize", "sort", "q"]);

export type ParamMap = Record<string, string | string[] | undefined>;

function firstValue(value: string | string[] | undefined): string {
  if (Array.isArray(value)) return value[0] ?? "";
  return value ?? "";
}

/**
 * How many distinct facets are active (brand, price, and each attribute code
 * count as one, regardless of how many values are OR'd inside them).
 */
export function countActiveFilters(sp: ParamMap): number {
  let n = 0;
  for (const [key, value] of Object.entries(sp)) {
    if (NON_FILTER_PARAMS.has(key)) continue;
    if (firstValue(value).trim() === "") continue;
    n++;
  }
  return n;
}

export interface ListingSeo {
  /** Absolute canonical URL. */
  canonical: string;
  robots: NonNullable<Metadata["robots"]>;
}

/**
 * Canonical + robots policy for a faceted listing page.
 *
 * Filter state lives entirely in the URL, so every filter permutation is a
 * distinct URL. To avoid duplicate-content bloat:
 *
 *  - the canonical always points at the *unfiltered* listing;
 *  - `sort` is never canonical (same items, different order);
 *  - unfiltered page 2+ is self-canonical (`?page=N`) so deep pages stay
 *    indexable and crawlable — Google dropped rel=prev/next, self-canonical
 *    paginated URLs are the current guidance;
 *  - a single facet stays index,follow (useful landing pages like
 *    "Graphics Cards / NVIDIA") but consolidates to the parent via canonical;
 *  - two or more facets, or any filtered page 2+, are `noindex, follow` —
 *    crawlers still walk through to the products, but the permutation itself
 *    never enters the index.
 */
export function listingSeo(basePath: string, sp: ParamMap): ListingSeo {
  const filters = countActiveFilters(sp);
  const page = Math.max(1, Number.parseInt(firstValue(sp.page) || "1", 10) || 1);

  const canonicalPath =
    filters === 0 && page > 1 ? `${basePath}?page=${page}` : basePath;

  const deep = filters >= 2 || (filters >= 1 && page > 1);

  return {
    canonical: absoluteUrl(canonicalPath),
    robots: deep
      ? { index: false, follow: true }
      : { index: true, follow: true },
  };
}

/** Depth-first lookup of a category node by slug. */
export function findCategory(
  tree: CategoryNode[],
  slug: string
): CategoryNode | null {
  for (const node of tree) {
    if (node.slug === slug) return node;
    const hit = findCategory(node.children ?? [], slug);
    if (hit) return hit;
  }
  return null;
}

/** Flatten the category tree into a list (breadth of the whole tree). */
export function flattenCategories(tree: CategoryNode[]): CategoryNode[] {
  const out: CategoryNode[] = [];
  const walk = (nodes: CategoryNode[]) => {
    for (const n of nodes) {
      out.push(n);
      if (n.children?.length) walk(n.children);
    }
  };
  walk(tree);
  return out;
}

/** Fallback human title when the API is unreachable and we only have a slug. */
export function titleFromSlug(slug: string): string {
  return slug
    .split("-")
    .map((w) => (w ? w[0].toUpperCase() + w.slice(1) : w))
    .join(" ");
}

/** schema.org BreadcrumbList from [label, path] pairs. */
export function breadcrumbJsonLd(
  crumbs: { name: string; path: string }[]
): Record<string, unknown> {
  return {
    "@context": "https://schema.org",
    "@type": "BreadcrumbList",
    itemListElement: crumbs.map((c, i) => ({
      "@type": "ListItem",
      position: i + 1,
      name: c.name,
      item: absoluteUrl(c.path),
    })),
  };
}
