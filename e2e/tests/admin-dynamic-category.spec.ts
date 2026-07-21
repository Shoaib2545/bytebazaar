/**
 * The platform's headline requirement, end to end, through the real UI:
 *
 *   an admin defines a NEW category and a NEW filterable attribute as *data*,
 *   publishes a product into it, and the storefront's filter sidebar renders
 *   and applies that filter — with no code change and no deploy.
 *
 * Nothing in this spec is hardcoded on either side: the attribute code, its
 * options and the category slug are all generated at runtime, so a passing run
 * proves the filter UI really is derived from admin-defined definitions.
 *
 * Requires the admin SPA (default http://localhost:5173) in addition to the
 * storefront and API.
 */
import { test, expect, Page } from '@playwright/test';
import {
  adminApi,
  deleteCategory,
  deleteProduct,
  uniqueSuffix,
  ADMIN_EMAIL,
  ADMIN_PASSWORD,
  AuthedApi,
} from '../fixtures/api';
import {
  addTags,
  numberInput,
  searchSelectOption,
  selectIn,
  selectByFieldId,
  selectOption,
  selectTreeNode,
} from '../fixtures/antd';

const ADMIN_URL = process.env.E2E_ADMIN_URL ?? 'http://localhost:5173';

const suffix = uniqueSuffix();
const CATEGORY_NAME = `E2E Chairs ${suffix}`;
const CATEGORY_SLUG = `e2e-chairs-${suffix}`;
const ATTR_NAME = 'Armrest Type';
const ATTR_CODE = 'armrest_type';
const ATTR_OPTIONS = ['2D', '4D'];
const CHOSEN_OPTION = '4D';
const PRODUCT_NAME = `E2E Chair ${suffix}`;
const PRODUCT_SLUG = `e2e-chair-${suffix}`;
const PRODUCT_PRICE = 54321;

let api: AuthedApi;

test.describe.configure({ mode: 'serial' });

test.beforeAll(async () => {
  api = await adminApi();
});

// Cleanup goes through the API by slug lookup, because the UI flow is what
// creates the records and we do not know their ids up front.
test.afterAll(async () => {
  if (!api) return;
  try {
    const prods = await api.ctx.get('/api/admin/products?pageSize=100&search=' + encodeURIComponent(PRODUCT_NAME), {
      headers: { Authorization: `Bearer ${api.token}` },
    });
    if (prods.ok()) {
      const body = await prods.json();
      for (const p of body.items ?? []) {
        if (p.slug === PRODUCT_SLUG) await deleteProduct(api, p.id).catch(() => {});
      }
    }
    const cats = await api.ctx.get('/api/admin/categories', {
      headers: { Authorization: `Bearer ${api.token}` },
    });
    if (cats.ok()) {
      const list = await cats.json();
      const flat: any[] = [];
      const walk = (n: any) => {
        flat.push(n);
        (n.children ?? []).forEach(walk);
      };
      (Array.isArray(list) ? list : list.items ?? []).forEach(walk);
      const cat = flat.find((c) => c.slug === CATEGORY_SLUG);
      if (cat) await deleteCategory(api, cat.id).catch(() => {});
    }
  } finally {
    await api.ctx.dispose();
  }
});

async function adminLogin(page: Page) {
  await page.goto(`${ADMIN_URL}/login`);
  await page.getByLabel('Email').fill(ADMIN_EMAIL);
  await page.getByLabel('Password').fill(ADMIN_PASSWORD);
  await page.getByRole('button', { name: 'Sign in' }).click();
  // AuthGuard renders the layout (and therefore the sidebar menu) only after
  // POST /api/auth/refresh resolves.
  await expect(page.locator('.ant-menu-item').filter({ hasText: 'Products' })).toBeVisible();
}

