// UX suite — the Permissions area (grants + rules, design §14.2/§16.5.4) and the Access area (the who-can-do-what
// overview §6.1 + the §16.5 access-request inbox). Smoke already covers tab placement ("Grants and Rules live on the
// Permissions tab; Access holds the request inbox") and the persona-gated promotion flow; this file goes deeper into
// authoring, the correct-by-construction grantee flows, the resolved capability view, and the approver decisions.
//
// Grounding (selectors read from source):
//   src/components/scopes-panel.js   — <arazzo-rules-panel> (primary tag; arazzo-scopes-panel is the deprecated alias)
//   src/components/grants-panel.js   — <arazzo-grants-panel> master-detail, verb rows, additional clauses
//   src/components/grantee-picker.js — merged directory+observed search, partial-identity flag, kind badges
//   src/components/access-overview-panel.js, access-requests-panel.js, access-request-dialog.js
//
// Seeds: demo/index.html feeds the mock DEMO_SEED (demo/demo-seed.js) — example data that OVERRIDES the built-in
// mock-api.js seeds for rules, bindings, grantees, administrators and access requests. The example seed carries the
// full §16.5 shape vocabulary — seven bindings including a scope-conferring elevation (bind-5, sub=u-1042), a PIM
// eligibility (bind-6, team=growth), and the per-rule union pair (bind-2/bind-7: a grant's rules are a
// conjunction, so SRE's two write domains are two grants) — and seats the administrator persona on every admin set. The built-in seeds
// remain the set the COMPONENT tests assert against (demo-seed.js:1-3), so tests pinning built-in fixture shapes
// (bind-2/bind-4 wording, the platform tenant, req-2004) repoint at a fresh built-in-seed mock in-page (see
// useBuiltinSeeds below).
import { test, expect } from '@playwright/test';
import { watchErrors, assertClean, openApp, openTab } from './ux-helpers.js';

/**
 * Point the named kit elements at a FRESH in-page mock running the mock-api.js BUILT-IN seeds — the seed set the
 * component tests assert against (demo-seed.js:1-3). The demo page's example seed deliberately differs (different
 * bindings, grantees and requests), so tests that pin built-in fixture shapes — bind-2/bind-4's scope wording, the
 * platform tenant's administration, the two-request approver inbox with req-2004 — swap the seed here. Same panels,
 * same client, same mock implementation — only the seed data swaps.
 */
async function useBuiltinSeeds(page, selectors) {
  await page.evaluate(async (sels) => {
    const { createMockControlPlane } = await import('/demo/mock-api.js');
    const { ArazzoControlPlaneClient } = await import('/src/arazzo-client.js');
    const mock = createMockControlPlane(); // no options → the built-in seeds, administrator persona
    const client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
    for (const sel of sels) document.querySelector(sel).client = client;
  }, selectors);
}

// ---- Rules panel --------------------------------------------------------------------------------

test('the Rules panel lists the seeded rules with expressions and a seed-derived footer count, and search narrows server-side', async ({ page }) => {
  const errors = watchErrors(page);
  await openTab(page, 'Permissions');

  const rules = page.locator('arazzo-rules-panel');
  const rows = rules.locator('tbody tr.srow');
  await expect(rows).toHaveCount(5); // the five demo-seed rules, ordered by name
  await expect(rules.locator('tr[data-name="shares-a-label"] .sexpr')).toHaveText('$claims.intersects');
  await expect(rules.locator('tr[data-name="data-confidential"] .sexpr')).toHaveText("classification <= 'confidential'");
  await expect(rules.locator('tr[data-name="reach-payments"] .sexpr')).toHaveText("domain == 'payments'");
  // The footer count comes from the seeds (the count surface this demo has — the tabs carry no badge counts).
  await expect(rules.locator('arazzo-pager .count')).toContainText(/5\+? rules/);

  // Search is a server-side q over name/expression: 'reach' matches exactly the two reach-* rules.
  await rules.locator('input.search').fill('reach');
  await expect(rows).toHaveCount(2);
  await expect(rules.locator('tr[data-name="reach-onboarding"]')).toBeVisible();
  assertClean(errors);
});

