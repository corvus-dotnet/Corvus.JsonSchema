// UX suite — the Catalog area: list search, the version switcher and compare picker, the version
// detail (hash, downloads, evidence, management tags), the embedded promotion matrix, the operator's
// request-promotion dialog, the add-workflow wizard's readiness gate + administrators step, and the
// §15 administrators panel. Goes deeper than smoke.spec.js (which covers open-detail, compare modes,
// make-available, the persona gate, and the wizard's registered-source happy path).
import { test, expect } from '@playwright/test';
import { watchErrors, assertClean, openApp, openTab } from './ux-helpers.js';

const rows = (page) => page.locator('arazzo-catalog-table tbody tr[data-key]');

/** Open the Catalog tab and select one base workflow's row; resolves with the detail locator.
 *  Waits for the AUTHORITATIVE detail load: the detail first paints from the injected row summary and
 *  then re-renders (rebuilding the body, replacing any embedded editor) when getCatalogVersion lands —
 *  interacting between the two renders would be silently undone. Selecting a row sets `_loading`
 *  synchronously within the click dispatch, so polling it false observes the second render. */
async function openVersionDetail(page, baseWorkflowId) {
  await openTab(page, 'Catalog');
  await page.locator(`arazzo-catalog-table tbody tr[data-key="${baseWorkflowId}"]`).click();
  const detail = page.locator('arazzo-catalog-detail');
  await expect(detail).toBeVisible();
  await expect.poll(async () => page.evaluate(() => {
    // document.querySelector doesn't pierce shadow roots; the detail lives in arazzo-catalog's.
    const d = document.querySelector('arazzo-catalog')?.shadowRoot?.querySelector('arazzo-catalog-detail');
    return !!d && d._loading === false && !!d._version;
  })).toBe(true);
  return detail;
}

/** Build an Arazzo workflow document upload for the add wizard (declared sources are registered ones). */
function workflowUpload(workflowId, sourceDescriptions) {
  const doc = JSON.stringify({
    arazzo: '1.1.0', info: { title: workflowId },
    sourceDescriptions,
    workflows: [{ workflowId, steps: [] }],
  });
  return { name: 'workflow.json', mimeType: 'application/json', buffer: Buffer.from(doc) };
}

test('the free-text search narrows the catalog list to the matching workflow id', async ({ page }) => {
  const errors = watchErrors(page);
  await openTab(page, 'Catalog');
  await expect(rows(page).first()).toBeVisible();
  const before = await rows(page).count();
  expect(before).toBeGreaterThan(1);

  // q is a substring find over title/description/workflow id, so an id fragment narrows to that base.
  const search = page.locator('arazzo-catalog .q-search');
  await search.fill('onboard');
  await expect.poll(async () => rows(page).count()).toBe(1);
  await expect(rows(page).first()).toHaveAttribute('data-key', 'onboard-customer');

  // Clearing the search restores the full reach-scoped list.
  await search.fill('');
  await expect.poll(async () => rows(page).count()).toBe(before);
  assertClean(errors);
});

test('the detail header switches between the base workflow\'s versions; a single-version base hides the switcher', async ({ page }) => {
  const errors = watchErrors(page);
  const detail = await openVersionDetail(page, 'nightly-reconcile');

  // nightly-reconcile has three versions; the switcher lists them all, newest first, with status.
  const vswitch = detail.locator('[part="version-switch"]');
  await expect(vswitch).toBeVisible();
  await expect(detail.locator('.version-switch option')).toHaveCount(3);
  await expect(detail.locator('.version-switch option').first()).toHaveText(/v3 · Active/);

  // Switching to v1 reloads the detail on the obsolete version.
  await detail.locator('.version-switch').selectOption('1');
  await expect(detail.locator('header .ver')).toHaveText('nightly-reconcile · v1');
  await expect(detail.locator('header .badge')).toHaveText('Obsolete');

  // adopt-pet has a single version, so both the switcher and the compare picker are hidden.
  await page.locator('arazzo-catalog-table tbody tr[data-key="adopt-pet"]').click();
  await expect(detail.locator('header .ver')).toHaveText('adopt-pet · v1');
  await expect(detail.locator('[part="version-switch"]')).toBeHidden();
  await expect(detail.locator('[part="version-compare"]')).toBeHidden();
  assertClean(errors);
});

