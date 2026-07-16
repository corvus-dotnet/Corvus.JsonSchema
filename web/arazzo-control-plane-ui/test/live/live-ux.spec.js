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

test('every tab renders real data with a clean console (API-shape sweep)', async ({ page }) => {
  // The cheapest way real infrastructure catches drift: every panel fetches the REAL API — a wire
  // shape the mock got wrong surfaces here as an error banner or a console error.
  await signIn(page, LIVE_USERS.admin);
  const errors = watchLiveErrors(page);
  const views = {
    Runs: 'arazzo-control-plane', Catalog: 'arazzo-catalog', Environments: 'arazzo-environments',
    Sources: 'arazzo-sources', Credentials: 'arazzo-credentials', Runners: 'arazzo-runners',
    Security: 'arazzo-grants-panel', Approvals: 'arazzo-access-requests', Requests: 'arazzo-access-requests',
  };
  for (const [tab, root] of Object.entries(views)) {
    await openLiveTab(page, tab);
    const view = page.locator(`#view-${tab.toLowerCase()}`);
    await expect(view.locator(root).first()).toBeVisible();
    // Visible banners only: dialogs keep an empty, hidden .error-banner container structurally.
    await expect(view.locator('.error-banner:visible')).toHaveCount(0);
  }
  assertLiveClean(errors);
});

test('the real runner is registered, online, and hosting the seeded workflow versions', async ({ page }) => {
  // The Runners panel reads the live registry: the composition's runner heartbeats for real, so its
  // row must wear the Online health chip (stale-after would flip it if the heartbeat loop died) and
  // list the versions it has actually loaded.
  await signIn(page, LIVE_USERS.admin);
  const errors = watchLiveErrors(page);
  await openLiveTab(page, 'Runners');

  const runner = page.locator('arazzo-runners .runner').first();
  await expect(runner).toBeVisible();
  await expect(runner.locator('.health')).toHaveClass(/online/);
  await expect(runner.locator('.renv')).toHaveText('development'); // the composition's runner serves dev
  await expect(runner.locator('.hosted .hv').first()).toBeVisible(); // it loaded real workflow versions
  await expect(runner.locator('.hosted .lstate', { hasText: /^loaded$/ }).first()).toBeVisible();
  assertLiveClean(errors);
});

test('a REAL suspended run shows its durable wait, and the cancel confirm survives live auto-refresh (then Keep running leaves it suspended)', async ({ page }) => {
  // The seeded async onboarding run is genuinely suspended in the store (waiting on a message the
  // demo never sends). Its detail must narrate the durable wait — and with auto-refresh ON, the
  // periodic repaint must not tear the cancel confirm out from under the user (the live variant of
  // the poll-vs-modal defect the mock suite caught).
  await signIn(page, LIVE_USERS.admin);
  const errors = watchLiveErrors(page);

  await page.locator('#view-runs .status-chip[data-status="Suspended"]').click();
  const table = page.locator('arazzo-control-plane arazzo-runs-table');
  await expect(table.locator('tbody tr[data-id]').first()).toBeVisible();
  await page.locator('arazzo-control-plane #autorefresh').check(); // live polling ON for the modal window below
  await table.locator('tbody tr[data-id]').first().click();

  const detail = page.locator('arazzo-control-plane arazzo-run-detail');
  await expect(detail.locator('[part="wait"]')).toContainText('Suspended — waiting');
  await expect(detail.locator('arazzo-status-badge')).toHaveAttribute('status', 'Suspended');

  // Open the cancel confirm and hold it across at least one full refresh interval.
  await detail.locator('arazzo-cancel-button .trigger').click();
  const dlg = detail.locator('arazzo-cancel-button dialog');
  await expect(dlg).toBeVisible();
  await page.waitForTimeout(6_000); // > the composite's poll interval — refresh activity happens under the modal
  await expect(dlg).toBeVisible(); // still the SAME open dialog, not a rebuilt closed one

  // Keep running: the decision stands and the run is untouched (re-runnable — nothing mutated).
  await dlg.locator('button[value="dismiss"]').click();
  await expect(dlg).not.toBeVisible();
  await expect(detail.locator('arazzo-status-badge')).toHaveAttribute('status', 'Suspended');
  assertLiveClean(errors);
});

