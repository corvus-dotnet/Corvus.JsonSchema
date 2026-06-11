// Tier 3 — <arazzo-catalog-detail> and the <arazzo-catalog> panel against the in-memory mock.
import { ArazzoControlPlaneClient } from '../../src/arazzo-client.js';
import { createMockControlPlane } from '../../demo/mock-api.js';
import '../../src/components/catalog-detail.js';
import '../../src/arazzo-catalog.js';
import { ok, equal, nextEvent, waitFor, mount } from './helpers.js';

function detailWithMock(attrs = {}) {
  const mock = createMockControlPlane({ latencyMs: 0 });
  const el = document.createElement('arazzo-catalog-detail');
  for (const [k, v] of Object.entries(attrs)) el.setAttribute(k, v);
  el.client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
  return el;
}

function panel(scopes) {
  const mock = createMockControlPlane({ latencyMs: 0 });
  const el = document.createElement('arazzo-catalog');
  el.setAttribute('base-url', 'https://mock/arazzo/v1');
  if (scopes != null) el.setAttribute('scopes', scopes);
  el.fetch = mock.fetch; // inject the mock; rebuilds the client
  return el;
}

describe('<arazzo-catalog-detail>', () => {
  let el;
  afterEach(() => el?.remove());

  it('loads a version and renders its metadata, owner, audit and sources', async () => {
    el = detailWithMock({ 'base-workflow-id': 'nightly-reconcile', 'version-number': '1', scopes: 'catalog:read' });
    mount(el);
    await waitFor(() => el.shadowRoot.querySelector('[part="hash"]'));
    ok(el.shadowRoot.querySelector('[part="owner"]'), 'owner block present');
    ok(el.shadowRoot.querySelector('[part="audit"]'), 'audit block present');
    ok(el.shadowRoot.querySelector('[part="sources"]'), 'sources block present');
    equal(el.shadowRoot.querySelector('header .badge').textContent, 'Obsolete', 'status badge reflects the version');
  });

  it('shows a not-found banner for an unknown version', async () => {
    el = detailWithMock({ 'base-workflow-id': 'nightly-reconcile', 'version-number': '99' });
    // The element emits a composed `error` CustomEvent; stop it bubbling to window (the test runner's
    // global error handler would otherwise treat it as an uncaught error).
    el.addEventListener('error', (e) => e.stopPropagation());
    mount(el);
    await waitFor(() => el.shadowRoot.querySelector('.error-banner'));
    ok(el.shadowRoot.textContent.includes('not found'), 'reports not found');
  });

  it('hides governance actions without write/purge scope (scope honesty)', async () => {
    el = detailWithMock({ 'base-workflow-id': 'adopt-pet', 'version-number': '1', scopes: 'catalog:read' });
    mount(el);
    await waitFor(() => el.shadowRoot.querySelector('[part="actions"]'));
    ok(!el.shadowRoot.querySelector('.obsolete'), 'no obsolete button without catalog:write');
    ok(!el.shadowRoot.querySelector('.delete'), 'no delete button without catalog:purge');
  });

  it('obsoletes an active version and emits version-changed', async () => {
    el = detailWithMock({ 'base-workflow-id': 'adopt-pet', 'version-number': '1', scopes: 'catalog:read catalog:write' });
    mount(el);
    await waitFor(() => el.shadowRoot.querySelector('.obsolete'));
    const changed = nextEvent(el, 'version-changed');
    el.shadowRoot.querySelector('.obsolete').click();
    // confirmDialog renders into the shadow root; click its OK button.
    const okBtn = await waitFor(() => el.shadowRoot.querySelector('dialog.arazzo-confirm .ok'));
    okBtn.click();
    const e = await changed;
    equal(e.detail.version.status, 'Obsolete', 'version is now obsolete');
  });
});

describe('<arazzo-catalog> panel', () => {
  let el;
  afterEach(() => el?.remove());

  it('composes a catalog-table and loads versions', async () => {
    el = panel('catalog:read catalog:write catalog:purge');
    mount(el);
    const table = el.shadowRoot.querySelector('arazzo-catalog-table');
    ok(table, 'embeds the catalog table');
    await nextEvent(table, 'loaded');
    ok(table.shadowRoot.querySelectorAll('tbody tr[data-key]').length > 0, 'rows loaded');
  });

  it('hides Purge unless catalog:purge is granted (scope honesty)', async () => {
    el = panel('catalog:read catalog:write');
    mount(el);
    ok(el.shadowRoot.querySelector('.purge-btn').hidden, 'purge hidden without catalog:purge');
    el.setAttribute('scopes', 'catalog:read catalog:write catalog:purge');
    await waitFor(() => !el.shadowRoot.querySelector('.purge-btn').hidden);
    ok(true, 'purge shown once granted');
  });

  it('selecting a row opens the detail pane', async () => {
    el = panel('catalog:read');
    mount(el);
    const table = el.shadowRoot.querySelector('arazzo-catalog-table');
    await nextEvent(table, 'loaded');
    table.shadowRoot.querySelector('tbody tr[data-key]').click();
    const detail = await waitFor(() => el.shadowRoot.querySelector('arazzo-catalog-detail'));
    ok(detail, 'detail pane populated');
    await waitFor(() => detail.shadowRoot.querySelector('[part="hash"]'));
    ok(detail.shadowRoot.querySelector('[part="hash"]'), 'authoritative detail loaded');
  });

  it('the search input sets the table q attribute', async () => {
    el = panel('catalog:read');
    mount(el);
    const table = el.shadowRoot.querySelector('arazzo-catalog-table');
    await nextEvent(table, 'loaded');
    const input = el.shadowRoot.querySelector('.q-search');
    input.value = 'reconcile';
    input.dispatchEvent(new Event('input', { bubbles: true }));
    await waitFor(() => table.getAttribute('q') === 'reconcile');
    ok(true);
  });
});
