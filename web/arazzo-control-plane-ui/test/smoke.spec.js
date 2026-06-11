// Tier 3 — smoke test of the live demo page. Playwright CSS locators pierce open shadow roots, so we can
// assert across the kit's Shadow DOM. This also fails on any console/page error at load (which would have
// caught a bad cross-module import).
import { test, expect } from '@playwright/test';

test('demo loads cleanly, lists runs, and opens the resume dialog for a faulted run', async ({ page }) => {
  const errors = [];
  page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text()); });
  page.on('pageerror', (e) => errors.push(String(e)));

  await page.goto('/demo/index.html');

  // The panel and its table render, with rows from the mock.
  await expect(page.locator('arazzo-control-plane')).toBeVisible();
  const rows = page.locator('arazzo-runs-table tbody tr[data-id]');
  await expect(rows.first()).toBeVisible();
  expect(await rows.count()).toBeGreaterThan(1);

  // Select the faulted run → its detail panel appears.
  const faulted = page.locator('arazzo-runs-table tbody tr[data-id]', {
    has: page.locator('arazzo-status-badge[status="Faulted"]'),
  });
  await faulted.first().click();
  const detail = page.locator('arazzo-run-detail');
  await expect(detail).toBeVisible();
  await expect(detail.locator('[part="fault"]')).toBeVisible();

  // Open the resume dialog.
  await detail.getByRole('button', { name: /resume/i }).click();
  await expect(page.locator('arazzo-resume-dialog dialog')).toBeVisible();
  await expect(page.getByText('Retry faulted step')).toBeVisible();

  // No console or page errors during the whole flow.
  expect(errors, `console/page errors: ${errors.join(' | ')}`).toEqual([]);
});

test('the time-window filter narrows the list', async ({ page }) => {
  await page.goto('/demo/index.html');
  const rows = page.locator('arazzo-runs-table tbody tr[data-id]');
  await expect(rows.first()).toBeVisible();
  const before = await rows.count();

  // "Created before" 30 days ago — only the older seeded runs remain.
  const thirtyDaysAgo = new Date(Date.now() - 30 * 86400000);
  const local = new Date(thirtyDaysAgo.getTime() - thirtyDaysAgo.getTimezoneOffset() * 60000).toISOString().slice(0, 16);
  await page.locator('arazzo-control-plane .timewindow input[data-attr="created-before"]').fill(local);
  await page.locator('arazzo-control-plane .timewindow input[data-attr="created-before"]').dispatchEvent('change');

  await expect.poll(async () => rows.count()).toBeLessThan(before);
});

test('the Catalog tab lists versions and opens a version detail with downloads', async ({ page }) => {
  const errors = [];
  page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text()); });
  page.on('pageerror', (e) => errors.push(String(e)));

  await page.goto('/demo/index.html');

  // Switch to the Catalog tab.
  await page.getByRole('tab', { name: 'Catalog' }).click();
  await expect(page.locator('arazzo-catalog')).toBeVisible();
  const rows = page.locator('arazzo-catalog-table tbody tr[data-key]');
  await expect(rows.first()).toBeVisible();
  expect(await rows.count()).toBeGreaterThan(1);

  // Select a version → its detail panel appears with the content hash and download actions.
  await rows.first().click();
  const detail = page.locator('arazzo-catalog-detail');
  await expect(detail).toBeVisible();
  await expect(detail.locator('[part="hash"]')).toBeVisible();
  await expect(detail.getByRole('button', { name: /package/i })).toBeVisible();

  expect(errors, `console/page errors: ${errors.join(' | ')}`).toEqual([]);
});

test('the Catalog Add version flow builds a package in-browser and the catalog versions it', async ({ page }) => {
  const errors = [];
  page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text()); });
  page.on('pageerror', (e) => errors.push(String(e)));

  await page.goto('/demo/index.html');
  await page.getByRole('tab', { name: 'Catalog' }).click();

  // nightly-reconcile starts with three versions collapsed into one row.
  const ncRow = page.locator('arazzo-catalog-table tbody tr[data-key="nightly-reconcile"]');
  await expect(ncRow).toContainText('3 versions');

  await page.locator('arazzo-catalog .add-btn').click();
  const dialog = page.locator('arazzo-catalog-add-dialog dialog');
  await expect(dialog).toBeVisible();

  // Build mode: attach a workflow document for the SAME base id, plus its source.
  const workflow = JSON.stringify({
    arazzo: '1.1.0', info: { title: 'Nightly Reconcile' },
    sourceDescriptions: [{ name: 'petstore', type: 'openapi' }],
    workflows: [{ workflowId: 'nightly-reconcile', steps: [] }],
  });
  await page.locator('arazzo-catalog-add-dialog #workflowFile').setInputFiles({
    name: 'workflow.json', mimeType: 'application/json', buffer: Buffer.from(workflow),
  });
  // The dialog auto-suggests a "petstore" source row from sourceDescriptions; attach its file.
  await page.locator('arazzo-catalog-add-dialog .source-row .src-file').first().setInputFiles({
    name: 'petstore.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify({ openapi: '3.1.0' })),
  });
  await page.locator('arazzo-catalog-add-dialog #ownerName').fill('Reconciliation Team');
  await page.locator('arazzo-catalog-add-dialog #ownerEmail').fill('team@example.com');
  await page.locator('arazzo-catalog-add-dialog .confirm').click();

  // The catalog assigned v4; the detail opens on it.
  const detail = page.locator('arazzo-catalog-detail');
  await expect(detail).toContainText('nightly-reconcile · v4');
  await expect(ncRow).toContainText('4 versions');

  expect(errors, `console/page errors: ${errors.join(' | ')}`).toEqual([]);
});
