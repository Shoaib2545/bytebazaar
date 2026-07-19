import type { OrderStatus } from './types';

/** AntD Tag color per order status. */
export const ORDER_STATUS_COLORS: Record<OrderStatus, string> = {
  Pending: 'orange',
  Confirmed: 'blue',
  Shipped: 'geekblue',
  Delivered: 'green',
  Cancelled: 'red',
};

export const ORDER_STATUSES: OrderStatus[] = [
  'Pending',
  'Confirmed',
  'Shipped',
  'Delivered',
  'Cancelled',
];

export interface OrderStatusAction {
  /** Button label, e.g. "Confirm". */
  label: string;
  /** Target status of the transition. */
  to: OrderStatus;
  /** Render the button as danger (Cancel). */
  danger?: boolean;
}

/** Valid transitions: Pending->Confirmed|Cancelled; Confirmed->Shipped|Cancelled; Shipped->Delivered. */
export const ORDER_STATUS_ACTIONS: Record<OrderStatus, OrderStatusAction[]> = {
  Pending: [
    { label: 'Confirm', to: 'Confirmed' },
    { label: 'Cancel', to: 'Cancelled', danger: true },
  ],
  Confirmed: [
    { label: 'Ship', to: 'Shipped' },
    { label: 'Cancel', to: 'Cancelled', danger: true },
  ],
  Shipped: [{ label: 'Deliver', to: 'Delivered' }],
  Delivered: [],
  Cancelled: [],
};

/** Formats a money amount as "Rs. 12,345". */
export function formatRs(value: number | null | undefined): string {
  if (value == null) return '—';
  return `Rs. ${value.toLocaleString('en-PK', { maximumFractionDigits: 2 })}`;
}
