"use client";

import { useRouter, useSearchParams } from "next/navigation";
import { Suspense, useState } from "react";
import { CategoryFilters, FilterAttribute } from "@/lib/api";

/** Params that are not attribute/brand/price filters. */
const RESERVED = new Set(["page", "pageSize", "sort", "q"]);

interface Props {
  basePath: string;
  filters: CategoryFilters;
}

function parseValues(param: string | null): string[] {
  return param ? param.split(",").filter(Boolean) : [];
}

function FilterSidebarInner({ basePath, filters }: Props) {
  const router = useRouter();
  const searchParams = useSearchParams();

  const currentPrice = searchParams.get("price");
  const [priceMin, priceMax] = (currentPrice ?? "-").split("-");
  const [minInput, setMinInput] = useState(priceMin ?? "");
  const [maxInput, setMaxInput] = useState(priceMax ?? "");

  function navigate(params: URLSearchParams) {
    params.delete("page"); // any filter change resets pagination
    const qs = params.toString();
    router.push(qs ? `${basePath}?${qs}` : basePath);
  }

  function toggleValue(code: string, value: string, single: boolean) {
    const params = new URLSearchParams(searchParams.toString());
    let values = parseValues(params.get(code));
    if (single) {
      values = values.includes(value) ? [] : [value];
    } else if (values.includes(value)) {
      values = values.filter((v) => v !== value);
    } else {
      values = [...values, value];
    }
    if (values.length) params.set(code, values.join(","));
    else params.delete(code);
    navigate(params);
  }

  function applyPrice() {
    const params = new URLSearchParams(searchParams.toString());
    const min = minInput.trim();
    const max = maxInput.trim();
    if (min || max) {
      params.set(
        "price",
        `${min || filters.priceRange.min}-${max || filters.priceRange.max}`
      );
    } else {
      params.delete("price");
    }
    navigate(params);
  }

  function removeChip(code: string, value?: string) {
    const params = new URLSearchParams(searchParams.toString());
    if (value === undefined) {
      params.delete(code);
    } else {
      const values = parseValues(params.get(code)).filter((v) => v !== value);
      if (values.length) params.set(code, values.join(","));
      else params.delete(code);
    }
    if (code === "price") {
      setMinInput("");
      setMaxInput("");
    }
    navigate(params);
  }

  function clearAll() {
    const params = new URLSearchParams(searchParams.toString());
    for (const key of Array.from(params.keys())) {
      if (!RESERVED.has(key)) params.delete(key);
    }
    setMinInput("");
    setMaxInput("");
    navigate(params);
  }

  // Build active-filter chips from URL state.
  const chips: { code: string; value?: string; label: string }[] = [];
  searchParams.forEach((raw, key) => {
    if (RESERVED.has(key)) return;
    if (key === "price") {
      chips.push({ code: "price", label: `Price: ${raw.replace("-", " – ")}` });
      return;
    }
    const attr = filters.attributes.find((a) => a.code === key);
    const isBrand = key === "brand";
    for (const value of parseValues(raw)) {
      let label = value;
      if (isBrand) {
        const brand = filters.brands.find((b) => b.slug === value);
        label = `Brand: ${brand?.name ?? value}`;
      } else if (attr) {
        label = `${attr.name}: ${value}`;
      }
      chips.push({ code: key, value, label });
    }
  });

  return (
    <aside className="w-full shrink-0 lg:w-64">
      {chips.length > 0 && (
        <div className="mb-4 rounded-lg border border-slate-200 bg-white p-3">
          <div className="mb-2 flex items-center justify-between">
            <span className="text-xs font-semibold uppercase tracking-wide text-slate-500">
              Active filters
            </span>
            <button
              onClick={clearAll}
              className="text-xs font-medium text-orange-600 hover:underline"
            >
              Clear all
            </button>
          </div>
          <div className="flex flex-wrap gap-1.5">
            {chips.map((chip, i) => (
              <button
                key={`${chip.code}-${chip.value ?? "range"}-${i}`}
                onClick={() => removeChip(chip.code, chip.value)}
                className="inline-flex items-center gap-1 rounded-full bg-blue-950 px-2.5 py-1 text-xs text-white transition hover:bg-blue-900"
                title="Remove filter"
              >
                {chip.label}
                <span aria-hidden className="text-orange-400">
                  ×
                </span>
              </button>
            ))}
          </div>
        </div>
      )}

      <div className="space-y-4">
        {/* Price */}
        <section className="rounded-lg border border-slate-200 bg-white p-4">
          <h3 className="mb-3 text-sm font-semibold text-blue-950">Price</h3>
          {filters.priceRange.max > 0 && (
            <p className="mb-2 text-xs text-slate-500">
              Range: Rs. {filters.priceRange.min.toLocaleString("en-PK")} – Rs.{" "}
              {filters.priceRange.max.toLocaleString("en-PK")}
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
            onClick={applyPrice}
            className="mt-2 w-full rounded bg-blue-950 py-1.5 text-xs font-semibold text-white transition hover:bg-blue-900"
          >
            Apply
          </button>
        </section>

        {/* Brands */}
        {filters.brands.length > 0 && (
          <section className="rounded-lg border border-slate-200 bg-white p-4">
            <h3 className="mb-3 text-sm font-semibold text-blue-950">Brand</h3>
            <ul className="max-h-56 space-y-1.5 overflow-y-auto">
              {filters.brands.map((brand) => {
                const selected = parseValues(searchParams.get("brand"));
                const checked = selected.includes(brand.slug);
                return (
                  <li key={brand.id}>
                    <label className="flex cursor-pointer items-center gap-2 text-sm text-slate-700">
                      <input
                        type="checkbox"
                        checked={checked}
                        onChange={() => toggleValue("brand", brand.slug, false)}
                        className="h-4 w-4 rounded border-slate-300 accent-orange-500"
                      />
                      <span className="flex-1">{brand.name}</span>
                      <span className="text-xs text-slate-400">
                        ({brand.count})
                      </span>
                    </label>
                  </li>
                );
              })}
            </ul>
          </section>
        )}

        {/* Attributes */}
        {filters.attributes.map((attr) => (
          <AttributeGroup
            key={attr.code}
            attr={attr}
            selected={parseValues(searchParams.get(attr.code))}
            onToggle={(value, single) => toggleValue(attr.code, value, single)}
          />
        ))}
      </div>
    </aside>
  );
}

function AttributeGroup({
  attr,
  selected,
  onToggle,
}: {
  attr: FilterAttribute;
  selected: string[];
  onToggle: (value: string, single: boolean) => void;
}) {
  if (!attr.options.length) return null;
  const single = attr.widget === "Radio";

  return (
    <section className="rounded-lg border border-slate-200 bg-white p-4">
      <h3 className="mb-3 text-sm font-semibold text-blue-950">{attr.name}</h3>
      <ul className="max-h-56 space-y-1.5 overflow-y-auto">
        {attr.options.map((opt) => {
          const checked = selected.includes(opt.value);
          return (
            <li key={opt.value}>
              <label className="flex cursor-pointer items-center gap-2 text-sm text-slate-700">
                <input
                  type={single ? "radio" : "checkbox"}
                  checked={checked}
                  onChange={() => onToggle(opt.value, single)}
                  onClick={() => {
                    // allow unselecting a radio
                    if (single && checked) onToggle(opt.value, single);
                  }}
                  className="h-4 w-4 border-slate-300 accent-orange-500"
                />
                <span className="flex-1">{opt.value}</span>
                <span className="text-xs text-slate-400">({opt.count})</span>
              </label>
            </li>
          );
        })}
      </ul>
    </section>
  );
}

export default function FilterSidebar(props: Props) {
  return (
    <Suspense
      fallback={<aside className="w-full shrink-0 lg:w-64" />}
    >
      <FilterSidebarInner {...props} />
    </Suspense>
  );
}
