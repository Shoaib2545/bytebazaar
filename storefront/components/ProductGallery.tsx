"use client";

import { useState } from "react";
import RemoteImage from "./RemoteImage";

// Gallery is full-width on mobile and half of a max-w-7xl (1280px) grid on lg+.
const MAIN_SIZES = "(max-width: 1024px) 100vw, 620px";

export default function ProductGallery({
  images,
  name,
}: {
  images: string[];
  name: string;
}) {
  const [active, setActive] = useState(0);
  const main = images[active] ?? images[0];

  return (
    <div>
      <div className="relative flex aspect-square items-center justify-center overflow-hidden rounded-lg border border-slate-200 bg-white p-6">
        {main ? (
          <RemoteImage
            src={main}
            alt={name}
            sizes={MAIN_SIZES}
            // The product hero is the LCP element on the product page.
            eager
            className="object-contain p-6"
          />
        ) : (
          <div className="flex h-full w-full items-center justify-center text-slate-300">
            <svg
              xmlns="http://www.w3.org/2000/svg"
              fill="none"
              viewBox="0 0 24 24"
              strokeWidth={1}
              stroke="currentColor"
              className="h-24 w-24"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="m2.25 15.75 5.159-5.159a2.25 2.25 0 0 1 3.182 0l5.159 5.159m-1.5-1.5 1.409-1.409a2.25 2.25 0 0 1 3.182 0l2.909 2.909M3.75 21h16.5A1.5 1.5 0 0 0 21.75 19.5V4.5A1.5 1.5 0 0 0 20.25 3H3.75A1.5 1.5 0 0 0 2.25 4.5v15A1.5 1.5 0 0 0 3.75 21Z"
              />
            </svg>
          </div>
        )}
      </div>

      {images.length > 1 && (
        <div className="mt-3 flex gap-2 overflow-x-auto">
          {images.map((img, i) => (
            <button
              key={`${img}-${i}`}
              onClick={() => setActive(i)}
              className={`relative h-16 w-16 shrink-0 overflow-hidden rounded border-2 bg-white p-1 transition ${
                i === active
                  ? "border-orange-500"
                  : "border-slate-200 hover:border-slate-400"
              }`}
              aria-label={`View image ${i + 1} of ${name}`}
            >
              <RemoteImage
                src={img}
                alt=""
                sizes="64px"
                className="object-contain p-1"
              />
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
