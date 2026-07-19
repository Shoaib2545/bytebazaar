import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react';
import * as apiClient from './api';
import type { AuthUser } from './types';

export const ALLOWED_ROLES = ['Admin', 'Staff'];

export function hasAdminAccess(user: AuthUser | null): boolean {
  return !!user && user.roles.some((r) => ALLOWED_ROLES.includes(r));
}

interface AuthContextValue {
  user: AuthUser | null;
  loading: boolean;
  signIn: (email: string, password: string) => Promise<AuthUser>;
  signOut: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    // Bootstrap the session from the httpOnly refresh cookie.
    apiClient
      .refreshSession()
      .then((session) => {
        if (!cancelled) setUser(session?.user ?? null);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const signIn = useCallback(async (email: string, password: string) => {
    const res = await apiClient.login(email, password);
    setUser(res.user);
    return res.user;
  }, []);

  const signOut = useCallback(async () => {
    await apiClient.logout();
    setUser(null);
  }, []);

  const value = useMemo(
    () => ({ user, loading, signIn, signOut }),
    [user, loading, signIn, signOut],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
}
