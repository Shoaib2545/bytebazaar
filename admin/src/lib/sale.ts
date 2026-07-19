import dayjs from 'dayjs';

/**
 * True when the sale window [saleStart, saleEnd] is currently active.
 * A null bound is unbounded on that side. Requires a salePrice to exist.
 */
export function isSaleActive(
  salePrice: number | null | undefined,
  saleStart: string | null | undefined,
  saleEnd: string | null | undefined,
): boolean {
  if (salePrice == null) return false;
  const now = dayjs();
  if (saleStart && now.isBefore(dayjs(saleStart))) return false;
  if (saleEnd && now.isAfter(dayjs(saleEnd))) return false;
  return true;
}
