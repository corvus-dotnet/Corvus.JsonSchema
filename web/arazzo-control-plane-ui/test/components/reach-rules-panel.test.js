// Tier 3 — <arazzo-reach-rules-panel> mounted in a real browser against the in-memory mock.
import { ArazzoControlPlaneClient } from '../../src/arazzo-client.js';
import { createMockControlPlane } from '../../demo/mock-api.js';
import '../../src/components/reach-rules-panel.js';
import { ok, equal, nextEvent, mount } from './helpers.js';

function panelWithMock(attrs = {}) {
  const mock = createMockControlPlane({ latencyMs: 0 });
  const el = document.createElement('arazzo-reach-rules-panel');
  for (const [k, v] of Object.entries(attrs)) el.setAttribute(k, v);
  el.client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
  return el;
}

const rows = (el) => el.shadowRoot.querySelectorAll('.rrow');
const $ = (el, sel) => el.shadowRoot.querySelector(sel);
const setField = (el, sel, value) => { const i = $(el, sel); i.value = value; i.dispatchEvent(new Event('input')); };
const preview = (el) => $(el, '.preview-expr').textContent;

describe('<arazzo-reach-rules-panel>', () => {
  let el;
  afterEach(() => el?.remove());

  it('lists the seeded reach rules with their expressions', async () => {
    el = panelWithMock({ scopes: 'security:read' });
    mount(el);
    await nextEvent(el, 'loaded');
    ok(rows(el).length >= 4, 'several seeded rules');
    ok(el.shadowRoot.textContent.includes('tenant == $claim.tenant'), 'shows a rule expression');
  });

  it('builds a label-equals expression from the template, auto-suggests the name, and creates the rule', async () => {
    el = panelWithMock({ scopes: 'security:read security:write' });
    mount(el);
    await nextEvent(el, 'loaded');
    $(el, '.new').click();
    ok(!$(el, '.form').hidden, 'the form opened');
    setField(el, '.f-value', 'marketing');
    equal(preview(el), "domain == 'marketing'", 'template wrote the expression');
    equal($(el, '.f-name').value, 'reach-marketing', 'name auto-suggested from the value');

    const changed = nextEvent(el, 'rules-changed');
    $(el, '.create').click();
    const e = await changed;
    ok(e.detail.rules.some((r) => r.name === 'reach-marketing' && r.expression === "domain == 'marketing'"), 'rule created');
  });

  it('builds a set-membership expression (the slice-3 operator) from comma-separated values', async () => {
    el = panelWithMock({ scopes: 'security:read security:write' });
    mount(el);
    await nextEvent(el, 'loaded');
    $(el, '.new').click();
    $(el, 'input[name="tmpl"][value="in"]').click(); // switch template → re-renders the field set
    setField(el, '.f-dim', 'tenant');
    setField(el, '.f-valuesText', 'acme, globex');
    equal(preview(el), "tenant in ('acme', 'globex')", 'set-membership expression');
  });

  it('escapes a single quote in a literal value (grammar doubles it)', async () => {
    el = panelWithMock({ scopes: 'security:read security:write' });
    mount(el);
    await nextEvent(el, 'loaded');
    $(el, '.new').click();
    setField(el, '.f-value', "o'brien");
    equal(preview(el), "domain == 'o''brien'", "single quote doubled");
  });

  it('surfaces the server 400 for a malformed advanced expression', async () => {
    el = panelWithMock({ scopes: 'security:read security:write' });
    mount(el);
    await nextEvent(el, 'loaded');
    el.addEventListener('error', (e) => e.stopPropagation());
    $(el, '.new').click();
    $(el, 'input[name="tmpl"][value="advanced"]').click();
    setField(el, '.f-expression', '(tenant == ');
    setField(el, '.f-name', 'bad-rule');
    const errored = nextEvent(el, 'error');
    $(el, '.create').click();
    const e = await errored;
    equal(e.detail.problem.status, 400, 'malformed expression rejected');
    ok($(el, '.form-err .error-banner'), 'shows the form error banner');
  });

  it('surfaces the 409 for a duplicate rule name', async () => {
    el = panelWithMock({ scopes: 'security:read security:write' });
    mount(el);
    await nextEvent(el, 'loaded');
    el.addEventListener('error', (e) => e.stopPropagation());
    $(el, '.new').click();
    setField(el, '.f-value', 'x');
    setField(el, '.f-name', 'tenant-scoped'); // a seeded rule name
    const errored = nextEvent(el, 'error');
    $(el, '.create').click();
    const e = await errored;
    equal(e.detail.problem.status, 409, 'duplicate name conflicts');
  });

  it('edits an existing rule via the advanced form and saves the new expression', async () => {
    el = panelWithMock({ scopes: 'security:read security:write' });
    mount(el);
    await nextEvent(el, 'loaded');
    const editBtn = [...el.shadowRoot.querySelectorAll('.edit')].find((b) => b.dataset.name === 'reach-payments');
    editBtn.click();
    equal($(el, '.f-name').readOnly, true, 'the name is the immutable key on edit');
    setField(el, '.f-expression', "domain == 'billing'");
    const changed = nextEvent(el, 'rules-changed');
    $(el, '.create').click();
    const e = await changed;
    equal(e.detail.rules.find((r) => r.name === 'reach-payments').expression, "domain == 'billing'", 'expression updated');
  });

  it('deletes a rule (via the client + reload) and emits rules-changed', async () => {
    el = panelWithMock({ scopes: 'security:read security:write' });
    mount(el);
    await nextEvent(el, 'loaded');
    const changed = nextEvent(el, 'rules-changed');
    await el.client.deleteSecurityRule('reach-payments');
    await el.reloadAndEmit();
    const e = await changed;
    ok(!e.detail.rules.some((r) => r.name === 'reach-payments'), 'rule removed');
  });

  it('hides the mutating controls without security:write', async () => {
    el = panelWithMock({ scopes: 'security:read' });
    mount(el);
    await nextEvent(el, 'loaded');
    ok($(el, '.new').hidden, 'the New reach button is hidden');
    ok(!$(el, '.edit'), 'no edit buttons');
    ok(!$(el, '.del'), 'no delete buttons');
  });
});
