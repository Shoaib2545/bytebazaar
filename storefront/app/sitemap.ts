import type { MetadataRoute } from "next";
import { getCategoryProducts, getCategoryTree } from "@/lib/api";
import { SITE_URL, flattenCategories } from "@/lib/seo";

// sitemap.ts is a Route Handler and is cached (i.e. evaluated at build time)
// by default. The catalog lives behind the API, which is not guaranteed to be
// up during `next build` — same reason every catalog page sets this. With
// force-dynamic the sitemap is generated per request and a build never fails
// because the API is unreachable.
export const dynamic = "force-dynamic";

/** Cap per category so the sitemap stays under the 50k-URL / 50MB limits. */
const MAX_PRODUCTS_PER_CATEGORY = 500;

const STATIC_ROUTES: {
  path: string;
  changeFrequency: MetadataRoute.Sitemap[number]["changeFrequency"];
  priority: number;
}[] = [
  { path: "/", changeFrequency: "daily", priority: 1 },
  { path: "/login", changeFrequency: "yearly", priority: 0.1 },
  { path: "/register", changeFrequency: "yearly", priority: 0.1 },
];

export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  const now = new Date();

  const entries: MetadataRoute.Sitemap = STATIC_ROUTES.map((r) => ({
    url: `${SITE_URL}${r.path}`,
    lastModified: now,
    changeFrequency: r.changeFrequency,
    priority: r.priority,
  }));

  // getCategoryTree() already swallows API errors and returns []; if the API is
  // down we simply emit the static routes rather than failing the request.
  const tree = await getCategoryTree();
  const categories = flattenCategories(tree);

  for (const cat of categories) {
    entries.push({
      url: `${SITE_URL}/category/${cat.slug}`,
      lastModified: now,
      changeFrequency: "daily",
      priority: 0.8,
    });
  }

  // Only leaf categories are queried: a parent's product list is the union of
  // its children's, so this visits every active product exactly once.
  const leaves = categories.filter((c) => !c.children?.length);

  const productSlugs = new Set<string>();
  const productImages = new Map<string, string>();

  const pages = await Promise.all(
    leaves.map((cat) =>
      getCategoryProducts(cat.slug, {
        page: "1",
        pageSize: String(MAX_PRODUCTS_PER_CATEGORY),
      })
    )
  );

  for (const page of pages) {
    for (const p of page.items) {
      if (productSlugs.has(p.slug)) continue;
      productSlugs.add(p.slug);
      if (p.imageUrl) productImages.set(p.slug, p.imageUrl);
    }
  }

  for (const slug of productSlugs) {
    const image = productImages.get(slug);
    entries.push({
      url: `${SITE_URL}/product/${slug}`,
      lastModified: now,
      changeFrequency: "weekly",
      priority: 0.7,
      ...(image ? { images: [image] } : {}),
    });
  }

  return entries;
}
