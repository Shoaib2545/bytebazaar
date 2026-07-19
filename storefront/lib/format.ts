/** Format a PKR price like "Rs. 149,999" */
export function formatPrice(amount: number): string {
  return `Rs. ${Math.round(amount).toLocaleString("en-PK")}`;
}
