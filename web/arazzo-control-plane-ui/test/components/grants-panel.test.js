// Tier 3 — <arazzo-grants-panel> mounted in a real browser against the in-memory mock.
import { ArazzoControlPlaneClient } from '../../src/arazzo-client.js';
import { createMockControlPlane } from '../../demo/mock-api.js';
import '../../src/components/grants-panel.js';
import { ok, equal, nextEvent, waitFor, mount } from './helpers.js';

function panelWithMock(attrs = {}, mockOptions = {}) {
  const mock = createMockControlPlane({ latencyMs: 0, ...mockOptions });
  const el = document.createElement('arazzo-grants-panel');
  for (const [k, v] of Object.entries(attrs)) el.setAttribute(k, v);
  el.client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
  return el;
}

const rows = (el) => el.shadowRoot.querySelectorAll('.grow-row');
const $ = (el, sel) => el.shadowRoot.querySelector(sel);
const setInput = (el, sel, value) => { const i = $(el, sel); i.value = value; i.dispatchEvent(new Event('input')); };
const verbSelect = (el, verb) => [...el.shadowRoot.querySelectorAll('.verb-mode')].find((s) => s.dataset.verb === verb);
const setVerbMode = (el, verb, mode) => { const s = verbSelect(el, verb); s.value = mode; s.dispatchEvent(new Event('change')); };
// The editor is a RHS detail pane (master-detail), not a modal — "open" means the pane holds an authoring form.
const editorOpen = (el) => !!$(el, '.detail-pane .content');

async function pickGrantee(el, query) {
  const picker = $(el, '.who-picker');
  const q = picker.shadowRoot.querySelector('.q');
  q.value = query;
  q.dispatchEvent(new Event('input'));
  await waitFor(() => picker.shadowRoot.querySelectorAll('.results li[data-index]').length >= 1);
  picker.shadowRoot.querySelector('.results li[data-index]').click();
}

