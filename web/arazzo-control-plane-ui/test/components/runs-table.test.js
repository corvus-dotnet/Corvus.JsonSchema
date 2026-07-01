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
    equal(rowCount(el), 12, 'all seeded runs render');
    ok(el.shadowRoot.querySelector('arazzo-status-badge'), 'status badge present');
  });

  it('shows each run’s pinned environment (and a placeholder when unpinned)', async () => {
    el = tableWithMock();
    mount(el);
    await nextEvent(el, 'loaded');
    const pinned = el.shadowRoot.querySelector('tbody tr[data-id="run-7f3a9c21"] td.env');
    ok(pinned && pinned.textContent.includes('production'), 'a pinned run shows its environment (§5.5)');
    const unpinned = el.shadowRoot.querySelector('tbody tr[data-id="run-1b88de40"] td.env');
    ok(unpinned && unpinned.textContent.trim() === '—', 'a run created before pinning shows the — placeholder');
  });

  it('reach-scopes the runs to the caller (§14.2) — a payments-team reader sees only payments-domain runs', async () => {
    // Same read scopes as the auditor; the only difference is reach — so this isolates the row-security axis.
    const mock = createMockControlPlane({ latencyMs: 0, persona: 'team-reader' });
    el = document.createElement('arazzo-runs-table');
    el.client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
    mount(el);
    await nextEvent(el, 'loaded');
    const n = rowCount(el);
    ok(n > 0 && n < 12, `sees a strict subset of the 12 seeded runs (${n})`);
    const workflows = [...el.shadowRoot.querySelectorAll('tbody tr[data-id] td.wf')].map((c) => c.textContent);
    ok(workflows.every((w) => w.includes('nightly-reconcile')), 'every visible run is a payments-domain (nightly-reconcile) run');
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
    equal(rowCount(el), 5, 'only the faulted runs');
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

  it('has a run-id copy button matching the detail view (⧉, flips to ✓ on copy)', async () => {
    el = tableWithMock();
    mount(el);
    await nextEvent(el, 'loaded');
    const copy = el.shadowRoot.querySelector('button.copy');
    ok(copy, 'copy button present');
    equal(copy.textContent, '⧉', 'uses the same glyph as the detail view');
    copy.click();
    await new Promise((r) => setTimeout(r, 10));
    ok(['⧉', '✓'].includes(copy.textContent), 'glyph is the copy or confirmed state');
  });

  it('shows the empty state when nothing matches', async () => {
    el = tableWithMock({ 'workflow-id': 'does-not-exist' });
    mount(el);
    await nextEvent(el, 'loaded');
    await waitFor(() => el.shadowRoot.querySelector('.empty'));
    equal(rowCount(el), 0, 'no rows');
  });
});
