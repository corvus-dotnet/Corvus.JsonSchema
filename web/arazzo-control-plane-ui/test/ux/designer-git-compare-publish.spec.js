// UX suite — the designer's Git, comparison, and publish surfaces (workflow-designer §4.6/§4.7,
// snag 9): connect + bind from the Git side-tab, the headered history list with two-row selection,
// compare (with the working copy — the merge target — and between commits), pull/rollback as
// danger-confirmed replaces, the commit round-trip, and the deliberate publish act with its two
// 422 refusal routings (scenarios → Scenarios panel, validation → Problems panel).
//
// These are PAGE-level arcs on demo/designer.html against the in-browser mock. The mock's
// authorize URL self-completes when fetched: designer.html wires gitDialog.windowOpener to
// mockAuthOpener, so clicking "Connect GitHub…" completes the popup flow with no real window.
// Component-level history/menu mechanics live in test/components/git-dialog.test.js; the value
// here is the same wiring driven through the page (side-tab, autosave flush, host model merge).
import { test, expect } from '@playwright/test';
import { watchErrors, assertClean, openDesigner } from './ux-helpers.js';

// Open the Git side-tab (designer.html flushes any pending autosave first, then git-dialog
// fetches the STORED working copy and renders the connect stage).
async function openGitTab(page) {
  await page.locator('aside .side-tabs [data-tab="git"]').click();
  await expect(page.locator('#gitpanel .gh-connect .connect')).toBeVisible();
}

// Complete the brokered popup sign-in: the mock's authorize URL self-completes when fetched
// (mockAuthOpener), and the component's session poll (800ms) flips the row to the identity chip.
async function connectGitHub(page) {
  await page.locator('#gitpanel .gh-connect .connect').click();
  await expect(page.locator('#gitpanel .gh-connect .chip')).toContainText('octo', { timeout: 10_000 });
}

// Bind to acme-org/specs@main + flows/adopt.arazzo.json (the mock's seeded repo/branch/history).
async function bindToBranch(page, { scenariosDir } = {}) {
  await page.locator('#gitpanel .b-repo').selectOption('acme-org/specs');
  const branch = page.locator('#gitpanel .b-branch');
  await expect(branch).toBeEnabled();
  await expect(branch).toHaveValue('main'); // the repo's default branch preselects
  await page.locator('#gitpanel .b-path').fill('flows/adopt.arazzo.json');
  if (scenariosDir) await page.locator('#gitpanel .b-scenarios').fill(scenariosDir);
  const save = page.locator('#gitpanel .save-binding');
  await expect(save).toBeEnabled();
  await save.click();
  // Binding saved → the Load/Commit/History stages reveal; the seeded history is 3 commits.
  await expect(page.locator('#gitpanel .history-section')).toBeVisible();
  await expect(page.locator('#gitpanel .hist-commit')).toHaveCount(3);
}

// A real canvas edit through the Sources rail: keyboard activation of the first operation creates
// a bound step (the non-pointer path of drag-onto-canvas). Returns after the node lands.
async function addStepFromRail(page) {
  const nodes = page.locator('#surface .node');
  await nodes.first().waitFor({ state: 'attached' });
  const before = await nodes.count();
  await page.locator('aside .side-tabs [data-tab="sources"]').click();
  await page.locator('arazzo-operation-browser .group-head.toggle').first().click();
  const op = page.locator('arazzo-operation-browser button.op:not(.wfop)').first();
  await op.focus();
  await page.keyboard.press('Enter');
  await expect.poll(async () => nodes.count()).toBe(before + 1);
}

// Confirm the git panel's standard danger dialog (pull / rollback replaces are double-asked).
async function confirmGitDanger(page, titleFragment) {
  const ask = page.locator('#gitpanel .ask');
  await expect(ask.locator('dialog')).toBeVisible();
  await expect(ask.locator('.head')).toContainText(titleFragment);
  await ask.locator('.confirm.danger').click();
}

