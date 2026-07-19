"use client";

import { usePathname, useRouter } from "next/navigation";
import { useState } from "react";
import { useWishlist } from "./Providers";

interface Props {
  productId: string;
  className?: string;
}

/**
 * Heart toggle. Authenticated users toggle wishlist membership;
 * guests are sent to /login?next=<current page>.
 */
export default function WishlistButton({ productId, className = "" }: Props) {
  const { isWishlisted, toggle } = useWishlist();
  const router = useRouter();
  const pathname = usePathname();
  const [busy, setBusy] = useState(false);

  const active = isWishlisted(productId);

  async function onClick(e: React.MouseEvent) {
    e.preventDefault();
    e.stopPropagation();
    if (busy) return;
    setBusy(true);
    try {
      const result = await toggle(productId);
      if (result === "auth-required") {
        router.push(`/login?next=${encodeURIComponent(pathname)}`);
      }
    } catch {
      // API error — leave the heart unchanged.
    } finally {
      setBusy(false);
    }
  }

  return (
    <button
      type="button"
      onClick={onClick}
      disabled={busy}
      aria-label={active ? "Remove from wishlist" : "Add to wishlist"}
      title={active ? "Remove from wishlist" : "Add to wishlist"}
      className={`flex h-8 w-8 items-center justify-center rounded-full bg-white/90 shadow transition hover:scale-110 ${
        active ? "text-red-500" : "text-slate-400 hover:text-red-500"
      } ${className}`}
    >
      <svg
        xmlns="http://www.w3.org/2000/svg"
        viewBox="0 0 24 24"
        fill={active ? "currentColor" : "none"}
        strokeWidth={1.8}
        stroke="currentColor"
        className="h-5 w-5"
      >
        <path
          strokeLinecap="round"
          strokeLinejoin="round"
          d="M21 8.25c0-2.485-2.099-4.5-4.688-4.5-1.935 0-3.597 1.126-4.312 2.733-.715-1.607-2.377-2.733-4.313-2.733C5.1 3.75 3 5.765 3 8.25c0 7.22 9 12 9 12s9-4.78 9-12Z"
        />
      </svg>
    </button>
  );
}
