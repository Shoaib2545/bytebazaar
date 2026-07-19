"use client";

// Client-side providers: cart state + wishlist state, shared app-wide.
// Mounted once from the root layout; children stay server-rendered.

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
} from "react";
import {
  CART_EVENT,
  ensureSession,
  useAuth,
} from "@/lib/auth-client";
import {
  Cart,
  EMPTY_CART,
  addCartItem,
  getCart,
  removeCartItem,
  updateCartItem,
} from "@/lib/cart";
import {
  addToWishlist,
  getWishlist,
  removeFromWishlist,
} from "@/lib/account";

// ---------------- Cart context ----------------

interface CartContextValue {
  cart: Cart;
  itemCount: number;
  /** true until the first cart load finishes */
  loading: boolean;
  addItem: (productId: string, quantity: number) => Promise<void>;
  updateItem: (productId: string, quantity: number) => Promise<void>;
  removeItem: (productId: string) => Promise<void>;
  refresh: () => Promise<void>;
}

const CartContext = createContext<CartContextValue | null>(null);

export function useCart(): CartContextValue {
  const ctx = useContext(CartContext);
  if (!ctx) throw new Error("useCart must be used inside <Providers>");
  return ctx;
}

function CartProvider({ children }: { children: React.ReactNode }) {
  const [cart, setCart] = useState<Cart>(EMPTY_CART);
  const [loading, setLoading] = useState(true);

  const refresh = useCallback(async () => {
    try {
      setCart(await getCart());
    } catch {
      // API down or unreachable — keep whatever we had.
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    let cancelled = false;
    ensureSession().then(() => {
      if (!cancelled) refresh();
    });
    const onCartChanged = () => refresh();
    window.addEventListener(CART_EVENT, onCartChanged);
    return () => {
      cancelled = true;
      window.removeEventListener(CART_EVENT, onCartChanged);
    };
  }, [refresh]);

  const addItem = useCallback(async (productId: string, quantity: number) => {
    setCart(await addCartItem(productId, quantity));
  }, []);

  const updateItem = useCallback(
    async (productId: string, quantity: number) => {
      setCart(await updateCartItem(productId, quantity));
    },
    []
  );

  const removeItem = useCallback(async (productId: string) => {
    setCart(await removeCartItem(productId));
  }, []);

  const value = useMemo<CartContextValue>(
    () => ({
      cart,
      itemCount: cart.itemCount,
      loading,
      addItem,
      updateItem,
      removeItem,
      refresh,
    }),
    [cart, loading, addItem, updateItem, removeItem, refresh]
  );

  return <CartContext.Provider value={value}>{children}</CartContext.Provider>;
}

// ---------------- Wishlist context ----------------

export type WishlistToggleResult = "added" | "removed" | "auth-required";

interface WishlistContextValue {
  /** null until loaded (or while logged out) */
  ids: ReadonlySet<string> | null;
  isWishlisted: (productId: string) => boolean;
  /** Toggles membership. Returns "auth-required" for guests. */
  toggle: (productId: string) => Promise<WishlistToggleResult>;
  refresh: () => Promise<void>;
}

const WishlistContext = createContext<WishlistContextValue | null>(null);

export function useWishlist(): WishlistContextValue {
  const ctx = useContext(WishlistContext);
  if (!ctx) throw new Error("useWishlist must be used inside <Providers>");
  return ctx;
}

function WishlistProvider({ children }: { children: React.ReactNode }) {
  const { status } = useAuth();
  const [loadedIds, setLoadedIds] = useState<Set<string> | null>(null);

  // Only expose wishlist state while authenticated (derived, no effect needed).
  const ids = status === "authenticated" ? loadedIds : null;

  const refresh = useCallback(async () => {
    try {
      const items = await getWishlist();
      setLoadedIds(new Set(items.map((p) => p.id)));
    } catch {
      // API down — leave as-is.
    }
  }, []);

  useEffect(() => {
    if (status !== "authenticated") return;
    let cancelled = false;
    (async () => {
      try {
        const items = await getWishlist();
        if (!cancelled) setLoadedIds(new Set(items.map((p) => p.id)));
      } catch {
        // API down — leave as-is.
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [status]);

  const isWishlisted = useCallback(
    (productId: string) => ids?.has(productId) ?? false,
    [ids]
  );

  const toggle = useCallback(
    async (productId: string): Promise<WishlistToggleResult> => {
      if (status !== "authenticated") return "auth-required";
      if (ids?.has(productId)) {
        await removeFromWishlist(productId);
        setLoadedIds((prev) => {
          const next = new Set(prev);
          next.delete(productId);
          return next;
        });
        return "removed";
      }
      await addToWishlist(productId);
      setLoadedIds((prev) => {
        const next = new Set(prev);
        next.add(productId);
        return next;
      });
      return "added";
    },
    [status, ids]
  );

  const value = useMemo<WishlistContextValue>(
    () => ({ ids, isWishlisted, toggle, refresh }),
    [ids, isWishlisted, toggle, refresh]
  );

  return (
    <WishlistContext.Provider value={value}>
      {children}
    </WishlistContext.Provider>
  );
}

// ---------------- combined export ----------------

export default function Providers({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <CartProvider>
      <WishlistProvider>{children}</WishlistProvider>
    </CartProvider>
  );
}