test('a rule is authored template-first: the ordered template is offered (orderings configured) and label-eq builds a simple-grammar expression with a suggested name', async ({ page }) => {
  const errors = watchErrors(page);
  await openTab(page, 'Permissions');

  const rules = page.locator('arazzo-rules-panel');
  // The orderings load with the first page; wait for the rows so the create pane opens with them present.
  await expect(rules.locator('tbody tr.srow')).toHaveCount(5);
  await rules.locator('button.new').click();
  await expect(rules.locator('.detail .dtitle')).toHaveText('New rule');

  // The classification-ordering template is offered BECAUSE the deployment configures an ordering — with its labels.
  const radios = rules.locator('input[name="tmpl"]');
  await expect(radios).toHaveCount(7);
  await rules.locator('input[name="tmpl"][value="ordered"]').check();
  await expect(rules.locator('select.f-dim option')).toHaveText(['classification']);
  await expect(rules.locator('select.f-value option')).toHaveText(['public', 'internal', 'confidential', 'restricted']);
  await expect(rules.locator('.preview-expr')).toHaveText("classification <= 'public'");

  // Back to the default goal: a label-equality rule. The preview tracks the fields live and the name is suggested.
  await rules.locator('input[name="tmpl"][value="label-eq"]').check();
  await rules.locator('input.f-dim').fill('domain');
  await rules.locator('input.f-value').fill('ops');
  await expect(rules.locator('.preview-expr')).toHaveText("domain == 'ops'");
  await expect(rules.locator('input.f-name')).toHaveValue('rule-ops');

  await rules.locator('.dfoot .confirm').click();
  await expect(rules.locator('tr[data-name="rule-ops"] .sexpr')).toHaveText("domain == 'ops'");
  await expect(rules.locator('arazzo-pager .count')).toContainText(/6\+? rules/);
  assertClean(errors);
});

test('editing a rule opens it in the detail pane with the name immutable, and Save persists the new expression', async ({ page }) => {
  const errors = watchErrors(page);
  await openTab(page, 'Permissions');

  const rules = page.locator('arazzo-rules-panel');
  await rules.locator('tr[data-name="reach-payments"]').click();
  await expect(rules.locator('.detail .dtitle')).toHaveText("Edit rule 'reach-payments'");
  await expect(rules.locator('input.f-name')).toHaveValue('reach-payments');
  await expect(rules.locator('input.f-name')).toHaveAttribute('readonly', '');
  await expect(rules.locator('textarea.f-expression')).toHaveValue("domain == 'payments'");

  await rules.locator('textarea.f-expression').fill("domain == 'payments' or domain == 'refunds'");
  await rules.locator('.dfoot .confirm').click();
  await expect(rules.locator('tr[data-name="reach-payments"] .sexpr')).toContainText("or domain == 'refunds'");
  assertClean(errors);
});

test('deleting a rule is confirm-gated: Cancel keeps it, confirming removes it from the list', async ({ page }) => {
  const errors = watchErrors(page);
  await openTab(page, 'Permissions');

  const rules = page.locator('arazzo-rules-panel');
  await rules.locator('tr[data-name="data-confidential"]').click();
  await rules.locator('.dfoot .del').click();
  const confirm = rules.locator('dialog.arazzo-confirm');
  await expect(confirm).toBeVisible();
  await expect(confirm).toContainText("Delete the rule 'data-confidential'");
  await confirm.locator('button.cancel').click();
  await expect(confirm).not.toBeVisible();
  await expect(rules.locator('tr[data-name="data-confidential"]')).toBeVisible(); // cancel deleted nothing

  await rules.locator('.dfoot .del').click();
  await rules.locator('dialog.arazzo-confirm button.ok').click();
  await expect(rules.locator('tr[data-name="data-confidential"]')).toHaveCount(0);
  await expect(rules.locator('arazzo-pager .count')).toContainText(/4\+? rules/);
  assertClean(errors);
});

// ---- Grants panel -------------------------------------------------------------------------------

