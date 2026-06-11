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
