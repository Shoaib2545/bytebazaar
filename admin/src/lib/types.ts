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

export interface DashboardSummary {
  ordersToday: number;
  salesToday: number;
  pendingOrders: number;
  totalProducts: number;
  lowStock: LowStockProduct[];
}
