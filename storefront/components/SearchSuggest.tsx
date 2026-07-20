"use client";

// The full search-as-you-type combobox. This module is **never** part of the
// initial page bundle: SearchBar.tsx renders a plain, server-friendly search
// form and only `import()`s this component once the shopper actually touches
// the box. That keeps ~all of the header's JavaScript — this file, next/image,
// the price formatter and the suggest client — out of first-load hydration,
// which is what the category page's Total Blocking Time was paying for.

import Link from "next/link";
import { useRouter } from "next/navigation";
import {
  FormEvent,
  KeyboardEvent,
  useCallback,
  useEffect,
  useId,
  useMemo,
  useRef,
  useState,
} from "react";
import { formatPrice } from "@/lib/format";
import {
  EMPTY_SUGGEST,
  fetchSuggestions,
  type SuggestResponse,
} from "@/lib/search";
import RemoteImage from "./RemoteImage";

/** Wait this long after the last keystroke before asking the API. */
const DEBOUNCE_MS = 180;

/** Shorter terms match almost everything — not worth a round trip. */
const MIN_CHARS = 2;

/** The dropdown is ~64px wide per thumbnail at 2x. */
const THUMB_SIZES = "48px";

type Kind = "product" | "category" | "brand";

/**
 * The dropdown renders three grouped sections but keyboard navigation moves
 * through one flat sequence, so we flatten once and index into that.
 */
interface Row {
  key: string;
  kind: Kind;
  href: string;
}

function flatten(data: SuggestResponse): Row[] {
  return [
    ...data.products.map((p) => ({
      key: `p:${p.id}`,
      kind: "product" as const,
      href: `/product/${p.slug}`,
    })),
    ...data.categories.map((c) => ({
      key: `c:${c.slug}`,
      kind: "category" as const,
      href: `/category/${c.slug}`,
    })),
    ...data.brands.map((b) => ({
      key: `b:${b.slug}`,
      kind: "brand" as const,
      href: `/search?q=${encodeURIComponent(b.name)}`,
    })),
  ];
}

export interface SearchSuggestProps {
  /** Text already typed into the plain shell before this module arrived. */
  initialQuery?: string;
  /** Take focus on mount — true when swapped in from a real interaction. */
  autoFocus?: boolean;
}