test('the Git tab connects a GitHub identity through the brokered popup and binds the working copy to a branch', async ({ page }) => {
  const errors = watchErrors(page);
  await openDesigner(page);
  await openGitTab(page);

  // Progressive disclosure: before a session, only the connect stage shows.
  await expect(page.locator('#gitpanel .connect-hint')).toBeVisible();
  await expect(page.locator('#gitpanel .binding-section')).toBeHidden();

  // Connect via the mock popup → the status chip names the resolved identity (never typed in).
  await connectGitHub(page);
  await expect(page.locator('#gitpanel .gh-connect .chip')).toContainText('Connected as');

  // The binding form seeds from the session: the installation's repository lists.
  await expect(page.locator('#gitpanel .binding-section')).toBeVisible();
  await expect(page.locator('#gitpanel .b-repo option', { hasText: 'acme-org/specs' })).toHaveCount(1);

  // Repo picked → the branch picker browses the repo's REAL branches (default preselected) and
  // offers the create sentinel; the paths stage reveals; save gates on the document path.
  await page.locator('#gitpanel .b-repo').selectOption('acme-org/specs');
  const branch = page.locator('#gitpanel .b-branch');
  await expect(branch).toBeEnabled();
  await expect(branch).toHaveValue('main');
  await expect(branch.locator('option', { hasText: '＋ New branch…' })).toHaveCount(1);
  await expect(page.locator('#gitpanel .paths-section')).toBeVisible();
  await expect(page.locator('#gitpanel .save-binding')).toBeDisabled();

  await page.locator('#gitpanel .b-path').fill('flows/adopt.arazzo.json');
  const save = page.locator('#gitpanel .save-binding');
  await expect(save).toBeEnabled();
  await save.click();

  // Bound: the host refreshes its save token and says so; Load, Commit, and History all reveal.
  await expect(page.locator('#save-status')).toHaveText('git binding saved');
  await expect(page.locator('#gitpanel .load-section')).toBeVisible();
  await expect(page.locator('#gitpanel .roundtrip-section')).toBeVisible();
  await expect(page.locator('#gitpanel .hist-commit')).toHaveCount(3);
  assertClean(errors);
});

test('the history renders as a headered list, newest first, and one selection arms Compare and Roll back', async ({ page }) => {
  const errors = watchErrors(page);
  await openDesigner(page);
  await openGitTab(page);
  await connectGitHub(page);
  await bindToBranch(page);

  const rows = page.locator('#gitpanel .hist-commit');
  await expect(rows.nth(0)).toContainText('Confirm adoptions after submission'); // newest first
  await expect(rows.nth(2)).toContainText('Initial adopt workflow');             // oldest last
  await expect(page.locator('#gitpanel .hist .count')).toContainText('3 commits');

  // No selection → both header actions disabled.
  const cmp = page.locator('#gitpanel .hist-cmp');
  const rollback = page.locator('#gitpanel .hist-rollback');
  await expect(cmp).toBeDisabled();
  await expect(rollback).toBeDisabled();

  // One selection (the newest) → both enable; the menu offers the working copy AND the
  // (enabled) immediate predecessor.
  await rows.nth(0).click();
  await expect(rows.nth(0)).toHaveAttribute('aria-selected', 'true');
  await expect(page.locator('#gitpanel .hist .count')).toContainText('1 selected');
  await expect(cmp).toBeEnabled();
  await expect(rollback).toBeEnabled();
  await cmp.click();
  const items = page.locator('#gitpanel .cmp-menu .menu-item');
  await expect(items).toHaveCount(2);
  await expect(items.filter({ hasText: 'With the working copy' })).toBeEnabled();
  await expect(items.filter({ hasText: /predecessor \(b7e2d8c\)/ })).toBeEnabled();
  await cmp.click(); // toggle the menu closed

  // The OLDEST commit has no predecessor (and no more pages): the item is disabled, not hidden.
  await rows.nth(0).click(); // deselect
  await rows.nth(2).click();
  await cmp.click();
  await expect(items).toHaveCount(2);
  const pred = items.filter({ hasText: 'predecessor' });
  await expect(pred).toBeDisabled();
  await expect(pred).toContainText('none (first commit)');
  assertClean(errors);
});

