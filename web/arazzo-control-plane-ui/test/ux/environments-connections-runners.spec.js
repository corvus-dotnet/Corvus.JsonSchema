// UX suite — the governance areas of the control-plane app shell: Environments (§7.7 create/edit/delete
// with the creator-becomes-administrator rule), Connections (§13 credential bindings: status-first list,
// the auth-kind-driven guided editor, rotation, merge semantics, revoke), Runners (§5.4 registry depth +
// keyset paging), and Runner auth (§5.5 authorize / revoke / per-environment queue).
// Smoke already covers "the tabs open and list"; these go deeper into each surface's behavior.
import { test, expect } from '@playwright/test';
import { watchErrors, assertClean, openTab } from './ux-helpers.js';

// ---- Environments (§7.7) ------------------------------------------------------------------------

test('creating an environment seats its creator as the sole administrator (§7.7)', async ({ page }) => {
  const errors = watchErrors(page);
  await openTab(page, 'Environments');
  const env = page.locator('arazzo-environments');
  await expect(env.locator('.erow').first()).toBeVisible();

  await env.locator('.new').click();
  await expect(env.locator('dialog[open]')).toBeVisible();
  await env.locator('.f-name').fill('load-test');
  await env.locator('.f-displayName').fill('Load test');
  await env.locator('.confirm').click();

  // The new environment lands in the list and opens selected.
  await expect(env.locator('.erow[data-name="load-test"]')).toBeVisible();
  await expect(env.locator('.detail-pane .dtitle')).toContainText('Load test');

  // Governance: creating an environment grants the caller administration of it — the admin set is
  // exactly the creator's resolved identity (the mock stamps the acting subject, alice@ops).
  const admins = env.locator('arazzo-administrators-panel');
  await expect(admins.locator('.arow')).toHaveCount(1);
  await expect(admins.locator('.arow .glabel')).toHaveText('You (creator)');
  await expect(admins.locator('.arow .gident')).toContainText('sys:sub=alice@ops');

  // A brand-new environment has no availability yet.
  await expect(env.locator('.detail-pane')).toContainText('No workflow versions are available in this environment yet.');
  assertClean(errors);
});

test('an environment administrator edits metadata and management tags, stamping the audit line', async ({ page }) => {
  const errors = watchErrors(page);
  await openTab(page, 'Environments');
  const env = page.locator('arazzo-environments');
  await env.locator('.erow[data-name="staging"]').click();
  await expect(env.locator('.detail-pane .dtitle')).toContainText('Staging');

  await env.locator('.d-displayName').fill('Staging (EU)');
  await env.locator('.d-description').fill('EU pre-production.');
  // Re-tag the reach scope (§14.2) through the shared tag editor.
  await env.locator('.d-mgmt-editor .add').click();
  await env.locator('.d-mgmt-editor .tag-row .tk').last().fill('team');
  await env.locator('.d-mgmt-editor .tag-row .tv').last().fill('qa');
  await env.locator('.d-save').click();

  // The audit line stamps who updated; the list row reflects the new display name; the saved
  // management tag re-seeds the editor (the round-trip proves it persisted).
  await expect(env.locator('.detail-pane .audit')).toContainText(/updated/);
  await expect(env.locator('.detail-pane .audit')).toContainText('alice@ops');
  await expect(env.locator('.erow[data-name="staging"]')).toContainText('Staging (EU)');
  await expect(env.locator('.d-mgmt-editor .tag-row .tk')).toHaveValue('team');
  await expect(env.locator('.d-mgmt-editor .tag-row .tv')).toHaveValue('qa');
  assertClean(errors);
});

test('deleting an environment is confirm-gated: cancel keeps it, confirm removes it', async ({ page }) => {
  const errors = watchErrors(page);
  await openTab(page, 'Environments');
  const env = page.locator('arazzo-environments');
  await expect(env.locator('arazzo-pager .count')).toContainText('4 environments');
  await env.locator('.erow[data-name="uat"]').click();
  await expect(env.locator('.detail-pane .dtitle')).toContainText('UAT');

  // Cancel path: the strong confirm appears, cancelling leaves the environment untouched.
  await env.locator('.d-delete').click();
  const confirm = env.locator('dialog.arazzo-confirm');
  await expect(confirm).toBeVisible();
  await expect(confirm).toContainText("Delete the environment 'uat'?");
  await confirm.locator('.cancel').click();
  await expect(confirm).toHaveCount(0); // the transient dialog is removed, not just hidden
  await expect(env.locator('.erow[data-name="uat"]')).toBeVisible();

  // Confirm path: the environment is removed, the detail closes, and the footer count drops.
  await env.locator('.d-delete').click();
  await env.locator('dialog.arazzo-confirm .ok').click();
  await expect(env.locator('.erow[data-name="uat"]')).toHaveCount(0);
  await expect(env.locator('.detail-pane .dtitle')).toHaveCount(0);
  await expect(env.locator('arazzo-pager .count')).toContainText('3 environments');
  assertClean(errors);
});