test('the Grants panel lists the seeded bindings with per-verb reach summaries and a seed-derived footer count', async ({ page }) => {
  const errors = watchErrors(page);
  await openTab(page, 'Permissions');

  const grants = page.locator('arazzo-grants-panel');
  await expect(grants.locator('tbody tr.grow-row')).toHaveCount(7);
  await expect(grants.locator('arazzo-pager .count')).toContainText(/7\+? grants/);

  // bind-1: rule-scoped read+write, denied purge.
  const b1 = grants.locator('tr[data-id="bind-1"]');
  await expect(b1.locator('.claim')).toContainText('team=payments');
  await expect(b1.locator('.verbs')).toContainText('read reach-payments');
  await expect(b1.locator('.verbs')).toContainText('purge Denied');

  // bind-2/bind-7: SRE's two write domains are TWO grants (a grant's rules are a conjunction —
  // §14.2 — so either/or reach is expressed as a union of bindings, one route per rule).
  const b2 = grants.locator('tr[data-id="bind-2"]');
  await expect(b2.locator('.claim')).toContainText('role=sre');
  await expect(b2.locator('.verbs')).toContainText('read Unrestricted');
  await expect(b2.locator('.verbs')).toContainText('write reach-payments');
  await expect(grants.locator('tr[data-id="bind-7"] .verbs')).toContainText('write reach-onboarding');
  await expect(grants.locator('tr[data-id="bind-7"] .gdesc')).toContainText('rules within one grant must ALL match');

  // bind-4 is a direct person grant, keyed on the sub claim, with its description shown in the row.
  await expect(grants.locator('tr[data-id="bind-4"] .claim')).toContainText('sub=u-1042');
  await expect(grants.locator('tr[data-id="bind-4"] .gdesc')).toContainText('a direct person grant');

  // The elevation shapes (§16.5.2/§16.5.3) wear their capability wording in the list: bind-5 is an
  // approval-written ACTIVE conferral (time-boxed scopes), bind-6 a PIM eligibility (confers nothing active).
  await expect(grants.locator('tr[data-id="bind-5"] .gscopes')).toContainText('confers runs:read, runs:write until');
  await expect(grants.locator('tr[data-id="bind-6"] .gscopes')).toContainText('eligible for runs:write until');
  assertClean(errors);
});

test('a grant is authored via the grantee picker with per-verb reach and identity clauses, and re-opening it pins the identity read-only', async ({ page }) => {
  const errors = watchErrors(page);
  await openTab(page, 'Permissions');

  const grants = page.locator('arazzo-grants-panel');
  await expect(grants.locator('tbody tr.grow-row')).toHaveCount(7); // first load settled
  await grants.locator('button.new').click();
  await expect(grants.locator('.detail .dtitle')).toHaveText('New grant');
  // The multi-IdP caveat is surfaced on create.
  await expect(grants.locator('.detail')).toContainText('A claim is only as trustworthy as the issuer asserting it');

  // WHO — pick the payments team from the directory: the resolved identity derives the canonical claim.
  const pickerInput = grants.locator('arazzo-grantee-picker input.q');
  await pickerInput.click();
  await pickerInput.fill('payments');
  const teamHit = grants.locator('arazzo-grantee-picker .results li[data-index]', { hasText: 'Payments' });
  await expect(teamHit.locator('.badge')).toHaveText('team');
  await teamHit.click();
  await expect(grants.locator('input.f-claimType')).toHaveValue('team');
  await expect(grants.locator('input.f-claimValue')).toHaveValue('payments');

  // WHERE — read is limited to a rule picked from the server-paged typeahead; write is unrestricted.
  await grants.locator('select.verb-mode[data-verb="read"]').selectOption('scopes');
  await grants.locator('.scope-input[data-verb="read"]').click();
  await grants.locator('.results[data-verb="read"] li[data-name="reach-payments"]').click();
  await expect(grants.locator('.verb-row .chip', { hasText: 'reach-payments' })).toBeVisible();
  // Selecting a rule closes the suggestions and they STAY closed across the refocus (the dropdown
  // overlays the pane's footer, so a lingering listbox would swallow the Create click).
  await expect(grants.locator('.results[data-verb="read"]')).toBeHidden();
  await grants.locator('select.verb-mode[data-verb="write"]').selectOption('unrestricted');

  // Pin another identity dimension the caller must ALSO carry (§16.5.4 tag-set selector).
  await grants.locator('button.add-clause').click();
  await grants.locator('.clause-row input.f-clause-dim').fill('iss');
  await grants.locator('.clause-row input.f-clause-val').fill('https://idp.example.com');
  await grants.locator('input.f-description').fill('UX test grant');
  await grants.locator('.dfoot .confirm').click();

  // The list shows the new binding: claim + ANDed clause + the verb summary.
  const row = grants.locator('tbody tr.grow-row', { hasText: 'UX test grant' });
  await expect(row.locator('.claim')).toContainText('team=payments');
  await expect(row.locator('.claim .claim-and')).toContainText('+ iss=https://idp.example.com');
  await expect(row.locator('.verbs')).toContainText('read reach-payments');
  await expect(row.locator('.verbs')).toContainText('write Unrestricted');

  // Re-open it: the claim selector is fixed after creation — the clauses render as READ-ONLY chips with the
  // full-resolved-identity caveat, not editable inputs.
  await row.click();
  await expect(grants.locator('input.f-claimType')).toHaveAttribute('readonly', '');
  await expect(grants.locator('.detail .chips .chip', { hasText: 'iss = https://idp.example.com' })).toBeVisible();
  await expect(grants.locator('.detail .caveat', { hasText: 'full resolved identity' })).toBeVisible();
  await expect(grants.locator('.detail input.f-clause-dim')).toHaveCount(0);
  assertClean(errors);
});