test('the compare-with picker offers only the sibling versions, never the version being viewed', async ({ page }) => {
  const errors = watchErrors(page);
  const detail = await openVersionDetail(page, 'nightly-reconcile');

  // Viewing v3 (the representative): the picker holds the placeholder plus exactly v2 and v1.
  const compare = detail.locator('.compare-with');
  await expect(detail.locator('[part="version-compare"]')).toBeVisible();
  await expect(compare.locator('option')).toHaveCount(3);
  await expect(compare.locator('option[value="3"]')).toHaveCount(0);
  await expect(compare.locator('option[value="2"]')).toHaveCount(1);
  await expect(compare.locator('option[value="1"]')).toHaveCount(1);

  // After switching the detail to v2, the picker re-derives: v3 and v1 offered, v2 excluded.
  await detail.locator('.version-switch').selectOption('2');
  await expect(detail.locator('header .ver')).toHaveText('nightly-reconcile · v2');
  await expect(compare.locator('option[value="2"]')).toHaveCount(0);
  await expect(compare.locator('option[value="3"]')).toHaveCount(1);
  await expect(compare.locator('option[value="1"]')).toHaveCount(1);
  assertClean(errors);
});

test('the detail shows the content hash and offers every download: package, workflow, and each source document', async ({ page }) => {
  const errors = watchErrors(page);
  const detail = await openVersionDetail(page, 'onboard-customer');

  // The seeded hash is the base+version padded to 64 chars, with a copy affordance beside it.
  await expect(detail.locator('[part="hash"]')).toContainText('onboard-customer1');
  await expect(detail.locator('.copy-hash')).toBeVisible();

  // Downloads: the package and the workflow document, plus one Download per referenced source.
  await expect(detail.locator('.dl-package')).toHaveText('Package (.awp)');
  await expect(detail.locator('.dl-workflow')).toHaveText('Workflow (.json)');
  await expect(detail.locator('.dl-source')).toHaveCount(2);
  await expect(detail.locator('.dl-source[data-name="accounts"]')).toBeVisible();
  await expect(detail.locator('.dl-source[data-name="events"]')).toBeVisible();

  // The source strip resolves each source's credential bindings (both live in staging).
  await expect(detail.locator('.src-binds[data-name="events"] .bind')).toContainText('staging');
  assertClean(errors);
});

test('the evidence badge attests the publish suite: green when all passed, red on a failure, absent without evidence', async ({ page }) => {
  const errors = watchErrors(page);
  const detail = await openVersionDetail(page, 'nightly-reconcile');

  // nightly-reconcile v3 was published with a fully green attested suite.
  await expect(detail.locator('[part="evidence"] .evd')).toHaveClass(/evd-ok/);
  await expect(detail.locator('[part="evidence"]')).toContainText('3/3 scenarios ✓');
  await expect(detail.locator('[part="evidence"]')).toContainText('at publish');

  // adopt-pet v1 carries a failing scenario, so the badge is red.
  await page.locator('arazzo-catalog-table tbody tr[data-key="adopt-pet"]').click();
  await expect(detail.locator('header .ver')).toHaveText('adopt-pet · v1');
  await expect(detail.locator('[part="evidence"] .evd')).toHaveClass(/evd-bad/);
  await expect(detail.locator('[part="evidence"]')).toContainText('1/2 scenarios ✗');

  // onboard-customer v1 predates evidence: no badge renders at all.
  await page.locator('arazzo-catalog-table tbody tr[data-key="onboard-customer"]').click();
  await expect(detail.locator('header .ver')).toHaveText('onboard-customer · v1');
  await expect(detail.locator('[part="hash"]')).toContainText('onboard-customer1');
  await expect(detail.locator('[part="evidence"]')).toHaveCount(0);
  assertClean(errors);
});

test('management tags edit through the tag editor and the save round-trips to the server', async ({ page }) => {
  const errors = watchErrors(page);
  const detail = await openVersionDetail(page, 'onboard-customer');

  // With catalog:write the section is a live key=value editor, seeded from the version's tags.
  const editor = detail.locator('#sectag-editor');
  await expect(editor.locator('.tag-row .tk')).toHaveValue('domain');
  await expect(editor.locator('.tag-row .tv')).toHaveValue('identity');

  await editor.locator('.tag-row .tv').fill('identity-core');
  await detail.locator('.sectag-save').click();

  // Prove persistence rather than trusting the local input: navigate away and back, then the editor
  // reseeds from the server's stored entity.
  await page.locator('arazzo-catalog-table tbody tr[data-key="adopt-pet"]').click();
  await expect(detail.locator('header .ver')).toHaveText('adopt-pet · v1');
  await page.locator('arazzo-catalog-table tbody tr[data-key="onboard-customer"]').click();
  await expect(detail.locator('header .ver')).toHaveText('onboard-customer · v1');
  await expect(editor.locator('.tag-row .tv')).toHaveValue('identity-core');
  assertClean(errors);
});

