/**
 * The money path.
 *
 * browse category -> apply a dynamic filter -> product detail -> add to cart
 *   -> COD checkout -> order confirmation -> customer sees the order in /account/orders
 *
 * All catalog data used here is created by this spec under a run-unique slug, so
 * the assertions are exact (we know there are precisely two products and exactly
 * one matches the filter) and immune to seed-data drift.
 */
import { test, expect, Page } from '@playwright/test';
import {
  adminApi,
  createAttribute,
  createCategory,
  createProduct,
  deleteCategory,
  deleteProduct,
  registerCustomer,
  uniqueSuffix,
  AuthedApi,
} from '../fixtures/api';

const suffix = uniqueSuffix();

const CATEGORY_SLUG = `e2e-cooling-${suffix}`;
const CATEGORY_NAME = `E2E Cooling ${suffix}`;
const ATTR_CODE = `e2e_fan_size`;
const ATTR_NAME = 'Fan Size';
const MATCHING_VALUE = '120mm';
const OTHER_VALUE = '140mm';

const MATCH_SLUG = `e2e-cooler-match-${suffix}`;
const MATCH_NAME = `E2E Cooler Match ${suffix}`;
const MATCH_PRICE = 12345;

const MATCH2_SLUG = `e2e-cooler-match2-${suffix}`;
const MATCH2_NAME = `E2E Cooler Match Two ${suffix}`;

const OTHER_SLUG = `e2e-cooler-other-${suffix}`;
const OTHER_NAME = `E2E Cooler Other ${suffix}`;

let api: AuthedApi;
let categoryId: string;
let matchProductId: string;
let match2ProductId: string;
let otherProductId: string;
let customer: { email: string; password: string };

test.describe.configure({ mode: 'serial' });

test.beforeAll(async () => {
  api = await adminApi();

  const category = await createCategory(api, { name: CATEGORY_NAME, slug: CATEGORY_SLUG });
  categoryId = category.id;

  await createAttribute(api, {
    categoryId,
    name: ATTR_NAME,
    code: ATTR_CODE,
    options: [MATCHING_VALUE, OTHER_VALUE],
  });

  const match = await createProduct(api, {
    name: MATCH_NAME,
    slug: MATCH_SLUG,
    categoryId,
    price: MATCH_PRICE,
    stock: 40,
    attributes: { [ATTR_CODE]: MATCHING_VALUE },
  });
  matchProductId = match.id;

  // A second product on the SAME attribute value, so the facet counts differ
  // (120mm -> 2, 140mm -> 1). Equal counts would not prove they are computed.
  const match2 = await createProduct(api, {
    name: MATCH2_NAME,
    slug: MATCH2_SLUG,
    categoryId,
    price: MATCH_PRICE + 500,
    stock: 40,
    attributes: { [ATTR_CODE]: MATCHING_VALUE },
  });
  match2ProductId = match2.id;

  const other = await createProduct(api, {
    name: OTHER_NAME,
    slug: OTHER_SLUG,
    categoryId,
    price: 9999,
    stock: 40,
    attributes: { [ATTR_CODE]: OTHER_VALUE },
  });
  otherProductId = other.id;

  customer = await registerCustomer(suffix);
});

test.afterAll(async () => {
  // Best-effort cleanup. Orders intentionally survive — deleting a product that
  // an order references would either fail on the FK or corrupt order history,
  // and leaving one E2E order behind is harmless.
  if (!api) return;
  await deleteProduct(api, matchProductId).catch(() => {});
  await deleteProduct(api, match2ProductId).catch(() => {});
  await deleteProduct(api, otherProductId).catch(() => {});
  await deleteCategory(api, categoryId).catch(() => {});
  await api.ctx.dispose();
});

/** Logs in through the storefront UI so the session cookie + client auth state are real. */
async function loginAsCustomer(page: Page) {
  await page.goto('/login');
  await page.locator('#email').fill(customer.email);
  await page.locator('#password').fill(customer.password);
  await page.getByRole('button', { name: 'Sign In' }).click();
  await expect(page.getByRole('button', { name: /^Hi,/ })).toBeVisible();
}

