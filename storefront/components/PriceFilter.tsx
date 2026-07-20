"use client";

// The one part of the filter sidebar that genuinely needs client state: two
// number inputs that hold un-submitted text until "Apply" is pressed. Everything
// else in FilterSidebar.tsx is server-rendered links.
//
// Current params are passed in as props (resolved server-side) rather than read
// with useSearchParams, so this island subscribes to nothing and needs no
// Suspense boundary.

import { useRouter } from "next/navigation";
import { useState } from "react";

interface Props {
  basePath: string;
  params: Record<string, string>;
  range: { min: number; max: number };
}

export default function PriceFilter({ basePath, params, range }: Props) {
  const router = useRouter();
  const [priceMin, priceMax] = (params.price ?? "-").split("-");
  const [minInput, setMinInput] = useState(priceMin ?? "");
  const [maxInput, setMaxInput] = useState(priceMax ?? "");

  function applyPrice() {
    const sp = new URLSearchParams(params);
    const min = minInput.trim();
    const max = maxInput.trim();
    if (min || max) {
      sp.set("price", `${min || range.min}-${max || range.max}`);
    } else {
      sp.delete("price");
    }
    sp.delete("page"); // any filter change resets pagination
    const qs = sp.toString();
    router.push(qs ? `${basePath}?${qs}` : basePath, { scroll: false });
  }

  return (
    <section className="rounded-lg border border-slate-200 bg-white p-4">
      <h3 className="mb-3 text-sm font-semibold text-blue-950">Price</h3>
      {range.max > 0 && (
        <p className="mb-2 text-xs text-slate-500">
          Range: Rs. {range.min.toLocaleString("en-PK")} – Rs.{" "}
          {range.max.toLocaleString("en-PK")}
        </p>
      )}
      <div className="flex items-center gap-2">
        <input
          type="number"
          inputMode="numeric"
          min={0}
          placeholder="Min"
          value={minInput}
          onChange={(e) => setMinInput(e.target.value)}
          className="w-full rounded border border-slate-300 px-2 py-1.5 text-sm focus:border-orange-500 focus:outline-none"
          aria-label="Minimum price"
        />
        <span className="text-slate-400">–</span>
        <input
          type="number"
          inputMode="numeric"
          min={0}
          placeholder="Max"
          value={maxInput}
          onChange={(e) => setMaxInput(e.target.value)}
          className="w-full rounded border border-slate-300 px-2 py-1.5 text-sm focus:border-orange-500 focus:outline-none"
          aria-label="Maximum price"
        />
      </div>
      <button
        type="button"
        onClick={applyPrice}
        className="mt-2 w-full rounded bg-blue-950 py-1.5 text-xs font-semibold text-white transition hover:bg-blue-900"
      >
        Apply
      </button>
    </section>
  );
}
