// Tier 3 — <arazzo-control-plane> panel: scope gating and the time-window filter wiring.
import { createMockControlPlane } from '../../demo/mock-api.js';
import '../../src/arazzo-control-plane.js';
import { ok, equal, nextEvent, waitFor, mount } from './helpers.js';

function panel(scopes) {
  const mock = createMockControlPlane({ latencyMs: 0 });
  const el = document.createElement('arazzo-control-plane');
  el.setAttribute('base-url', 'https://mock/arazzo/v1');
  if (scopes != null) el.setAttribute('scopes', scopes);
  el.fetch = mock.fetch; // inject the mock; rebuilds the client
  return el;
}

describe('<arazzo-control-plane>', () => {
  let el;
  afterEach(() => el?.remove());

  it('composes a runs-table and loads runs', async () => {
    el = panel('runs:read runs:write runs:purge');
    mount(el);
    const table = el.shadowRoot.querySelector('arazzo-runs-table');
    ok(table, 'embeds the runs table');
    await nextEvent(table, 'loaded');
    ok(table.shadowRoot.querySelectorAll('tbody tr[data-id]').length > 0, 'rows loaded');
  });

  it('hides the Purge action unless runs:purge is granted (scope honesty)', async () => {
    el = panel('runs:read runs:write');
    mount(el);
    const purge = el.shadowRoot.querySelector('.purge-btn');
    ok(purge.hidden, 'purge hidden without runs:purge');
    el.setAttribute('scopes', 'runs:read runs:write runs:purge');
    await waitFor(() => !el.shadowRoot.querySelector('.purge-btn').hidden);
    ok(!el.shadowRoot.querySelector('.purge-btn').hidden, 'purge shown once granted');
  });

  it('time-window inputs set the corresponding table attributes as ISO instants', async () => {
    el = panel('runs:read');
    mount(el);
    const table = el.shadowRoot.querySelector('arazzo-runs-table');
    await nextEvent(table, 'loaded');
    const input = el.shadowRoot.querySelector('.timewindow input[data-attr="created-after"]');
    ok(input, 'created-after input present');
    input.value = '2026-01-01T00:00';
    input.dispatchEvent(new Event('change', { bubbles: true }));
    const attr = await waitFor(() => table.getAttribute('created-after'));
    ok(attr.startsWith('2026-01-01T'), `table received an ISO created-after (${attr})`);
    ok(attr.endsWith('Z'), 'normalised to a UTC instant');
  });

  it('Clear dates removes the time-window attributes', async () => {
    el = panel('runs:read');
    mount(el);
    const table = el.shadowRoot.querySelector('arazzo-runs-table');
    await nextEvent(table, 'loaded');
    const input = el.shadowRoot.querySelector('.timewindow input[data-attr="updated-after"]');
    input.value = '2026-01-01T00:00';
    input.dispatchEvent(new Event('change', { bubbles: true }));
    await waitFor(() => table.hasAttribute('updated-after'));
    el.shadowRoot.querySelector('.clear-time').click();
    await waitFor(() => !table.hasAttribute('updated-after'));
    ok(!table.hasAttribute('updated-after'), 'cleared');
  });

  it('tag + correlation filters set the table attributes', async () => {
    el = panel('runs:read');
    mount(el);
    const table = el.shadowRoot.querySelector('arazzo-runs-table');
    await nextEvent(table, 'loaded');
    const tagInput = el.shadowRoot.querySelector('.tag-search');
    tagInput.value = 'tenant-42 priority';
    tagInput.dispatchEvent(new Event('input', { bubbles: true }));
    await waitFor(() => table.getAttribute('tags') === 'tenant-42 priority');
    const corrInput = el.shadowRoot.querySelector('.corr-search');
    corrInput.value = 'trace-abc';
    corrInput.dispatchEvent(new Event('input', { bubbles: true }));
    await waitFor(() => table.getAttribute('correlation-id') === 'trace-abc');
    ok(true);
  });

  it('populates the workflowId filter autocomplete from the catalog', async () => {
    el = panel('runs:read');
    mount(el);
    const list = await waitFor(() => {
      const dl = el.shadowRoot.querySelector('#wf-id-options');
      return dl && dl.options.length ? dl : null;
    });
    const values = [...list.options].map((o) => o.value);
    ok(el.shadowRoot.querySelector('.wf-search').getAttribute('list') === 'wf-id-options', 'input wired to the datalist');
    ok(values.includes('adopt-pet'), 'offers base workflow ids');
    ok(values.includes('nightly-reconcile-v3'), 'offers the versioned ids the runs carry');
  });

  it('caps the workflowId autocomplete at 10 and narrows it to what is typed', async () => {
    // A catalog far larger than the dropdown should cap; one row per base, two ids each (base + v1).
    const seed = Array.from({ length: 20 }, (_, i) => {
      const base = `wf-${String(i).padStart(2, '0')}`;
      return {
        baseWorkflowId: base, versionNumber: 1, workflowId: `${base}-v1`, title: `Workflow ${i}`,
        status: 'Active', tags: [], owner: { name: 'Team', email: 'team@example.com' }, sources: [],
        createdBy: 'a', createdAt: new Date().toISOString(),
      };
    });
    const mock = createMockControlPlane({ latencyMs: 0, catalogSeed: seed });
    el = document.createElement('arazzo-control-plane');
    el.setAttribute('base-url', 'https://mock/arazzo/v1');
    el.setAttribute('scopes', 'runs:read');
    el.fetch = mock.fetch;
    mount(el);

    const dl = await waitFor(() => { const d = el.shadowRoot.querySelector('#wf-id-options'); return d && d.options.length ? d : null; });
    ok(dl.options.length <= 10, `empty filter capped at 10 (was ${dl.options.length})`);

    const input = el.shadowRoot.querySelector('.wf-search');
    input.value = 'wf-01';
    input.dispatchEvent(new Event('input', { bubbles: true }));
    // Wait for the debounced server query to narrow the dropdown to only matching ids.
    await waitFor(() => {
      const opts = [...el.shadowRoot.querySelector('#wf-id-options').options];
      return opts.length > 0 && opts.every((o) => o.value.includes('wf-01')) ? opts : null;
    });
    const opts = [...el.shadowRoot.querySelector('#wf-id-options').options].map((o) => o.value);
    ok(opts.length <= 10 && opts.every((v) => v.includes('wf-01')), 'narrowed to the typed text and still capped');
  });

  it('re-selecting the already-selected run keeps the full detail (regression)', async () => {
    el = panel('runs:read');
    mount(el);
    const table = el.shadowRoot.querySelector('arazzo-runs-table');
    await nextEvent(table, 'loaded');
    const faultedRow = table.shadowRoot.querySelector('tbody tr[data-id="run-7f3a9c21"]');
    ok(faultedRow, 'faulted run row present');

    faultedRow.click();
    // The authoritative detail (with a fault block, which the list summary lacks) loads.
    const detail = await waitFor(() => el.shadowRoot.querySelector('arazzo-run-detail'));
    await waitFor(() => detail.shadowRoot.querySelector('[part="fault"]'));

    // Click the SAME row again: it must re-fetch the full detail, not strip it back to the summary.
    faultedRow.click();
    await new Promise((r) => setTimeout(r, 50));
    await waitFor(() => detail.shadowRoot.querySelector('[part="fault"]'));
    ok(detail.shadowRoot.querySelector('[part="fault"]'), 'fault block still present after re-select');
  });
});
