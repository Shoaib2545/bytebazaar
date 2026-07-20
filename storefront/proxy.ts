// Admin-managed URL redirects, applied before the app renders.
//
// Next.js 16 renamed the `middleware` file convention to `proxy` (same
// functionality, now defaulting to the Node.js runtime). See
// node_modules/next/dist/docs/01-app/03-api-reference/03-file-conventions/proxy.md
//
// The rule set is cached in-process by lib/redirects.ts, so a request for a
// path with no redirect (i.e. almost every request) costs one Map lookup and
// zero network calls. If the redirects API is unreachable the lookup resolves
// to null and the request falls through untouched — redirects must never be
// able to take the site down.

import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";
import { lookupRedirect } from "@/lib/redirects";

export const config = {
  // Everything except API routes, Next internals and the metadata files.
  // Assets under public/ are excluded by the file-extension rule below since
  // matcher patterns cannot express "has a dot in the last segment" cleanly
  // alongside the other exclusions.
  matcher: [
    "/((?!api|_next/static|_next/image|favicon.ico|sitemap.xml|robots.txt).*)",
  ],
};

/** Paths the proxy never rewrites, regardless of the matcher. */
function isSkippable(pathname: string): boolean {
  if (pathname.startsWith("/_next") || pathname.startsWith("/api/")) {
    return true;
  }
  // Static assets served from public/ — "/logo.svg", "/fonts/x.woff2".
  // A trailing-segment dot is a good enough proxy for "this is a file".
  const lastSegment = pathname.slice(pathname.lastIndexOf("/") + 1);
  return lastSegment.includes(".");
}

export async function proxy(request: NextRequest) {
  const { pathname, search } = request.nextUrl;

  if (isSkippable(pathname)) return NextResponse.next();

  let match: Awaited<ReturnType<typeof lookupRedirect>> = null;
  try {
    match = await lookupRedirect(pathname);
  } catch {
    // Defensive: lookupRedirect already swallows its own failures.
    return NextResponse.next();
  }

  if (!match) return NextResponse.next();

  // Absolute targets (another host) are honoured as-is; relative ones resolve
  // against the current origin. Query strings are carried over so campaign
  // params survive the hop — unless the rule sets its own.
  let destination: URL;
  try {
    destination = new URL(match.toPath, request.nextUrl);
  } catch {
    return NextResponse.next();
  }

  if (!destination.search && search) destination.search = search;

  // Never bounce a path to itself — a self-referential rule would otherwise
  // produce an infinite redirect loop.
  if (
    destination.origin === request.nextUrl.origin &&
    destination.pathname === pathname
  ) {
    return NextResponse.next();
  }

  return NextResponse.redirect(destination, match.statusCode);
}
