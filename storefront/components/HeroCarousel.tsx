"use client";

// Simple auto-rotating hero carousel for Hero-placement banners.
// No external deps — plain state + interval, pauses on hover.

import Link from "next/link";
import { useEffect, useState } from "react";
import type { Banner } from "@/lib/api";
import RemoteImage from "./RemoteImage";

const ROTATE_MS = 5000;

// The hero spans the full max-w-7xl (1280px) container.
const HERO_SIZES = "(max-width: 1280px) 100vw, 1280px";

function SlideContent({
  banner,
  eager,
}: {
  banner: Banner;
  eager: boolean;
}) {
  return (
    <>
      {banner.imageUrl && (
        <RemoteImage
          src={banner.imageUrl}
          alt={banner.title}
          sizes={HERO_SIZES}
          // Only the first slide is the LCP candidate; the rest lazy-load so
          // they don't compete for bandwidth during initial paint.
          eager={eager}
          className="object-cover"
        />
      )}
      {/* Darken so overlay text stays readable over any image */}
      <div className="absolute inset-0 bg-gradient-to-r from-blue-950/90 via-blue-950/60 to-blue-950/20" />
      <div className="relative z-10 flex h-full flex-col justify-center px-8 sm:px-12">
        <h2 className="max-w-xl text-2xl font-extrabold leading-tight text-white sm:text-4xl">
          {banner.title}
        </h2>
        {banner.subtitle && (
          <p className="mt-3 max-w-lg text-sm text-white/80 sm:text-base">
            {banner.subtitle}
          </p>
        )}
        {banner.linkUrl && (
          <span className="mt-6 inline-block w-fit rounded-md bg-orange-500 px-6 py-3 text-sm font-semibold text-white transition group-hover:bg-orange-600">
            Shop Now
          </span>
        )}
      </div>
    </>
  );
}

export default function HeroCarousel({ banners }: { banners: Banner[] }) {
  const [index, setIndex] = useState(0);
  const [paused, setPaused] = useState(false);

  // Keep index valid if the banner list changes.
  const safeIndex = banners.length > 0 ? index % banners.length : 0;

  useEffect(() => {
    if (paused || banners.length <= 1) return;
    const id = setInterval(
      () => setIndex((i) => (i + 1) % banners.length),
      ROTATE_MS
    );
    return () => clearInterval(id);
  }, [paused, banners.length]);

  if (banners.length === 0) return null;

  return (
    <section
      aria-roledescription="carousel"
      aria-label="Promotions"
      className="relative h-64 overflow-hidden rounded-2xl bg-blue-950 sm:h-80"
      onMouseEnter={() => setPaused(true)}
      onMouseLeave={() => setPaused(false)}
    >
      {banners.map((banner, i) => {
        const active = i === safeIndex;
        const slide = (
          <div
            key={banner.id}
            aria-hidden={!active}
            className={`group absolute inset-0 transition-opacity duration-700 ${
              active ? "opacity-100" : "pointer-events-none opacity-0"
            }`}
          >
            {banner.linkUrl ? (
              <Link href={banner.linkUrl} className="block h-full w-full">
                <SlideContent banner={banner} eager={i === 0} />
              </Link>
            ) : (
              <SlideContent banner={banner} eager={i === 0} />
            )}
          </div>
        );
        return slide;
      })}

      {banners.length > 1 && (
        <div className="absolute bottom-4 left-1/2 z-20 flex -translate-x-1/2 gap-2">
          {banners.map((banner, i) => (
            <button
              key={banner.id}
              type="button"
              aria-label={`Go to slide ${i + 1}`}
              aria-current={i === safeIndex}
              onClick={() => setIndex(i)}
              className={`h-2 rounded-full transition-all ${
                i === safeIndex
                  ? "w-6 bg-orange-500"
                  : "w-2 bg-white/50 hover:bg-white/80"
              }`}
            />
          ))}
        </div>
      )}
    </section>
  );
}