// ---- Connections (§13 credential bindings) ------------------------------------------------------

test('the connections list is status-first: seeded valid/expiring/expired badges, footer pills, and filters', async ({ page }) => {
  const errors = watchErrors(page);
  await openTab(page, 'Connections');
  const table = page.locator('#view-credentials arazzo-credentials-table');
  const rows = table.locator('tbody tr[data-key]');
  await expect(rows.first()).toBeVisible();
  await expect(rows).toHaveCount(8);

  // The derived credentialStatus renders per seed: no/late expiry → valid, within 7 days → expiring
  // soon, past → expired.
  await expect(table.locator('tr[data-key="petstore@production"] .badge')).toHaveText('valid');
  await expect(table.locator('tr[data-key="billing@production"] .badge')).toHaveText('expiring soon');
  await expect(table.locator('tr[data-key="legacy@production"] .badge')).toHaveText('expired');

  // The operator's "what's about to break?" footer.
  const foot = table.locator('arazzo-pager .count');
  await expect(foot).toContainText('8 bindings');
  await expect(foot.locator('.pill.amber')).toHaveText('1 expiring soon');
  await expect(foot.locator('.pill.red')).toHaveText('1 expired');

  // The status filter narrows to the worklist; the source filter narrows by name.
  await table.locator('select.status').selectOption('expired');
  await expect(rows).toHaveCount(1);
  await expect(rows.first()).toContainText('legacy');
  await table.locator('select.status').selectOption('');
  await table.locator('input.src').fill('billing');
  await expect(rows).toHaveCount(2);
  await expect(foot).toContainText('2 bindings');
  assertClean(errors);
});

test('the credential editor is auth-kind driven: switching relabels the secret slots and config, preserving entries', async ({ page }) => {
  const errors = watchErrors(page);
  await openTab(page, 'Connections');
  const table = page.locator('#view-credentials arazzo-credentials-table');
  await table.locator('tr[data-key="events@staging"]').click();
  const detail = page.locator('#view-credentials arazzo-credential-detail');
  await expect(detail).toBeVisible();
  await detail.locator('.edit').click();

  const dlg = page.locator('#view-credentials arazzo-credential-dialog');
  await expect(dlg.locator('dialog')).toBeVisible();
  await expect(dlg.locator('.title')).toHaveText('Edit events@staging');

  // bearer: one guided slot, parsed back into the AWS Secrets Manager fields; no extra config.
  await expect(dlg.locator('.refrow .slot-label')).toContainText('Bearer token');
  await expect(dlg.locator('.refrow select.scheme')).toHaveValue('awssm');
  await expect(dlg.locator('.refrow input[data-key="id"]')).toHaveValue('events-token');
  await expect(dlg.locator('.config-fields')).toContainText('no extra config');

  // → apiKey: the slot relabels, the kind's config fields appear, and the entered reference survives
  // the switch (same 'value' role).
  await dlg.locator('#authKind').fill('apiKey');
  await dlg.locator('#authKind').dispatchEvent('change');
  await expect(dlg.locator('.refrow .slot-label').first()).toContainText('API key');
  await expect(dlg.locator('.config-fields input[data-cfg="parameterName"]')).toBeVisible();
  await expect(dlg.locator('.config-fields select[data-cfg="location"]')).toBeVisible();
  await expect(dlg.locator('.refrow input[data-key="id"]')).toHaveValue('events-token');

  // → basic: a different role ('password'), so the old 'value' reference is preserved as a flagged,
  // removable extra rather than silently dropped.
  await dlg.locator('#authKind').fill('basic');
  await dlg.locator('#authKind').dispatchEvent('change');
  await expect(dlg.locator('.refrow .slot-label').first()).toContainText('Password');
  await expect(dlg.locator('.config-fields')).toContainText('Username');
  const unused = dlg.locator('.refrow', { hasText: 'not used by this auth kind' });
  await expect(unused).toHaveCount(1);
  await expect(unused.locator('.rm')).toBeVisible();

  await dlg.locator('.cancel').click();
  await expect(dlg.locator('dialog')).not.toBeVisible();
  assertClean(errors);
});

