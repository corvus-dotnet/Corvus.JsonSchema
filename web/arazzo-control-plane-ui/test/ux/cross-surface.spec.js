// UX suite — cross-surface reactivity. Every tab shares one backend, and the demo host promises two
// consistency mechanisms (demo/index.html selectTab + applyPersona): activating a tab re-fetches every
// panel in its view (`el.refresh?.()`), and a persona change reloads every surface back to page 1. This
// file pins the loops where a change made on ONE surface must show up on ANOTHER — the seams none of
// the per-area files exercise end to end.
//
// Grounding (selectors read from source):
//   demo/index.html               — tab wiring, persona wiring, per-view composition
//   src/components/grants-panel.js, scopes-panel.js (rules), access-overview-panel.js,
//   access-requests-panel.js, access-request-dialog.js, catalog-detail.js, administrators-panel.js,
//   availability-matrix.js, environments-panel.js, src/arazzo-control-plane.js (runs)
//   demo/mock-api.js              — decideAccessRequest writes/removes the approval binding (§16.5)
import { test, expect } from '@playwright/test';
import { watchErrors, assertClean, openApp, openTab } from './ux-helpers.js';

// Open a catalog version's detail pane WITHOUT reloading the page (openTab navigates, which resets
// the in-memory mock — mid-test tab switches must click the tab directly or the state under test is
// wiped). The detail lives in arazzo-catalog's shadow root, so readiness is polled through it.
async function switchToVersionDetail(page, baseWorkflowId) {
  await page.getByRole('tab', { name: 'Catalog' }).click();
  await page.locator(`arazzo-catalog-table tbody tr[data-key="${baseWorkflowId}"]`).click();
  const detail = page.locator('arazzo-catalog-detail');
  await expect(detail).toBeVisible();
  await expect.poll(async () => page.evaluate(() => {
    const d = document.querySelector('arazzo-catalog')?.shadowRoot?.querySelector('arazzo-catalog-detail');
    return !!d && d._loading === false && !!d._version;
  })).toBe(true);
  return detail;
}

// The from-scratch variant for tests whose FIRST surface is the catalog.
async function openVersionDetail(page, baseWorkflowId) {
  await openApp(page);
  return switchToVersionDetail(page, baseWorkflowId);
}

// ---- Permissions view: sibling panels share the rule vocabulary --------------------------------

test('a rule created in the Rules panel is immediately offered by the Grants editor rule typeahead', async ({ page }) => {
  const errors = watchErrors(page);
  await openTab(page, 'Permissions');

  // Author a new reach rule (label-eq template; the name is suggested from the fields).
  const rules = page.locator('arazzo-rules-panel');
  await expect(rules.locator('tbody tr.srow')).toHaveCount(5);
  await rules.locator('button.new').click();
  await rules.locator('input[name="tmpl"][value="label-eq"]').check();
  await rules.locator('input.f-dim').fill('domain');
  await rules.locator('input.f-value').fill('ops');
  await expect(rules.locator('input.f-name')).toHaveValue('rule-ops');
  await rules.locator('.dfoot .confirm').click();
  await expect(rules.locator('tr[data-name="rule-ops"]')).toBeVisible();

  // The sibling Grants editor's typeahead is server-backed, so the new rule is offered without any
  // refresh — the two panels share one rule vocabulary, not two cached copies.
  const grants = page.locator('arazzo-grants-panel');
  await grants.locator('button.new').click();
  await grants.locator('select.verb-mode[data-verb="read"]').selectOption('scopes');
  const scopeInput = grants.locator('.scope-input[data-verb="read"]');
  await scopeInput.click();
  await scopeInput.fill('rule-ops');
  const hit = grants.locator('.results[data-verb="read"] li[data-name="rule-ops"]');
  await expect(hit).toBeVisible();
  await hit.click();
  await expect(grants.locator('.verb-row .chip', { hasText: 'rule-ops' })).toBeVisible();
  assertClean(errors);
});

// ---- Access ⇄ Permissions: the approval is what writes the grant --------------------------------