test('a management tag with the reserved sys: prefix is refused with a problem banner', async ({ page }) => {
  const errors = watchErrors(page);
  const detail = await openVersionDetail(page, 'onboard-customer');

  const editor = detail.locator('#sectag-editor');
  await expect(editor.locator('.tag-row .tk')).toHaveValue('domain');
  await editor.locator('.add').click();
  await editor.locator('.tag-row').last().locator('.tk').fill('sys:owner');
  await editor.locator('.tag-row').last().locator('.tv').fill('me');
  await detail.locator('.sectag-save').click();

  // The server owns the sys: prefix; the 400 problem surfaces in the detail's banner. (Scoped to the
  // body: the embedded credential dialog carries its own, hidden, .error-banner.)
  await expect(detail.locator('.body .error-banner')).toBeVisible();
  await expect(detail.locator('.body .error-banner')).toContainText('Reserved security tag');
  assertClean(errors);
});

test('the embedded promotion matrix scopes to this version\'s row across every environment, and withdraw returns the cell to promotable', async ({ page }) => {
  const errors = watchErrors(page);
  const detail = await openVersionDetail(page, 'nightly-reconcile');
  const matrix = detail.locator('arazzo-availability-matrix');
  await expect(matrix).toBeVisible();

  // Single-version mode: one row (the viewed v3) against all four governed environments.
  await expect(matrix.locator('thead th')).toHaveCount(5);
  for (const env of ['Development', 'Production', 'Staging', 'UAT']) {
    await expect(matrix.locator('thead th', { hasText: env })).toHaveCount(1);
  }
  await expect(matrix.locator('tbody tr')).toHaveCount(1);
  await expect(matrix.locator('tbody td.ver')).toHaveText(/v3/);

  // v3 is seeded available in production; the administrator sees a Withdraw there.
  await expect(matrix.locator('.badge.available')).toContainText('✓ Available');
  const withdraw = matrix.locator('button[data-action="withdraw"][data-env="production"]');
  await expect(withdraw).toBeVisible();
  await withdraw.click();

  // Withdraw is confirm-gated (the kit's dialog, not window.confirm), then the cell flips back to
  // promotable: billing is still credentialed in production, so the version reads Ready + Make available.
  const confirm = matrix.locator('dialog.arazzo-confirm');
  await expect(confirm).toBeVisible();
  await expect(confirm).toContainText('Withdraw v3');
  await confirm.locator('button.ok').click();
  await expect(matrix.locator('button[data-action="make"][data-env="production"]')).toBeVisible();
  await expect(matrix.locator('button[data-action="withdraw"]')).toHaveCount(0);
  assertClean(errors);
});

test('the operator\'s request-promotion dialog offers only the environments where the version is ready', async ({ page }) => {
  const errors = watchErrors(page);
  await openApp(page);
  await page.locator('#persona').selectOption('operator');
  await page.getByRole('tab', { name: 'Catalog' }).click();
  await page.locator('arazzo-catalog-table tbody tr[data-key="onboard-customer"]').click();
  const detail = page.locator('arazzo-catalog-detail');

  // Without availability:write the availability block offers the elevation path.
  const request = detail.locator('.request-promotion');
  await expect(request).toBeVisible();
  await request.click();

  const dlg = detail.locator('arazzo-availability-request-dialog');
  await expect(dlg.locator('dialog')).toBeVisible();
  // Locked to the version it was opened from — no workflow/version pickers.
  await expect(dlg.locator('.locked-wf')).toHaveText('onboard-customer v1');

  // onboard-customer needs accounts + events, both credentialed only in staging, so staging is the
  // single environment offered: production/uat/development are not requestable.
  const env = dlg.locator('.env-in');
  await expect(env).toBeEnabled();
  await expect(env.locator('option')).toHaveCount(1);
  await expect(env.locator('option[value="staging"]')).toHaveCount(1);
  await expect(env.locator('option[value="production"]')).toHaveCount(0);

  await dlg.locator('.reason-in').fill('Needed for the staging integration test.');
  await dlg.locator('.ok').click();
  await expect(dlg.locator('dialog')).not.toBeVisible(); // submitted — the POST succeeded
  assertClean(errors);
});

