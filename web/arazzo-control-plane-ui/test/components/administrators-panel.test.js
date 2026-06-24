// Tier 3 — <arazzo-administrators-panel> mounted in a real browser against the in-memory mock.
import { ArazzoControlPlaneClient } from '../../src/arazzo-client.js';
import { createMockControlPlane } from '../../demo/mock-api.js';
import '../../src/components/administrators-panel.js';
import { ok, equal, nextEvent, waitFor, mount } from './helpers.js';

function panelWithMock(attrs = {}) {
  const mock = createMockControlPlane({ latencyMs: 0 });
  const el = document.createElement('arazzo-administrators-panel');
  for (const [k, v] of Object.entries(attrs)) el.setAttribute(k, v);
  el.client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
  return el;
}

const rows = (el) => el.shadowRoot.querySelectorAll('.arow');

describe('<arazzo-administrators-panel>', () => {
  let el;
  afterEach(() => el?.remove());

  it('lists the administrators of a base id (as {dimension,value} grants)', async () => {
    el = panelWithMock({ 'base-workflow-id': 'onboard-customer', scopes: 'administrators:read' });
    mount(el);
    await nextEvent(el, 'loaded');
    equal(rows(el).length, 2, 'two seeded administrators');
    ok(el.shadowRoot.textContent.includes('tenant=platform'), 'shows a grant identity');
  });

  it('shows the empty state for a base id with no established administration', async () => {
    el = panelWithMock({ 'base-workflow-id': 'ghost', scopes: 'administrators:read' });
    mount(el);
    await nextEvent(el, 'loaded');
    ok(el.shadowRoot.querySelector('.empty'), 'empty state');
    equal(rows(el).length, 0, 'no administrators');
  });

  it('adds an administrator via the resolved-grantee picker and emits administrators-changed', async () => {
    el = panelWithMock({ 'base-workflow-id': 'nightly-reconcile', scopes: 'administrators:write' });
    mount(el);
    await nextEvent(el, 'loaded');
    equal(rows(el).length, 1, 'one seeded administrator');
    // Drive the nested picker: type a query, pick the server-resolved grantee, then Add.
    const picker = el.shadowRoot.querySelector('arazzo-grantee-picker');
    const q = picker.shadowRoot.querySelector('.q');
    q.value = 'payments';
    q.dispatchEvent(new Event('input'));
    const pickerResults = () => picker.shadowRoot.querySelectorAll('.results li[data-index]');
    await waitFor(() => pickerResults().length === 1);
    pickerResults()[0].click();
    const changed = nextEvent(el, 'administrators-changed');
    el.shadowRoot.querySelector('.addbtn').click();
    const e = await changed;
    equal(e.detail.administrators.length, 2, 'the set grew');
    ok(e.detail.administrators.some((a) => a.kind === 'team' && (a.identity || []).some((g) => g.dimension === 'team' && g.value === 'payments')), 'added the resolved team grantee');
  });

  it('surfaces the 409 when the last administrator would be removed', async () => {
    el = panelWithMock({ 'base-workflow-id': 'nightly-reconcile', scopes: 'administrators:write' });
    mount(el);
    await nextEvent(el, 'loaded');
    // The element emits a composed `error` CustomEvent; stop it bubbling to window (the test runner's global
    // error handler would otherwise treat it as an uncaught error).
    el.addEventListener('error', (e) => e.stopPropagation());
    // removeMember() pops a confirm dialog; drive the same mutate path directly to assert the 409 banner.
    const errored = nextEvent(el, 'error');
    await el.mutate(() => el.client.removeAdministrator('nightly-reconcile', el._admins[0].digest));
    const e = await errored;
    equal(e.detail.problem.status, 409, 'last-administrator removal conflicts');
    ok(el.shadowRoot.querySelector('.error-banner'), 'shows an error banner');
  });

  it('hides the mutating controls without administrators:write', async () => {
    el = panelWithMock({ 'base-workflow-id': 'onboard-customer', scopes: 'administrators:read' });
    mount(el);
    await nextEvent(el, 'loaded');
    ok(el.shadowRoot.querySelector('.add').hidden, 'the add form is hidden');
    ok(!el.shadowRoot.querySelector('.rm'), 'no remove buttons');
  });
});
