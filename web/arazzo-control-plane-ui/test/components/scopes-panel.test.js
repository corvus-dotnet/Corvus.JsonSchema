// Tier 3 — <arazzo-scopes-panel> mounted in a real browser against the in-memory mock.
import { ArazzoControlPlaneClient } from '../../src/arazzo-client.js';
import { createMockControlPlane } from '../../demo/mock-api.js';
import '../../src/components/scopes-panel.js';
import { ok, equal, nextEvent, mount } from './helpers.js';

function panelWithMock(attrs = {}, mockOptions = {}) {
  const mock = createMockControlPlane({ latencyMs: 0, ...mockOptions });
  const el = document.createElement('arazzo-scopes-panel');
  for (const [k, v] of Object.entries(attrs)) el.setAttribute(k, v);
  el.client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
  return el;
}

const rows = (el) => el.shadowRoot.querySelectorAll('.srow');
const $ = (el, sel) => el.shadowRoot.querySelector(sel);
const setField = (el, sel, value) => { const i = $(el, sel); i.value = value; i.dispatchEvent(new Event('input')); };
const preview = (el) => $(el, '.preview-expr').textContent;
const dialogOpen = (el) => $(el, 'dialog').open;

describe('<arazzo-scopes-panel>', () => {
  let el;
  afterEach(() => el?.remove());

  it('lists the seeded scopes and filters them with the search box', async () => {
    el = panelWithMock({ scopes: 'security:read' });
    mount(el);
    await nextEvent(el, 'loaded');
    ok(rows(el).length >= 4, 'several seeded scopes');
    // Search is debounced + server-side, so await the reload before asserting on the filtered page.
    setField(el, '.search', 'tenant');
    await nextEvent(el, 'loaded');
    ok([...rows(el)].every((r) => r.textContent.toLowerCase().includes('tenant')), 'search filters the list');
    setField(el, '.search', 'zzz-no-match');
    await nextEvent(el, 'loaded');
    ok($(el, '.empty'), 'no-match empty state');
  });

  it('opens a modal editor, builds a label-equals expression, auto-suggests the name, and creates the scope', async () => {
    el = panelWithMock({ scopes: 'security:read security:write' });
    mount(el);
    await nextEvent(el, 'loaded');
    $(el, '.new').click();
    ok(dialogOpen(el), 'the editor opens as a modal dialog');
    setField(el, '.f-value', 'marketing');
    equal(preview(el), "domain == 'marketing'", 'template wrote the expression');
    equal($(el, '.f-name').value, 'scope-marketing', 'name auto-suggested');

    const changed = nextEvent(el, 'scopes-changed');
    $(el, '.confirm').click();
    const e = await changed;
    ok(e.detail.scopes.some((s) => s.name === 'scope-marketing' && s.expression === "domain == 'marketing'"), 'scope created');
    ok(!dialogOpen(el), 'the dialog closes after create');
  });

  it('builds a set-membership expression and escapes a single quote', async () => {
    el = panelWithMock({ scopes: 'security:read security:write' });
    mount(el);
    await nextEvent(el, 'loaded');
    $(el, '.new').click();
    $(el, 'input[name="tmpl"][value="in"]').click();
    setField(el, '.f-dim', 'tenant');
    setField(el, '.f-valuesText', "acme, o'brien");
    equal(preview(el), "tenant in ('acme', 'o''brien')", 'set-membership with quote escaped');
  });

  it('builds a classification ordered expression from the configured orderings', async () => {
    el = panelWithMock({ scopes: 'security:read security:write' });
    mount(el);
    await nextEvent(el, 'loaded');
    $(el, '.new').click();
    $(el, 'input[name="tmpl"][value="ordered"]').click();
    equal(preview(el), "classification <= 'public'", 'seeded ordered expression');
    setField(el, '.f-value', 'confidential');
    setField(el, '.f-comparator', '>');
    equal(preview(el), "classification > 'confidential'", 'dropdowns drive the comparison');
  });

  it('hides the classification template when no orderings are configured', async () => {
    el = panelWithMock({ scopes: 'security:read security:write' }, { securityOrderingsSeed: [] });
    mount(el);
    await nextEvent(el, 'loaded');
    $(el, '.new').click();
    ok(!$(el, 'input[name="tmpl"][value="ordered"]'), 'no classification template');
    ok($(el, 'input[name="tmpl"][value="label-eq"]'), 'other templates remain');
  });

  it('surfaces the server 400 in the dialog for a malformed advanced expression', async () => {
    el = panelWithMock({ scopes: 'security:read security:write' });
    mount(el);
    await nextEvent(el, 'loaded');
    el.addEventListener('error', (e) => e.stopPropagation());
    $(el, '.new').click();
    $(el, 'input[name="tmpl"][value="advanced"]').click();
    setField(el, '.f-expression', '(tenant == ');
    setField(el, '.f-name', 'bad-scope');
    const errored = nextEvent(el, 'error');
    $(el, '.confirm').click();
    const e = await errored;
    equal(e.detail.problem.status, 400, 'malformed expression rejected');
    ok($(el, '.content .error-banner'), 'the dialog shows the error and stays open');
    ok(dialogOpen(el), 'dialog stays open on error');
  });

  it('surfaces the 409 for a duplicate scope name', async () => {
    el = panelWithMock({ scopes: 'security:read security:write' });
    mount(el);
    await nextEvent(el, 'loaded');
    el.addEventListener('error', (e) => e.stopPropagation());
    $(el, '.new').click();
    setField(el, '.f-value', 'x');
    const nameInput = $(el, '.f-name');
    nameInput.value = 'tenant-scoped';
    nameInput.dispatchEvent(new Event('input'));
    const errored = nextEvent(el, 'error');
    $(el, '.confirm').click();
    const e = await errored;
    equal(e.detail.problem.status, 409, 'duplicate name conflicts');
  });

  it('edits a scope (name is the immutable key) and saves the new expression', async () => {
    el = panelWithMock({ scopes: 'security:read security:write' });
    mount(el);
    await nextEvent(el, 'loaded');
    [...el.shadowRoot.querySelectorAll('.edit')].find((b) => b.dataset.name === 'reach-payments').click();
    equal($(el, '.f-name').readOnly, true, 'name is the key on edit');
    setField(el, '.f-expression', "domain == 'billing'");
    const changed = nextEvent(el, 'scopes-changed');
    $(el, '.confirm').click();
    const e = await changed;
    equal(e.detail.scopes.find((s) => s.name === 'reach-payments').expression, "domain == 'billing'", 'expression updated');
  });

  it('deletes a scope (via the client + reload) and emits scopes-changed', async () => {
    el = panelWithMock({ scopes: 'security:read security:write' });
    mount(el);
    await nextEvent(el, 'loaded');
    const changed = nextEvent(el, 'scopes-changed');
    await el.client.deleteSecurityRule('reach-payments');
    await el.reloadAndEmit();
    const e = await changed;
    ok(!e.detail.scopes.some((s) => s.name === 'reach-payments'), 'scope removed');
  });

  it('hides the mutating controls without security:write', async () => {
    el = panelWithMock({ scopes: 'security:read' });
    mount(el);
    await nextEvent(el, 'loaded');
    ok($(el, '.new').hidden, 'New scope button hidden');
    ok(!$(el, '.edit'), 'no edit buttons');
  });
});
