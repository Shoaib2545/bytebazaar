"use client";

// Typed client for authenticated account features: orders, addresses, wishlist.

import { apiFetch, readErrorMessage } from "./auth-client";
import type { ProductListItem } from "./api";

export type OrderStatus =
  | "Pending"
  | "Confirmed"
  | "Shipped"
  | "Delivered"
  | "Cancelled";

export interface OrderSummary {
  orderNumber: string;
  createdAt: string;
  status: OrderStatus;
  total: number;
  itemCount: number;
}

export interface PagedOrders {
  items: OrderSummary[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface OrderShippingAddress {
  fullName: string;
  phone: string;
  email: string;
  addressLine: string;
  city: string;
  region: string;
}

export interface OrderItemLine {
  productId: string;
  name: string;
  slug: string;
  imageUrl: string | null;
  unitPrice: number;
  quantity: number;
  lineTotal: number;
}

export interface OrderHistoryEntry {
  status: OrderStatus;
  note: string | null;
  createdAt: string;
}

export interface OrderDetail {
  orderNumber: string;
  createdAt: string;
  status: OrderStatus;
  paymentMethod: string;
  subtotal: number;
  /** Coupon applied to the order, if any. */
  couponCode: string | null;
  /** Discount amount from the coupon; 0 when none. */
  discount: number;
  shippingFee: number;
  total: number;
  shippingAddress: OrderShippingAddress;
  items: OrderItemLine[];
  history: OrderHistoryEntry[];
}

export interface Address {
  id: string;
  fullName: string;
  phone: string;
  addressLine: string;
  city: string;
  region: string;
  isDefault: boolean;
}

export interface AddressInput {
  fullName: string;
  phone: string;
  addressLine: string;
  city: string;
  region: string;
  isDefault: boolean;
}

const jsonHeaders = { "Content-Type": "application/json" };

async function request<T>(
  path: string,
  init: RequestInit = {},
  fallbackError = "Request failed. Please try again."
): Promise<T> {
  const res = await apiFetch(path, init);
  if (!res.ok) {
    throw new Error(await readErrorMessage(res, fallbackError));
  }
  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}

// ---------- orders ----------

export function getOrders(page = 1, pageSize = 10): Promise<PagedOrders> {
  return request<PagedOrders>(
    `/api/orders?page=${page}&pageSize=${pageSize}`,
    {},
    "Could not load your orders."
  );
}

export function getOrder(orderNumber: string): Promise<OrderDetail> {
  return request<OrderDetail>(
    `/api/orders/${encodeURIComponent(orderNumber)}`,
    {},
    "Could not load this order."
  );
}

// ---------- addresses ----------

export function getAddresses(): Promise<Address[]> {
  return request<Address[]>("/api/addresses", {}, "Could not load addresses.");
}

export function createAddress(input: AddressInput): Promise<Address> {
  return request<Address>("/api/addresses", {
    method: "POST",
    headers: jsonHeaders,
    body: JSON.stringify(input),
  });
}

export function updateAddress(
  id: string,
  input: AddressInput
): Promise<Address> {
  return request<Address>(`/api/addresses/${encodeURIComponent(id)}`, {
    method: "PUT",
    headers: jsonHeaders,
    body: JSON.stringify(input),
  });
}

export function deleteAddress(id: string): Promise<void> {
  return request<void>(`/api/addresses/${encodeURIComponent(id)}`, {
    method: "DELETE",
  });
}

// ---------- wishlist ----------

export function getWishlist(): Promise<ProductListItem[]> {
  return request<ProductListItem[]>(
    "/api/wishlist",
    {},
    "Could not load your wishlist."
  );
}

export function addToWishlist(productId: string): Promise<void> {
  return request<void>(`/api/wishlist/${encodeURIComponent(productId)}`, {
    method: "POST",
  });
}

export function removeFromWishlist(productId: string): Promise<void> {
  return request<void>(`/api/wishlist/${encodeURIComponent(productId)}`, {
    method: "DELETE",
  });
}
