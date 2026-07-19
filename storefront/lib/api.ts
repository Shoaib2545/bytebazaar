// Server-side typed fetch helpers for the ByteBazaar public catalog API.
// Every helper tolerates the API being down and returns a safe empty fallback.

const API_BASE =
  process.env.API_URL ||
  process.env.NEXT_PUBLIC_API_URL ||
  "http://localhost:5080";

// ---------- DTOs (match the API contract exactly, camelCase) ----------

export interface CategoryNode {
  id: string;
  name: string;
  slug: string;
  imageUrl: string | null;
  sortOrder: number;
  children: CategoryNode[];
}

export type AttributeType =
  | "Select"
  | "MultiSelect"
  | "Number"
  | "Boolean"
  | "Text";

export type FilterWidget = "Checkbox" | "Radio" | "Range";

export interface FilterOption {
  value: string;
  count: number;
}

export interface FilterAttribute {
  code: string;
  name: string;
  type: AttributeType;
  widget: FilterWidget;
  options: FilterOption[];
}

export interface FilterBrand {
  id: string;
  name: string;
  slug: string;
  count: number;
}

export interface PriceRange {
  min: number;
  max: number;
}

export interface CategoryFilters {
  attributes: FilterAttribute[];
  brands: FilterBrand[];
  priceRange: PriceRange;
}

export interface ProductListItem {
  id: string;
  name: string;
  slug: string;
  price: number;
  salePrice: number | null;
  imageUrl: string | null;
  brandName: string | null;
  stock: number;
}

export interface PagedProducts {
  items: ProductListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface ProductAttribute {
  name: string;
  value: string;
}

export interface ProductDetail {
  id: string;
  name: string;
  slug: string;
  description: string | null;
  price: number;
  salePrice: number | null;
  stock: number;
  brandName: string | null;
  categorySlug: string | null;
  categoryName: string | null;
  images: string[];
  attributes: ProductAttribute[];
  metaTitle: string | null;
  metaDescription: string | null;
}

export type BannerPlacement = "Hero" | "Strip";

export interface Banner {
  id: string;
  title: string;
  subtitle: string | null;
  imageUrl: string | null;
  linkUrl: string | null;
  placement: BannerPlacement;
  sortOrder: number;
}

// ---------- fallbacks ----------

export const EMPTY_PAGED: PagedProducts = {
  items: [],
  totalCount: 0,
  page: 1,
  pageSize: 24,
};

export const EMPTY_FILTERS: CategoryFilters = {
  attributes: [],
  brands: [],
  priceRange: { min: 0, max: 0 },
};

// ---------- internal fetch helper ----------

async function apiGet<T>(path: string, fallback: T): Promise<T> {
  try {
    const res = await fetch(`${API_BASE}${path}`, {
      cache: "no-store",
      headers: { Accept: "application/json" },
    });
    if (!res.ok) return fallback;
    return (await res.json()) as T;
  } catch {
    // API down or unreachable — degrade gracefully.
    return fallback;
  }
}

// ---------- public endpoint helpers ----------

export async function getCategoryTree(): Promise<CategoryNode[]> {
  return apiGet<CategoryNode[]>("/api/catalog/categories/tree", []);
}

export async function getCategoryFilters(
  slug: string
): Promise<CategoryFilters> {
  return apiGet<CategoryFilters>(
    `/api/catalog/categories/${encodeURIComponent(slug)}/filters`,
    EMPTY_FILTERS
  );
}

/**
 * Fetch products for a category. `params` is passed through as query string
 * (page, pageSize, sort, brand, price and any attribute-code filters).
 */
export async function getCategoryProducts(
  slug: string,
  params: Record<string, string> = {}
): Promise<PagedProducts> {
  const qs = new URLSearchParams(params).toString();
  return apiGet<PagedProducts>(
    `/api/catalog/categories/${encodeURIComponent(slug)}/products${
      qs ? `?${qs}` : ""
    }`,
    EMPTY_PAGED
  );
}

export async function getProduct(slug: string): Promise<ProductDetail | null> {
  return apiGet<ProductDetail | null>(
    `/api/catalog/products/${encodeURIComponent(slug)}`,
    null
  );
}

/** Active banners currently within their [startsAt, endsAt] window. */
export async function getBanners(): Promise<Banner[]> {
  return apiGet<Banner[]>("/api/content/banners", []);
}

/** Featured (isFeatured, Active) products, newest first. */
export async function getFeaturedProducts(
  count = 8
): Promise<ProductListItem[]> {
  return apiGet<ProductListItem[]>(`/api/catalog/featured?count=${count}`, []);
}

export async function searchProducts(
  q: string,
  params: Record<string, string> = {}
): Promise<PagedProducts> {
  const search = new URLSearchParams({ q, ...params }).toString();
  return apiGet<PagedProducts>(`/api/catalog/search?${search}`, EMPTY_PAGED);
}

// ---------- convenience ----------

/** Depth-first search for the first leaf category in the tree. */
export function firstLeafCategory(tree: CategoryNode[]): CategoryNode | null {
  for (const node of tree) {
    if (!node.children || node.children.length === 0) return node;
    const leaf = firstLeafCategory(node.children);
    if (leaf) return leaf;
  }
  return null;
}