test('admin defines a category + filterable attribute and the storefront filter works without a deploy', async ({
  page,
}) => {
  await adminLogin(page);

  await test.step('create the category', async () => {
    await page.goto(`${ADMIN_URL}/categories`);
    await page.getByRole('button', { name: 'Add category' }).click();

    const modal = page.locator('.ant-modal');
    await expect(modal.getByText('New category')).toBeVisible();
    await modal.locator('#name').fill(CATEGORY_NAME);
    // The slug auto-derives from the name; overwrite it so the test controls it.
    await modal.locator('#slug').fill(CATEGORY_SLUG);
    await modal.getByRole('button', { name: 'Save' }).click();

    await expect(page.getByText(CATEGORY_NAME)).toBeVisible();
  });

  await test.step('define a filterable attribute on it', async () => {
    await page.goto(`${ADMIN_URL}/attributes`);

    // Attributes are scoped to a category; the Add button stays disabled until one is picked.
    const categoryPicker = page.locator('.ant-select').first();
    await selectTreeNode(page, categoryPicker, CATEGORY_NAME);

    const addBtn = page.getByRole('button', { name: 'Add attribute' });
    await expect(addBtn).toBeEnabled();
    await addBtn.click();

    const modal = page.locator('.ant-modal');
    await expect(modal.getByText('New attribute')).toBeVisible();
    await modal.locator('#name').fill(ATTR_NAME);
    await modal.locator('#code').fill(ATTR_CODE);
    // Type defaults to Select and Filterable defaults on — assert rather than assume,
    // since "filterable" is the whole point of this test.
    await expect(selectByFieldId(modal, 'type').locator('.ant-select-content').first()).toContainText('Select');
    await expect(modal.locator('button[role="switch"]').first()).toHaveClass(/ant-switch-checked/);

    await addTags(selectByFieldId(modal, 'options'), ATTR_OPTIONS);
    await modal.getByRole('button', { name: 'Save' }).click();

    // The new definition must show up in the category's attribute table as filterable.
    const row = page.locator('.ant-table-row').filter({ hasText: ATTR_CODE });
    await expect(row).toBeVisible();
    await expect(row).toContainText('Yes');
  });

  await test.step('publish a product into the category', async () => {
    await page.goto(`${ADMIN_URL}/products/new`);

    const basic = page.locator('.ant-card').filter({ hasText: 'Basic information' }).first();
    await basic.getByPlaceholder('Product name').fill(PRODUCT_NAME);
    await basic.getByPlaceholder('product-slug').fill(PRODUCT_SLUG);
    await searchSelectOption(page, basic.locator('.ant-select').first(), CATEGORY_NAME);

    const pricing = page.locator('.ant-card').filter({ hasText: 'Pricing & inventory' }).first();
    await numberInput(pricing, 'Price').fill(String(PRODUCT_PRICE));
    await numberInput(pricing, 'Stock').fill('7');
    // Products default to Draft; Active is what makes them public.
    await selectOption(page, selectIn(pricing, 'Status'), 'Active');

    // THE CRUX: this field exists only because the attribute definition above was
    // saved moments ago. It is generated from API data, not from any code path.
    const attrs = page.locator('.ant-card').filter({ hasText: 'Attributes' }).last();
    await expect(attrs.getByText(ATTR_NAME, { exact: true })).toBeVisible();
    await selectOption(page, selectIn(attrs, ATTR_NAME), CHOSEN_OPTION);

    await page.getByRole('button', { name: 'Create product' }).click();
    await expect(page).toHaveURL(new RegExp(`${ADMIN_URL}/products$`));
  });

  await test.step('storefront renders the new filter and it actually filters', async () => {
    await page.goto(`/category/${CATEGORY_SLUG}`);
    await expect(page.getByRole('heading', { level: 1, name: CATEGORY_NAME })).toBeVisible();
    await expect(page.getByRole('heading', { name: PRODUCT_NAME })).toBeVisible();

    const sidebar = page.locator('aside');
    // Section heading = the attribute's admin-entered display name.
    await expect(sidebar.getByRole('heading', { name: ATTR_NAME })).toBeVisible();
    // Both admin-entered options are offered, with counts derived from products.
    for (const opt of ATTR_OPTIONS) {
      await expect(sidebar.getByRole('link', { name: `Filter by ${opt}` })).toBeVisible();
    }
    await expect(sidebar.getByRole('listitem').filter({ hasText: CHOSEN_OPTION })).toContainText('(1)');

    // Selecting the matching option keeps the product.
    await sidebar.getByRole('link', { name: `Filter by ${CHOSEN_OPTION}` }).click();
    await expect(page).toHaveURL(new RegExp(`${ATTR_CODE}=${CHOSEN_OPTION}`));
    await expect(page.getByRole('heading', { name: PRODUCT_NAME })).toBeVisible();

    // Selecting the other option filters it out — proves the predicate is applied,
    // not merely that the sidebar rendered.
    const otherOption = ATTR_OPTIONS.find((o) => o !== CHOSEN_OPTION)!;
    await page.goto(`/category/${CATEGORY_SLUG}?${ATTR_CODE}=${otherOption}`);
    await expect(page.getByRole('heading', { name: PRODUCT_NAME })).toHaveCount(0);
    await expect(page.getByText('No products found.')).toBeVisible();
  });
});
