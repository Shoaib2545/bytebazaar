"use client";

import Link from "next/link";
import { useMemo, useSyncExternalStore } from "react";
import { AUTH_EVENT, AuthUser, logout } from "@/lib/auth-client";

const USER_KEY = "bb.user";

function subscribe(callback: () => void) {
  window.addEventListener(AUTH_EVENT, callback);
  window.addEventListener("storage", callback);
  return () => {
    window.removeEventListener(AUTH_EVENT, callback);
    window.removeEventListener("storage", callback);
  };
}

function getSnapshot(): string | null {
  return window.localStorage.getItem(USER_KEY);
}

function getServerSnapshot(): string | null {
  return null;
}

export default function UserMenu() {
  const rawUser = useSyncExternalStore(subscribe, getSnapshot, getServerSnapshot);

  const user = useMemo<AuthUser | null>(() => {
    if (!rawUser) return null;
    try {
      return JSON.parse(rawUser) as AuthUser;
    } catch {
      return null;
    }
  }, [rawUser]);

  if (user) {
    return (
      <div className="flex items-center gap-3 text-sm">
        <span className="hidden text-white/90 sm:inline">
          Hi, {user.fullName?.split(" ")[0] ?? user.email}
        </span>
        <button
          onClick={() => logout()}
          className="rounded-md border border-white/30 px-3 py-1.5 text-white/90 transition hover:bg-white/10"
        >
          Logout
        </button>
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