test('approving an access request writes the approval-service binding into the Grants panel; revoking the approval removes it', async ({ page }) => {
  const errors = watchErrors(page);
  await openTab(page, 'Permissions');
  await expect(page.locator('arazzo-grants-panel tbody tr.grow-row')).toHaveCount(6); // demo seed settled

  // Approve the seeded pending request (req-2001, Grace's on-call cover for payments-reconcile).
  await page.getByRole('tab', { name: 'Access' }).click();
  const panel = page.locator('arazzo-access-requests');
  await panel.locator('.tab-queue').click();
  await panel.locator('tr[data-id="req-2001"] .act[data-action="approve"]').click();
  const decide = panel.locator('dialog.decision-dialog');
  await decide.locator('.reason-in').fill('On-call cover approved.');
  await decide.locator('button.ok').click();
  await expect(panel.locator('tr[data-id="req-2001"]')).toHaveCount(0); // left the pending queue

  // The approval WROTE the grant (§16.5.2): the Permissions tab now lists a seventh binding — keyed
  // on the request's subject claim, approval-service-authored, conferring the requested scopes for
  // the granted window. The tab activation re-fetches, so no manual refresh is involved.
  await page.getByRole('tab', { name: 'Permissions' }).click();
  const grants = page.locator('arazzo-grants-panel');
  await expect(grants.locator('tbody tr.grow-row')).toHaveCount(7);
  await expect(grants.locator('arazzo-pager .count')).toContainText(/7\+? grants/);
  const granted = grants.locator('tbody tr.grow-row', { hasText: 'req-2001' });
  await expect(granted.locator('.claim')).toContainText('preferred_username=grace');
  await expect(granted.locator('.gscopes')).toContainText('confers runs:read, runs:write until');

  // Revoking the approval deletes the binding it wrote — the grant disappears from the list again.
  await page.getByRole('tab', { name: 'Access' }).click();
  await panel.locator('select.status').selectOption('Approved');
  await panel.locator('tr[data-id="req-2001"] .act[data-action="revoke"]').click();
  await panel.locator('dialog.arazzo-confirm button.ok').click();
  await expect(panel.locator('tr[data-id="req-2001"]')).toHaveCount(0);
  await page.getByRole('tab', { name: 'Permissions' }).click();
  await expect(grants.locator('tbody tr.grow-row')).toHaveCount(6);
  await expect(grants.locator('tbody tr.grow-row', { hasText: 'req-2001' })).toHaveCount(0);
  assertClean(errors);
});

test('the request loop crosses personas: the operator submits, the administrator approves, the operator sees Approved', async ({ page }) => {
  const errors = watchErrors(page);
  await openApp(page);

  // The operator (omar@ops) requests time-boxed access to nightly-reconcile — a workflow he does
  // NOT administer (its set seats alice@ops, the Payments team and Ada).
  await page.locator('#persona').selectOption('operator');
  await page.getByRole('tab', { name: 'Access' }).click();
  const panel = page.locator('arazzo-access-requests');
  await panel.locator('button.new').click();
  const dlg = page.locator('arazzo-access-request-dialog');
  const wfInput = dlg.locator('arazzo-workflow-picker input.q');
  await wfInput.click();
  await wfInput.fill('nightly');
  await dlg.locator('arazzo-workflow-picker .results li[data-index]', { hasText: 'Nightly Reconcile' }).click();
  await dlg.locator('.scope-cb[value="runs:write"]').check();
  await dlg.locator('.reason-in').fill('Cross-surface UX test: re-run the reconcile.');
  await dlg.locator('.dur-in').fill('2');
  await dlg.locator('button.ok').click();
  const mine = panel.locator('tbody tr[data-id]', { hasText: 'nightly-reconcile' });
  await expect(mine.locator('.badge')).toHaveText('Pending');

  // The administrator persona (alice@ops administers nightly-reconcile) finds it in the approver
  // queue — the persona change re-fetched the same panel under the new caller.
  await page.locator('#persona').selectOption('administrator');
  await page.getByRole('tab', { name: 'Access' }).click();
  await panel.locator('.tab-queue').click();
  const queued = panel.locator('tbody tr[data-id]', { hasText: 'nightly-reconcile' });
  await expect(queued).toHaveCount(1);
  await queued.locator('.act[data-action="approve"]').click();
  await panel.locator('dialog.decision-dialog .reason-in').fill('Approved for the on-call window.');
  await panel.locator('dialog.decision-dialog button.ok').click();
  await expect(panel.locator('tbody tr[data-id]', { hasText: 'nightly-reconcile' })).toHaveCount(0);

  // Back as the operator: My requests shows the decision — the loop closes for the requester.
  await page.locator('#persona').selectOption('operator');
  await page.getByRole('tab', { name: 'Access' }).click();
  await panel.locator('.tab-mine').click();
  await expect(mine.locator('.badge')).toHaveText('Approved');
  assertClean(errors);
});

// ---- Catalog administrators ⇄ Access overview ---------------------------------------------------

test('seating a team on a workflow admin set (Catalog) shows up in the team\'s Access overview Administers section', async ({ page }) => {
  const errors = watchErrors(page);

  // Seat the Payments team on onboard-customer's administrator set (§15).
  const detail = await openVersionDetail(page, 'onboard-customer');
  const admins = detail.locator('arazzo-administrators-panel');
  await expect(admins.locator('.arow')).toHaveCount(3); // Alice (Ops), Growth, Platform
  const picker = admins.locator('.grant-in');
  await picker.locator('.q').fill('Payments');
  await picker.locator('.results li', { hasText: 'Payments' }).click();
  await admins.locator('.addbtn').click();
  await expect(admins.locator('.arow')).toHaveCount(4);

  // The Access overview aggregates the same server state: Payments now administers all three —
  // the two seeded seats (nightly-reconcile, payments-reconcile) plus the one just added.
  await page.getByRole('tab', { name: 'Access' }).click();
  const overview = page.locator('arazzo-access-overview');
  const input = overview.locator('arazzo-grantee-picker input.q');
  await input.click();
  await input.fill('Payments');
  await overview.locator('arazzo-grantee-picker .results li[data-index]', { hasText: 'Payments' }).click();
  const administered = overview.locator('.section', { hasText: 'Administers' }).first().locator('.row .grow');
  await expect(administered).toHaveCount(3);
  await expect(administered.filter({ hasText: 'onboard-customer' })).toHaveCount(1);
  assertClean(errors);
});

