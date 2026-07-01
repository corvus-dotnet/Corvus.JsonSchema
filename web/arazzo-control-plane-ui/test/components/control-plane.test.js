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

  it('reload() re-fetches the runs list (the demo calls it on a persona change → reach re-applies)', async () => {
    const mock = createMockControlPlane({ latencyMs: 0 });
    el = document.createElement('arazzo-control-plane');
    el.setAttribute('base-url', 'https://mock/arazzo/v1');
    el.setAttribute('scopes', 'runs:read');
    el.fetch = mock.fetch;
    mount(el);
    const table = el.shadowRoot.querySelector('arazzo-runs-table');
    await nextEvent(table, 'loaded');
    const all = table.shadowRoot.querySelectorAll('tbody tr[data-id]').length;
    equal(all, 12, 'the default (full-reach) persona sees every run');

    // Switch to the reach-scoped reader and reload the wrapper, exactly as the demo's persona selector does.
    mock.setPersona('team-reader');
    const reloaded = nextEvent(table, 'loaded');
    el.reload();
    await reloaded;
    const scoped = table.shadowRoot.querySelectorAll('tbody tr[data-id]').length;
    ok(scoped > 0 && scoped < all, `after the persona change the list is reach-scoped (${scoped} of ${all})`);
    const workflows = [...table.shadowRoot.querySelectorAll('tbody tr[data-id] td.wf')].map((c) => c.textContent);
    ok(workflows.every((w) => w.includes('nightly-reconcile')), 'only payments-domain runs remain');
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

  it('embeds the workflowId autocomplete and applies it to the table filter', async () => {
    el = panel('runs:read');
    mount(el);
    const table = el.shadowRoot.querySelector('arazzo-runs-table');
    await nextEvent(table, 'loaded');
    const wfInput = el.shadowRoot.querySelector('arazzo-workflow-id-input.wf-search');
    ok(wfInput, 'embeds the <arazzo-workflow-id-input> component');
    // Type into the component's inner input; its input event bubbles out and the panel applies the filter.
    const inner = wfInput.shadowRoot.querySelector('input');
    inner.value = 'onboard';
    inner.dispatchEvent(new Event('input', { bubbles: true, composed: true }));
    await waitFor(() => table.getAttribute('workflow-id') === 'onboard');
    ok(true);
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
