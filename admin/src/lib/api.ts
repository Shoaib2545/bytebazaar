import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios';
import type {
  AdminCustomerDetail,
  AdminCustomerListItem,
  AdminCustomerListParams,
  AdminOrderDetail,
  AdminOrderListItem,
  AdminOrderListParams,
  AdminProduct,
  AdminProductListItem,
  AttributeDefinition,
  AttributeInput,
  AuthResponse,
  Banner,
  BannerInput,
  Brand,
  BrandInput,
  BrandReportRow,
  Category,
  CategoryInput,
  CategoryReportRow,
  Coupon,
  CouponInput,
  DashboardSummary,
  Id,
  OrderStatusInput,
  Paged,
  ProductInput,
  ProductListParams,
  ReportParams,
  SalesReportRow,
  StaffCreateInput,
  StaffUpdateInput,
  StaffUser,
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

// ---------------------------------------------------------------------------
// Orders (M4)
// ---------------------------------------------------------------------------

export async function listAdminOrders(
  params: AdminOrderListParams,
): Promise<Paged<AdminOrderListItem>> {
  const res = await api.get<Paged<AdminOrderListItem>>('/api/admin/orders', {
    params: {
      status: params.status ?? undefined,
      search: params.search || undefined,
      page: params.page,
      pageSize: params.pageSize,
    },
  });
  return res.data;
}

export async function getAdminOrder(id: Id): Promise<AdminOrderDetail> {
  const res = await api.get<AdminOrderDetail>(`/api/admin/orders/${id}`);
  return res.data;
}

export async function updateAdminOrderStatus(
  id: Id,
  input: OrderStatusInput,
): Promise<AdminOrderDetail> {
  const res = await api.post<AdminOrderDetail>(`/api/admin/orders/${id}/status`, input);
  return res.data;
}

// ---------------------------------------------------------------------------
// Dashboard (M4)
// ---------------------------------------------------------------------------

export async function getDashboardSummary(): Promise<DashboardSummary> {
  const res = await api.get<DashboardSummary>('/api/admin/dashboard/summary');
  return res.data;
}

// ---------------------------------------------------------------------------
// Coupons (M5)
// ---------------------------------------------------------------------------

export async function listCoupons(): Promise<Coupon[]> {
  const res = await api.get<Coupon[]>('/api/admin/coupons');
  return res.data;
}

export async function createCoupon(input: CouponInput): Promise<Coupon> {
  const res = await api.post<Coupon>('/api/admin/coupons', input);
  return res.data;
}

export async function updateCoupon(id: Id, input: CouponInput): Promise<Coupon> {
  const res = await api.put<Coupon>(`/api/admin/coupons/${id}`, input);
  return res.data;
}

export async function deleteCoupon(id: Id): Promise<void> {
  await api.delete(`/api/admin/coupons/${id}`);
}

// ---------------------------------------------------------------------------
// Banners (M5)
// ---------------------------------------------------------------------------

export async function listBanners(): Promise<Banner[]> {
  const res = await api.get<Banner[]>('/api/admin/banners');
  return res.data;
}

export async function createBanner(input: BannerInput): Promise<Banner> {
  const res = await api.post<Banner>('/api/admin/banners', input);
  return res.data;
}

export async function updateBanner(id: Id, input: BannerInput): Promise<Banner> {
  const res = await api.put<Banner>(`/api/admin/banners/${id}`, input);
  return res.data;
}

export async function deleteBanner(id: Id): Promise<void> {
  await api.delete(`/api/admin/banners/${id}`);
}

// ---------------------------------------------------------------------------
// Customers (M5)
// ---------------------------------------------------------------------------

export async function listCustomers(
  params: AdminCustomerListParams,
): Promise<Paged<AdminCustomerListItem>> {
  const res = await api.get<Paged<AdminCustomerListItem>>('/api/admin/customers', {
    params: {
      search: params.search || undefined,
      page: params.page,
      pageSize: params.pageSize,
    },
  });
  return res.data;
}

export async function getCustomer(id: Id): Promise<AdminCustomerDetail> {
  const res = await api.get<AdminCustomerDetail>(`/api/admin/customers/${id}`);
  return res.data;
}

// ---------------------------------------------------------------------------
// Staff (M5, Admin role only)
// ---------------------------------------------------------------------------

export async function listStaff(): Promise<StaffUser[]> {
  const res = await api.get<StaffUser[]>('/api/admin/staff');
  return res.data;
}

export async function createStaff(input: StaffCreateInput): Promise<StaffUser> {
  const res = await api.post<StaffUser>('/api/admin/staff', input);
  return res.data;
}

export async function updateStaff(id: Id, input: StaffUpdateInput): Promise<StaffUser> {
  const res = await api.put<StaffUser>(`/api/admin/staff/${id}`, input);
  return res.data;
}

export async function resetStaffPassword(id: Id, newPassword: string): Promise<void> {
  await api.post(`/api/admin/staff/${id}/reset-password`, { newPassword });
}

// ---------------------------------------------------------------------------
// Reports (M5)
// ---------------------------------------------------------------------------

export async function getSalesReport(params: ReportParams): Promise<SalesReportRow[]> {
  const res = await api.get<SalesReportRow[]>('/api/admin/reports/sales', {
    params: { from: params.from, to: params.to, groupBy: 'day' },
  });
  return res.data;
}

export async function getCategoryReport(params: ReportParams): Promise<CategoryReportRow[]> {
  const res = await api.get<CategoryReportRow[]>('/api/admin/reports/by-category', {
    params: { from: params.from, to: params.to },
  });
  return res.data;
}

export async function getBrandReport(params: ReportParams): Promise<BrandReportRow[]> {
  const res = await api.get<BrandReportRow[]>('/api/admin/reports/by-brand', {
    params: { from: params.from, to: params.to },
  });
  return res.data;
}

/**
 * Downloads a report as CSV (format=csv) and triggers a browser download.
 * `report` is one of "sales" | "by-category" | "by-brand".
 */
export async function downloadReportCsv(
  report: 'sales' | 'by-category' | 'by-brand',
  params: ReportParams,
): Promise<void> {
  const res = await api.get<Blob>(`/api/admin/reports/${report}`, {
    params: {
      from: params.from,
      to: params.to,
      groupBy: report === 'sales' ? 'day' : undefined,
      format: 'csv',
    },
    responseType: 'blob',
  });
  const disposition = (res.headers['content-disposition'] as string | undefined) ?? '';
  const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition);
  const filename = match?.[1] ?? `${report}-report.csv`;
  const url = URL.createObjectURL(res.data);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}