test('picking a person grantee steers to the access-request flow: the grant editor blocks a direct per-person grant', async ({ page }) => {
  const errors = watchErrors(page);
  await openTab(page, 'Permissions');

  const grants = page.locator('arazzo-grants-panel');
  await expect(grants.locator('tbody tr.grow-row')).toHaveCount(7);
  await grants.locator('button.new').click();
  const pickerInput = grants.locator('arazzo-grantee-picker input.q');
  await pickerInput.click();
  await pickerInput.fill('Ada');
  const personHit = grants.locator('arazzo-grantee-picker .results li[data-index]', { hasText: 'Ada Lovelace' });
  await expect(personHit.locator('.badge')).toHaveText('person');
  await personHit.click();

  // The steer renders inline and the claim stays empty — a person is requested-and-approved, never granted directly.
  await expect(grants.locator('.error-banner.steer')).toContainText('access-request flow');
  await expect(grants.locator('input.f-claimType')).toHaveValue('');

  // Submitting anyway is refused client-side with the same steer.
  await grants.locator('.dfoot .confirm').click();
  await expect(grants.locator('.form-err .error-banner')).toContainText('Use the access-request flow for a person');
  await expect(grants.locator('tbody tr.grow-row')).toHaveCount(7); // nothing was created
  assertClean(errors);
});

test('deleting a grant is confirm-gated and removes the binding from the list', async ({ page }) => {
  const errors = watchErrors(page);
  await openTab(page, 'Permissions');

  const grants = page.locator('arazzo-grants-panel');
  await grants.locator('tr[data-id="bind-3"]').click();
  await expect(grants.locator('.detail .dtitle')).toContainText("Edit grant 'team=growth'");
  await grants.locator('.dfoot .del').click();
  const confirm = grants.locator('dialog.arazzo-confirm');
  await expect(confirm).toContainText("Delete the grant for 'team=growth'");
  await confirm.locator('button.ok').click();
  await expect(grants.locator('tr[data-id="bind-3"]')).toHaveCount(0);
  await expect(grants.locator('tbody tr.grow-row')).toHaveCount(6);
  assertClean(errors);
});

// ---- Grantee picker (on the Access overview) ----------------------------------------------------

test('the grantee picker merges directory and observed results with kind badges, and flags a partial identity through to the selection chip', async ({ page }) => {
  const errors = watchErrors(page);
  await openTab(page, 'Access');

  const picker = page.locator('arazzo-access-overview arazzo-grantee-picker');
  await picker.locator('input.q').click(); // focus pops the initial (empty-q) suggestions — no typing needed

  // All six seeded grantees, directory hits first (the merge prefers + orders the directory source).
  const items = picker.locator('.results li[data-index]');
  await expect(items).toHaveCount(6);
  await expect(items.first().locator('.label')).toHaveText('Ada Lovelace');
  await expect(items.first().locator('.src')).toHaveText('directory');
  await expect(picker.locator('.results li .src', { hasText: 'observed' }).first()).toBeVisible();
  // Every seeded kind renders as a badge.
  for (const kind of ['person', 'team', 'role', 'workflow']) {
    await expect(picker.locator('.results li .badge', { hasText: kind }).first()).toBeVisible();
  }

  // The kind filter narrows the search to one kind.
  await picker.locator('select.kind').selectOption('team');
  await expect(items).toHaveCount(2);
  await expect(items.locator('.label')).toHaveText(['Payments', 'Growth']);

  // Grace Hopper is an observed, INCOMPLETE identity: flagged in the list, and the warning follows the selection —
  // under membership matching a partial identity is broader than intended.
  await picker.locator('select.kind').selectOption('person');
  const grace = picker.locator('.results li[data-index]', { hasText: 'Grace Hopper' });
  await expect(grace.locator('.badge')).toHaveClass(/partial/);
  await expect(grace.locator('.ident')).toContainText('partial identity');
  await grace.click();
  await expect(picker.locator('.selected .badge')).toHaveClass(/partial/);
  await expect(picker.locator('.selected .warn')).toContainText('partial identity');
  assertClean(errors);
});

