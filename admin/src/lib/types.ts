export type Id = string | number;

export type AttributeType = 'Select' | 'MultiSelect' | 'Number' | 'Boolean' | 'Text';
export type FilterWidget = 'Checkbox' | 'Radio' | 'Range';
export type ProductStatus = 'Draft' | 'Active';

export interface AuthUser {
  id: Id;
  email: string;
  fullName: string;
  roles: string[];
}

export interface AuthResponse {
  accessToken: string;
  user: AuthUser;
}

export interface Category {
  id: Id;
  name: string;
  slug: string;
  parentId: Id | null;
  imageUrl: string | null;
  sortOrder: number;
  isActive: boolean;
  metaTitle: string | null;
  metaDescription: string | null;
}

export interface CategoryInput {
  name: string;
  slug: string;
  parentId: Id | null;
  imageUrl: string | null;
  sortOrder: number;
  isActive: boolean;
  metaTitle: string | null;
  metaDescription: string | null;
}

export interface AttributeDefinition {
  id: Id;
  categoryId: Id;
  name: string;
  code: string;
  type: AttributeType;
  options: string[];
  isFilterable: boolean;
  isRequired: boolean;
  filterWidget: FilterWidget;
  sortOrder: number;
}

export interface AttributeInput {
  categoryId: Id;
  name: string;
  code: string;
  type: AttributeType;
  options: string[];
  isFilterable: boolean;
  isRequired: boolean;
  filterWidget: FilterWidget;
  sortOrder: number;
}

export interface Brand {
  id: Id;
  name: string;
  slug: string;
  logoUrl: string | null;
}

export interface BrandInput {
  name: string;
  slug: string;
  logoUrl: string | null;
}

export interface Paged<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface AdminProductListItem {
  id: Id;
  name: string;
  slug: string;
  categoryId?: Id;
  categoryName?: string;
  brandId?: Id | null;
  brandName?: string | null;
  price: number;
  salePrice: number | null;
  saleStart: string | null;
  saleEnd: string | null;
  isFeatured: boolean;
  stock: number;
  status: ProductStatus;
}

export interface AdminProduct {
  id: Id;
  name: string;
  slug: string;
  categoryId: Id;
  brandId: Id | null;
  description: string | null;
  price: number;
  salePrice: number | null;
  saleStart: string | null;
  saleEnd: string | null;
  isFeatured: boolean;
  stock: number;
  status: ProductStatus;
  images: string[];
  attributes: Record<string, string>;
  metaTitle: string | null;
  metaDescription: string | null;
}

export interface ProductInput {
  name: string;
  slug: string;
  categoryId: Id;
  brandId: Id | null;
  description: string | null;
  price: number;
  salePrice: number | null;
  saleStart: string | null;
  saleEnd: string | null;
  isFeatured: boolean;
  stock: number;
  status: ProductStatus;
  images: string[];
  attributes: Record<string, string>;
  metaTitle: string | null;
  metaDescription: string | null;
}

export interface ProductListParams {
  page?: number;
  pageSize?: number;
  search?: string;
  categoryId?: Id;
}

// ---------------------------------------------------------------------------
// Orders (M4)
// ---------------------------------------------------------------------------

export type OrderStatus = 'Pending' | 'Confirmed' | 'Shipped' | 'Delivered' | 'Cancelled';
export type PaymentMethod = 'COD';

export interface AdminOrderListItem {
  id: Id;
  orderNumber: string;
  createdAt: string;
  customerName: string;
  phone: string;
  city: string;
  status: OrderStatus;
  total: number;
  itemCount: number;
}

export interface OrderShippingAddress {
  fullName: string;
  phone: string;
  email: string;
  addressLine: string;
  city: string;
  region: string;
}

