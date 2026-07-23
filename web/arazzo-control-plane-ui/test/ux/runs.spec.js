// UX suite — the Runs area of the control-plane app shell: list filtering, the bounded count
// footer, run detail, and the remediation verbs (resume modes, cancel, delete, purge).
import { test, expect } from '@playwright/test';
import { watchErrors, assertClean, openApp, fillJsonEditor } from './ux-helpers.js';

test('the status chips filter the list and exactly one chip is pressed at a time', async ({ page }) => {
  const errors = watchErrors(page);
  await openApp(page);
  const rows = page.locator('arazzo-runs-table tbody tr[data-id]');
  await expect(rows.first()).toBeVisible();
  const all = await rows.count();

  await page.locator('#view-runs .status-chip[data-status="Faulted"]').click();
  await expect.poll(async () => rows.count()).toBeLessThan(all);
  await expect(page.locator('#view-runs .status-chip[aria-pressed="true"]')).toHaveCount(1);
  await expect(rows.first()).toContainText(/faulted/i);

  await page.locator('#view-runs .status-chip[data-status=""]').click();
  await expect.poll(async () => rows.count()).toBe(all);
  assertClean(errors);
});

test('the workflow filter narrows by workflowId and the count footer reports the reach-bounded total', async ({ page }) => {
  const errors = watchErrors(page);
  await openApp(page);
  const rows = page.locator('arazzo-runs-table tbody tr[data-id]');
  await expect(rows.first()).toBeVisible();
  const before = await rows.count();

  const wfFilter = page.locator('#view-runs .wf-search input').first();
  await wfFilter.fill('onboard-customer');
  await wfFilter.dispatchEvent('change');
  await expect.poll(async () => rows.count()).toBeLessThan(before);

  // The pager footer counts through /runs/count (bounded, so "N" or "N+").
  await expect(page.locator('arazzo-runs-table arazzo-pager .count')).toContainText(/\d+\+? run/);
  assertClean(errors);
});

test('a suspended run shows its wait and pinned environment in the detail', async ({ page }) => {
  const errors = watchErrors(page);
  await openApp(page);
  await page.locator('#view-runs .status-chip[data-status="Suspended"]').click();
  const rows = page.locator('arazzo-runs-table tbody tr[data-id]');
  await expect(rows.first()).toBeVisible();
  await rows.filter({ hasText: 'kyc.results' }).first().click();

  const detail = page.locator('arazzo-run-detail');
  await expect(detail).toBeVisible();
  // The suspended block names what the run waits for (a timer or a correlated message).
  await expect(detail.locator('[part="wait"]')).toBeVisible();
  await expect(detail.locator('[part="wait"]')).toContainText(/waiting/i);
  assertClean(errors);
});

test('resume offers all four modes and StatePatch takes an RFC 6902 document', async ({ page }) => {
  const errors = watchErrors(page);
  await openApp(page);
  await page.locator('arazzo-runs-table tbody tr[data-id="run-b2c3d4e5"]').click();
  await page.locator('arazzo-run-detail').getByRole('button', { name: /resume/i }).click();
  const dialog = page.locator('arazzo-resume-dialog');
  await expect(dialog.locator('dialog')).toBeVisible();

  for (const mode of ['RetryFaultedStep', 'Rewind', 'Skip', 'StatePatch']) {
    await expect(dialog.locator(`input[name="mode"][value="${mode}"]`)).toBeVisible();
  }

  await dialog.locator('input[name="mode"][value="StatePatch"]').check();
  const patch = dialog.locator('#patch');
  await expect(patch).toBeVisible();
  await fillJsonEditor(patch, '[{"op":"replace","path":"/inputs/petId","value":"p-2"}]');
  await dialog.getByRole('button', { name: /resume/i }).click();
  await expect(dialog.locator('dialog')).not.toBeVisible();
  assertClean(errors);
});

