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
// An authoring form is "open" when a container holds it: the modal for create, the detail pane for edit.
const editorOpen = (el) => !!$(el, '.cmodal[open] .content') || !!$(el, '.detail-pane .content');

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
    // Four grants are seeded (bind-1..bind-4); page-size=1 splits them across four keyset pages.
    el = panelWithMock({ scopes: 'security:read', 'page-size': '1' });
    mount(el);
    await nextEvent(el, 'loaded');
    // Page 1: one row, Prev disabled (no history), Next enabled (further pages exist).
    equal(rows(el).length, 1, 'one grant per page');
    ok($(el, '.pager'), 'a pager (not Load more) is shown');
    ok(!$(el, '.more'), 'no Load more button');
    ok($(el, '.prev').disabled, 'Prev disabled on page 1');
    ok(!$(el, '.next').disabled, 'Next enabled with a further page');
    const firstClaim = el.shadowRoot.querySelector('.claim').textContent;

    // Next → page 2: replaces (does not append) the list; Prev now enabled, Next still enabled (a third page follows).
    let loaded = nextEvent(el, 'loaded');
    $(el, '.next').click();
    await loaded;
    equal(rows(el).length, 1, 'page 2 replaces, not appends');
    const secondClaim = el.shadowRoot.querySelector('.claim').textContent;
    ok(secondClaim !== firstClaim, 'a different grant on page 2');
    ok(!$(el, '.prev').disabled, 'Prev enabled on page 2');
    ok(!$(el, '.next').disabled, 'Next still enabled — a third page follows');

    // Next → page 3, then page 4 (the last page): Next now disabled.
    loaded = nextEvent(el, 'loaded');
    $(el, '.next').click();
    await loaded;
    equal(rows(el).length, 1, 'page 3 holds one grant');
    ok(!$(el, '.prev').disabled, 'Prev enabled on page 3');
    ok(!$(el, '.next').disabled, 'Next still enabled — a fourth page follows');

    loaded = nextEvent(el, 'loaded');
    $(el, '.next').click();
    await loaded;
    equal(rows(el).length, 1, 'page 4 holds the last grant');
    ok(!$(el, '.prev').disabled, 'Prev enabled on page 4');
    ok($(el, '.next').disabled, 'Next disabled on the last page');

    // Prev → back to page 3 before returning to page 2.
    loaded = nextEvent(el, 'loaded');
    $(el, '.prev').click();
    await loaded;

    // Prev → back to page 2.
    loaded = nextEvent(el, 'loaded');
    $(el, '.prev').click();
    await loaded;
    equal(rows(el).length, 1, 'back to one row');
    equal(el.shadowRoot.querySelector('.claim').textContent, secondClaim, 'Prev returns to page 2');
    ok(!$(el, '.prev').disabled, 'Prev still enabled on page 2');
  });

  it('opens the detail-pane editor and creates a grant from a raw claim + an unrestricted action', async () => {
    el = panelWithMock({ scopes: 'security:read security:write' });
    mount(el);
    await nextEvent(el, 'loaded');
    $(el, '.new').click();
    ok(editorOpen(el), 'the create form opens in a modal');
    ok($(el, '.cmodal[open]'), 'create authors in a modal, matching the other create flows');
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

  it('the rule dropdown closes on selection and stays closed; Escape and a footer-aimed pointerdown dismiss it', async () => {
    el = panelWithMock({ scopes: 'security:read security:write' });
    mount(el);
    await nextEvent(el, 'loaded');
    $(el, '.new').click();
    setVerbMode(el, 'read', 'scopes');
    const input = () => [...el.shadowRoot.querySelectorAll('.scope-input')].find((i) => i.dataset.verb === 'read');
    const list = () => [...el.shadowRoot.querySelectorAll('.results')].find((l) => l.dataset.verb === 'read');

    // Focus pops the initial suggestions (a server-backed empty search).
    input().dispatchEvent(new Event('focus'));
    await waitFor(() => !list().hidden && list().querySelectorAll('li[data-name]').length >= 1);

    // Selecting a rule adds the chip and CLOSES the dropdown — the programmatic refocus must not
    // re-pop it (the dropdown overlays the pane's footer; a lingering listbox swallows Create).
    list().querySelector('li[data-name="reach-payments"]').click();
    ok([...el.shadowRoot.querySelectorAll('.chip')].some((c) => c.textContent.includes('reach-payments')), 'rule added as a chip');
    ok(list().hidden, 'the dropdown is closed after the selection');
    await el.loadScopeOptions(''); // a stale in-flight search resolving late has no active verb to render into
    ok(list().hidden, 'a late-resolving search cannot re-open the dismissed dropdown');

    // Escape dismisses a re-opened dropdown.
    input().dispatchEvent(new Event('focus'));
    await waitFor(() => !list().hidden);
    input().dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    ok(list().hidden, 'Escape closes the dropdown');

    // A pointerdown aimed at the footer (inside the panel, outside the input/list) dismisses too.
    input().dispatchEvent(new Event('focus'));
    await waitFor(() => !list().hidden);
    $(el, '.confirm').dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, composed: true }));
    ok(list().hidden, 'a pointerdown outside the input/list closes the dropdown');
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

  it('pins a multi-dimension grantee identity as a tag-set selector (primary + additional clauses)', async () => {
    el = panelWithMock({ scopes: 'security:read security:write' }, {
      granteesSeed: [{ kind: 'team', value: 'payments-eu', label: 'Payments EU (keycloak)', source: 'directory', complete: true, identity: [{ dimension: 'team', value: 'payments-eu' }, { dimension: 'sys:iss', value: 'https://keycloak' }] }],
    });
    mount(el);
    await nextEvent(el, 'loaded');
    $(el, '.new').click();
    await pickGrantee(el, 'payments-eu');
    equal($(el, '.f-claimType').value, 'team', 'primary claim is the first identity dimension');
    equal($(el, '.f-claimValue').value, 'payments-eu', 'primary value');
    ok([...el.shadowRoot.querySelectorAll('.f-clause-dim')].some((i) => i.value === 'sys:iss')
      && [...el.shadowRoot.querySelectorAll('.f-clause-val')].some((i) => i.value === 'https://keycloak'),
      'the remaining identity dimension is shown as an editable additional-clause row');
    setVerbMode(el, 'read', 'unrestricted');
    const changed = nextEvent(el, 'grants-changed');
    $(el, '.confirm').click();
    const e = await changed;
    const g = e.detail.grants.find((x) => x.claimType === 'team' && x.claimValue === 'payments-eu');
    ok(g, 'grant created');
    ok((g.additionalClauses || []).some((c) => c.dimension === 'sys:iss' && c.value === 'https://keycloak'), 'the issuer dimension is pinned as an additional clause (not dropped)');
  });

  it('authors an additional identity clause by hand (add-clause), not only via the picker', async () => {
    el = panelWithMock({ scopes: 'security:read security:write' });
    mount(el);
    await nextEvent(el, 'loaded');
    $(el, '.new').click();
    setInput(el, '.f-claimType', 'team');
    setInput(el, '.f-claimValue', 'payments');
    // No grantee picked: the operator pins an issuer clause directly, so a single-claim grant is not the only option.
    $(el, '.add-clause').click();
    setInput(el, '.f-clause-dim', 'iss');
    setInput(el, '.f-clause-val', 'https://keycloak');
    setVerbMode(el, 'read', 'unrestricted');
    const changed = nextEvent(el, 'grants-changed');
    $(el, '.confirm').click();
    const e = await changed;
    const g = e.detail.grants.find((x) => x.claimType === 'team' && x.claimValue === 'payments'
      && (x.additionalClauses || []).some((c) => c.dimension === 'iss' && c.value === 'https://keycloak'));
    ok(g, 'a hand-authored multi-clause grant is created');
  });

  it('creates a plain single-claim grant with no order supplied (server defaults it)', async () => {
    el = panelWithMock({ scopes: 'security:read security:write' });
    mount(el);
    await nextEvent(el, 'loaded');
    $(el, '.new').click();
    setInput(el, '.f-claimType', 'role');
    setInput(el, '.f-claimValue', 'tenant-admin');
    setVerbMode(el, 'read', 'unrestricted');
    const changed = nextEvent(el, 'grants-changed');
    $(el, '.confirm').click();
    const e = await changed;
    const g = e.detail.grants.find((x) => x.claimType === 'role' && x.claimValue === 'tenant-admin');
    ok(g, 'a single-claim grant is created without the client ever sending an order');
    ok(!editorOpen(el), 'the pane clears after create (no error banner)');
  });

  it('does not offer a person as a grantee (per-person access goes through requests)', async () => {
    el = panelWithMock({ scopes: 'security:read security:write' });
    mount(el);
    await nextEvent(el, 'loaded');
    $(el, '.new').click();
    // The grants picker is constrained to team/role/workflow, so searching a person (Ada) yields no
    // result — a person is requested-and-approved, never granted directly.
    const picker = $(el, '.who-picker');
    const q = picker.shadowRoot.querySelector('.q');
    q.value = 'ada';
    q.dispatchEvent(new Event('input'));
    // Give the debounced search room to run, then assert no person appears.
    await new Promise((r) => setTimeout(r, 400));
    const results = [...picker.shadowRoot.querySelectorAll('.results li[data-index]')];
    ok(!results.some((li) => /ada lovelace/i.test(li.textContent)), 'no person in the results');
    ok(!results.some((li) => li.querySelector('.badge')?.textContent === 'person'), 'no person-kind results');
  });

  it('selects a grant row into the pane (claim is the immutable key) and saves the new access', async () => {
    el = panelWithMock({ scopes: 'security:read security:write' });
    mount(el);
    await nextEvent(el, 'loaded');
    $(el, '.grow-row[data-id="bind-1"]').click();
    ok(editorOpen(el), 'selecting a row opens its record in the pane');
    equal($(el, '.f-claimType').value, 'team', 'prefilled claim');
    equal($(el, '.f-claimType').readOnly, true, 'claim type is the key on edit');
    equal($(el, '.f-claimValue').readOnly, true, 'claim value is fixed too — the whole WHO is locked on edit');
    setVerbMode(el, 'read', 'unrestricted');
    const changed = nextEvent(el, 'grants-changed');
    $(el, '.confirm').click();
    const e = await changed;
    equal(e.detail.grants.find((g) => g.id === 'bind-1').read.unrestricted, true, 'read access updated');
  });

  it('deletes a grant from the detail pane and emits grants-changed — a system-critical grant demands the typed challenge', async () => {
    el = panelWithMock({ scopes: 'security:read security:write' });
    mount(el);
    await nextEvent(el, 'loaded');
    // bind-2 carries Unrestricted reach, so its delete is guarded: the confirm arrives with the
    // button DISABLED until the claim is typed back (severity-proportional friction).
    $(el, '.grow-row[data-id="bind-2"]').click();
    const changed = nextEvent(el, 'grants-changed');
    $(el, '.del').click();
    const okBtn = await waitFor(() => $(el, 'dialog.arazzo-confirm .ok'));
    ok(okBtn.disabled, 'a system-critical delete starts disabled');
    okBtn.click(); // a click without the challenge must do nothing
    ok($(el, 'dialog.arazzo-confirm'), 'the dialog survived the premature click');
    const input = $(el, 'dialog.arazzo-confirm .chal-in');
    input.value = 'tenant=acme';
    input.dispatchEvent(new Event('input'));
    ok(!okBtn.disabled, 'typing the claim arms the button');
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

describe('<arazzo-grants-panel> conjunction semantics', () => {
  let el;
  afterEach(() => el?.remove());

  // A grant's rules are a CONJUNCTION (§14.2): the editor separates the chips with '+', states the
  // intersection semantics once two rules are chosen, and calls out a provably-empty pair (two
  // simple equality rules pinning the same dimension to different values). Advisory only — the
  // server remains the authority.
  it("renders a multi-rule grant as 'all must match' and warns on a provably-empty pair", async () => {
    el = panelWithMock({ scopes: 'security:read security:write' });
    mount(el);
    await nextEvent(el, 'loaded');
    // A second domain rule so a same-dimension pair exists (reach-payments is domain == 'payments').
    await el.client.createSecurityRule({ name: 'reach-ops', expression: "domain == 'ops'", description: 'Ops rows.' });
    $(el, '.new').click();
    setVerbMode(el, 'read', 'scopes');
    await el.loadScopeOptions(''); // populates the name → expression map the advisory reasons over

    el.addRule('read', 'reach-payments');
    ok(!detailText(el).includes('All rules must match'), 'one rule needs no conjunction hint');

    // Different dimensions: the hint states the intersection; no impossibility warning.
    el.addRule('read', 'tenant-scoped');
    ok(detailText(el).includes('All rules must match'), 'two rules surface the conjunction hint');
    ok(!el.shadowRoot.querySelector('.conj-warn'), 'no warning for a satisfiable pair');
    ok([...el.shadowRoot.querySelectorAll('.conj')].length >= 1, "chips are separated by '+'");

    // Same dimension, different values: provably empty — the advisory names the pair.
    el.addRule('read', 'reach-ops');
    const warn = el.shadowRoot.querySelector('.conj-warn');
    ok(warn, 'the impossible pair raises the advisory');
    ok(warn.textContent.includes('reach-payments') && warn.textContent.includes('reach-ops'), 'both rule names are called out');
    ok(warn.textContent.includes('domain'), 'the pinned dimension is named');
  });

  function detailText(panel) {
    // The active authoring form lives in the modal (create) or the detail pane (edit).
    const modal = panel.shadowRoot.querySelector('.cmodal[open]');
    return (modal ?? panel.shadowRoot.querySelector('.detail-pane'))?.textContent ?? '';
  }
});