// ---- Environments ⇄ Catalog promotion matrix -----------------------------------------------------

test('a newly created environment appears as a promotion-matrix column in the catalog detail', async ({ page }) => {
  const errors = watchErrors(page);
  await openTab(page, 'Environments');

  const envs = page.locator('arazzo-environments');
  await envs.locator('button.new').click();
  await envs.locator('.f-name').fill('qa');
  await envs.locator('dialog .confirm').click();
  await expect(envs.locator('tr.erow[data-name="qa"]')).toBeVisible();

  // The matrix loads the environment registry fresh with the detail — the new environment is a
  // column, its cells stating "not ready" (no credentials exist for it yet).
  const detail = await switchToVersionDetail(page, 'nightly-reconcile');
  const matrix = detail.locator('arazzo-availability-matrix');
  await expect(matrix.locator('th.env', { hasText: 'qa' })).toBeVisible();
  assertClean(errors);
});

test('making a version available from the matrix lists it under the environment\'s available versions', async ({ page }) => {
  const errors = watchErrors(page);

  // onboard-customer's sources (accounts + events) both carry workflow-usable staging credentials,
  // so (v1, staging) is Ready and the administrator gets the direct "Make available" action (§7.8).
  // (nightly-reconcile would NOT do here: its staging billing credential is usage-scoped to a
  // person, which correctly does not make the environment ready for the workflow.)
  const detail = await openVersionDetail(page, 'onboard-customer');
  const matrix = detail.locator('arazzo-availability-matrix');
  const makeBtn = matrix.locator('button[data-action="make"][data-version="1"][data-env="staging"]');
  await expect(makeBtn).toBeVisible();
  await makeBtn.click();
  await expect(matrix.locator('span.badge.available')).toHaveCount(1); // staging (nothing was seeded)

  // The Environments tab reads the same availability registry: staging now lists the version.
  await page.getByRole('tab', { name: 'Environments' }).click();
  const envs = page.locator('arazzo-environments');
  await envs.locator('tr.erow[data-name="staging"]').click();
  const row = envs.locator('.avail-row', { hasText: 'onboard-customer' });
  await expect(row).toHaveCount(1);
  await expect(row.locator('.avail-ver')).toHaveText('v1');
  assertClean(errors);
});

// ---- Permissions ⇄ Access overview: tab activation refreshes a stale aggregation ----------------

test('a grant deleted under Permissions is gone from an already-open Access overview when its tab is re-activated', async ({ page }) => {
  const errors = watchErrors(page);
  await openTab(page, 'Access');

  // Leave the overview showing Growth's aggregation (two grants: bind-3 + the bind-6 eligibility).
  const overview = page.locator('arazzo-access-overview');
  const input = overview.locator('arazzo-grantee-picker input.q');
  await input.click();
  await input.fill('Growth');
  await overview.locator('arazzo-grantee-picker .results li[data-index]', { hasText: 'Growth' }).click();
  await expect(overview.locator('.grant')).toHaveCount(2);

  // Delete Growth's standing read grant under the Permissions tab.
  await page.getByRole('tab', { name: 'Permissions' }).click();
  const grants = page.locator('arazzo-grants-panel');
  await grants.locator('tr[data-id="bind-3"]').click();
  await grants.locator('.dfoot .del').click();
  await grants.locator('dialog.arazzo-confirm button.ok').click();
  await expect(grants.locator('tr[data-id="bind-3"]')).toHaveCount(0);

  // Returning to the Access tab re-runs the aggregation for the SAME selected grantee — the stale
  // two-grant overview would misreport access someone just revoked.
  await page.getByRole('tab', { name: 'Access' }).click();
  await expect(overview.locator('.grant')).toHaveCount(1);
  await expect(overview.locator('.who .glabel')).toHaveText('Growth'); // the selection survived the refresh
  assertClean(errors);
});

// ---- Persona change resets pagination ------------------------------------------------------------

test('a persona change resets a paged list to page 1 (a stale cursor must not carry over)', async ({ page }) => {
  const errors = watchErrors(page);
  await openApp(page);

  // Shrink the page so the 12 seeded runs paginate, and walk to page 2.
  const table = page.locator('arazzo-control-plane arazzo-runs-table');
  await table.evaluate((el) => el.setAttribute('page-size', '5'));
  const pager = table.locator('arazzo-pager');
  await expect(pager.locator('.count')).toContainText('12 runs');
  await pager.locator('button.next').click();
  await expect(pager.locator('.count')).toContainText('page 2');

  // Switching personas swaps the caller's reach — every surface reloads from page 1.
  await page.locator('#persona').selectOption('viewer');
  await expect(pager.locator('.count')).toContainText('12 runs');
  await expect(pager.locator('.count')).not.toContainText('page 2');
  await expect(table.locator('tbody tr[data-id]')).toHaveCount(5);
  assertClean(errors);
});