test('rotating a credential re-points the secretRef through the guided composer and stamps Rotated', async ({ page }) => {
  const errors = watchErrors(page);
  await openTab(page, 'Connections');
  const table = page.locator('#view-credentials arazzo-credentials-table');
  await table.locator('tr[data-key="petstore@production"]').click();
  const detail = page.locator('#view-credentials arazzo-credential-detail');
  await expect(detail).toBeVisible();
  await expect(detail.locator('dt', { hasText: 'Rotated' })).toHaveCount(0); // not rotated yet
  await detail.locator('.edit').click();

  const dlg = page.locator('#view-credentials arazzo-credential-dialog');
  await expect(dlg.locator('dialog')).toBeVisible();

  // The seeded ref (keyvault://petstore-key#3) has no host/name split, so it cannot be represented in
  // the guided Key Vault shape — the dialog falls back to the raw-reference escape hatch.
  const row = dlg.locator('.refrow').first();
  await expect(row.locator('select.scheme')).toHaveValue('raw');
  await expect(row.locator('input.rawref')).toHaveValue('keyvault://petstore-key#3');

  // Rotate by composing a canonical reference through the guided per-store fields; the exact stored
  // value is previewed live, alongside the §13.5 runner-access note.
  await row.locator('select.scheme').selectOption('keyvault');
  await row.locator('input[data-key="host"]').fill('petstore-kv');
  await row.locator('input[data-key="name"]').fill('api-key');
  await row.locator('input[data-key="version"]').fill('4');
  await expect(row.locator('.refpreview')).toContainText('keyvault://petstore-kv/api-key#4');
  await expect(row.locator('.refaccess')).toContainText('Runner access');
  await dlg.locator('.confirm').click();
  await expect(dlg.locator('dialog')).not.toBeVisible();

  // A reference change IS a rotation: the detail shows the new ref and the Rotated stamp; the list
  // row carries the new reference too.
  await expect(detail.locator('.refs .mono').first()).toContainText('keyvault://petstore-kv/api-key#4');
  await expect(detail.locator('dt', { hasText: 'Rotated' })).toHaveCount(1);
  await expect(table.locator('tr[data-key="petstore@production"] .ref')).toHaveText('keyvault://petstore-kv/api-key#4');
  assertClean(errors);
});

test('editing a credential keeps usage immutable, edits management tags, and preserves unknown config keys', async ({ page }) => {
  const errors = watchErrors(page);
  await openTab(page, 'Connections');
  const table = page.locator('#view-credentials arazzo-credentials-table');
  await table.locator('tr[data-key="billing@staging"]').click();
  const detail = page.locator('#view-credentials arazzo-credential-detail');
  await expect(detail).toBeVisible();
  await detail.locator('.edit').click();

  const dlg = page.locator('#view-credentials arazzo-credential-dialog');
  await expect(dlg.locator('dialog')).toBeVisible();

  // Usage is set at create and immutable after: on edit the usage fieldset is gone and the grant
  // shows read-only (the seeded person restriction).
  await expect(dlg.locator('fieldset.create-only')).toBeHidden();
  await expect(dlg.locator('.scopes-readonly')).toBeVisible();
  await expect(dlg.locator('.scopes-readonly')).toContainText('Ada Lovelace');

  // Add a config key the auth kind does not define (the escape hatch) plus a management tag.
  await dlg.locator('.addcfg').click();
  await dlg.locator('.config-extra .row .a').fill('regionHint');
  await dlg.locator('.config-extra .row .b').fill('eu-west-1');
  await dlg.locator('.mgmt-editor .add').click();
  await dlg.locator('.mgmt-editor .tag-row .tk').last().fill('team');
  await dlg.locator('.mgmt-editor .tag-row .tv').last().fill('billing');
  await dlg.locator('.confirm').click();
  await expect(dlg.locator('dialog')).not.toBeVisible();

  // The save merged: unknown config preserved, tags re-tagged, usage untouched — and since the
  // references did not change, NO rotation was stamped.
  await expect(detail.locator('.cfg')).toContainText('regionHint = eu-west-1');
  await expect(detail.locator('.tags .tag')).toHaveText(['team=billing']);
  await expect(detail).toContainText('Ada Lovelace');
  await expect(detail.locator('dt', { hasText: 'Rotated' })).toHaveCount(0);

  // Round-trip: re-opening the editor shows the unknown key as an editable extra row and the kind's
  // own fields still populated.
  await detail.locator('.edit').click();
  await expect(dlg.locator('dialog')).toBeVisible();
  await expect(dlg.locator('.config-extra .row .a')).toHaveValue('regionHint');
  await expect(dlg.locator('.config-fields input[data-cfg="tokenUrl"]')).toHaveValue('https://idp.example.com/oauth/token');
  await dlg.locator('.cancel').click();
  assertClean(errors);
});