test('two history selections collapse Compare to the pair item, disable Roll back, and a third pick evicts the earliest', async ({ page }) => {
  const errors = watchErrors(page);
  await openDesigner(page);
  await openGitTab(page);
  await connectGitHub(page);
  await bindToBranch(page);

  const rows = page.locator('#gitpanel .hist-commit');
  const cmp = page.locator('#gitpanel .hist-cmp');
  await rows.nth(0).click();
  await rows.nth(2).click();
  await expect(page.locator('#gitpanel .hist-commit[aria-selected="true"]')).toHaveCount(2);
  await expect(page.locator('#gitpanel .hist .count')).toContainText('2 selected');
  await expect(page.locator('#gitpanel .hist-rollback')).toBeDisabled(); // rollback needs exactly one

  // The menu collapses to the single pair compare, phrased older → newer.
  await cmp.click();
  const items = page.locator('#gitpanel .cmp-menu .menu-item');
  await expect(items).toHaveCount(1);
  await expect(items.first()).toContainText('older → newer');
  await expect(items.first()).toContainText('c1d9e5b');
  await expect(items.first()).toContainText('a3f1c9d');

  // The pair compare opens read-only: two surfaces, a painted diff, and NO merge verbs (neither
  // side is the working copy, so nothing is a merge target).
  await items.first().click();
  const compare = page.locator('#gitpanel arazzo-workflow-compare');
  await expect(compare.locator('dialog')).toBeVisible();
  await expect(compare.locator('arazzo-design-surface')).toHaveCount(2);
  await expect(compare.locator('arazzo-design-surface[readonly]')).toHaveCount(2);
  await expect(compare.locator('.legend')).toContainText('added'); // the newer commit adds steps
  await expect(compare.locator('.cl-take')).toHaveCount(0);
  await compare.locator('.close').click();

  // A third pick evicts the EARLIEST pick — never more than two, never a dead click.
  await rows.nth(1).click();
  await expect(page.locator('#gitpanel .hist-commit[aria-selected="true"]')).toHaveCount(2);
  await expect(rows.nth(0)).toHaveAttribute('aria-selected', 'false');
  await expect(page.locator('#gitpanel .hist .count')).toContainText('2 selected');
  assertClean(errors);
});

test('comparing a commit with the working copy is a merge: Take pulls the commit\'s step into the live model', async ({ page }) => {
  const errors = watchErrors(page);
  await openDesigner(page);
  await openGitTab(page);
  await connectGitHub(page);
  await bindToBranch(page);

  // Roll back to the oldest commit first, so the working copy (2 steps) trails the newest commit
  // (4 steps) and the diff offers ADDED steps to take.
  const rows = page.locator('#gitpanel .hist-commit');
  await rows.nth(2).click();
  await page.locator('#gitpanel .hist-rollback').click();
  await confirmGitDanger(page, 'Roll back to c1d9e5b');
  await expect(page.locator('#workflow')).toHaveValue('adopt-pet-v1');
  await page.locator('#surface .node[data-id="findPet"]').waitFor({ state: 'attached' });
  await expect(page.locator('#surface .node[data-id="submitAdoption"]')).toHaveCount(0);

  // Compare the NEWEST commit with the working copy: the working copy is the merge target (left),
  // read live from the host model, so the change list carries Take/Keep verbs.
  await rows.nth(2).click(); // deselect the rollback pick
  await rows.nth(0).click();
  await page.locator('#gitpanel .hist-cmp').click();
  await page.locator('#gitpanel .cmp-menu .menu-item', { hasText: 'With the working copy' }).click();
  const compare = page.locator('#gitpanel arazzo-workflow-compare');
  await expect(compare.locator('dialog')).toBeVisible();
  await expect(compare.locator('.side-left .side-head')).toContainText('Working copy (current)');

  // Take the added submitAdoption step: change-accepted bubbles to the designer host, which
  // applies it to the ONE live model as an ordinary labelled edit and refreshes the diff.
  const entry = compare.locator('.cl-item', { hasText: 'submitAdoption', has: page.locator('.cl-take') });
  await expect(entry).toHaveCount(1);
  await entry.locator('.cl-take').click();
  // The resolved entry disappears from the recomputed change list…
  await expect(compare.locator('.cl-item', { hasText: 'submitAdoption', has: page.locator('.cl-take') })).toHaveCount(0);
  await compare.locator('.close').click();

  // …and the edit landed: the step is on the canvas (the live model mutated, not a copy).
  await page.locator('#surface .node[data-id="submitAdoption"]').waitFor({ state: 'attached' });
  assertClean(errors);
});

