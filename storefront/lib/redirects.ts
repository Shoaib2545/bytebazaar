// Admin-managed URL redirects, cached for the proxy (middleware).
//
// Design constraints, in priority order:
//
//  1. A cache MISS must be cheap. The whole active rule set is small, so we
//     fetch it once and answer every request from an in-memory Map lookup —
//     no upstream call per request, and no call at all for the overwhelmingly
//     common "this path has no redirect" case.
//  2. The site must work when the redirects endpoint is unreachable. Every
//     failure path resolves to "no redirect"; a failed refresh keeps serving
//     the previous snapshot (stale-if-error) rather than dropping all rules.
//  3. Stale rules must self-heal. The snapshot revalidates after a short TTL.
//     Revalidation happens in the background while the current snapshot keeps
//     answering, so no visitor ever waits on it after the first load.
//
// This module deliberately avoids importing lib/api.ts: the proxy runs on a
// separate, pre-render code path and should not pull React or page-level
// modules into its bundle.

const API_BASE =
  process.env.API_URL ||
  process.env.NEXT_PUBLIC_API_URL ||
  "http://localhost:5080";

/** How long a snapshot is served before a background refresh is kicked off. */
const TTL_MS = Number.parseInt(process.env.REDIRECTS_TTL_MS || "", 10) || 60_000;

/** How long to wait before giving up on the redirects API. */
const FETCH_TIMEOUT_MS = 2_000;

/** Backoff after a failed refresh so a down API is not hammered per request. */
const ERROR_BACKOFF_MS = 10_000;

export interface RedirectRule {
  id: string;
  fromPath: string;
  toPath: string;
  isPermanent: boolean;
  isActive: boolean;
  createdAt: string;
}

export interface RedirectMatch {
  toPath: string;
  isPermanent: boolean;
  /** 308/307 — the method-preserving modern equivalents of 301/302. */
  statusCode: 308 | 307;
}

/**
 * Mirrors RedirectService.NormalizePath on the backend: lowercase, no query or
 * fragment, single leading slash, no trailing slash (the root stays "/").
 * Keep the two in sync — rules are stored in this normalized form.
 */
export function normalizePath(path: string | null | undefined): string {
  let value = (path ?? "").trim();
  const cut = value.search(/[?#]/);
  if (cut >= 0) value = value.slice(0, cut);

  value = value.trim().replace(/\/+$/, "");
  if (value.length === 0) return "/";
  if (!value.startsWith("/")) value = `/${value}`;
  return value.toLowerCase();
}

// ---------- snapshot state (module scope, per server instance) ----------

let rules: Map<string, RedirectRule> | null = null;
let fetchedAt = 0;
let inFlight: Promise<void> | null = null;

function toMap(list: RedirectRule[]): Map<string, RedirectRule> {
  const map = new Map<string, RedirectRule>();
  for (const rule of list) {
    if (!rule?.isActive || !rule.fromPath || !rule.toPath) continue;
    map.set(normalizePath(rule.fromPath), rule);
  }
  return map;
}

async function load(): Promise<void> {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), FETCH_TIMEOUT_MS);
  try {
    const res = await fetch(`${API_BASE}/api/redirects`, {
      signal: controller.signal,
      headers: { Accept: "application/json" },
      // `cache`/`next` options are no-ops in the proxy runtime; this module
      // does its own caching precisely because of that.
      cache: "no-store",
    });
    if (!res.ok) throw new Error(`redirects: HTTP ${res.status}`);
    const body = (await res.json()) as RedirectRule[];
    if (!Array.isArray(body)) throw new Error("redirects: bad payload");
    rules = toMap(body);
    fetchedAt = Date.now();
  } catch {
    // Unreachable / slow / malformed: keep whatever snapshot we already have
    // (possibly an empty one) and back off before trying again.
    if (rules === null) rules = new Map();
    fetchedAt = Date.now() - TTL_MS + ERROR_BACKOFF_MS;
  } finally {
    clearTimeout(timer);
  }
}

/** Single-flight refresh — concurrent requests share one upstream call. */
function refresh(): Promise<void> {
  if (!inFlight) {
    inFlight = load().finally(() => {
      inFlight = null;
    });
  }
  return inFlight;
}

/**
 * Resolve a request path to a redirect, or null when there is none.
 *
 * Only the very first call (per server instance) awaits the network. After
 * that the answer is a Map lookup, with staleness repaired in the background.
 */
export async function lookupRedirect(
  pathname: string
): Promise<RedirectMatch | null> {
  if (rules === null) {
    await refresh();
  } else if (Date.now() - fetchedAt > TTL_MS) {
    // Stale-while-revalidate: answer from the current snapshot immediately.
    void refresh();
  }

  const rule = rules?.get(normalizePath(pathname));
  if (!rule) return null;

  const to = rule.toPath.trim();
  if (!to) return null;

  return {
    toPath: to,
    isPermanent: rule.isPermanent,
    // 308/307 rather than 301/302 so non-GET methods are preserved; they are
    // the same cacheability contract search engines treat 301/302 as.
    statusCode: rule.isPermanent ? 308 : 307,
  };
}

/** Test/debug hook — drops the snapshot so the next lookup refetches. */
export function resetRedirectCache(): void {
  rules = null;
  fetchedAt = 0;
}