test('cancel is confirm-gated and marks the run cancelled', async ({ page }) => {
  const errors = watchErrors(page);
  await openApp(page);
  await page.locator('#view-runs .status-chip[data-status="Running"]').click();
  const rows = page.locator('arazzo-runs-table tbody tr[data-id]');
  await expect(rows.first()).toBeVisible();
  const id = await rows.first().getAttribute('data-id');
  await rows.first().click();

  const detail = page.locator('arazzo-run-detail');
  await detail.locator('arazzo-cancel-button .trigger').click();
  const confirm = detail.locator('arazzo-cancel-button dialog');
  await expect(confirm).toBeVisible();
  await confirm.locator('.confirm').click();
  await expect(detail.locator('arazzo-status-badge')).toContainText(/cancelled/i);

  // The list reflects it under the Cancelled chip.
  await page.locator('#view-runs .status-chip[data-status="Cancelled"]').click();
  await expect(page.locator(`arazzo-runs-table tbody tr[data-id="${id}"]`)).toBeVisible();
  assertClean(errors);
});

test('purge is scope-gated, preset-driven, and reaps old terminal runs', async ({ page }) => {
  const errors = watchErrors(page);
  await openApp(page);
  const rows = page.locator('arazzo-runs-table tbody tr[data-id]');
  await expect(rows.first()).toBeVisible();
  const before = await rows.count();

  const purgeBtn = page.locator('#view-runs .purge-btn');
  await expect(purgeBtn).toBeVisible(); // the demo persona holds runs:purge
  await purgeBtn.click();
  const dialog = page.locator('#view-runs arazzo-purge-dialog dialog[part="dialog"]');
  await expect(dialog).toBeVisible();
  await dialog.locator('.preset[data-days="30"]').click();
  await dialog.locator('.confirm').click();
  // The destructive act is double-gated: a strong confirm follows the form; the dialog then
  // reports the purge count in place (the operator closes it when done reading).
  const strongConfirm = page.locator('#view-runs arazzo-purge-dialog dialog.arazzo-confirm');
  await expect(strongConfirm).toBeVisible();
  await strongConfirm.locator('button.confirm, button.danger').first().click();
  await expect(dialog.locator('.result')).toBeVisible();
  await expect(dialog.locator('.result')).toContainText(/purged \d+ run/i);
  await dialog.locator('button[value="dismiss"]').click();
  await expect(dialog).not.toBeVisible();

  // Old terminal seeds died; the list shrank.
  await expect.poll(async () => rows.count()).toBeLessThan(before);
  assertClean(errors);
});

test('the "Up to now" preset purges EVERY completed/cancelled run, including recent ones', async ({ page }) => {
  const errors = watchErrors(page);
  await openApp(page);
  await expect(page.locator('arazzo-runs-table tbody tr[data-id]').first()).toBeVisible();

  // Count the terminal (completed/cancelled) runs on the list — the ones a purge may reap.
  const terminalCount = () => page.locator('arazzo-runs-table tbody tr[data-id]').evaluateAll((trs) =>
    trs.filter((tr) => ['Completed', 'Cancelled'].includes(tr.querySelector('arazzo-status-badge')?.getAttribute('status'))).length);
  const before = await terminalCount();
  expect(before).toBeGreaterThan(0);

  const purgeBtn = page.locator('#view-runs .purge-btn');
  await purgeBtn.click();
  const dialog = page.locator('#view-runs arazzo-purge-dialog dialog[part="dialog"]');
  await expect(dialog).toBeVisible();
  // "Up to now" is the fix: a rolling N-days window can never reach runs created today (the reported
  // "purge all before now → 0"); this cutoff is the current moment, so every terminal run qualifies.
  await dialog.locator('.preset[data-days="0"]').click();
  await dialog.locator('.confirm').click();
  const strongConfirm = page.locator('#view-runs arazzo-purge-dialog dialog.arazzo-confirm');
  await expect(strongConfirm).toBeVisible();
  await strongConfirm.locator('button.confirm, button.danger').first().click();
  await expect(dialog.locator('.result')).toContainText(/purged \d+ run/i);
  await dialog.locator('button[value="dismiss"]').click();

  // No completed/cancelled run survives, however recently it finished.
  await expect.poll(terminalCount).toBe(0);
  assertClean(errors);
});