test('commit writes the document and scenario files to the branch as the signed-in user, with a draft PR', async ({ page }) => {
  const errors = watchErrors(page);
  await openDesigner(page);
  await addStepFromRail(page); // a real canvas edit, autosave-flushed when the Git tab opens
  await openGitTab(page);
  await connectGitHub(page);
  await bindToBranch(page, { scenariosDir: 'scenarios/order' });

  // The commit gates on a message; the draft-PR checkbox targets the default base branch.
  const commit = page.locator('#gitpanel .commit');
  await expect(commit).toBeDisabled();
  await page.locator('#gitpanel .c-message').fill('sync the order flow from the designer');
  await expect(commit).toBeEnabled();
  await page.locator('#gitpanel .c-pr').check();
  await expect(page.locator('#gitpanel .c-base')).toHaveValue('main');
  await commit.click();

  // The result names the signed-in identity (§4.7 — authored as YOUR GitHub identity), lists the
  // written files (document + the scenario set under the bound directory), and links the PR.
  const result = page.locator('#gitpanel .result');
  await expect(result).toBeVisible();
  await expect(result).toContainText('Committed as octo');
  await expect(result).toContainText('flows/adopt.arazzo.json');
  await expect(result).toContainText('scenarios/order/happy-path.scenario.json');
  await expect(result).toContainText('scenarios/order/declined-goes-to-review.scenario.json');
  await expect(result.locator('a', { hasText: '#42' })).toBeVisible();
  await expect(page.locator('#save-status')).toHaveText('committed to GitHub');

  // The commit lands in the branch history, newest first — the list reloads with the new commit at
  // its head, as the real broker's history would show it.
  await expect(page.locator('#gitpanel .hist-commit')).toHaveCount(4);
  await expect(page.locator('#gitpanel .hist-commit').first().locator('.msg')).toHaveText('sync the order flow from the designer');
  await expect(page.locator('#gitpanel .hist .count')).toContainText('4 commits');
  assertClean(errors);
});

test('pull (Load from branch) is danger-confirmed and replaces the working copy with the branch contents', async ({ page }) => {
  const errors = watchErrors(page);
  await openDesigner(page);
  await addStepFromRail(page); // a local edit that the pull will discard (etag bumped by the flush)

  // Pull with a STEP NODE still selected — the replacement document lacks the current workflow, so
  // the designer must clear the stale selection back to the document settings page instead of
  // re-rendering an inspector against a workflow that no longer exists (this used to throw three
  // console TypeErrors, which assertClean would catch).
  await page.locator('#surface .node').first().click();

  await openGitTab(page);
  await connectGitHub(page);
  await bindToBranch(page);

  // Load is a REPLACE, not a merge — the standard danger dialog says so before doing it.
  await page.locator('#gitpanel .pull').click();
  await confirmGitDanger(page, 'Load replaces this working copy');

  // The bound branch's document (the adopt flow) replaced the Order-processing document: the
  // designer reloaded its model like an open, and the panel reports the source.
  await expect(page.locator('#gitpanel .load-result')).toContainText('acme-org/specs@main');
  await expect(page.locator('#workflow')).toHaveValue('adopt-pet-v1');
  await page.locator('#surface .node[data-id="findPet"]').waitFor({ state: 'attached' });
  await expect(page.locator('#save-status')).toHaveText('pulled from GitHub');
  // The stale step selection was cleared to the document settings page, not re-rendered.
  await expect(page.locator('#selection')).toHaveText('(document)');
  assertClean(errors);
});