test('the add-workflow wizard hard-gates on readiness: sources credentialed only in different environments refuse to continue', async ({ page }) => {
  const errors = watchErrors(page);
  await openTab(page, 'Catalog');
  await page.locator('arazzo-catalog .add-btn').click();
  const dlg = page.locator('arazzo-catalog-add-dialog');
  await expect(dlg.locator('dialog')).toBeVisible();

  // petstore is credentialed in production only, events in staging only: no common environment.
  await dlg.locator('#workflowFile').setInputFiles(workflowUpload('cross-env-flow', [
    { name: 'petstore', type: 'openapi' },
    { name: 'events', type: 'asyncapi' },
  ]));
  await expect(dlg.locator('.wf-status.ok')).toBeVisible();
  await dlg.locator('#ownerName').fill('Cross Env Team');
  await dlg.locator('#ownerEmail').fill('crossenv@example.com');
  await dlg.locator('.next').click();

  // Both sources resolve from the registry, but the readiness banner warns: no environment has both.
  await expect(dlg.locator('.src-badge.registered')).toHaveCount(2);
  await expect(dlg.locator('.readiness.warn')).toContainText('Not ready in any environment');

  // Next refuses: the wizard stays on the Sources step with the hard-gate error.
  await dlg.locator('.next').click();
  await expect(dlg.locator('.error-banner')).toBeVisible();
  await expect(dlg.locator('.error-banner')).toContainText('ready in any environment');
  await expect(dlg.locator('.step-chip.active')).toHaveText(/Sources & credentials/);
  assertClean(errors);
});

test('the wizard\'s administrators step names additional admins through the grantee picker, and review lists them', async ({ page }) => {
  const errors = watchErrors(page);
  await openTab(page, 'Catalog');
  await page.locator('arazzo-catalog .add-btn').click();
  const dlg = page.locator('arazzo-catalog-add-dialog');
  await expect(dlg.locator('dialog')).toBeVisible();

  // Details + a ready source (petstore is credentialed in production) to reach the admins step.
  await dlg.locator('#workflowFile').setInputFiles(workflowUpload('wizard-admins-demo', [{ name: 'petstore', type: 'openapi' }]));
  await expect(dlg.locator('.wf-status.ok')).toBeVisible();
  await dlg.locator('#ownerName').fill('Wizard Team');
  await dlg.locator('#ownerEmail').fill('wizard@example.com');
  await dlg.locator('.next').click();
  await expect(dlg.locator('.readiness.ok')).toContainText('production');
  await dlg.locator('.next').click();

  // The creator always administers; more admins resolve through the picker, never a typed tuple.
  await expect(dlg.locator('.creator-note')).toContainText('You (the creator)');
  const picker = dlg.locator('.admin-picker');
  await picker.locator('.q').fill('Ada');
  const hit = picker.locator('.results li', { hasText: 'Ada Lovelace' });
  await expect(hit).toBeVisible();
  await hit.click();
  await expect(picker.locator('.selected .chip')).toContainText('Ada Lovelace');
  await dlg.locator('.add-admin').click();
  await expect(dlg.locator('.admins .admin-row')).toHaveCount(1);
  await expect(dlg.locator('.admins .admin-row')).toContainText('Ada Lovelace');

  // Review names both the creator and the staged admin before anything commits.
  await dlg.locator('.next').click();
  await expect(dlg.locator('.next')).toHaveText('Add workflow');
  await expect(dlg.locator('.review-grid')).toContainText('You (the creator)');
  await expect(dlg.locator('.review-grid')).toContainText('Ada Lovelace');
  assertClean(errors);
});

test('the detail\'s administrators panel lists the §15 set and adds a directory grantee; a partial identity warns first', async ({ page }) => {
  const errors = watchErrors(page);
  const detail = await openVersionDetail(page, 'onboard-customer');
  const panel = detail.locator('arazzo-administrators-panel');
  await expect(panel).toBeVisible();

  // The demo-seeded set (demo-seed.js administratorsSeed): the administrator persona plus the Growth and
  // Platform teams (alice@ops is seated on every set so the demo's approver inbox can light up).
  await expect(panel.locator('.arow')).toHaveCount(3);
  await expect(panel.locator('.arow', { hasText: 'Alice (Ops)' })).toHaveCount(1);
  await expect(panel.locator('.arow', { hasText: 'Growth' })).toHaveCount(1);
  await expect(panel.locator('.arow', { hasText: 'Platform' })).toHaveCount(1);

  // A store-observed grantee with an incomplete identity carries the broadening advisory on selection.
  const picker = panel.locator('.grant-in');
  await picker.locator('.q').fill('Grace');
  const partial = picker.locator('.results li', { hasText: 'Grace Hopper' });
  await expect(partial).toContainText('partial identity');
  await partial.click();
  await expect(picker.locator('.selected .warn')).toContainText('partial identity');
  await picker.locator('.selected .clear').click();

  // A directory-resolved team adds cleanly and the set re-renders from the server's response.
  await picker.locator('.q').fill('Payments');
  const team = picker.locator('.results li', { hasText: 'Payments' });
  await expect(team).toBeVisible();
  await team.click();
  await panel.locator('.addbtn').click();
  await expect(panel.locator('.arow')).toHaveCount(4);
  await expect(panel.locator('.arow', { hasText: 'Payments' })).toHaveCount(1);
  assertClean(errors);
});
