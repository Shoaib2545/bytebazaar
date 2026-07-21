import { defineConfig, devices } from '@playwright/test';

/**
 * ByteBazaar E2E configuration.
 *
 * Three URLs are in play and each is overridable so the same suite runs against
 * a local dev stack, a compose stack in CI, or a staging deployment:
 *   E2E_BASE_URL   storefront (Playwright's `baseURL`, used by page.goto('/...'))
 *   E2E_API_URL    backend API — tests use it to provision fixture data
 *   E2E_ADMIN_URL  admin SPA
 */
const STOREFRONT = process.env.E2E_BASE_URL ?? 'http://localhost:3000';
const API = process.env.E2E_API_URL ?? 'http://localhost:5080';
const ADMIN = process.env.E2E_ADMIN_URL ?? 'http://localhost:5173';

export default defineConfig({
  testDir: './tests',
  // Fixtures create uniquely-slugged data, so files are independent — but within
  // a file the steps are a single user journey and must run in order.
  fullyParallel: false,
  workers: process.env.CI ? 1 : undefined,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  timeout: 60_000,
  expect: { timeout: 10_000 },

  reporter: process.env.CI
    ? [['list'], ['html', { open: 'never' }], ['github']]
    : [['list'], ['html', { open: 'never' }]],

  use: {
    baseURL: STOREFRONT,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    actionTimeout: 15_000,
    navigationTimeout: 30_000,
  },

  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],

  metadata: { storefront: STOREFRONT, api: API, admin: ADMIN },
});

export const urls = { STOREFRONT, API, ADMIN };
