"use client";

// Typed client for the cart API. Works for both guests (httpOnly bb_cart_id
// cookie) and authenticated users — apiFetch always sends credentials and a
// bearer token when one is in memory.

import { apiFetch, readErrorMessage } from "./auth-client";

export interface CartItem {
  productId: string;
  name: string;
  slug: string;
  imageUrl: string | null;
  unitPrice: number;
  quantity: number;
  lineTotal: number;
  stock: number;
}

export interface Cart {
  items: CartItem[];
  subtotal: number;
  itemCount: number;
}

export const EMPTY_CART: Cart = { items: [], subtotal: 0, itemCount: 0 };

async function cartRequest(path: string, init: RequestInit = {}): Promise<Cart> {
  const res = await apiFetch(path, init);
  if (!res.ok) {
    throw new Error(await readErrorMessage(res, "Cart request failed."));
  }
  return (await res.json()) as Cart;
}

const jsonHeaders = { "Content-Type": "application/json" };

export function getCart(): Promise<Cart> {
  return cartRequest("/api/cart");
}

/** Add a product (or increment its quantity). 400 when quantity > stock. */
export function addCartItem(productId: string, quantity: number): Promise<Cart> {
  return cartRequest("/api/cart/items", {
    method: "POST",
    headers: jsonHeaders,
    body: JSON.stringify({ productId, quantity }),
  });
}

/** Set an item's quantity. 0 removes the line. */
export function updateCartItem(
  productId: string,
  quantity: number
): Promise<Cart> {
  return cartRequest(`/api/cart/items/${encodeURIComponent(productId)}`, {
    method: "PUT",
    headers: jsonHeaders,
    body: JSON.stringify({ quantity }),
  });
}

export function removeCartItem(productId: string): Promise<Cart> {
  return cartRequest(`/api/cart/items/${encodeURIComponent(productId)}`, {
    method: "DELETE",
  });
}