test('roll back replaces the working copy at the commit and never rebinds', async ({ page }) => {
  const errors = watchErrors(page);
  await openDesigner(page);
  await openGitTab(page);
  await connectGitHub(page);
  await bindToBranch(page);

  // Select exactly the oldest commit and roll back through the header action's danger confirm.
  await page.locator('#gitpanel .hist-commit').nth(2).click();
  const rollback = page.locator('#gitpanel .hist-rollback');
  await expect(rollback).toBeEnabled();
  await rollback.click();
  await confirmGitDanger(page, 'Roll back to c1d9e5b');

  // The oldest commit's state (the 2-step first cut) replaced the document — the designer
  // reloaded onto it; the dropped steps are gone from the canvas.
  await expect(page.locator('#workflow')).toHaveValue('adopt-pet-v1');
  await page.locator('#surface .node[data-id="findPet"]').waitFor({ state: 'attached' });
  await page.locator('#surface .node[data-id="reservePayment"]').waitFor({ state: 'attached' });
  await expect(page.locator('#surface .node[data-id="submitAdoption"]')).toHaveCount(0);
  await expect(page.locator('#surface .node[data-id="confirmAdoption"]')).toHaveCount(0);

  // A rollback never rebinds: the branch label is intact and the result narrates that the NEXT
  // commit records the rollback on the branch.
  await expect(page.locator('#gitpanel .b-branch')).toHaveValue('main');
  await expect(page.locator('#gitpanel .result')).toContainText('Rolled back to c1d9e5b');
  await expect(page.locator('#gitpanel .result')).toContainText('main');
  await expect(page.locator('#save-status')).toHaveText('pulled from GitHub');
  assertClean(errors);
});

test('publish validates + re-runs the suite server-side and mints a draft catalog version', async ({ page }) => {
  const errors = watchErrors(page);
  await openDesigner(page);

  // The deliberate publish act opens the owner dialog (the server requires an owner email; two
  // prefilled fields make the requirement obvious).
  await page.locator('#publish').click();
  const ask = page.locator('#ask');
  await expect(ask.locator('dialog')).toBeVisible();
  await expect(ask.locator('.head')).toContainText('Publish to the catalog');
  await expect(ask.locator('.in-field[data-key="name"]')).toHaveValue('You');
  await expect(ask.locator('.in-field[data-key="email"]')).toHaveValue('you@example.com');
  await ask.locator('.confirm').click();

  // Validated + suite re-run server-side; the new version starts as a draft, the toast narrates
  // the server-attested evidence counts (the publish response carries evidence.suite) and points
  // at the catalog promotion flow.
  const toast = page.locator('#toast');
  await expect(toast).toBeVisible();
  await expect(toast).toContainText('Published place-order v1 (draft) — 2/2 scenarios green');
  await expect(toast).toContainText('Promote it from the catalog when ready.');
  await expect(page.locator('#save-status')).toHaveText('published place-order v1 (draft)');
  assertClean(errors);
});

