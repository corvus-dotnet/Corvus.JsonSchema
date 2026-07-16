// LIVE UX suite — the same kit the mock suite exercises, served by the SAMPLE APP: the Aspire
// composition's real control plane (SQLite stores, seeded example data), Keycloak sign-in through
// the BFF (§16.3), server-enforced authorization, and a real runner that has actually executed the
// seeded runs. Where the mock suite pins exact seed shapes, this suite validates the layers a mock
// cannot: the OIDC round trip, cookie+CSRF plumbing, real persistence, real counts, and real 403s.
//
// Opt-in (needs the composition running — see README.md): ARAZZO_LIVE_UX=1 npm run test:live
// Rules of the road are documented in live-helpers.js: unique names, cleanup, relative assertions.
import { test, expect } from '@playwright/test';
import { LIVE_USERS, signIn, watchLiveErrors, assertLiveClean, liveTab, openLiveTab, openLiveSubTab, uniq } from './live-helpers.js';

test('sign-in lands on a real shell: Keycloak round trip, nine tabs, and a runs list the real runner produced', async ({ page }) => {
  await signIn(page, LIVE_USERS.admin);
  const errors = watchLiveErrors(page); // pre-login 401 bounce is expected; from here the console must stay clean

  // The full surface renders (Approvals may carry a live badge — prefix match).
  for (const name of ['Runs', 'Catalog', 'Environments', 'Sources', 'Credentials', 'Runners', 'Security', 'Approvals', 'Requests']) {
    await expect(liveTab(page, name)).toBeVisible();
  }

  // Real runs from the real store — relative assertions only (the runner keeps executing).
  const table = page.locator('arazzo-control-plane arazzo-runs-table');
  await expect(table.locator('tbody tr[data-id]').first()).toBeVisible();
  await expect(table.locator('arazzo-pager .count')).toContainText(/\d+\+? runs?/); // the bounded server-side count

  // The status chips drive a real server-side filter round trip. Fully relative: take whatever
  // status the first REAL row wears, filter by it, and every remaining row must wear it too.
  const firstStatus = await table.locator('tbody tr[data-id] arazzo-status-badge').first()
    .evaluate((el) => el.getAttribute('status'));
  await page.locator(`#view-runs .status-chip[data-status="${firstStatus}"]`).click();
  await expect(table.locator(`tbody tr[data-id] arazzo-status-badge[status="${firstStatus}"]`).first()).toBeVisible();
  await expect.poll(() => table.locator(`tbody tr[data-id] arazzo-status-badge:not([status="${firstStatus}"])`).count()).toBe(0);
  assertLiveClean(errors);
});

test('the catalog reads the seeded registry and its detail drives a real promotion matrix', async ({ page }) => {
  await signIn(page, LIVE_USERS.admin);
  const errors = watchLiveErrors(page);
  await openLiveTab(page, 'Catalog');

  // The example seed's workflows are catalogued for real.
  const catalogTable = page.locator('arazzo-catalog-table');
  await expect(catalogTable.locator('tbody tr[data-key="nightly-reconcile"]')).toBeVisible();
  await expect(catalogTable.locator('tbody tr[data-key="onboard-customer"]')).toBeVisible();

  // The detail aggregates real sub-resources: the §15 administrator set and the §7.8 promotion
  // matrix over the real environment registry.
  await catalogTable.locator('tbody tr[data-key="nightly-reconcile"]').click();
  const detail = page.locator('arazzo-catalog-detail');
  await expect(detail).toBeVisible();
  await expect(detail.locator('arazzo-administrators-panel')).toBeVisible();
  const matrix = detail.locator('arazzo-availability-matrix');
  await expect(matrix.locator('th.env').first()).toBeVisible();
  await expect(matrix.locator('tbody tr').first()).toBeVisible();
  assertLiveClean(errors);
});

test('a rule authored in Security→Rules persists for real and the Grants editor typeahead offers it (then cleanup deletes it)', async ({ page }) => {
  await signIn(page, LIVE_USERS.admin);
  const errors = watchLiveErrors(page);
  await openLiveTab(page, 'Security');
  await openLiveSubTab(page, 'sub-security-rules');

  // Author a uniquely named rule against the real store.
  const name = uniq('live-rule');
  const rules = page.locator('arazzo-rules-panel');
  await rules.locator('button.new').click();
  await rules.locator('input[name="tmpl"][value="label-eq"]').check();
  await rules.locator('input.f-dim').fill('domain');
  await rules.locator('input.f-value').fill('live-ux');
  await rules.locator('input.f-name').fill(name);
  await rules.locator('.dfoot .confirm').click();
  await expect(rules.locator(`tr[data-name="${name}"]`)).toBeVisible();

  // The Grants editor's server-paged typeahead finds it — one shared vocabulary, no cached copy.
  await openLiveSubTab(page, 'sub-security-grants');
  const grants = page.locator('arazzo-grants-panel');
  await grants.locator('button.new').click();
  await grants.locator('select.verb-mode[data-verb="read"]').selectOption('scopes');
  const scopeInput = grants.locator('.scope-input[data-verb="read"]');
  await scopeInput.click();
  await scopeInput.fill(name);
  await expect(grants.locator(`.results[data-verb="read"] li[data-name="${name}"]`)).toBeVisible();
  await scopeInput.press('Escape');
  await grants.locator('.dfoot .cancel').click();

  // Cleanup: the rule leaves with the test.
  await openLiveSubTab(page, 'sub-security-rules');
  await rules.locator(`tr[data-name="${name}"]`).click();
  await rules.locator('.dfoot .del').click();
  await rules.locator('dialog.arazzo-confirm button.ok').click();
  await expect(rules.locator(`tr[data-name="${name}"]`)).toHaveCount(0);
  assertLiveClean(errors);
});