test('revoking a credential is confirm-gated and removes the binding from the worklist', async ({ page }) => {
  const errors = watchErrors(page);
  await openTab(page, 'Connections');
  const table = page.locator('#view-credentials arazzo-credentials-table');
  const rows = table.locator('tbody tr[data-key]');
  await expect(rows).toHaveCount(8);
  await table.locator('tr[data-key="legacy@production"]').click();
  const detail = page.locator('#view-credentials arazzo-credential-detail');
  await expect(detail).toBeVisible();

  await detail.locator('.revoke').click();
  const confirm = detail.locator('dialog.arazzo-confirm');
  await expect(confirm).toBeVisible();
  await expect(confirm).toContainText('legacy@production'); // the confirm names the exact binding
  await confirm.locator('.ok').click();

  // The binding is gone, the detail pane closes, and the footer no longer reports an expired binding
  // (legacy was the only expired seed).
  await expect(table.locator('tr[data-key="legacy@production"]')).toHaveCount(0);
  await expect(rows).toHaveCount(7);
  await expect(page.locator('#view-credentials arazzo-credential-detail')).toHaveCount(0);
  const foot = table.locator('arazzo-pager .count');
  await expect(foot).toContainText('7 bindings');
  await expect(foot.locator('.pill.red')).toHaveCount(0);
  assertClean(errors);
});

// ---- Runners (§5.4 registry) --------------------------------------------------------------------

test('the runner registry details each host: environment, transports, hosted versions, and staleness', async ({ page }) => {
  const errors = watchErrors(page);
  await openTab(page, 'Runners');
  const runners = page.locator('arazzo-runners');
  await expect(runners.locator('.runner')).toHaveCount(3);

  // runner-eu-1: healthy production host — env chip, both transports, two loaded versions, concurrency.
  const eu1 = runners.locator('.runner', { hasText: 'runner-eu-1' });
  await expect(eu1.locator('.renv')).toHaveText('production');
  await expect(eu1.locator('.health.online')).toHaveText(/Online/);
  await expect(eu1.locator('.badge')).toHaveText(['http', 'amqp']);
  await expect(eu1.locator('.hv')).toHaveCount(2);
  await expect(eu1.locator('.hv').first()).toContainText('adopt-pet');
  await expect(eu1.locator('.hv .lstate').first()).toHaveText('loaded');
  await expect(eu1.locator('.rmeta')).toContainText('concurrency 8');

  // runner-eu-2: staging host still loading one hosted version.
  const eu2 = runners.locator('.runner', { hasText: 'runner-eu-2' });
  await expect(eu2.locator('.renv')).toHaveText('staging');
  await expect(eu2.locator('.hv', { hasText: 'onboard-customer' }).locator('.lstate')).toHaveText('loading');

  // runner-us-1: the lapsed heartbeat renders Stale, with the absolute heartbeat on the tooltip.
  const us1 = runners.locator('.runner', { hasText: 'runner-us-1' });
  await expect(us1.locator('.health.stale')).toHaveText(/Stale/);
  await expect(us1.locator('.health')).toHaveAttribute('title', /Last heartbeat/);

  // The footer counts the fleet and its stale members.
  await expect(runners.locator('arazzo-pager .count')).toContainText('3 registered · 1 stale');
  assertClean(errors);
});