export default function SearchSuggest({
  initialQuery = "",
  autoFocus = false,
}: SearchSuggestProps) {
  const router = useRouter();
  const [q, setQ] = useState(initialQuery);
  const [data, setData] = useState<SuggestResponse>(EMPTY_SUGGEST);
  const [open, setOpen] = useState(autoFocus);
  const [active, setActive] = useState(-1);
  /**
   * Sticky kill-switch. If suggest ever errors we stop calling it for the rest
   * of the session and the component behaves as a plain search box — a broken
   * search service must never break the site header.
   */
  const [degraded, setDegraded] = useState(false);

  const rootRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const abortRef = useRef<AbortController | null>(null);

  const listboxId = useId();
  const optionId = useCallback(
    (index: number) => `${listboxId}-opt-${index}`,
    [listboxId]
  );

  const rows = useMemo(() => flatten(data), [data]);
  const hasRows = rows.length > 0;

  // ---- debounced suggest ----
  useEffect(() => {
    if (degraded) return;

    const term = q.trim();

    // All state updates happen inside the timer (i.e. asynchronously, after
    // the render commits) rather than in the effect body, so a keystroke never
    // triggers a synchronous cascading re-render.
    const timer = setTimeout(() => {
      abortRef.current?.abort();

      if (term.length < MIN_CHARS) {
        setData(EMPTY_SUGGEST);
        setActive(-1);
        return;
      }

      const controller = new AbortController();
      abortRef.current = controller;

      fetchSuggestions(term, controller.signal)
        .then((next) => {
          if (controller.signal.aborted) return;
          setData(next);
          setActive(-1);
        })
        .catch((err: unknown) => {
          if (controller.signal.aborted) return;
          if (err instanceof DOMException && err.name === "AbortError") return;
          setDegraded(true);
          setData(EMPTY_SUGGEST);
          setOpen(false);
        });
    }, DEBOUNCE_MS);

    return () => clearTimeout(timer);
  }, [q, degraded]);

  // Abort any in-flight request when the component goes away.
  useEffect(() => () => abortRef.current?.abort(), []);

  // Hand focus back to the shopper: the shell input they just touched has been
  // replaced by this one, so restore focus and put the caret after their text.
  useEffect(() => {
    if (!autoFocus) return;
    const input = inputRef.current;
    if (!input) return;
    input.focus();
    const end = input.value.length;
    try {
      input.setSelectionRange(end, end);
    } catch {
      // Some input types disallow selection APIs — focus alone is enough.
    }
  }, [autoFocus]);

  // ---- click-outside dismiss ----
  useEffect(() => {
    if (!open) return;
    function onPointerDown(event: MouseEvent | TouchEvent) {
      if (!rootRef.current?.contains(event.target as Node)) setOpen(false);
    }
    document.addEventListener("mousedown", onPointerDown);
    document.addEventListener("touchstart", onPointerDown);
    return () => {
      document.removeEventListener("mousedown", onPointerDown);
      document.removeEventListener("touchstart", onPointerDown);
    };
  }, [open]);

  const expanded = open && hasRows && !degraded;

  function go(href: string) {
    setOpen(false);
    setActive(-1);
    router.push(href);
  }

  function submitQuery() {
    const term = q.trim();
    if (!term) return;
    go(`/search?q=${encodeURIComponent(term)}`);
  }

  function onSubmit(event: FormEvent) {
    event.preventDefault();
    // Enter on a highlighted row is handled in onKeyDown; reaching here means
    // the user wants the full results page.
    submitQuery();
  }

  function onKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === "Escape") {
      if (expanded) {
        event.preventDefault();
        setOpen(false);
        setActive(-1);
      }
      return;
    }

    if (event.key === "ArrowDown" || event.key === "ArrowUp") {
      if (!hasRows || degraded) return;
      event.preventDefault();
      if (!open) {
        setOpen(true);
        setActive(event.key === "ArrowDown" ? 0 : rows.length - 1);
        return;
      }
      const delta = event.key === "ArrowDown" ? 1 : -1;
      // Wrap around, with -1 ("nothing selected") as part of the cycle so the
      // user can always get back to their raw query text.
      const next = active + delta;
      setActive(next >= rows.length ? -1 : next < -1 ? rows.length - 1 : next);
      return;
    }

    if (event.key === "Enter") {
      if (expanded && active >= 0 && rows[active]) {
        event.preventDefault();
        go(rows[active].href);
      }
      return;
    }

    if (event.key === "Home" && expanded) {
      event.preventDefault();
      setActive(0);
    } else if (event.key === "End" && expanded) {
      event.preventDefault();
      setActive(rows.length - 1);
    }
  }

  /** Index of the first row of each section, for the flat option ids. */
  const productOffset = 0;
  const categoryOffset = data.products.length;
  const brandOffset = categoryOffset + data.categories.length;

  return (
    <div ref={rootRef} className="relative w-full max-w-xl">
      <form onSubmit={onSubmit} className="flex w-full" role="search">
        {/* WAI-ARIA 1.2 combobox: the input owns the popup, the listbox is a
            sibling, and the highlighted option is referenced by id rather than
            actually focused (focus stays in the input the whole time). */}
        <input
          ref={inputRef}
          type="text"
          role="combobox"
          value={q}
          onChange={(e) => {
            setQ(e.target.value);
            setOpen(true);
            setActive(-1);
          }}
          onFocus={() => setOpen(true)}
          onKeyDown={onKeyDown}
          placeholder="Search for graphics cards, CPUs, laptops..."
          className="w-full rounded-l-md border-0 bg-white px-4 py-2 text-sm text-slate-900 placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-orange-500"
          aria-label="Search products"
          aria-expanded={expanded}
          aria-controls={expanded ? listboxId : undefined}
          aria-autocomplete="list"
          aria-activedescendant={
            expanded && active >= 0 ? optionId(active) : undefined
          }
          autoComplete="off"
          spellCheck={false}
        />
        <button
          type="submit"
          className="rounded-r-md bg-orange-500 px-4 py-2 text-sm font-semibold text-white transition hover:bg-orange-600"
          aria-label="Search"
        >
          <svg
            xmlns="http://www.w3.org/2000/svg"
            viewBox="0 0 20 20"
            fill="currentColor"
            className="h-5 w-5"
          >
            <path
              fillRule="evenodd"
              d="M9 3.5a5.5 5.5 0 1 0 0 11 5.5 5.5 0 0 0 0-11ZM2 9a7 7 0 1 1 12.452 4.391l3.328 3.329a.75.75 0 1 1-1.06 1.06l-3.329-3.328A7 7 0 0 1 2 9Z"
              clipRule="evenodd"
            />
          </svg>
        </button>
      </form>

      {expanded && (
        <div className="absolute left-0 right-0 top-full z-50 mt-1 overflow-hidden rounded-md border border-slate-200 bg-white shadow-xl">
          <ul
            id={listboxId}
            role="listbox"
            aria-label="Search suggestions"
            className="max-h-[70vh] overflow-y-auto py-1"
          >
            {data.products.length > 0 && (
              <li role="presentation">
                <ul role="group" aria-label="Products" className="contents">
                  <li
                    role="presentation"
                    className="px-3 py-1 text-[11px] font-semibold uppercase tracking-wide text-slate-400"
                  >
                    Products
                  </li>
                  {data.products.map((p, i) => {
                    const index = productOffset + i;
                    const onSale =
                      p.salePrice != null && p.salePrice < p.price;
                    return (
                      <li
                        key={p.id}
                        id={optionId(index)}
                        role="option"
                        aria-selected={active === index}
                        onMouseEnter={() => setActive(index)}
                        onMouseDown={(e) => {
                          // Beat the input's blur so the navigation still runs.
                          e.preventDefault();
                          go(`/product/${p.slug}`);
                        }}
                        className={`flex cursor-pointer items-center gap-3 px-3 py-2 ${
                          active === index ? "bg-orange-50" : ""
                        }`}
                      >
                        <div className="relative h-10 w-10 shrink-0 overflow-hidden rounded bg-slate-50">
                          {p.imageUrl && (
                            <RemoteImage
                              src={p.imageUrl}
                              alt=""
                              sizes={THUMB_SIZES}
                              className="object-contain p-0.5"
                            />
                          )}
                        </div>
                        <div className="min-w-0 flex-1">
                          {p.brandName && (
                            <span className="block text-[11px] uppercase tracking-wide text-slate-400">
                              {p.brandName}
                            </span>
                          )}
                          <span className="block truncate text-sm text-slate-800">
                            {p.name}
                          </span>
                        </div>
                        <div className="shrink-0 text-right">
                          {onSale ? (
                            <>
                              <span className="block text-sm font-bold text-orange-600">
                                {formatPrice(p.salePrice!)}
                              </span>
                              <span className="block text-[11px] text-slate-400 line-through">
                                {formatPrice(p.price)}
                              </span>
                            </>
                          ) : (
                            <span className="block text-sm font-bold text-blue-950">
                              {formatPrice(p.price)}
                            </span>
                          )}
                        </div>
                      </li>
                    );
                  })}
                </ul>
              </li>
            )}

            {data.categories.length > 0 && (
              <li role="presentation">
                <ul role="group" aria-label="Categories" className="contents">
                  <li
                    role="presentation"
                    className="border-t border-slate-100 px-3 pb-1 pt-2 text-[11px] font-semibold uppercase tracking-wide text-slate-400"
                  >
                    Categories
                  </li>
                  {data.categories.map((c, i) => {
                    const index = categoryOffset + i;
                    return (
                      <li
                        key={c.slug}
                        id={optionId(index)}
                        role="option"
                        aria-selected={active === index}
                        onMouseEnter={() => setActive(index)}
                        onMouseDown={(e) => {
                          e.preventDefault();
                          go(`/category/${c.slug}`);
                        }}
                        className={`cursor-pointer px-3 py-1.5 text-sm text-slate-700 ${
                          active === index ? "bg-orange-50" : ""
                        }`}
                      >
                        {c.name}
                      </li>
                    );
                  })}
                </ul>
              </li>
            )}

            {data.brands.length > 0 && (
              <li role="presentation">
                <ul role="group" aria-label="Brands" className="contents">
                  <li
                    role="presentation"
                    className="border-t border-slate-100 px-3 pb-1 pt-2 text-[11px] font-semibold uppercase tracking-wide text-slate-400"
                  >
                    Brands
                  </li>
                  {data.brands.map((b, i) => {
                    const index = brandOffset + i;
                    return (
                      <li
                        key={b.slug}
                        id={optionId(index)}
                        role="option"
                        aria-selected={active === index}
                        onMouseEnter={() => setActive(index)}
                        onMouseDown={(e) => {
                          e.preventDefault();
                          go(`/search?q=${encodeURIComponent(b.name)}`);
                        }}
                        className={`cursor-pointer px-3 py-1.5 text-sm text-slate-700 ${
                          active === index ? "bg-orange-50" : ""
                        }`}
                      >
                        {b.name}
                      </li>
                    );
                  })}
                </ul>
              </li>
            )}
          </ul>

          {data.totalProducts > data.products.length && (
            <Link
              href={`/search?q=${encodeURIComponent(q.trim())}`}
              onClick={() => setOpen(false)}
              className="block border-t border-slate-100 bg-slate-50 px-3 py-2 text-center text-xs font-semibold text-blue-950 hover:text-orange-600"
            >
              See all {data.totalProducts} results
            </Link>
          )}
        </div>
      )}

      {/* Announces result counts to screen readers without stealing focus. */}
      <div role="status" aria-live="polite" className="sr-only">
        {expanded ? `${rows.length} suggestions available.` : ""}
      </div>
    </div>
  );
}
