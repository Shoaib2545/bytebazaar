// Search DTOs shared by the server-rendered /search page and the client-side
// search-as-you-type dropdown in the header.
//
// This module is deliberately isomorphic: it reads only NEXT_PUBLIC_API_URL so
// it is safe to import from a client component, and `fetchSuggestions` is the
// one browser-side call. Server-side search goes through lib/api.ts.

import type { ProductListItem } from "./api";

/** Which backend answered — "SearchEngine" (Meilisearch) or "Database". */
export type SearchSource = "SearchEngine" | "Database";

/** A single product row in the suggest dropdown. */
export interface ProductSuggestion {
  id: string;
  name: string;
  slug: string;
  price: number;
  salePrice: number | null;
  imageUrl: string | null;
  brandName: string | null;
  categorySlug: string;
}

/** A category or brand shortcut in the suggest dropdown. */
export interface TermSuggestion {
  name: string;
  slug: string;
}

export interface SuggestResponse {
  query: string;
  products: ProductSuggestion[];
  categories: TermSuggestion[];
  brands: TermSuggestion[];
  totalProducts: number;
  source: SearchSource;
}

export interface SearchResults {
  query: string;
  items: ProductListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
  source: SearchSource;
}

export const EMPTY_SUGGEST: SuggestResponse = {
  query: "",
  products: [],
  categories: [],
  brands: [],
  totalProducts: 0,
  source: "Database",
};

export const EMPTY_SEARCH_RESULTS: SearchResults = {
  query: "",
  items: [],
  totalCount: 0,
  page: 1,
  pageSize: 24,
  source: "Database",
};

const CLIENT_API_BASE =
  process.env.NEXT_PUBLIC_API_URL || "http://localhost:5080";

/**
 * Browser-side suggest call for the header dropdown.
 *
 * Throws on transport failure / non-2xx so the caller can fall back to a plain
 * search box — the header must keep working when search is down. Pass an
 * AbortSignal so a superseded keystroke cancels its in-flight request.
 */
export async function fetchSuggestions(
  q: string,
  signal?: AbortSignal,
  limit = 6
): Promise<SuggestResponse> {
  const params = new URLSearchParams({ q, limit: String(limit) });
  const res = await fetch(`${CLIENT_API_BASE}/api/search/suggest?${params}`, {
    signal,
    headers: { Accept: "application/json" },
    // Suggest is public and must not depend on (or disturb) the session.
    credentials: "omit",
    cache: "no-store",
  });
  if (!res.ok) throw new Error(`suggest failed: ${res.status}`);
  return (await res.json()) as SuggestResponse;
}