test('the runner registry pages with the keyset cursor', async ({ page }) => {
  const errors = watchErrors(page);
  await openTab(page, 'Runners');
  const list = page.locator('arazzo-runners .runner');
  await expect(list).toHaveCount(3);

  // Shrink the page size to force paging over the 3 seeded hosts. The page-size attribute change also
  // resets the panel's client (it assumes base-url wiring), so re-inject the demo's mock-backed client
  // — grabbed from a sibling panel, since the demo holds it in module scope.
  await page.evaluate(() => {
    const shared = document.querySelector('arazzo-credentials').client;
    const el = document.querySelector('arazzo-runners');
    el.setAttribute('page-size', '2');
    el.client = shared;
  });

  await expect(list).toHaveCount(2);
  await expect(list.first()).toContainText('runner-eu-1'); // ordered by runnerId
  const pager = page.locator('arazzo-runners arazzo-pager');
  await expect(pager.locator('.next')).toBeEnabled();
  await pager.locator('.next').click();

  await expect(list).toHaveCount(1);
  await expect(list.first()).toContainText('runner-us-1');
  await expect(pager.locator('.count')).toContainText('page 2');
  await expect(pager.locator('.next')).toBeDisabled();

  await pager.locator('.prev').click();
  await expect(list).toHaveCount(2);
  await expect(list.first()).toContainText('runner-eu-1');
  assertClean(errors);
});

// ---- Runner authorizations (§5.5) ----------------------------------------------------------------

test('authorizing a pending runner records the decision and stops offering Authorize (§5.5)', async ({ page }) => {
  const errors = watchErrors(page);
  await openTab(page, 'Runner auth');
  const inbox = page.locator('arazzo-runner-authorizations');
  const rows = inbox.locator('tbody tr[data-key]');
  await expect(rows.first()).toBeVisible();
  await expect(rows).toHaveCount(2); // the two Pending seeds across the administered environments

  // Authorize runner-us-1 for production, recording a note with the decision.
  await inbox.locator('tr[data-key="production runner-us-1"] .act[data-action="authorize"]').click();
  const dlg = inbox.locator('dialog.decision-dialog');
  await expect(dlg).toBeVisible();
  await expect(dlg).toContainText('runner-us-1');
  await expect(dlg).toContainText('production');
  await dlg.locator('.reason-in').fill('Vetted US production host.');
  await dlg.locator('.ok').click();

  // The pending inbox shrank to the one remaining to-do.
  await expect(rows).toHaveCount(1);
  await expect(rows.first()).toContainText('runner-eu-2');

  // The Authorized view shows the flip with the recorded decision — and never offers Authorize again
  // (deciding into the current state is a no-op server-side, so the verb is absent, only Revoke).
  await inbox.locator('.toolbar .status').selectOption('Authorized');
  const authRow = inbox.locator('tr[data-key="production runner-us-1"]');
  await expect(authRow).toBeVisible();
  await expect(authRow.locator('.badge')).toHaveText('Authorized');
  await expect(authRow).toContainText('alice@ops');
  await expect(authRow).toContainText('Vetted US production host.');
  await expect(authRow.locator('.act[data-action="authorize"]')).toHaveCount(0);
  await expect(authRow.locator('.act[data-action="revoke"]')).toHaveCount(1);
  assertClean(errors);
});

test('a revoked runner leaves the dispatchable set and may be re-authorized (§5.5)', async ({ page }) => {
  const errors = watchErrors(page);
  await openTab(page, 'Runner auth');
  const inbox = page.locator('arazzo-runner-authorizations');
  const rows = inbox.locator('tbody tr[data-key]');
  await expect(rows.first()).toBeVisible();

  // The one seeded Authorized runner; revoke it with a reason.
  await inbox.locator('.toolbar .status').selectOption('Authorized');
  const eu1 = inbox.locator('tr[data-key="production runner-eu-1"]');
  await expect(eu1).toBeVisible();
  await eu1.locator('.act[data-action="revoke"]').click();
  const dlg = inbox.locator('dialog.decision-dialog');
  await expect(dlg).toBeVisible();
  await dlg.locator('.reason-in').fill('Host compromised - rotate first.');
  await dlg.locator('.ok').click();

  // No authorized runner remains; the contextual empty state says so.
  await expect(inbox.locator('tbody')).toContainText(/no authorized runner authorizations/i);

  // Under Revoked it sits beside the long-decommissioned seed, offering re-authorize (never re-revoke).
  await inbox.locator('.toolbar .status').selectOption('Revoked');
  await expect(rows).toHaveCount(2);
  const revoked = inbox.locator('tr[data-key="production runner-eu-1"]');
  await expect(revoked.locator('.badge')).toHaveText('Revoked');
  await expect(revoked).toContainText('Host compromised - rotate first.');
  await expect(revoked.locator('.act[data-action="revoke"]')).toHaveCount(0);
  // A revoked runner returns to service by re-authorization (a deliberate action, distinct from authorizing a pending one).
  await revoked.locator('.act[data-action="reauthorize"]').click();
  await expect(inbox.locator('dialog.decision-dialog')).toBeVisible();
  await inbox.locator('dialog.decision-dialog .ok').click();

  // Re-authorized: it leaves the Revoked view (only the decommissioned host remains).
  await expect(rows).toHaveCount(1);
  await expect(rows.first()).toContainText('runner-eu-old');
  assertClean(errors);
});

