/**
 * Thin API helper used to provision E2E fixture data.
 *
 * Design rule for this suite: **tests never depend on seed data.** The seeder
 * (backend/src/ByteBazaar.Infrastructure/Persistence/DbSeeder.cs) can gain,
 * lose or reprice products at any time, and a suite pinned to "MSI Katana 15"
 * would rot. Instead every spec provisions its own category / attribute /
 * products under a run-unique slug and asserts against those.
 */
import { APIRequestContext, request } from '@playwright/test';

export const API_URL = process.env.E2E_API_URL ?? 'http://localhost:5080';

export const ADMIN_EMAIL = process.env.E2E_ADMIN_EMAIL ?? 'admin@bytebazaar.local';
export const ADMIN_PASSWORD = process.env.E2E_ADMIN_PASSWORD ?? 'Admin123$';

/** Short, collision-proof suffix shared by everything one spec creates. */
export function uniqueSuffix(): string {
  return `${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`;
}

export interface AuthedApi {
  ctx: APIRequestContext;
  token: string;
}

async function login(email: string, password: string): Promise<AuthedApi> {
  const ctx = await request.newContext({ baseURL: API_URL });
  const res = await ctx.post('/api/auth/login', { data: { email, password } });
  if (!res.ok()) {
    throw new Error(`Login failed for ${email}: ${res.status()} ${await res.text()}`);
  }
  const body = await res.json();
  return { token: body.accessToken, ctx };
}

export async function adminApi(): Promise<AuthedApi> {
  return login(ADMIN_EMAIL, ADMIN_PASSWORD);
}

/** Registers a brand-new customer. Returns the credentials so the UI can log in as them. */
export async function registerCustomer(suffix: string) {
  const ctx = await request.newContext({ baseURL: API_URL });
  const email = `e2e.customer.${suffix}@bytebazaar.test`;
  const password = 'E2ePassw0rd!';
  const res = await ctx.post('/api/auth/register', {
    data: { email, password, fullName: `E2E Customer ${suffix}`, phone: '03001234567' },
  });
  if (!res.ok()) {
    throw new Error(`Register failed: ${res.status()} ${await res.text()}`);
  }
  await ctx.dispose();
  return { email, password };
}

function authHeaders(api: AuthedApi) {
  return { Authorization: `Bearer ${api.token}` };
}

async function postJson(api: AuthedApi, url: string, data: unknown) {
  const res = await api.ctx.post(url, { data, headers: authHeaders(api) });
  if (!res.ok()) {
    throw new Error(`POST ${url} -> ${res.status()} ${await res.text()}`);
  }
  return res.json();
}

export async function createCategory(
  api: AuthedApi,
  opts: { name: string; slug: string; parentId?: string | null }
) {
  return postJson(api, '/api/admin/categories', {
    name: opts.name,
    slug: opts.slug,
    parentId: opts.parentId ?? null,
    sortOrder: 999,
    isActive: true,
  });
}

export async function createAttribute(
  api: AuthedApi,
  opts: {
    categoryId: string;
    name: string;
    code: string;
    options: string[];
    type?: string;
    filterWidget?: string;
  }
) {
  return postJson(api, '/api/admin/attributes', {
    categoryId: opts.categoryId,
    name: opts.name,
    code: opts.code,
    type: opts.type ?? 'Select',
    options: opts.options,
    isFilterable: true,
    isRequired: false,
    filterWidget: opts.filterWidget ?? 'Checkbox',
    sortOrder: 1,
  });
}

export async function createProduct(
  api: AuthedApi,
  opts: {
    name: string;
    slug: string;
    categoryId: string;
    price: number;
    stock?: number;
    attributes?: Record<string, string>;
    status?: 'Draft' | 'Active';
  }
) {
  return postJson(api, '/api/admin/products', {
    name: opts.name,
    slug: opts.slug,
    categoryId: opts.categoryId,
    description: 'Created by the ByteBazaar E2E suite.',
    price: opts.price,
    stock: opts.stock ?? 25,
    isFeatured: false,
    status: opts.status ?? 'Active',
    images: [],
    attributes: opts.attributes ?? {},
  });
}

export async function deleteCategory(api: AuthedApi, id: string) {
  await api.ctx.delete(`/api/admin/categories/${id}`, { headers: authHeaders(api) });
}

export async function deleteProduct(api: AuthedApi, id: string) {
  await api.ctx.delete(`/api/admin/products/${id}`, { headers: authHeaders(api) });
}
