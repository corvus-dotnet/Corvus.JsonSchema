// LIVE UX — the schedules surface (#896), against the sample app (real control plane + Keycloak + runner).
// State is real and shared: this test creates a uniquely-named schedule, exercises run-now, and deletes it (cleanup),
// asserting relatively (contains / gained / gone), never seed-exact counts. See test/live/live-helpers.js.
import { test, expect } from '@playwright/test';
import { LIVE_USERS, signIn, watchLiveErrors, assertLiveClean, liveTab, openLiveTab, uniq } from './live-helpers.js';

test('a schedule can be created, run now, and deleted; runners show scheduling capability', async ({ page }) => {
  await signIn(page, LIVE_USERS.admin);
  const errors = watchLiveErrors(page);

  // The Schedules tab is part of the shell and its panel lists the seeded nightly-reconcile schedule.
  await expect(liveTab(page, 'Schedules')).toBeVisible();
  await openLiveTab(page, 'Schedules');
  const panel = page.locator('arazzo-schedules');
  await expect(panel.locator('.sched .sid', { hasText: 'nightly-reconcile-cron' })).toBeVisible({ timeout: 15_000 });

  // Create a schedule for a real, available, hosted target (nightly-reconcile v2 in development). Its id is unique so
  // the shared backend does not collide across runs.
  const scheduleId = uniq('live-sched');
  await panel.locator('button.new').click();
  await panel.locator('#f-scheduleId').fill(scheduleId);
  await panel.locator('#f-environment').fill('development');
  // The environment is schedulable (a runner there advertises it) — the capability hint reads positive, not a warning.
  await expect(panel.locator('.cap .hint.ok')).toBeVisible();
  await panel.locator('#f-base').fill('nightly-reconcile');
  await panel.locator('#f-version').fill('2');
  await panel.locator('#f-cron').fill('0 3 * * *');
  await panel.locator('.modal button.submit').click();

  // It appears in the list, targeting the versioned workflow.
  const row = panel.locator('.sched', { hasText: scheduleId });
  await expect(row).toBeVisible({ timeout: 15_000 });
  await expect(row).toContainText('nightly-reconcile-v2');

  // Run now: confirm the kit dialog, then the panel flashes the started run.
  await panel.locator(`[data-run="${scheduleId}"]`).click();
  await panel.locator('dialog.arazzo-confirm button.ok').click();
  await expect(panel.locator('.flash.ok')).toContainText('nightly-reconcile-v2', { timeout: 15_000 });

  // Delete (cleanup): confirm the danger dialog; the row goes away.
  await panel.locator(`[data-del="${scheduleId}"]`).click();
  await panel.locator('dialog.arazzo-confirm button.ok').click();
  await expect(panel.locator(`.sid:text-is("${scheduleId}")`)).toHaveCount(0, { timeout: 15_000 });

  // The Runners tab shows the scheduling-capability chip on the runner that serves schedules (ask #4).
  await openLiveTab(page, 'Runners');
  await expect(page.locator('arazzo-runners .scap').first()).toBeVisible({ timeout: 15_000 });

  assertLiveClean(errors);
});