// ---- Access overview ----------------------------------------------------------------------------

test('the Access overview aggregates one grantee: reach grants with inline Revoke, administered workflows, and usable credentials', async ({ page }) => {
  const errors = watchErrors(page);
  await openTab(page, 'Access');

  const overview = page.locator('arazzo-access-overview');
  const input = overview.locator('arazzo-grantee-picker input.q');
  await input.click();
  await input.fill('Ada');
  await overview.locator('arazzo-grantee-picker .results li[data-index]', { hasText: 'Ada Lovelace' }).click();

  // The who chip shows the resolved sys: identity.
  await expect(overview.locator('.who .glabel')).toHaveText('Ada Lovelace');
  await expect(overview.locator('.who .gident')).toContainText('sys:sub=u-1042');

  // Reach: her direct sub-keyed grants — the standing one (bind-4) and the approval-written elevation
  // (bind-5) — each with the inline Revoke (the demo persona holds security:write).
  const grantBlocks = overview.locator('.grant');
  await expect(grantBlocks).toHaveCount(2);
  await expect(overview.locator('.grant .claim')).toHaveText(['sub = u-1042', 'sub = u-1042']);
  await expect(overview.locator('.grant:has(button[data-revoke="bind-4"]) .verb', { hasText: 'read' })).toContainText('reach-payments');
  await expect(overview.locator('.grant button.revoke')).toHaveCount(2);

  // The elevation confers ACTIVE, time-boxed capability scopes — solid chips, each carrying its expiry.
  const caps = overview.locator('.caps .cap');
  await expect(caps).toHaveCount(2);
  await expect(caps.first()).toContainText('runs:read');
  await expect(caps.nth(1)).toContainText('runs:write');
  await expect(overview.locator('.caps .cap.eligible')).toHaveCount(0); // conferred, not PIM-eligible
  await expect(caps.first().locator('.until')).toContainText('until');

  // Administers: nightly-reconcile names her sys:sub in its administrator set; no environment does.
  await expect(overview.locator('.section', { hasText: 'Administers' }).first().locator('.row .grow')).toHaveText('nightly-reconcile');
  await expect(overview.locator('.body')).toContainText('Administers no environments.');

  // Credential usage: the staging billing credential is usage-scoped to her identity.
  const creds = overview.locator('.section', { hasText: 'Credential usage' });
  await expect(creds.locator('.row')).toContainText('billing / staging');
  await expect(creds.locator('button.link')).toHaveText('Open');
  assertClean(errors);
});

test('the overview inline Revoke deletes the underlying binding: the overview refreshes and the Grants list agrees', async ({ page }) => {
  const errors = watchErrors(page);
  await openTab(page, 'Access');

  const overview = page.locator('arazzo-access-overview');
  const input = overview.locator('arazzo-grantee-picker input.q');
  await input.click();
  await input.fill('Ada');
  await overview.locator('arazzo-grantee-picker .results li[data-index]', { hasText: 'Ada Lovelace' }).click();
  await expect(overview.locator('.grant')).toHaveCount(2); // bind-4 (standing) + bind-5 (elevation)

  await overview.locator('button[data-revoke="bind-4"]').click();
  const confirm = overview.locator('dialog.arazzo-confirm');
  await expect(confirm).toContainText('every principal it matches'); // the blast radius is named before the act
  await confirm.locator('button.ok').click();

  // The revoked binding leaves the overview; the elevation grant is untouched.
  await expect(overview.locator('.grant')).toHaveCount(1);
  await expect(overview.locator('button[data-revoke="bind-4"]')).toHaveCount(0);
  await expect(overview.locator('button[data-revoke="bind-5"]')).toHaveCount(1);

  // The Permissions tab reflects the deletion (same backend; the tab refresh re-fetches).
  await page.getByRole('tab', { name: 'Permissions' }).click();
  await expect(page.locator('arazzo-grants-panel tbody tr.grow-row').first()).toBeVisible();
  await expect(page.locator('arazzo-grants-panel tr[data-id="bind-4"]')).toHaveCount(0);
  assertClean(errors);
});

