"use client";

// Client-side auth for the storefront.
//
// - The access token lives ONLY in memory (never localStorage).
// - The session is bootstrapped on first use via POST /api/auth/refresh,
//   which relies on the httpOnly `bb_refresh` cookie.
// - `apiFetch` attaches the bearer token and, on a 401 while authenticated,
//   refreshes once and retries the request.
// - Right after a successful login/register the anonymous cookie cart is
//   merged into the user's cart via POST /api/cart/merge.

import { useSyncExternalStore } from "react";

const API_BASE = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5080";

export interface AuthUser {
  id: string;
  email: string;
  fullName: string;
  roles: string[];
}

export interface AuthResponse {
  accessToken: string;
  user: AuthUser;
}

export type AuthStatus = "loading" | "authenticated" | "guest";

export interface AuthState {
  status: AuthStatus;
  user: AuthUser | null;
}

/** Fired on window whenever login/logout/refresh changes the session. */
export const AUTH_EVENT = "bb-auth-changed";
/** Fired on window whenever the server-side cart likely changed (merge, logout). */
export const CART_EVENT = "bb-cart-changed";

// ---------- module-level store ----------

let accessToken: string | null = null;
let state: AuthState = { status: "loading", user: null };
const listeners = new Set<() => void>();

const SERVER_STATE: AuthState = { status: "loading", user: null };

function setState(next: AuthState) {
  state = next;
  listeners.forEach((l) => l());
  if (typeof window !== "undefined") {
    window.dispatchEvent(new Event(AUTH_EVENT));
  }
}

export function subscribeAuth(callback: () => void): () => void {
  listeners.add(callback);
  return () => {
    listeners.delete(callback);
  };
}

export function getAuthState(): AuthState {
  return state;
}

function getServerAuthState(): AuthState {
  return SERVER_STATE;
}

/** React hook — current auth state ("loading" until the bootstrap finishes). */
export function useAuth(): AuthState {
  return useSyncExternalStore(subscribeAuth, getAuthState, getServerAuthState);
}

export function getAccessToken(): string | null {
  return accessToken;
}

// ---------- helpers ----------

/** Extract a human-friendly error message from an API response. */
export async function readErrorMessage(
  res: Response,
  fallback = "Request failed. Please try again."
): Promise<string> {
  try {
    const data = await res.json();
    if (typeof data?.message === "string") return data.message;
    if (typeof data?.detail === "string") return data.detail;
    if (typeof data?.title === "string") return data.title;
    if (data?.errors && typeof data.errors === "object") {
      const first = Object.values(data.errors as Record<string, unknown>)[0];
      if (Array.isArray(first) && typeof first[0] === "string") return first[0];
    }
  } catch {
    // ignore body parse errors
  }
  return fallback;
}

function applyAuth(auth: AuthResponse) {
  accessToken = auth.accessToken;
  setState({ status: "authenticated", user: auth.user });
}

function applyGuest() {
  accessToken = null;
  setState({ status: "guest", user: null });
}

// ---------- session bootstrap & refresh ----------

let bootstrapPromise: Promise<void> | null = null;

/**
 * Bootstrap the session exactly once per page load by exchanging the
 * httpOnly refresh cookie for an access token. Safe to call many times.
 */
export function ensureSession(): Promise<void> {
  if (typeof window === "undefined") return Promise.resolve();
  if (!bootstrapPromise) {
    bootstrapPromise = refreshSession().then(() => undefined);
  }
  return bootstrapPromise;
}

let refreshPromise: Promise<boolean> | null = null;

/** Single-flight refresh. Resolves true when a new access token was obtained. */
function refreshSession(): Promise<boolean> {
  if (!refreshPromise) {
    refreshPromise = (async () => {
      try {
        const res = await fetch(`${API_BASE}/api/auth/refresh`, {
          method: "POST",
          credentials: "include",
        });
        if (res.ok) {
          applyAuth((await res.json()) as AuthResponse);
          return true;
        }
      } catch {
        // API unreachable — treat as guest.
      }
      applyGuest();
      return false;
    })().finally(() => {
      refreshPromise = null;
    });
  }
  return refreshPromise;
}

// ---------- authenticated fetch ----------

/**
 * Fetch against the API with credentials (cart + refresh cookies) and the
 * in-memory bearer token when present. On a 401 while we hold a token,
 * refresh once and retry the request a single time.
 */
export async function apiFetch(
  path: string,
  init: RequestInit = {}
): Promise<Response> {
  await ensureSession();

  const doFetch = () => {
    const headers = new Headers(init.headers);
    headers.set("Accept", "application/json");
    if (accessToken) headers.set("Authorization", `Bearer ${accessToken}`);
    return fetch(`${API_BASE}${path}`, {
      ...init,
      headers,
      credentials: "include",
    });
  };

  let res = await doFetch();
  if (res.status === 401 && accessToken) {
    const refreshed = await refreshSession();
    if (refreshed) res = await doFetch();
  }
  return res;
}

// ---------- cart merge (called right after login/register) ----------

async function mergeGuestCart(): Promise<void> {
  try {
    await fetch(`${API_BASE}/api/cart/merge`, {
      method: "POST",
      credentials: "include",
      headers: accessToken
        ? { Authorization: `Bearer ${accessToken}` }
        : undefined,
    });
  } catch {
    // best-effort — the user still has their account cart
  }
  if (typeof window !== "undefined") {
    window.dispatchEvent(new Event(CART_EVENT));
  }
}

// ---------- auth actions ----------

async function postAuth(
  path: string,
  body: Record<string, string>
): Promise<AuthResponse> {
  const res = await fetch(`${API_BASE}${path}`, {
    method: "POST",
    credentials: "include",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  if (!res.ok) {
    throw new Error(await readErrorMessage(res));
  }
  const auth = (await res.json()) as AuthResponse;
  applyAuth(auth);
  await mergeGuestCart();
  return auth;
}

export function login(email: string, password: string): Promise<AuthResponse> {
  return postAuth("/api/auth/login", { email, password });
}

export function register(
  email: string,
  password: string,
  fullName: string,
  phone: string
): Promise<AuthResponse> {
  return postAuth("/api/auth/register", { email, password, fullName, phone });
}

export async function logout(): Promise<void> {
  try {
    await fetch(`${API_BASE}/api/auth/logout`, {
      method: "POST",
      credentials: "include",
    });
  } catch {
    // ignore network errors on logout
  }
  applyGuest();
  if (typeof window !== "undefined") {
    window.dispatchEvent(new Event(CART_EVENT));
  }
}
