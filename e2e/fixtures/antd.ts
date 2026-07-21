/**
 * Ant Design interaction helpers.
 *
 * The admin SPA is built on AntD v6, whose Select/TreeSelect are portalled
 * div-based widgets rather than native <select>, and whose Switch is a
 * role="switch" button. Playwright's semantic APIs (selectOption, check) do not
 * work on them, so these helpers encapsulate the DOM dance in one place instead
 * of scattering `.ant-*` class selectors through the specs.
 */
import { Locator, Page, expect } from '@playwright/test';

/** The currently-open, non-hidden dropdown panel (AntD portals these to <body>). */
function openDropdown(page: Page): Locator {
  return page.locator('.ant-select-dropdown:not(.ant-select-dropdown-hidden)').last();
}

/** Opens an AntD Select and clicks the option whose text matches exactly. */
export async function selectOption(page: Page, select: Locator, optionText: string) {
  await select.click();
  const option = openDropdown(page)
    .locator('.ant-select-item-option')
    .filter({ has: page.getByText(optionText, { exact: true }) })
    .first();
  await expect(option).toBeVisible();
  await option.click();
  // AntD v6 renders the chosen value as the text of `.ant-select-content`
  // (v5's `.ant-select-selection-item` only appears for multiple/tags modes).
  await expect(select.locator('.ant-select-content').first()).toContainText(optionText);
}

/** Opens a searchable AntD Select, types to narrow, then picks the exact option. */
export async function searchSelectOption(page: Page, select: Locator, optionText: string) {
  await select.click();
  await select.locator('input').fill(optionText);
  const option = openDropdown(page)
    .locator('.ant-select-item-option')
    .filter({ hasText: optionText })
    .first();
  await expect(option).toBeVisible();
  await option.click();
}

/** Opens an AntD TreeSelect and clicks the node with the given title. */
export async function selectTreeNode(page: Page, treeSelect: Locator, title: string) {
  await treeSelect.click();
  const node = openDropdown(page).locator('.ant-select-tree-title').filter({ hasText: title }).first();
  await expect(node).toBeVisible();
  await node.click();
}

/** Adds values to an AntD Select in `mode="tags"` (open={false}: Enter commits). */
export async function addTags(select: Locator, values: string[]) {
  await select.click();
  const input = select.locator('input');
  for (const v of values) {
    await input.fill(v);
    await input.press('Enter');
  }
  for (const v of values) {
    await expect(select.locator('.ant-select-selection-item').filter({ hasText: v })).toBeVisible();
  }
}

/**
 * The product form uses a hand-rolled Field wrapper that renders its label in a
 * plain <div> with no htmlFor/id, so getByLabel() cannot reach these controls.
 *
 * Every field shares the shape
 *     <div>            <- wrapper (what we want)
 *       <div><span>Label</span></div>
 *       <control/>
 *     </div>
 * but the *outer* container differs by card (`.ant-col` in "Pricing & inventory",
 * a bare div in the dynamic "Attributes" card), so anchor on the label span and
 * climb two levels rather than depending on any container class.
 */
export function fieldWrapper(scope: Locator, label: string): Locator {
  return scope.getByText(label, { exact: true }).first().locator('xpath=../..');
}

export function numberInput(scope: Locator, label: string): Locator {
  return fieldWrapper(scope, label).locator('.ant-input-number-input');
}

export function selectIn(scope: Locator, label: string): Locator {
  return fieldWrapper(scope, label).locator('.ant-select').first();
}

/**
 * AntD Form.Item puts the given `name` as the id of the Select's *inner search
 * input*, several levels below the widget root. Walk back up to the `.ant-select`
 * wrapper, which is what must be clicked and where the value is displayed.
 */
export function selectByFieldId(scope: Locator, id: string): Locator {
  // Exact class-token match: a naive contains(@class,"ant-select") also matches
  // the inner `ant-select-content-item` wrapper and silently picks the wrong node.
  return scope
    .locator(`#${id}`)
    .locator('xpath=ancestor::div[contains(concat(" ", normalize-space(@class), " "), " ant-select ")][1]');
}

/** AntD renders toasts as .ant-message-notice; useful as a save confirmation. */
export async function expectToast(page: Page, text: string) {
  await expect(page.locator('.ant-message-notice').filter({ hasText: text })).toBeVisible();
}