test('a team grantee resolves through its team-keyed binding and administered workflow, with empty sections stated honestly', async ({ page }) => {
  const errors = watchErrors(page);
  await openTab(page, 'Access');

  const overview = page.locator('arazzo-access-overview');
  const input = overview.locator('arazzo-grantee-picker input.q');
  await input.click();
  await input.fill('Growth');
  await overview.locator('arazzo-grantee-picker .results li[data-index]', { hasText: 'Growth' }).click();

  // Both team=growth bindings match the resolved identity: the standing read grant (bind-3) and the
  // PIM eligibility (bind-6).
  await expect(overview.locator('.grant .claim')).toHaveText(['team = growth', 'team = growth']);
  await expect(overview.locator('.grant:has(button[data-revoke="bind-3"]) .verb', { hasText: 'read' })).toContainText('reach-onboarding');

  // The eligibility renders as a dashed ELIGIBLE chip — never an active scope.
  const caps = overview.locator('.caps .cap');
  await expect(caps).toHaveCount(1);
  await expect(caps.first()).toHaveClass(/eligible/);
  await expect(caps.first()).toContainText('runs:write (eligible)');

  // Growth administers onboard-customer; every other section is an explicit empty, never a blank.
  await expect(overview.locator('.section', { hasText: 'Administers' }).first().locator('.row .grow')).toHaveText('onboard-customer');
  await expect(overview.locator('.body')).toContainText('Administers no environments.');
  await expect(overview.locator('.body')).toContainText('No usable source credentials.');
  assertClean(errors);
});

test('under the built-in seeds the capability view resolves conferred vs eligible vs nothing: list wording, the eligible chip, and admin resolution', async ({ page }) => {
  const errors = watchErrors(page);
  await openTab(page, 'Permissions');
  await expect(page.locator('arazzo-grants-panel tbody tr.grow-row')).toHaveCount(7); // demo seed settled

  // This pins the BUILT-IN fixture shapes (bind-2 confers scopes; bind-4 is an eligibleOnly PIM assignment;
  // the platform tenant administers workflows and an environment), so it drives the same panels against a
  // fresh built-in-seed mock — see useBuiltinSeeds.
  await useBuiltinSeeds(page, ['arazzo-grants-panel', 'arazzo-access-overview']);

  // The grants list distinguishes an active, time-boxed conferral from a PIM eligibility.
  const grants = page.locator('arazzo-grants-panel');
  await expect(grants.locator('tr[data-id="bind-2"] .gscopes')).toContainText('confers runs:read, runs:write until');
  await expect(grants.locator('tr[data-id="bind-4"] .gscopes')).toContainText('eligible for runs:purge until');

  // The overview resolves Ada's FULL membership-expanded identity: her own sub grant plus the two team=payments
  // bindings she inherits, and the eligibleOnly binding renders as a dashed ELIGIBLE chip — never an active scope.
  await page.getByRole('tab', { name: 'Access' }).click();
  const overview = page.locator('arazzo-access-overview');
  const input = overview.locator('arazzo-grantee-picker input.q');
  await input.click();
  await input.fill('Ada');
  await overview.locator('arazzo-grantee-picker .results li[data-index]', { hasText: 'Ada Lovelace' }).click();
  await expect(overview.locator('.grant')).toHaveCount(3);
  await expect(overview.locator('.grant .claim', { hasText: 'team = payments' })).toHaveCount(2);
  const caps = overview.locator('.caps .cap');
  await expect(caps).toHaveCount(1);
  await expect(caps.first()).toHaveClass(/eligible/);
  await expect(caps.first()).toContainText('runs:purge (eligible)');
  await expect(caps.first().locator('.until')).toContainText('until');

  // The platform tenant confers no reach or capability, yet its administration resolves — two workflows and the
  // production environment name it.
  await overview.locator('arazzo-grantee-picker .selected .clear').click();
  await input.click();
  await input.fill('platform');
  await overview.locator('arazzo-grantee-picker .results li[data-index]', { hasText: 'platform' }).click();
  await expect(overview.locator('.body')).toContainText('No reach grants match');
  await expect(overview.locator('.body')).toContainText('No capability scopes.');
  await expect(overview.locator('.section', { hasText: 'Administers' }).first().locator('.row .grow')).toHaveText(['nightly-reconcile', 'onboard-customer']);
  await expect(overview.locator('.section', { hasText: 'Administers environments' }).locator('.row .grow')).toHaveText('production');
  assertClean(errors);
});

