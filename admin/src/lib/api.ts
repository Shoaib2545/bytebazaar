import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios';
import type {
  AdminProduct,
  AdminProductListItem,
  AttributeDefinition,
  AttributeInput,
  AuthResponse,
  Brand,
  BrandInput,
  Category,
  CategoryInput,
  Id,
  Paged,
  ProductInput,
  ProductListParams,
} from './types';

const TOKEN_STORAGE_KEY = 'bytebazaar_admin_access_token';

// Module-level access token, bootstrapped from localStorage.
let accessToken: string | null = localStorage.getItem(TOKEN_STORAGE_KEY);

export function setAccessToken(token: string | null): void {
  accessToken = token;
  if (token) {
    localStorage.setItem(TOKEN_STORAGE_KEY, token);
  } else {
    localStorage.removeItem(TOKEN_STORAGE_KEY);
  }
}

export function getAccessToken(): string | null {
  return accessToken;
}

const baseURL: string = import.meta.env.VITE_API_URL ?? 'http://localhost:5080';

export const api = axios.create({
  baseURL,
  withCredentials: true,
});

api.interceptors.request.use((config) => {
  if (accessToken) {
    config.headers.set('Authorization', `Bearer ${accessToken}`);
  }
  return config;
});

let refreshPromise: Promise<AuthResponse | null> | null = null;

/**
 * Calls POST /api/auth/refresh (using the httpOnly refresh cookie).
 * Deduplicated so concurrent 401s trigger a single refresh.
 */
export function refreshSession(): Promise<AuthResponse | null> {
  if (!refreshPromise) {
    refreshPromise = axios
      .post<AuthResponse>(`${baseURL}/api/auth/refresh`, null, { withCredentials: true })
      .then((res) => {
        setAccessToken(res.data.accessToken);
        return res.data;
      })
      .catch(() => {
        setAccessToken(null);
        return null;
      })
      .finally(() => {
        refreshPromise = null;
      });
  }
  return refreshPromise;
}

type RetriableConfig = InternalAxiosRequestConfig & { _retry?: boolean };

api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const original = error.config as RetriableConfig | undefined;
    const url = original?.url ?? '';
    if (
      error.response?.status === 401 &&
      original &&
      !original._retry &&
      !url.includes('/api/auth/')
    ) {
      original._retry = true;
      const session = await refreshSession();
      if (session) {
        original.headers.set('Authorization', `Bearer ${session.accessToken}`);
        return api.request(original);
      }
    }
    return Promise.reject(error);
  },
);

// ---------------------------------------------------------------------------
// Auth
// ---------------------------------------------------------------------------

export async function login(email: string, password: string): Promise<AuthResponse> {
  const res = await api.post<AuthResponse>('/api/auth/login', { email, password });
  setAccessToken(res.data.accessToken);
  return res.data;
}

export async function logout(): Promise<void> {
  try {
    await api.post('/api/auth/logout');
  } finally {
    setAccessToken(null);
  }
}

// ---------------------------------------------------------------------------
// Categories
// ---------------------------------------------------------------------------

export async function listCategories(): Promise<Category[]> {
  const res = await api.get<Category[]>('/api/admin/categories');
  return res.data;
}

export async function createCategory(input: CategoryInput): Promise<Category> {
  const res = await api.post<Category>('/api/admin/categories', input);
  return res.data;
}

export async function updateCategory(id: Id, input: CategoryInput): Promise<Category> {
  const res = await api.put<Category>(`/api/admin/categories/${id}`, input);
  return res.data;
}

export async function deleteCategory(id: Id): Promise<void> {
  await api.delete(`/api/admin/categories/${id}`);
}

// ---------------------------------------------------------------------------
// Attributes
// ---------------------------------------------------------------------------

export async function listCategoryAttributes(categoryId: Id): Promise<AttributeDefinition[]> {
  const res = await api.get<AttributeDefinition[]>(`/api/admin/categories/${categoryId}/attributes`);
  return res.data;
}

export async function createAttribute(input: AttributeInput): Promise<AttributeDefinition> {
  const res = await api.post<AttributeDefinition>('/api/admin/attributes', input);
  return res.data;
}

export async function updateAttribute(id: Id, input: AttributeInput): Promise<AttributeDefinition> {
  const res = await api.put<AttributeDefinition>(`/api/admin/attributes/${id}`, input);
  return res.data;
}

export async function deleteAttribute(id: Id): Promise<void> {
  await api.delete(`/api/admin/attributes/${id}`);
}

// ---------------------------------------------------------------------------
// Brands
// ---------------------------------------------------------------------------

export async function listBrands(): Promise<Brand[]> {
  const res = await api.get<Brand[]>('/api/admin/brands');
  return res.data;
}

export async function createBrand(input: BrandInput): Promise<Brand> {
  const res = await api.post<Brand>('/api/admin/brands', input);
  return res.data;
}

export async function updateBrand(id: Id, input: BrandInput): Promise<Brand> {
  const res = await api.put<Brand>(`/api/admin/brands/${id}`, input);
  return res.data;
}

export async function deleteBrand(id: Id): Promise<void> {
  await api.delete(`/api/admin/brands/${id}`);
}

// ---------------------------------------------------------------------------
// Products
// ---------------------------------------------------------------------------

export async function listProducts(
  params: ProductListParams,
): Promise<Paged<AdminProductListItem>> {
  const res = await api.get<Paged<AdminProductListItem>>('/api/admin/products', {
    params: {
      page: params.page,
      pageSize: params.pageSize,
      search: params.search || undefined,
      categoryId: params.categoryId ?? undefined,
    },
  });
  return res.data;
}

export async function getProduct(id: Id): Promise<AdminProduct> {
  const res = await api.get<AdminProduct>(`/api/admin/products/${id}`);
  return res.data;
}

export async function createProduct(input: ProductInput): Promise<AdminProduct> {
  const res = await api.post<AdminProduct>('/api/admin/products', input);
  return res.data;
}

export async function updateProduct(id: Id, input: ProductInput): Promise<AdminProduct> {
  const res = await api.put<AdminProduct>(`/api/admin/products/${id}`, input);
  return res.data;
}

export async function deleteProduct(id: Id): Promise<void> {
  await api.delete(`/api/admin/products/${id}`);
}
