// Tier 3 — <arazzo-runs-table> mounted in a real browser against the in-memory mock.
import { ArazzoControlPlaneClient } from '../../src/arazzo-client.js';
import { createMockControlPlane } from '../../demo/mock-api.js';
import '../../src/components/runs-table.js';
import { ok, equal, nextEvent, waitFor, mount } from './helpers.js';

function tableWithMock(attrs = {}) {
  const mock = createMockControlPlane({ latencyMs: 0 });
  const el = document.createElement('arazzo-runs-table');
  for (const [k, v] of Object.entries(attrs)) el.setAttribute(k, v);
  el.client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
  return el;
}

const rowCount = (el) => el.shadowRoot.querySelectorAll('tbody tr[data-id]').length;

describe('<arazzo-runs-table>', () => {
  let el;
  afterEach(() => el?.remove());

  it('renders a row per run with a status badge', async () => {
    el = tableWithMock();
    mount(el);
    await nextEvent(el, 'loaded');
    equal(rowCount(el), 8, 'eight seeded runs render');
    ok(el.shadowRoot.querySelector('arazzo-status-badge'), 'status badge present');
  });

  it('emits run-selected when a selectable row is clicked', async () => {
    el = tableWithMock({ selectable: '' });
    mount(el);
    await nextEvent(el, 'loaded');
    const firstRow = el.shadowRoot.querySelector('tbody tr[data-id]');
    const selected = nextEvent(el, 'run-selected');
    firstRow.click();
    const e = await selected;
    ok(e.detail.run && e.detail.run.id, 'event carries the run');
    equal(e.detail.run.id, firstRow.dataset.id, 'selected the clicked run');
  });

  it('filters by the status attribute', async () => {
    el = tableWithMock({ status: 'Faulted' });
    mount(el);
    await nextEvent(el, 'loaded');
    equal(rowCount(el), 1, 'only the faulted run');
  });

  it('filters by a created-before time window', async () => {
    el = tableWithMock();
    mount(el);
    await nextEvent(el, 'loaded');
    const all = rowCount(el);
    const loaded = nextEvent(el, 'loaded');
    el.setAttribute('created-before', new Date(Date.now() - 30 * 86400000).toISOString());
    await loaded;
    const older = await waitFor(() => { const n = rowCount(el); return n < all ? n : 0; });
    ok(older >= 1 && older < all, `created-before narrows the set (${older} < ${all})`);
  });

  it('shows the empty state when nothing matches', async () => {
    el = tableWithMock({ 'workflow-id': 'does-not-exist' });
    mount(el);
    await nextEvent(el, 'loaded');
    await waitFor(() => el.shadowRoot.querySelector('.empty'));
    equal(rowCount(el), 0, 'no rows');
  });
});