// ---- Access requests ----------------------------------------------------------------------------

test('the submit flow requests scoped, time-boxed access: write forces read, and the new request lands Pending then withdraws', async ({ page }) => {
  const errors = watchErrors(page);
  await openTab(page, 'Access');

  const panel = page.locator('arazzo-access-requests');
  // No demo-seed request belongs to the administrator persona, so "My requests" opens empty with the call to action.
  await expect(panel.locator('tbody')).toContainText('You have no access requests');

  await panel.locator('button.new').click();
  const dlg = page.locator('arazzo-access-request-dialog');
  await expect(dlg.locator('dialog')).toBeVisible();

  // Pick a real catalogued workflow (no free-typed ids).
  const wfInput = dlg.locator('arazzo-workflow-picker input.q');
  await wfInput.click();
  await wfInput.fill('onboard');
  await dlg.locator('arazzo-workflow-picker .results li[data-index]', { hasText: 'Onboard Customer' }).click();
  await expect(dlg.locator('.chip .wf')).toHaveText('onboard-customer');

  // Scope coherence is enforced by construction: requesting runs:write forces-and-locks runs:read on.
  await dlg.locator('.scope-cb[value="runs:write"]').check();
  await expect(dlg.locator('.scope-cb[value="runs:read"]')).toBeChecked();
  await expect(dlg.locator('.scope-cb[value="runs:read"]')).toBeDisabled();

  await dlg.locator('.reason-in').fill('UX test: need to operate onboarding runs.');
  await dlg.locator('.dur-in').fill('4');
  await dlg.locator('button.ok').click();
  await expect(dlg.locator('dialog')).not.toBeVisible();

  // The request lands in "My requests" as Pending with the requested scope chips and a Withdraw action.
  const row = panel.locator('tbody tr[data-id]', { hasText: 'onboard-customer' });
  await expect(row.locator('.badge')).toHaveText('Pending');
  for (const scope of ['catalog:read', 'runs:read', 'runs:write']) {
    await expect(row.locator('.scope', { hasText: scope })).toBeVisible();
  }

  // Withdraw is confirm-gated and transitions the request in place.
  await row.locator('.act[data-action="withdraw"]').click();
  await panel.locator('dialog.arazzo-confirm button.ok').click();
  await expect(row.locator('.badge')).toHaveText('Withdrawn');
  await expect(row.locator('.act')).toHaveCount(0);
  assertClean(errors);
});

test('the approver inbox offers approve / make-eligible / deny on each pending request; approve and deny transition through to inbox zero and revoke', async ({ page }) => {
  const errors = watchErrors(page);
  await openTab(page, 'Access');

  // The demo example seed's inbox holds a single pending request; this walkthrough needs the built-in seeds'
  // TWO (req-2001 + req-2004) so approve and deny can each transition one on the way to inbox zero.
  await useBuiltinSeeds(page, ['arazzo-access-requests']);
  const panel = page.locator('arazzo-access-requests');
  await panel.locator('.tab-queue').click();

  // The inbox defaults to Pending — the actionable to-do across the workflows the admin administers (2 seeded).
  await expect(panel.locator('tbody tr[data-id]')).toHaveCount(2);
  await expect(panel.locator('arazzo-pager .count')).toContainText('2 requests');
  const first = panel.locator('tr[data-id="req-2001"]');
  for (const action of ['approve', 'approve-as-eligible', 'deny']) {
    await expect(first.locator(`.act[data-action="${action}"]`)).toBeVisible();
  }

  // Approve req-2001 with a recorded note.
  await first.locator('.act[data-action="approve"]').click();
  const decide = panel.locator('dialog.decision-dialog');
  await expect(decide.locator('.dhead')).toHaveText('Approve request');
  await decide.locator('.reason-in').fill('On-call cover approved.');
  await decide.locator('button.ok').click();
  await expect(panel.locator('tr[data-id="req-2001"]')).toHaveCount(0); // no longer pending

  // Deny req-2004 — the inbox reaches zero.
  await panel.locator('tr[data-id="req-2004"] .act[data-action="deny"]').click();
  await expect(panel.locator('dialog.decision-dialog .dhead')).toHaveText('Deny request');
  await panel.locator('dialog.decision-dialog button.ok').click();
  await expect(panel.locator('tbody')).toContainText('all caught up');

  // The transitions are queryable: Approved shows the grant with a Revoke action; revoking moves it to Revoked.
  await panel.locator('select.status').selectOption('Approved');
  const approved = panel.locator('tr[data-id="req-2001"]');
  await expect(approved.locator('.badge')).toHaveText('Approved');
  await approved.locator('.act[data-action="revoke"]').click();
  await panel.locator('dialog.arazzo-confirm button.ok').click();
  await expect(panel.locator('tr[data-id="req-2001"]')).toHaveCount(0); // no longer Approved
  await panel.locator('select.status').selectOption('Revoked');
  await expect(panel.locator('tr[data-id="req-2001"] .badge')).toHaveText('Revoked');
  assertClean(errors);
});

