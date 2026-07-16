// Shared helpers for the comprehensive UX suite (test/ux/*.spec.js). Every spec runs against the
// in-browser mock (demo/mock-api.js) served by the smoke server — same world as smoke.spec.js.
// Conventions: one behavior per test, fresh page per test, zero console/page errors asserted via
// the `errors` fixture from watchErrors(), Playwright locators piercing open shadow roots.
import { expect } from '@playwright/test';

/** Collect console/page errors (ignoring benign resource-load 404s — the standalone demo has no
 *  BFF, so <arazzo-auth-status>'s /me probe 404s by design). Call assertClean(errors) at the end. */
export function watchErrors(page) {
  const errors = [];
  page.on('console', (m) => { if (m.type() === 'error' && !/Failed to load resource/.test(m.text())) errors.push(m.text()); });
  page.on('pageerror', (e) => errors.push(String(e)));
  return errors;
}

export function assertClean(errors) {
  expect(errors, `console/page errors: ${errors.join(' | ')}`).toEqual([]);
}

/** Open the control-plane app shell (runs list visible). */
export async function openApp(page) {
  await page.goto('/demo/index.html');
  await expect(page.locator('arazzo-control-plane')).toBeVisible();
}

/** Open the app shell on a named top-level tab (Runs, Catalog, Environments, Connections, …). */
export async function openTab(page, name) {
  await openApp(page);
  await page.getByRole('tab', { name }).click();
}

/** Open the designer on a seeded working copy (by its display name in the workspace table). */
export async function openDesigner(page, workingCopyName = 'Order processing') {
  await page.goto('/demo/designer.html');
  await page.locator('arazzo-workspace-table').getByText(workingCopyName).click();
  await expect(page.locator('#surface')).toBeVisible();
}

/** Start a designer debug session against mocks (no environment): ▶ Run through the run-inputs
 *  dialog; resolves when the dock is visible. */
export async function runAgainstMocks(page) {
  await page.locator('#simulate').click();
  const dlg = page.locator('#run-inputs-dialog');
  await expect(dlg).toBeVisible();
  await dlg.locator('.ri-run').click();
  await expect(page.locator('#debug-dock')).toBeVisible();
}

/** Start a §18 durable debug run in the seeded development environment. */
export async function runInDevelopment(page, { transientFault = false } = {}) {
  await page.locator('#simulate').click();
  const dlg = page.locator('#run-inputs-dialog');
  await expect(dlg).toBeVisible();
  await page.locator('#run-inputs-env').selectOption('development');
  if (transientFault) await dlg.locator('#run-inputs-fault-cb').check();
  await dlg.locator('.ri-run').click();
  await expect(page.locator('#debug-dock')).toBeVisible();
}
