import Link from "next/link";

interface Props {
  basePath: string;
  /** Current URL params (already resolved on the server). */
  params: Record<string, string>;
  page: number;
  pageSize: number;
  totalCount: number;
}

function pageHref(
  basePath: string,
  params: Record<string, string>,
  page: number
): string {
  const sp = new URLSearchParams(params);
  if (page <= 1) sp.delete("page");
  else sp.set("page", String(page));
  const qs = sp.toString();
  return qs ? `${basePath}?${qs}` : basePath;
}

export default function Pagination({
  basePath,
  params,
  page,
  pageSize,
  totalCount,
}: Props) {
  const totalPages = Math.max(1, Math.ceil(totalCount / Math.max(1, pageSize)));
  if (totalPages <= 1) return null;

  // window of pages around current
  const pages: number[] = [];
  const start = Math.max(1, page - 2);
  const end = Math.min(totalPages, page + 2);
  for (let p = start; p <= end; p++) pages.push(p);

  const linkCls =
    "rounded border border-slate-300 bg-white px-3 py-1.5 text-sm text-slate-700 transition hover:border-orange-500 hover:text-orange-600";
  const activeCls =
    "rounded border border-blue-950 bg-blue-950 px-3 py-1.5 text-sm font-semibold text-white";
  const disabledCls =
    "rounded border border-slate-200 bg-slate-50 px-3 py-1.5 text-sm text-slate-300";

  return (
    <nav
      className="mt-8 flex items-center justify-center gap-1.5"
      aria-label="Pagination"
    >
      {page > 1 ? (
        <Link href={pageHref(basePath, params, page - 1)} className={linkCls}>
          ‹ Prev
        </Link>
      ) : (
        <span className={disabledCls}>‹ Prev</span>
      )}

      {start > 1 && (
        <>
          <Link href={pageHref(basePath, params, 1)} className={linkCls}>
            1
          </Link>
          {start > 2 && <span className="px-1 text-slate-400">…</span>}
        </>
      )}

      {pages.map((p) =>
        p === page ? (
          <span key={p} className={activeCls} aria-current="page">
            {p}
          </span>
        ) : (
          <Link key={p} href={pageHref(basePath, params, p)} className={linkCls}>
            {p}
          </Link>
        )
      )}

      {end < totalPages && (
        <>
          {end < totalPages - 1 && (
            <span className="px-1 text-slate-400">…</span>
          )}
          <Link
            href={pageHref(basePath, params, totalPages)}
            className={linkCls}
          >
            {totalPages}
          </Link>
        </>
      )}

      {page < totalPages ? (
        <Link href={pageHref(basePath, params, page + 1)} className={linkCls}>
          Next ›
        </Link>
      ) : (
        <span className={disabledCls}>Next ›</span>
      )}
    </nav>
  );
}