test('approve-as-eligible captures an eligibility window and lands the request in the Eligible state (PIM, not active)', async ({ page }) => {
  const errors = watchErrors(page);
  await openTab(page, 'Access');

  await useBuiltinSeeds(page, ['arazzo-access-requests']); // req-2004 is a built-in fixture
  const panel = page.locator('arazzo-access-requests');
  await panel.locator('.tab-queue').click();
  await panel.locator('tr[data-id="req-2004"] .act[data-action="approve-as-eligible"]').click();

  const decide = panel.locator('dialog.decision-dialog');
  await expect(decide.locator('.dhead')).toHaveText('Make eligible (self-elevation)');
  await expect(decide.locator('.win-in')).toBeVisible(); // the eligibility window is this decision's extra input
  await decide.locator('.win-in').fill('8');
  await decide.locator('button.ok').click();

  await expect(panel.locator('tr[data-id="req-2004"]')).toHaveCount(0); // no longer pending
  await panel.locator('select.status').selectOption('Eligible');
  await expect(panel.locator('tr[data-id="req-2004"] .badge')).toHaveText('Eligible');
  assertClean(errors);
});

// ---- Persona gating -----------------------------------------------------------------------------

test('personas re-gate Permissions: the security admin authors, the operator gets a read-only pane, the team reader loses the tab', async ({ page }) => {
  const errors = watchErrors(page);
  await openApp(page);

  // Administrator (security:write): both panels offer their New action.
  await page.getByRole('tab', { name: 'Permissions' }).click();
  await expect(page.locator('arazzo-grants-panel button.new')).toBeVisible();
  await expect(page.locator('arazzo-rules-panel button.new')).toBeVisible();

  // Operator holds security:read only: the tab remains, but authoring is gone — no New buttons, and an opened rule
  // is a read-only view (no Save/Delete; Cancel becomes Close; the expression is not editable).
  await page.locator('#persona').selectOption('operator');
  await expect(page.locator('#tab-permissions')).toBeVisible();
  await page.getByRole('tab', { name: 'Permissions' }).click();
  await expect(page.locator('arazzo-grants-panel button.new')).toBeHidden();
  await expect(page.locator('arazzo-rules-panel button.new')).toBeHidden();
  const rules = page.locator('arazzo-rules-panel');
  await rules.locator('tr[data-name="shares-a-label"]').click();
  await expect(rules.locator('.detail')).toBeVisible();
  await expect(rules.locator('.dfoot .confirm')).toHaveCount(0);
  await expect(rules.locator('.dfoot .del')).toHaveCount(0);
  await expect(rules.locator('.dfoot .cancel')).toHaveText('Close');
  await expect(rules.locator('textarea.f-expression')).toBeDisabled();

  // The payments team reader lacks security:read entirely: nav honesty removes the Permissions tab (absent, not
  // empty) and falls back to a surface the persona can read.
  await page.locator('#persona').selectOption('team-reader');
  await expect(page.locator('#tab-permissions')).toBeHidden();
  await expect(page.locator('#view-runs')).toBeVisible();

  // Back to the administrator: authoring returns.
  await page.locator('#persona').selectOption('administrator');
  await page.getByRole('tab', { name: 'Permissions' }).click();
  await expect(page.locator('arazzo-grants-panel button.new')).toBeVisible();
  assertClean(errors);
});
