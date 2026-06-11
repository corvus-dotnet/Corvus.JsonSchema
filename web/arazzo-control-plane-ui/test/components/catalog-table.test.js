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

  it('renders a row per catalog version with a status badge', async () => {
    el = tableWithMock();
    mount(el);
    await nextEvent(el, 'loaded');
    equal(rowCount(el), 5, 'five seeded versions render');
    ok(el.shadowRoot.querySelector('[part="status"]'), 'status badge present');
  });

  it('emits version-selected when a selectable row is clicked', async () => {
    el = tableWithMock({ selectable: '' });
    mount(el);
    await nextEvent(el, 'loaded');
    const firstRow = el.shadowRoot.querySelector('tbody tr[data-key]');
    const selected = nextEvent(el, 'version-selected');
    firstRow.click();
    const e = await selected;
    ok(e.detail.version && e.detail.version.baseWorkflowId, 'event carries the version');
    equal(`${e.detail.version.baseWorkflowId}@${e.detail.version.versionNumber}`, firstRow.dataset.key, 'selected the clicked version');
  });

  it('filters by the status attribute', async () => {
    el = tableWithMock({ status: 'Obsolete' });
    mount(el);
    await nextEvent(el, 'loaded');
    equal(rowCount(el), 1, 'only the obsolete version');
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
