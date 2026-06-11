// Tier 3 — <arazzo-catalog-table> mounted in a real browser against the in-memory mock.
import { ArazzoControlPlaneClient } from '../../src/arazzo-client.js';
import { createMockControlPlane } from '../../demo/mock-api.js';
import '../../src/components/catalog-table.js';
import { ok, equal, nextEvent, waitFor, mount } from './helpers.js';

function tableWithMock(attrs = {}) {
  const mock = createMockControlPlane({ latencyMs: 0 });
  const el = document.createElement('arazzo-catalog-table');
  for (const [k, v] of Object.entries(attrs)) el.setAttribute(k, v);
  el.client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
  return el;
}

const rowCount = (el) => el.shadowRoot.querySelectorAll('tbody tr[data-key]').length;

describe('<arazzo-catalog-table>', () => {
  let el;
  afterEach(() => el?.remove());

  it('renders one row per base workflow (versions collapse together)', async () => {
    el = tableWithMock();
    mount(el);
    await nextEvent(el, 'loaded');
    // Five seeded versions across three base workflows (nightly-reconcile ×3, adopt-pet, onboard-customer).
    equal(rowCount(el), 3, 'three base workflows render');
    const keys = el.$$('tbody tr[data-key]').map((tr) => tr.dataset.key);
    equal(keys.filter((k) => k === 'nightly-reconcile').length, 1, 'nightly-reconcile appears once');
    ok(el.shadowRoot.querySelector('[part="status"]'), 'status badge present');
  });

  it('shows the version count for a multi-version workflow', async () => {
    el = tableWithMock();
    mount(el);
    await nextEvent(el, 'loaded');
    const row = el.shadowRoot.querySelector('tbody tr[data-key="nightly-reconcile"]');
    ok(row.textContent.includes('3 versions'), 'nightly-reconcile shows its three versions');
    ok(row.textContent.includes('v3'), 'represented by its latest version');
  });

  it('emits version-selected with the latest version when a row is clicked', async () => {
    el = tableWithMock({ selectable: '' });
    mount(el);
    await nextEvent(el, 'loaded');
    const row = el.shadowRoot.querySelector('tbody tr[data-key="nightly-reconcile"]');
    const selected = nextEvent(el, 'version-selected');
    row.click();
    const e = await selected;
    equal(e.detail.baseWorkflowId, 'nightly-reconcile', 'event carries the base id');
    equal(e.detail.version.versionNumber, 3, 'representative is the latest version');
    equal(e.detail.versions.length, 3, 'event carries all sibling versions');
  });

  it('filters by the status attribute', async () => {
    el = tableWithMock({ status: 'Obsolete' });
    mount(el);
    await nextEvent(el, 'loaded');
    equal(rowCount(el), 1, 'only the workflow with an obsolete version');
  });

  it('filters by a free-text q attribute', async () => {
    el = tableWithMock({ q: 'adopt' });
    mount(el);
    await nextEvent(el, 'loaded');
    equal(rowCount(el), 1, 'only the matching title');
  });

  it('shows the empty state when nothing matches', async () => {
    el = tableWithMock({ 'base-workflow-id': 'does-not-exist' });
    mount(el);
    await nextEvent(el, 'loaded');
    await waitFor(() => el.shadowRoot.querySelector('.empty'));
    equal(rowCount(el), 0, 'no rows');
  });
});