export interface OrderItem {
  productId: Id;
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

export interface AdminOrderDetail {
  id: Id;
  orderNumber: string;
  createdAt: string;
  status: OrderStatus;
  paymentMethod: PaymentMethod;
  subtotal: number;
  shippingFee: number;
  total: number;
  shippingAddress: OrderShippingAddress;
  items: OrderItem[];
  history: OrderHistoryEntry[];
}

export interface AdminOrderListParams {
  status?: OrderStatus;
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface OrderStatusInput {
  status: OrderStatus;
  note?: string;
}

export interface LowStockProduct {
  id: Id;
  name: string;
  stock: number;
}

export interface DashboardTopProduct {
  productId: Id;
  name: string;
  units: number;
  revenue: number;
}

export interface DashboardSalesDay {
  date: string;
  revenue: number;
}

export interface DashboardSummary {
  ordersToday: number;
  salesToday: number;
  pendingOrders: number;
  totalProducts: number;
  lowStock: LowStockProduct[];
  topProducts: DashboardTopProduct[];
  salesLast7Days: DashboardSalesDay[];
}

// ---------------------------------------------------------------------------
// Coupons (M5)
// ---------------------------------------------------------------------------

export type CouponType = 'Percent' | 'Fixed';

export interface Coupon {
  id: Id;
  code: string;
  type: CouponType;
  value: number;
  minOrderAmount: number | null;
  maxUses: number | null;
  usedCount: number;
  validFrom: string | null;
  validTo: string | null;
  isActive: boolean;
}

export interface CouponInput {
  code: string;
  type: CouponType;
  value: number;
  minOrderAmount: number | null;
  maxUses: number | null;
  validFrom: string | null;
  validTo: string | null;
  isActive: boolean;
}

// ---------------------------------------------------------------------------
// Banners (M5)
// ---------------------------------------------------------------------------

export type BannerPlacement = 'Hero' | 'Strip';

export interface Banner {
  id: Id;
  title: string;
  subtitle: string | null;
  imageUrl: string;
  linkUrl: string | null;
  placement: BannerPlacement;
  sortOrder: number;
  isActive: boolean;
  startsAt: string | null;
  endsAt: string | null;
}

export interface BannerInput {
  title: string;
  subtitle: string | null;
  imageUrl: string;
  linkUrl: string | null;
  placement: BannerPlacement;
  sortOrder: number;
  isActive: boolean;
  startsAt: string | null;
  endsAt: string | null;
}

// ---------------------------------------------------------------------------
// Customers (M5)
// ---------------------------------------------------------------------------

export interface AdminCustomerListItem {
  id: Id;
  fullName: string;
  email: string;
  phone: string | null;
  ordersCount: number;
  totalSpent: number;
}

export interface CustomerRecentOrder {
  /** Order id — optional so the UI degrades gracefully if the API omits it. */
  id?: Id;
  orderNumber: string;
  createdAt: string;
  status: OrderStatus;
  total: number;
}

export interface AdminCustomerDetail extends AdminCustomerListItem {
  recentOrders: CustomerRecentOrder[];
}

export interface AdminCustomerListParams {
  search?: string;
  page?: number;
  pageSize?: number;
}

// ---------------------------------------------------------------------------
// Staff (M5, Admin role only)
// ---------------------------------------------------------------------------

export type StaffRole = 'Admin' | 'Staff';

export interface StaffUser {
  id: Id;
  email: string;
  fullName: string;
  role: StaffRole;
  isActive: boolean;
}

export interface StaffCreateInput {
  email: string;
  fullName: string;
  password: string;
  role: StaffRole;
}

export interface StaffUpdateInput {
  fullName: string;
  role: StaffRole;
  isActive: boolean;
}

// ---------------------------------------------------------------------------
// Reports (M5)
// ---------------------------------------------------------------------------

export interface ReportParams {
  from?: string;
  to?: string;
}

export interface SalesReportRow {
  period: string;
  orders: number;
  revenue: number;
}

export interface CategoryReportRow {
  categoryName: string;
  orders: number;
  units: number;
  revenue: number;
}

export interface BrandReportRow {
  brandName: string;
  orders: number;
  units: number;
  revenue: number;
}