test('a faulted runner can be quarantined (temporary, in-flight drains) and reinstated (§5.5)', async ({ page }) => {
  const errors = watchErrors(page);
  await openTab(page, 'Runner auth');
  const inbox = page.locator('arazzo-runner-authorizations');
  const rows = inbox.locator('tbody tr[data-key]');
  await expect(rows.first()).toBeVisible();

  // The authorized runner offers Quarantine (amber, temporary) beside Revoke (red, permanent).
  await inbox.locator('.toolbar .status').selectOption('Authorized');
  const eu1 = inbox.locator('tr[data-key="production runner-eu-1"]');
  const quarantine = eu1.locator('.act[data-action="quarantine"]');
  await expect(quarantine).toHaveClass(/warn/);
  await quarantine.click();
  const dlg = inbox.locator('dialog.decision-dialog');
  await expect(dlg).toBeVisible();
  await expect(dlg).toContainText(/in-flight runs finish/i);
  await dlg.locator('.reason-in').fill('Intermittent 5xx; draining.');
  await dlg.locator('.ok').click();

  // It leaves the Authorized view; under Quarantined it sits with the pre-seeded quarantined host.
  await expect(inbox.locator('tbody')).toContainText(/no authorized runner authorizations/i);
  await inbox.locator('.toolbar .status').selectOption('Quarantined');
  await expect(rows).toHaveCount(2);
  const quarantined = inbox.locator('tr[data-key="production runner-eu-1"]');
  await expect(quarantined.locator('.badge')).toHaveText('Quarantined');
  await expect(quarantined).toContainText('Intermittent 5xx; draining.');
  await expect(quarantined.locator('.act[data-action="revoke"]')).toHaveCount(1);

  // Reinstate returns it to service with no re-registration; it leaves the Quarantined view.
  await quarantined.locator('.act[data-action="reinstate"]').click();
  await expect(dlg).toBeVisible();
  await dlg.locator('.ok').click();
  await expect(rows).toHaveCount(1);
  await expect(rows.first()).toContainText('runner-eu-fault');

  // Back under Authorized it is dispatchable again.
  await inbox.locator('.toolbar .status').selectOption('Authorized');
  await expect(inbox.locator('tr[data-key="production runner-eu-1"]').locator('.badge')).toHaveText('Authorized');
  assertClean(errors);
});

test("the runner-authorization inbox narrows to one environment's queue per status", async ({ page }) => {
  const errors = watchErrors(page);
  await openTab(page, 'Runner auth');
  const inbox = page.locator('arazzo-runner-authorizations');
  const rows = inbox.locator('tbody tr[data-key]');
  await expect(rows).toHaveCount(2); // all administered environments, Pending default

  // Narrow to staging via the standard environments picker: pick from the reach-scoped registry, never free-type.
  const envPicker = inbox.locator('.toolbar .env');
  await envPicker.locator('input.q').fill('staging');
  await envPicker.locator('.results li[data-index]', { hasText: 'staging' }).first().click();
  await expect(rows).toHaveCount(1);
  await expect(rows.first()).toContainText('runner-eu-2');
  await expect(rows.first().locator('.env-name')).toHaveText('staging');

  // Re-point at production: clear the picked chip, pick again — its pending queue, then its revoked roster.
  await envPicker.locator('.clear').click();
  await envPicker.locator('input.q').fill('production');
  await envPicker.locator('.results li[data-index]', { hasText: 'production' }).first().click();
  await expect(rows).toHaveCount(1);
  await expect(rows.first()).toContainText('runner-us-1');
  await inbox.locator('.toolbar .status').selectOption('Revoked');
  await expect(rows).toHaveCount(1);
  await expect(rows.first()).toContainText('runner-eu-old');
  await expect(inbox.locator('arazzo-pager .count')).toContainText('1 authorization');
  assertClean(errors);
});