test('publish refuses a failing scenario suite with 422 and routes to the Scenarios panel', async ({ page }) => {
  const errors = watchErrors(page);
  await openDesigner(page);

  // Make the seeded happy-path scenario fail deterministically: edit its expected outcome to
  // "faulted" through the typed scenario editor (the run actually completes, so it must fail).
  await page.locator('aside .side-tabs [data-tab="scenarios"]').click();
  await page.locator('#scpanel [data-edit="happy-path"]').click();
  const editor = page.locator('#scpanel arazzo-scenario-editor');
  await expect(editor.locator('.x-outcome')).toBeVisible(); // the typed form (schemas loaded)
  await editor.locator('.x-outcome').selectOption('faulted');
  await editor.locator('.save').click();
  await expect(editor).toHaveCount(0); // saved → the panel refreshed out of edit mode

  // Leave the Scenarios tab so the refusal ROUTING is observable, then publish.
  await page.locator('aside .side-tabs [data-tab="inspect"]').click();
  await expect(page.locator('#panel-scenarios')).toBeHidden();
  await page.locator('#publish').click();
  const ask = page.locator('#ask');
  await expect(ask.locator('dialog')).toBeVisible();
  await ask.locator('.confirm').click();

  // 422 reason=scenarios: the refusal lands where it is actionable — the Scenarios panel — and
  // the toast names the failing count (evidence is server-attested, so the gate is the server's).
  const toast = page.locator('#toast');
  await expect(toast).toContainText('Publish refused');
  await expect(toast).toContainText('1 scenario(s) failing');
  await expect(page.locator('#panel-scenarios')).toBeVisible();
  await expect(page.locator('#save-status')).toContainText('publish refused');
  assertClean(errors);
});

test('publish refuses an invalid document with 422 and routes to the Problems panel', async ({ page }) => {
  const errors = watchErrors(page);
  await openDesigner(page);
  await page.locator('#surface .node').first().waitFor({ state: 'attached' });

  // Author a nonsense inputs schema ("type" must be a string) through the schema editor's JSON
  // tier — the same §15-8 path smoke exercises; the guarded textarea commits the moment it parses.
  await page.evaluate(() => {
    document.querySelector('#surface').dispatchEvent(new CustomEvent('selection-changed', {
      detail: { selection: { type: 'node', id: '#start' } }, bubbles: true, composed: true,
    }));
  });
  await expect.poll(async () => page.evaluate(() =>
    !!document.querySelector('arazzo-workflow-inspector')?.shadowRoot?.querySelector('arazzo-schema-editor'),
  )).toBe(true);
  await page.evaluate(() => {
    const se = document.querySelector('arazzo-workflow-inspector').shadowRoot.querySelector('arazzo-schema-editor');
    se.shadowRoot.querySelector('.tier').shadowRoot.querySelector('button[data-value="json"]').click();
    const ta = se.shadowRoot.querySelector('arazzo-text-editor.json-ed');
    const v = '{"type": 123}';
    ta.value = v;
    ta.dispatchEvent(new CustomEvent('text-changed', { detail: { text: v }, bubbles: true, composed: true }));
  });
  // Wait for the autosave + validate cycle so publish acts on the STORED (invalid) document and
  // no autosave races the publish's own flush.
  const badge = page.locator('#problems-badge');
  await expect.poll(async () => Number((await badge.textContent().catch(() => '0')) || '0'), { timeout: 8000 }).toBeGreaterThan(0);

  await page.locator('#publish').click();
  const ask = page.locator('#ask');
  await expect(ask.locator('dialog')).toBeVisible();
  await ask.locator('.confirm').click();

  // 422 reason=validation: the diagnostics render into Problems (revealed — a refusal is a
  // deliberate act, so stealing the tab is right here), positioned at the offending keyword.
  const toast = page.locator('#toast');
  await expect(toast).toContainText('Publish refused');
  await expect(toast).toContainText('validation findings');
  await expect(page.locator('#panel-problems')).toBeVisible();
  await expect(page.locator('#problems')).toContainText('/workflows/0/inputs/type');
  await expect(page.locator('#save-status')).toContainText('publish refused');
  assertClean(errors);
});

// NOT covered here, deliberately: the catalog "Compare with version…" host lives on
// demo/index.html's <arazzo-catalog-detail> (smoke.spec.js covers its side-by-side/overlay/text
// modes); designer.html hosts no distinct catalog-compare surface, so there is no designer-side
// arc to test beyond the git-history compares above.
