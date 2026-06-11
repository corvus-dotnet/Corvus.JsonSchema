// Tier 3 — <arazzo-workflow-step-picker> against the in-memory mock.
import { ArazzoControlPlaneClient } from '../../src/arazzo-client.js';
import { createMockControlPlane } from '../../demo/mock-api.js';
import '../../src/components/workflow-step-picker.js';
import { ok, equal, nextEvent, waitFor, mount } from './helpers.js';

function pickerWithMock(attrs = {}) {
  const mock = createMockControlPlane({ latencyMs: 0 });
  const el = document.createElement('arazzo-workflow-step-picker');
  for (const [k, v] of Object.entries(attrs)) el.setAttribute(k, v);
  el.client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
  return el;
}

describe('<arazzo-workflow-step-picker>', () => {
  let el;
  afterEach(() => el?.remove());

  it('lists the workflow steps from the catalog and defaults to the current cursor', async () => {
    el = pickerWithMock({ 'workflow-id': 'adopt-pet-v1', cursor: '2' });
    mount(el);
    const sel = await waitFor(() => el.shadowRoot.querySelector('select'));
    const labels = [...sel.options].map((o) => o.textContent);
    ok(labels.some((t) => t.includes('reservePayment')), 'lists named steps from the workflow doc');
    equal(Number(sel.value), 2, 'defaults to the run cursor');
    equal(el.value, 2, '.value is the selected step index');
  });

  it('emits change with the chosen step index and id', async () => {
    el = pickerWithMock({ 'workflow-id': 'adopt-pet-v1', cursor: '0' });
    mount(el);
    const sel = await waitFor(() => el.shadowRoot.querySelector('select'));
    const changed = nextEvent(el, 'change');
    sel.value = '1';
    sel.dispatchEvent(new Event('change', { bubbles: true }));
    const e = await changed;
    equal(e.detail.index, 1);
    equal(e.detail.stepId, 'reservePayment');
    equal(el.value, 1);
  });

  it('falls back to a numeric input when the workflow is not in the catalog', async () => {
    el = pickerWithMock({ 'workflow-id': 'ghost-v9', cursor: '3' });
    mount(el);
    const input = await waitFor(() => el.shadowRoot.querySelector('input[type="number"]'));
    equal(input.value, '3', 'seeded with the current cursor');
    equal(el.value, 3, '.value still works for resume');
  });
});