test('the access-request loop crosses real identities: wanda submits, the administrator finds it in Approvals and denies, wanda sees the decision', async ({ browser }) => {
  // Two REAL identities = two browser contexts, each with its own Keycloak session cookie.
  // The requester is wanda (reconcile-owners): the request dialog's workflow picker is REACH-scoped
  // for real here, and wanda is the seeded non-admin whose reach admits a workflow at all
  // (nightly-reconcile). alice/erin see an empty catalog — correct-by-construction, a request names
  // a workflow the requester can at least see.
  const wandaCtx = await browser.newContext({ ignoreHTTPSErrors: true });
  const adminCtx = await browser.newContext({ ignoreHTTPSErrors: true });
  try {
    const wandaPage = await wandaCtx.newPage();
    await signIn(wandaPage, LIVE_USERS.wanda);

    // Wanda requests time-boxed access under a unique reason (the reason is how both views identify
    // THIS request among any other pending work in the shared backend).
    const reason = uniq('live-ux request');
    await openLiveTab(wandaPage, 'Requests');
    const minePanel = wandaPage.locator('#sub-requests-access arazzo-access-requests');
    await minePanel.locator('button.new').click();
    const dlg = wandaPage.locator('arazzo-access-request-dialog');
    const wfInput = dlg.locator('arazzo-workflow-picker input.q');
    await wfInput.click();
    await wfInput.fill('nightly');
    await dlg.locator('arazzo-workflow-picker .results li[data-index]', { hasText: 'nightly-reconcile' }).first().click();
    await dlg.locator('.scope-cb[value="runs:write"]').check();
    await dlg.locator('.reason-in').fill(reason);
    await dlg.locator('.dur-in').fill('2');
    await dlg.locator('button.ok').click();
    const mineRow = minePanel.locator('tbody tr[data-id]', { hasText: reason });
    await expect(mineRow.locator('.badge')).toHaveText('Pending');

    // The administrator's Approvals queue sees the same request through the real store, resolved
    // through the real directory (§16.5.4) — and denies it with a recorded decision reason.
    const adminPage = await adminCtx.newPage();
    await signIn(adminPage, LIVE_USERS.admin);
    await openLiveTab(adminPage, 'Approvals');
    const queue = adminPage.locator('#sub-approvals-access arazzo-access-requests');
    const queued = queue.locator('tbody tr[data-id]', { hasText: reason });
    await expect(queued).toHaveCount(1);
    await queued.locator('.act[data-action="deny"]').click();
    await queue.locator('dialog.decision-dialog .reason-in').fill('Live UX test cleanup: denied by design.');
    await queue.locator('dialog.decision-dialog button.ok').click();
    await expect(queue.locator('tbody tr[data-id]', { hasText: reason })).toHaveCount(0);

    // The loop closes for the requester: wanda's own view shows the decision.
    await minePanel.locator('button.refresh').click();
    await expect(mineRow.locator('.badge')).toHaveText('Denied');
  } finally {
    await wandaCtx.close();
    await adminCtx.close();
  }
});

test('authorization is enforced by the SERVER: the observer persona is refused a mutation with a rendered 403 problem', async ({ page }) => {
  // oscar (observers group) can read but not author. The live host page deliberately sets no
  // scopes attributes — UI gating is the mock demo's concern; here the server is the authority,
  // and the UX contract is that its refusal surfaces as a problem banner, not a silent no-op.
  // (No assertLiveClean here: the deliberate 403 logs a failed-resource console error by design.)
  await signIn(page, LIVE_USERS.oscar);
  await openLiveTab(page, 'Security');
  await openLiveSubTab(page, 'sub-security-rules');

  const rules = page.locator('arazzo-rules-panel');
  await expect(rules.locator('tbody tr.srow, tbody .empty').first()).toBeVisible(); // reading is allowed
  const name = uniq('live-forbidden');
  await rules.locator('button.new').click();
  await rules.locator('input[name="tmpl"][value="label-eq"]').check();
  await rules.locator('input.f-dim').fill('domain');
  await rules.locator('input.f-value').fill('nope');
  await rules.locator('input.f-name').fill(name);
  await rules.locator('.dfoot .confirm').click();

  // The server refused; the editor surfaces the problem and nothing was created.
  await expect(rules.locator('.detail .error-banner')).toBeVisible();
  await expect(rules.locator(`tr[data-name="${name}"]`)).toHaveCount(0);
});
