"use client";

// Header search box, split in two for first-load performance.
//
// This shell is deliberately tiny: a real `<form action="/search" method="get">`
// that works with no JavaScript at all, plus one piece of state. The full
// search-as-you-type combobox (SearchSuggest.tsx — ~400 lines of JSX plus
// next/image, the price formatter and the suggest client) is `import()`ed only
// once the shopper touches the box, so none of it is parsed, evaluated or
// hydrated during initial page load.
//
// That matters because the header renders on every route, twice (desktop +
// mobile), and the dropdown is by definition useless until first interaction.
//
// Loading is triggered on pointer/touch/focus, i.e. as the tap begins, so in
// practice the real combobox is mounted by the time the caret lands.

import { useSearchParams } from "next/navigation";
import {
  ComponentType,
  FormEvent,
  Suspense,
  useCallback,
  useRef,
  useState,
} from "react";
import type { SearchSuggestProps } from "./SearchSuggest";

function SearchIcon() {
  return (
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
  );
}

/**
 * Module-level cache of the resolved component. Two SearchBars are mounted at
 * once (desktop + mobile) and a shopper may focus one after the other — the
 * chunk should be fetched at most once per page load.
 */
let suggestModule: ComponentType<SearchSuggestProps> | null = null;

function SearchBarShell() {
  const searchParams = useSearchParams();
  const [q, setQ] = useState(searchParams.get("q") ?? "");
  const [Suggest, setSuggest] =
    useState<ComponentType<SearchSuggestProps> | null>(suggestModule);
  const loading = useRef(false);

  const enhance = useCallback(() => {
    if (suggestModule) {
      const resolved = suggestModule;
      setSuggest(() => resolved);
      return;
    }
    if (loading.current) return;
    loading.current = true;
    import("./SearchSuggest")
      .then((mod) => {
        suggestModule = mod.default;
        // Store the component itself — the updater form would otherwise treat
        // a function value as a lazy state initialiser.
        setSuggest(() => mod.default);
      })
      .catch(() => {
        // Chunk failed to load: the plain form below still submits to /search,
        // so search keeps working. Allow a retry on the next interaction.
        loading.current = false;
      });
  }, []);

  // Swap in the real combobox, carrying over anything typed while it loaded.
  if (Suggest) return <Suggest initialQuery={q} autoFocus />;

  function onSubmit(event: FormEvent) {
    // Empty query: nothing to search for, so suppress the native navigation.
    if (!q.trim()) event.preventDefault();
  }

  return (
    <div className="relative w-full max-w-xl">
      <form
        action="/search"
        method="get"
        onSubmit={onSubmit}
        className="flex w-full"
        role="search"
      >
        <input
          type="text"
          name="q"
          value={q}
          onChange={(e) => {
            setQ(e.target.value);
            enhance();
          }}
          onFocus={enhance}
          onPointerDown={enhance}
          placeholder="Search for graphics cards, CPUs, laptops..."
          className="w-full rounded-l-md border-0 bg-white px-4 py-2 text-sm text-slate-900 placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-orange-500"
          aria-label="Search products"
          autoComplete="off"
          spellCheck={false}
        />
        <button
          type="submit"
          className="rounded-r-md bg-orange-500 px-4 py-2 text-sm font-semibold text-white transition hover:bg-orange-600"
          aria-label="Search"
        >
          <SearchIcon />
        </button>
      </form>
    </div>
  );
}

export default function SearchBar() {
  return (
    <Suspense
      fallback={<div className="h-9 w-full max-w-xl rounded-md bg-white/20" />}
    >
      <SearchBarShell />
    </Suspense>
  );
}
