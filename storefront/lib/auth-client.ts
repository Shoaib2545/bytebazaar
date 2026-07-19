"use client";

// Minimal client-side auth: access token + user kept in localStorage,
// refresh cookie handled by the API (httpOnly, credentials: "include").

const API_BASE =
  process.env.NEXT_PUBLIC_API_URL || "http://localhost:5080";

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

const TOKEN_KEY = "bb.accessToken";
const USER_KEY = "bb.user";
export const AUTH_EVENT = "bb-auth-changed";

export function getStoredUser(): AuthUser | null {
  if (typeof window === "undefined") return null;
  try {
    const raw = window.localStorage.getItem(USER_KEY);
    return raw ? (JSON.parse(raw) as AuthUser) : null;
  } catch {
    return null;
  }
}

export function getAccessToken(): string | null {
  if (typeof window === "undefined") return null;
  return window.localStorage.getItem(TOKEN_KEY);
}

function storeAuth(auth: AuthResponse) {
  window.localStorage.setItem(TOKEN_KEY, auth.accessToken);
  window.localStorage.setItem(USER_KEY, JSON.stringify(auth.user));
  window.dispatchEvent(new Event(AUTH_EVENT));
}

function clearAuth() {
  window.localStorage.removeItem(TOKEN_KEY);
  window.localStorage.removeItem(USER_KEY);
  window.dispatchEvent(new Event(AUTH_EVENT));
}

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
    let message = "Request failed. Please try again.";
    try {
      const data = await res.json();
      if (typeof data?.message === "string") message = data.message;
      else if (typeof data?.title === "string") message = data.title;
    } catch {
      // ignore body parse errors
    }
    throw new Error(message);
  }
  const auth = (await res.json()) as AuthResponse;
  storeAuth(auth);
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
  clearAuth();
}