test('environment metadata round-trips real persistence: edit the display name, verify, restore', async ({ page }) => {
  // A full optimistic-concurrency loop against the real store (read → PATCH with etag → re-read),
  // self-restoring so the suite stays re-runnable: whatever display name staging carries now is
  // captured first and put back at the end.
  await signIn(page, LIVE_USERS.admin);
  const errors = watchLiveErrors(page);
  await openLiveTab(page, 'Environments');

  const envs = page.locator('arazzo-environments');
  await envs.locator('tr.erow[data-name="staging"]').click();
  const nameInput = envs.locator('.d-displayName');
  await expect(nameInput).toBeVisible();
  const original = await nameInput.inputValue();

  const edited = uniq('Staging');
  await nameInput.fill(edited);
  await envs.locator('.d-save').click();
  // The save round-trips the server and the list re-renders from the authoritative response.
  await expect(envs.locator('tr.erow[data-name="staging"]')).toContainText(edited);

  // Restore — the detail stays selected after a save, so edit again and put the original back.
  await nameInput.fill(original);
  await envs.locator('.d-save').click();
  await expect(envs.locator('tr.erow[data-name="staging"]')).not.toContainText(edited);
  assertLiveClean(errors);
});

test('the environment registry is genuinely reach-scoped per identity: the admin sees all, erin and wanda see exactly the preprod zone', async ({ browser }) => {
  // Three REAL identities, three cookie jars. erin (env-admins) and wanda (reconcile-owners) reach
  // staging through the seeded zone-access:preprod rule — administration alone never confers reach,
  // and their environment lists must show EXACTLY the one row the rule admits.
  for (const [user, expected] of [
    ['admin', ['development', 'production', 'staging']],
    ['erin', ['staging']],
    ['wanda', ['staging']],
  ]) {
    const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
    try {
      const page = await ctx.newPage();
      await signIn(page, LIVE_USERS[user]);
      await openLiveTab(page, 'Environments');
      const rows = page.locator('arazzo-environments tr.erow');
      await expect(rows).toHaveCount(expected.length);
      for (const name of expected) {
        await expect(page.locator(`arazzo-environments tr.erow[data-name="${name}"]`)).toBeVisible();
      }
    } finally {
      await ctx.close();
    }
  }
});

test('the promotion loop crosses real identities: wanda requests staging availability, erin decides it from her Approvals queue, wanda sees the decision', async ({ browser }) => {
  // The §7.8 elevation path end to end on real infrastructure: the requester's dialog is constrained
  // by REAL readiness (staging is credentialed for nightly-reconcile's sources, so it is offered;
  // wanda's reach admits no other environment), and the approver's queue is the environments she
  // administers. Deny (with a recorded reason) keeps the suite re-runnable — no availability lands.
  const wandaCtx = await browser.newContext({ ignoreHTTPSErrors: true });
  const erinCtx = await browser.newContext({ ignoreHTTPSErrors: true });
  try {
    const wandaPage = await wandaCtx.newPage();
    await signIn(wandaPage, LIVE_USERS.wanda);

    const reason = uniq('live-ux promotion');
    await openLiveTab(wandaPage, 'Requests');
    await openLiveSubTab(wandaPage, 'sub-requests-availability');
    const minePanel = wandaPage.locator('#sub-requests-availability arazzo-availability-requests');
    await minePanel.locator('button.new').click();
    const dlg = wandaPage.locator('arazzo-availability-request-dialog');
    const wfInput = dlg.locator('.sub-wf input.q');
    await wfInput.click();
    await wfInput.fill('nightly');
    await dlg.locator('.sub-wf .results li[data-index]', { hasText: 'nightly-reconcile' }).first().click();
    // The version dropdown preselects the newest real catalogued version; the environment dropdown
    // offers ONLY ready targets within wanda's reach — exactly staging (its credential set carries
    // the same preprod-zone tag her reach rule matches).
    await expect(dlg.locator('.ver-in')).toBeEnabled();
    await expect(dlg.locator('.env-in option[value="staging"]')).toHaveCount(1);
    await dlg.locator('.env-in').selectOption('staging');
    await dlg.locator('.reason-in').fill(reason);
    await dlg.locator('button.ok').click();
    const mineRow = minePanel.locator('tbody tr[data-id]', { hasText: reason });
    await expect(mineRow.locator('.badge')).toHaveText('Pending');

    // erin administers staging: the request is HER queue's to decide. Deny with a recorded reason.
    const erinPage = await erinCtx.newPage();
    await signIn(erinPage, LIVE_USERS.erin);
    await openLiveTab(erinPage, 'Approvals');
    await openLiveSubTab(erinPage, 'sub-approvals-availability');
    const queue = erinPage.locator('#sub-approvals-availability arazzo-availability-requests');
    const queued = queue.locator('tbody tr[data-id]', { hasText: reason });
    await expect(queued).toHaveCount(1);
    await queued.locator('.act[data-action="deny"]').click();
    await queue.locator('dialog.decision-dialog .reason-in').fill('Live UX test cleanup: denied by design.');
    await queue.locator('dialog.decision-dialog button.ok').click();
    await expect(queue.locator('tbody tr[data-id]', { hasText: reason })).toHaveCount(0);

    // The loop closes for the requester.
    await minePanel.locator('button.refresh').click();
    await expect(mineRow.locator('.badge')).toHaveText('Denied');
  } finally {
    await wandaCtx.close();
    await erinCtx.close();
  }
});
