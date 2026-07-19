"use client";

import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import { logout, useAuth } from "@/lib/auth-client";

const ACCOUNT_LINKS = [
  { href: "/account/orders", label: "My Orders" },
  { href: "/account/addresses", label: "Addresses" },
  { href: "/account/wishlist", label: "Wishlist" },
];

export default function UserMenu() {
  const { status, user } = useAuth();
  const [open, setOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    function onOutsideClick(e: MouseEvent) {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    }
    document.addEventListener("mousedown", onOutsideClick);
    return () => document.removeEventListener("mousedown", onOutsideClick);
  }, [open]);

  if (status === "authenticated" && user) {
    const firstName = user.fullName?.split(" ")[0] || user.email;
    return (
      <div ref={menuRef} className="relative text-sm">
        <button
          type="button"
          onClick={() => setOpen((v) => !v)}
          aria-expanded={open}
          aria-haspopup="menu"
          className="flex items-center gap-1.5 rounded-md border border-white/30 px-3 py-1.5 text-white/90 transition hover:bg-white/10"
        >
          <span className="max-w-28 truncate">Hi, {firstName}</span>
          <svg
            xmlns="http://www.w3.org/2000/svg"
            viewBox="0 0 20 20"
            fill="currentColor"
            className={`h-4 w-4 transition ${open ? "rotate-180" : ""}`}
          >
            <path
              fillRule="evenodd"
              d="M5.22 8.22a.75.75 0 0 1 1.06 0L10 11.94l3.72-3.72a.75.75 0 1 1 1.06 1.06l-4.25 4.25a.75.75 0 0 1-1.06 0L5.22 9.28a.75.75 0 0 1 0-1.06Z"
              clipRule="evenodd"
            />
          </svg>
        </button>

        {open && (
          <div
            role="menu"
            className="absolute right-0 z-50 mt-2 w-48 overflow-hidden rounded-lg border border-slate-200 bg-white py-1 shadow-lg"
          >
            <div className="border-b border-slate-100 px-4 py-2">
              <p className="truncate text-xs font-semibold text-slate-800">
                {user.fullName}
              </p>
              <p className="truncate text-xs text-slate-400">{user.email}</p>
            </div>
            {ACCOUNT_LINKS.map((link) => (
              <Link
                key={link.href}
                href={link.href}
                role="menuitem"
                onClick={() => setOpen(false)}
                className="block px-4 py-2 text-sm text-slate-700 transition hover:bg-slate-50 hover:text-orange-600"
              >
                {link.label}
              </Link>
            ))}
            <button
              type="button"
              role="menuitem"
              onClick={() => {
                setOpen(false);
                logout();
              }}
              className="block w-full border-t border-slate-100 px-4 py-2 text-left text-sm text-slate-700 transition hover:bg-slate-50 hover:text-red-600"
            >
              Logout
            </button>
          </div>
        )}
      </div>
    );
  }

  return (
    <div className="flex items-center gap-2 text-sm">
      <Link
        href="/login"
        className="rounded-md border border-white/30 px-3 py-1.5 text-white/90 transition hover:bg-white/10"
      >
        Login
      </Link>
      <Link
        href="/register"
        className="hidden rounded-md bg-orange-500 px-3 py-1.5 font-semibold text-white transition hover:bg-orange-600 sm:inline-block"
      >
        Register
      </Link>
    </div>
  );
}
