"use client";

// Client-side auth guard + sidebar for all /account pages.
// Guests are redirected to /login?next=<current path>.

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect } from "react";
import { ensureSession, logout, useAuth } from "@/lib/auth-client";

const NAV = [
  { href: "/account/orders", label: "Orders" },
  { href: "/account/addresses", label: "Addresses" },
  { href: "/account/wishlist", label: "Wishlist" },
];

export default function AccountLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const { status, user } = useAuth();
  const router = useRouter();
  const pathname = usePathname();

  useEffect(() => {
    ensureSession();
  }, []);

  useEffect(() => {
    if (status === "guest") {
      router.replace(`/login?next=${encodeURIComponent(pathname)}`);
    }
  }, [status, router, pathname]);

  if (status !== "authenticated") {
    return (
      <div className="mx-auto max-w-7xl px-4 py-20 text-center text-sm text-slate-400">
        {status === "loading" ? "Checking your session..." : "Redirecting to login..."}
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-7xl px-4 py-8">
      <div className="grid gap-8 md:grid-cols-[220px_1fr]">
        <aside className="h-fit rounded-lg border border-slate-200 bg-white p-4">
          <div className="border-b border-slate-100 px-2 pb-3">
            <p className="truncate text-sm font-bold text-blue-950">
              {user?.fullName}
            </p>
            <p className="truncate text-xs text-slate-400">{user?.email}</p>
          </div>
          <nav className="mt-2 space-y-1">
            {NAV.map((item) => {
              const active = pathname.startsWith(item.href);
              return (
                <Link
                  key={item.href}
                  href={item.href}
                  className={`block rounded-md px-3 py-2 text-sm font-medium transition ${
                    active
                      ? "bg-orange-50 text-orange-600"
                      : "text-slate-600 hover:bg-slate-50 hover:text-blue-950"
                  }`}
                >
                  {item.label}
                </Link>
              );
            })}
            <button
              type="button"
              onClick={async () => {
                await logout();
                router.push("/");
              }}
              className="block w-full rounded-md px-3 py-2 text-left text-sm font-medium text-slate-600 transition hover:bg-slate-50 hover:text-red-600"
            >
              Logout
            </button>
          </nav>
        </aside>

        <div className="min-w-0">{children}</div>
      </div>
    </div>
  );
}
