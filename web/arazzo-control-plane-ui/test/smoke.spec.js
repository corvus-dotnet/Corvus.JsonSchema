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

  // Select the adopt-pet run that faulted mid-flow (cursor 2, at submitAdoption): it has earlier steps to rewind
  // to (reservePayment) and rich faulted-step outputs for the skip builder — the seed shapes it for exactly this
  // flow. (Picking faulted.first() is seed-order dependent and can land on a run faulted at its first step, which
  // correctly has no earlier step to rewind to.)
  const faulted = page.locator('arazzo-runs-table tbody tr[data-id="run-b2c3d4e5"]');
  await expect(faulted).toBeVisible();
  await faulted.click();
  const detail = page.locator('arazzo-run-detail');
  await expect(detail).toBeVisible();
  await expect(detail.locator('[part="fault"]')).toBeVisible();

  // Open the resume dialog.
  await detail.getByRole('button', { name: /resume/i }).click();
  await expect(page.locator('arazzo-resume-dialog dialog')).toBeVisible();
  await expect(page.getByText('Retry faulted step')).toBeVisible();

  // Switch to Rewind: the step picker lists the workflow's steps (by name) pulled from the catalog.
  await page.locator('arazzo-resume-dialog input[name="mode"][value="Rewind"]').check();
  const stepSelect = page.locator('arazzo-resume-dialog .rewind-picker select');
  await expect(stepSelect).toBeVisible();
  await expect(stepSelect.locator('option', { hasText: 'reservePayment' })).toHaveCount(1);

  // Switch to Skip and opt to record outputs for the skipped step: the skip-outputs builder (gated behind that
  // checkbox) renders a strongly-typed form from the catalog metadata.
  await page.locator('arazzo-resume-dialog input[name="mode"][value="Skip"]').check();
  await page.locator('arazzo-resume-dialog input.record-outputs').check();
  const skipBuilder = page.locator('arazzo-resume-dialog arazzo-value-editor.skip-builder');
  await expect(skipBuilder.locator('input, select, textarea').first()).toBeVisible();

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

test('the Catalog Add wizard reuses a registered source and versions the workflow', async ({ page }) => {
  const errors = [];
  page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text()); });
  page.on('pageerror', (e) => errors.push(String(e)));

  await page.goto('/demo/index.html');
  await page.getByRole('tab', { name: 'Catalog' }).click();

  // nightly-reconcile starts with three versions collapsed into one row.
  const ncRow = page.locator('arazzo-catalog-table tbody tr[data-key="nightly-reconcile"]');
  await expect(ncRow).toContainText('3 versions');

  await page.locator('arazzo-catalog .add-btn').click();
  const dlg = page.locator('arazzo-catalog-add-dialog');
  await expect(dlg.locator('dialog')).toBeVisible();

  // Step 1 — Details: a workflow document for the SAME base id, declaring the petstore source (already registered).
  const workflow = JSON.stringify({
    arazzo: '1.1.0', info: { title: 'Nightly Reconcile' },
    sourceDescriptions: [{ name: 'petstore', type: 'openapi' }],
    workflows: [{ workflowId: 'nightly-reconcile', steps: [] }],
  });
  await dlg.locator('#workflowFile').setInputFiles({
    name: 'workflow.json', mimeType: 'application/json', buffer: Buffer.from(workflow),
  });
  await expect(dlg.locator('.wf-status.ok')).toBeVisible();
  await dlg.locator('#ownerName').fill('Reconciliation Team');
  await dlg.locator('#ownerEmail').fill('team@example.com');
  await dlg.locator('.next').click();

  // Step 2 — Sources: petstore is a registered source, resolved with no re-upload required.
  await expect(dlg.locator('.src-badge.registered')).toBeVisible();
  await dlg.locator('.next').click(); // → Administrators
  await dlg.locator('.next').click(); // → Review
  await expect(dlg.locator('.next')).toHaveText('Add workflow');
  await dlg.locator('.next').click(); // commit

  // The catalog assigned v4; the detail opens on it.
  const detail = page.locator('arazzo-catalog-detail');
  await expect(detail).toContainText('nightly-reconcile · v4');
  await expect(ncRow).toContainText('4 versions');

  expect(errors, `console/page errors: ${errors.join(' | ')}`).toEqual([]);
});

test('the Environments tab lists environments and opens one to administer it', async ({ page }) => {
  const errors = [];
  page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text()); });
  page.on('pageerror', (e) => errors.push(String(e)));

  await page.goto('/demo/index.html');
  await page.getByRole('tab', { name: 'Environments' }).click();

  const env = page.locator('arazzo-environments');
  await expect(env).toBeVisible();
  const rows = env.locator('.erow');
  await expect(rows.first()).toBeVisible();
  expect(await rows.count()).toBeGreaterThan(1);

  // Open production → its detail shows the administrators sub-panel (env mode) and the available versions.
  await env.locator('.erow[data-name="production"]').click();
  await expect(env.locator('.detail-pane .dtitle')).toContainText('Production');
  await expect(env.locator('arazzo-administrators-panel')).toBeVisible();
  await expect(env.locator('.detail-pane')).toContainText('Available workflow versions');
  await expect(env.locator('.avail-row').first()).toBeVisible();

  // Create a new environment via the dialog; it appears in the list and opens selected.
  await env.locator('.new').click();
  await expect(env.locator('dialog[open]')).toBeVisible();
  await env.locator('.f-name').fill('qa');
  await env.locator('.confirm').click();
  await expect(env.locator('.erow[data-name="qa"]')).toBeVisible();
  await expect(env.locator('.detail-pane .dtitle')).toContainText('qa');

  expect(errors, `console/page errors: ${errors.join(' | ')}`).toEqual([]);
});

test('Grants and Scopes live on the Permissions tab; Access holds the request inbox', async ({ page }) => {
  await page.goto('/demo/index.html');

  // Permissions carries the reach vocabulary — grants + scopes.
  await page.getByRole('tab', { name: 'Permissions' }).click();
  await expect(page.locator('arazzo-grants-panel')).toBeVisible();
  await expect(page.locator('arazzo-scopes-panel')).toBeVisible();

  // Access carries the request/approval inbox (and no longer the grants/scopes panels).
  await page.getByRole('tab', { name: 'Access' }).click();
  await expect(page.locator('arazzo-access-requests')).toBeVisible();
  await expect(page.locator('arazzo-grants-panel')).toBeHidden();
});

test('the Promotions tab shows the requester’s own promotion requests and the approver inbox', async ({ page }) => {
  const errors = [];
  page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text()); });
  page.on('pageerror', (e) => errors.push(String(e)));

  await page.goto('/demo/index.html');
  await page.getByRole('tab', { name: 'Promotions' }).click();

  const promotions = page.locator('arazzo-availability-requests');
  await expect(promotions).toBeVisible();

  // "My requests" opens by default with the demo user's own seeded request.
  const mineRows = promotions.locator('tbody tr[data-id]');
  await expect(mineRows.first()).toBeVisible();
  await expect(promotions.locator('tbody')).toContainText('nightly-reconcile');

  // The approver inbox opens to the actionable pending requests across the environments you administer.
  await promotions.locator('.tab-queue').click();
  const inboxRows = promotions.locator('tbody tr[data-id]');
  await expect(inboxRows.first()).toBeVisible();
  await expect(promotions.locator('.act[data-action="approve"]').first()).toBeVisible();

  expect(errors, `console/page errors: ${errors.join(' | ')}`).toEqual([]);
});