test('customer completes a COD purchase from a filtered category and sees the order in their account', async ({
  page,
}) => {
  // The account page is client-guarded, and the cart must belong to the customer
  // for the order to appear under /account/orders — so sign in first.
  await loginAsCustomer(page);

  await test.step('browse the category', async () => {
    await page.goto(`/category/${CATEGORY_SLUG}`);
    await expect(page.getByRole('heading', { level: 1, name: CATEGORY_NAME })).toBeVisible();
    // Exactly the three products this spec created.
    await expect(page.getByText('3 products')).toBeVisible();
    await expect(page.getByRole('heading', { name: MATCH_NAME })).toBeVisible();
    await expect(page.getByRole('heading', { name: MATCH2_NAME })).toBeVisible();
    await expect(page.getByRole('heading', { name: OTHER_NAME })).toBeVisible();
  });

  await test.step('apply the admin-defined dynamic filter', async () => {
    const sidebar = page.locator('aside');
    // The section heading is the attribute's admin-defined display name.
    await expect(sidebar.getByRole('heading', { name: ATTR_NAME })).toBeVisible();

    // Filter options are rendered as navigation links (aria-label "Filter by X")
    // with a styled pseudo-checkbox span, not native inputs — the sidebar is
    // link-driven so filtering works without client JS.
    const option = sidebar.getByRole('link', { name: `Filter by ${MATCHING_VALUE}` });
    await expect(option).toBeVisible();
    // Facet counts must be computed from real product data, not the option list.
    await expect(sidebar.getByRole('listitem').filter({ hasText: MATCHING_VALUE })).toContainText('(2)');
    await expect(sidebar.getByRole('listitem').filter({ hasText: OTHER_VALUE })).toContainText('(1)');

    await option.click();

    await expect(page).toHaveURL(new RegExp(`${ATTR_CODE}=${MATCHING_VALUE}`));
    await expect(page.getByText('2 products')).toBeVisible();
    await expect(page.getByRole('heading', { name: MATCH_NAME })).toBeVisible();
    await expect(page.getByRole('heading', { name: MATCH2_NAME })).toBeVisible();
    await expect(page.getByRole('heading', { name: OTHER_NAME })).toHaveCount(0);
  });

  await test.step('open the product detail page', async () => {
    await page.getByRole('link', { name: new RegExp(MATCH_NAME) }).first().click();
    await expect(page).toHaveURL(new RegExp(`/product/${MATCH_SLUG}$`));
    await expect(page.getByRole('heading', { level: 1, name: MATCH_NAME })).toBeVisible();
    // The spec table is generated from the category's attribute definitions —
    // assert the whole row so we prove name and value are paired, not just present.
    await expect(page.getByRole('heading', { level: 2, name: 'Specifications' })).toBeVisible();
    const specRow = page.getByRole('row').filter({ hasText: ATTR_NAME });
    await expect(specRow).toContainText(MATCHING_VALUE);
  });

  await test.step('add to cart', async () => {
    await page.getByRole('button', { name: 'Add to Cart' }).click();
    await expect(page.getByRole('button', { name: /Added/ })).toBeVisible();
    await expect(page.getByRole('link', { name: /Cart, 1 item/ })).toBeVisible();
  });

  await test.step('review the cart', async () => {
    await page.goto('/cart');
    await expect(page.getByRole('heading', { level: 1, name: 'Shopping Cart' })).toBeVisible();
    await expect(page.getByRole('link', { name: MATCH_NAME })).toBeVisible();
    await expect(page.getByText('Subtotal (1 item)')).toBeVisible();
    await page.getByRole('link', { name: 'Proceed to Checkout' }).click();
    await expect(page).toHaveURL(/\/checkout$/);
  });

  let orderNumber = '';

  await test.step('complete COD checkout', async () => {
    await expect(page.getByRole('heading', { level: 1, name: 'Checkout' })).toBeVisible();

    await page.locator('#fullName').fill(`E2E Customer ${suffix}`);
    await page.locator('#phone').fill('03001234567');
    await page.locator('#email').fill(customer.email);
    await page.locator('#addressLine').fill('12 Playwright Street, Block E2E');
    await page.locator('#city').selectOption('Karachi');
    await page.locator('#region').selectOption('Sindh');

    // Shipping options load async; pick the first one offered rather than
    // hardcoding a code that the admin could rename.
    const shipping = page.locator('input[name="shipping"]');
    await expect(shipping.first()).toBeVisible();
    await shipping.first().check();

    // COD is the only payment method wired up in M4.
    await expect(page.locator('input[name="payment"][value="COD"]')).toBeChecked();

    await page.getByRole('button', { name: /^Place Order/ }).click();

    await page.waitForURL(/\/order-confirmation\/.+/, { timeout: 30_000 });
    orderNumber = page.url().split('/order-confirmation/')[1];
    expect(orderNumber).toBeTruthy();
  });

  await test.step('see the order confirmation', async () => {
    await expect(
      page.getByRole('heading', { level: 1, name: 'Thank you for your order!' })
    ).toBeVisible();
    await expect(page.getByText('Order number', { exact: true })).toBeVisible();
    await expect(page.getByText(orderNumber, { exact: true })).toBeVisible();
  });

  await test.step('order appears in the customer account', async () => {
    await page.goto('/account/orders');
    await expect(page.getByRole('heading', { level: 1, name: 'My Orders' })).toBeVisible();

    const row = page.getByRole('link', { name: orderNumber });
    await expect(row).toBeVisible();

    await row.click();
    await expect(page).toHaveURL(new RegExp(`/account/orders/${orderNumber}$`));
    await expect(page.getByRole('heading', { level: 1, name: `Order ${orderNumber}` })).toBeVisible();
    await expect(page.getByText(MATCH_NAME)).toBeVisible();
  });
});