describe('<arazzo-grants-panel>', () => {
  let el;
  afterEach(() => el?.remove());

  it('lists the seeded grants with per-action access and filters with search', async () => {
    el = panelWithMock({ scopes: 'security:read' });
    mount(el);
    await nextEvent(el, 'loaded');
    ok(rows(el).length >= 2, 'two seeded grants');
    ok(el.shadowRoot.textContent.includes('team=payments'), 'shows a claim');
    // Search is debounced + server-side, so await the reload before asserting on the filtered page.
    setInput(el, '.search', 'tenant');
    await nextEvent(el, 'loaded');
    ok([...rows(el)].every((r) => r.textContent.toLowerCase().includes('tenant')), 'search filters');
  });

  it('pages the grants with Prev/Next (keyset), not append', async () => {
    // Two grants are seeded (bind-1, bind-2); page-size=1 splits them across two keyset pages.
    el = panelWithMock({ scopes: 'security:read', 'page-size': '1' });
    mount(el);
    await nextEvent(el, 'loaded');
    // Page 1: one row, Prev disabled (no history), Next enabled (a second page exists).
    equal(rows(el).length, 1, 'one grant per page');
    ok($(el, '.pager'), 'a pager (not Load more) is shown');
    ok(!$(el, '.more'), 'no Load more button');
    ok($(el, '.prev').disabled, 'Prev disabled on page 1');
    ok(!$(el, '.next').disabled, 'Next enabled with a further page');
    const firstClaim = el.shadowRoot.querySelector('.claim').textContent;

    // Next → page 2: replaces (does not append) the list; Prev now enabled, Next disabled (last page).
    let loaded = nextEvent(el, 'loaded');
    $(el, '.next').click();
    await loaded;
    equal(rows(el).length, 1, 'page 2 replaces, not appends');
    const secondClaim = el.shadowRoot.querySelector('.claim').textContent;
    ok(secondClaim !== firstClaim, 'a different grant on page 2');
    ok(!$(el, '.prev').disabled, 'Prev enabled on page 2');
    ok($(el, '.next').disabled, 'Next disabled on the last page');

    // Prev → back to page 1.
    loaded = nextEvent(el, 'loaded');
    $(el, '.prev').click();
    await loaded;
    equal(rows(el).length, 1, 'back to one row');
    equal(el.shadowRoot.querySelector('.claim').textContent, firstClaim, 'Prev returns to page 1');
    ok($(el, '.prev').disabled, 'Prev disabled again on page 1');
  });

  it('opens the detail-pane editor and creates a grant from a raw claim + an unrestricted action', async () => {
    el = panelWithMock({ scopes: 'security:read security:write' });
    mount(el);
    await nextEvent(el, 'loaded');
    $(el, '.new').click();
    ok(editorOpen(el), 'the authoring pane opens on the RHS (not a modal)');
    setInput(el, '.f-claimType', 'region');
    setInput(el, '.f-claimValue', 'eu');
    setVerbMode(el, 'read', 'unrestricted');
    const changed = nextEvent(el, 'grants-changed');
    $(el, '.confirm').click();
    const e = await changed;
    ok(e.detail.grants.some((g) => g.claimType === 'region' && g.claimValue === 'eu' && g.read.unrestricted === true), 'grant created');
    ok(!editorOpen(el), 'the pane clears after create');
  });

  it('scopes an action by picking scopes with the typeahead (not a checkbox list)', async () => {
    el = panelWithMock({ scopes: 'security:read security:write' });
    mount(el);
    await nextEvent(el, 'loaded');
    $(el, '.new').click();
    setInput(el, '.f-claimType', 'team');
    setInput(el, '.f-claimValue', 'billing');
    setVerbMode(el, 'write', 'scopes');
    const scopeInput = [...el.shadowRoot.querySelectorAll('.scope-input')].find((i) => i.dataset.verb === 'write');
    ok(scopeInput, 'a scope typeahead input is shown (no flat checkbox list)');
    // The typeahead is server-backed: load the matching scopes (as typing would) so the chosen name is a known scope.
    await el.loadScopeOptions('reach-payments');
    scopeInput.value = 'reach-payments';
    scopeInput.dispatchEvent(new Event('change'));
    ok([...el.shadowRoot.querySelectorAll('.chip')].some((c) => c.textContent.includes('reach-payments')), 'scope added as a chip');
    const changed = nextEvent(el, 'grants-changed');
    $(el, '.confirm').click();
    const e = await changed;
    ok(e.detail.grants.find((g) => g.claimType === 'team' && g.claimValue === 'billing')?.write.ruleNames?.includes('reach-payments'), 'scoped write');
  });

  it('derives the canonical claim from a group/role grantee via the picker', async () => {
    el = panelWithMock({ scopes: 'security:read security:write' });
    mount(el);
    await nextEvent(el, 'loaded');
    $(el, '.new').click();
    await pickGrantee(el, 'payments');
    equal($(el, '.f-claimType').value, 'team', 'claim type derived');
    equal($(el, '.f-claimValue').value, 'payments', 'claim value derived');
  });

  it('steers a person grantee to the request flow instead of a direct grant', async () => {
    el = panelWithMock({ scopes: 'security:read security:write' });
    mount(el);
    await nextEvent(el, 'loaded');
    $(el, '.new').click();
    await pickGrantee(el, 'ada');
    ok($(el, '.steer'), 'request-flow steer banner');
    $(el, '.confirm').click();
    ok($(el, '.form-err .error-banner'), 'create blocked for a person');
  });

  it('selects a grant row into the pane (claim is the immutable key) and saves the new access', async () => {
    el = panelWithMock({ scopes: 'security:read security:write' });
    mount(el);
    await nextEvent(el, 'loaded');
    $(el, '.grow-row[data-id="bind-1"]').click();
    ok(editorOpen(el), 'selecting a row opens its record in the pane');
    equal($(el, '.f-claimType').value, 'team', 'prefilled claim');
    equal($(el, '.f-claimType').readOnly, true, 'claim is the key on edit');
    setVerbMode(el, 'read', 'unrestricted');
    const changed = nextEvent(el, 'grants-changed');
    $(el, '.confirm').click();
    const e = await changed;
    equal(e.detail.grants.find((g) => g.id === 'bind-1').read.unrestricted, true, 'read access updated');
  });

  it('deletes a grant from the detail pane and emits grants-changed', async () => {
    el = panelWithMock({ scopes: 'security:read security:write' });
    mount(el);
    await nextEvent(el, 'loaded');
    $(el, '.grow-row[data-id="bind-2"]').click();
    const changed = nextEvent(el, 'grants-changed');
    $(el, '.del').click();
    const okBtn = await waitFor(() => $(el, 'dialog.arazzo-confirm .ok'));
    okBtn.click();
    const e = await changed;
    ok(!e.detail.grants.some((g) => g.id === 'bind-2'), 'grant removed');
  });

  it('deletes a grant (via the client + reload) and emits grants-changed', async () => {
    el = panelWithMock({ scopes: 'security:read security:write' });
    mount(el);
    await nextEvent(el, 'loaded');
    const changed = nextEvent(el, 'grants-changed');
    await el.client.deleteSecurityBinding('bind-2');
    await el.reloadAndEmit();
    const e = await changed;
    ok(!e.detail.grants.some((g) => g.id === 'bind-2'), 'grant removed');
  });

  it('hides the mutating controls without security:write (read-only detail pane)', async () => {
    el = panelWithMock({ scopes: 'security:read' });
    mount(el);
    await nextEvent(el, 'loaded');
    ok($(el, '.new').hidden, 'New grant button hidden');
    // Selecting a row opens a read-only view of the grant: no Save/Delete, fields disabled (the 403 is the backstop).
    $(el, '.grow-row').click();
    ok(editorOpen(el), 'the pane shows the selected grant');
    ok(!$(el, '.confirm'), 'no Save without security:write');
    ok(!$(el, '.del'), 'no Delete without security:write');
    ok($(el, '.f-claimType').disabled, 'fields are read-only');
  });
});
