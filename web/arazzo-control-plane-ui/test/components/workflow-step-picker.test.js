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
    const sel = await waitFor(() => el.shadowRoot.querySelector('select:not([disabled])'));
    const labels = [...sel.options].map((o) => o.textContent);
    ok(labels.some((t) => t.includes('reservePayment')), 'lists named steps from the workflow doc');
    equal(Number(sel.value), 2, 'defaults to the run cursor');
    equal(el.value, 2, '.value is the selected step index');
  });

  it('emits change with the chosen step index and id', async () => {
    el = pickerWithMock({ 'workflow-id': 'adopt-pet-v1', cursor: '0' });
    mount(el);
    const sel = await waitFor(() => el.shadowRoot.querySelector('select:not([disabled])'));
    const changed = nextEvent(el, 'change');
    sel.value = '1';
    sel.dispatchEvent(new Event('change', { bubbles: true }));
    const e = await changed;
    equal(e.detail.index, 1);
    equal(e.detail.stepId, 'reservePayment');
    equal(el.value, 1);
  });

  it('forward direction lists only later steps and defaults to the next one (Skip)', async () => {
    el = pickerWithMock({ 'workflow-id': 'adopt-pet-v1', cursor: '1', direction: 'forward' });
    mount(el);
    const sel = await waitFor(() => el.shadowRoot.querySelector('select:not([disabled])'));
    const values = [...sel.options].map((o) => Number(o.value));
    ok(values.length > 0 && values.every((v) => v > 1), 'only steps after the cursor are offered');
    equal(el.value, 2, 'defaults to the immediately following step');
  });

  it('backward direction lists only earlier steps and defaults to the previous one (Rewind)', async () => {
    el = pickerWithMock({ 'workflow-id': 'adopt-pet-v1', cursor: '2', direction: 'backward' });
    mount(el);
    const sel = await waitFor(() => el.shadowRoot.querySelector('select:not([disabled])'));
    const values = [...sel.options].map((o) => Number(o.value));
    ok(values.length > 0 && values.every((v) => v < 2), 'only steps before the cursor are offered');
    equal(el.value, 1, 'defaults to the immediately preceding step');
  });

  it('shows a loading placeholder while resolving, never the not-in-catalog fallback (#98)', async () => {
    el = pickerWithMock({ 'workflow-id': 'adopt-pet-v1', cursor: '2' });
    mount(el);
    // Synchronously after mount the catalog fetch is in flight: a disabled placeholder, not the numeric fallback.
    const placeholder = el.shadowRoot.querySelector('select[disabled]');
    ok(placeholder && /Resolving/.test(placeholder.textContent), 'a loading placeholder is shown first');
    ok(!el.shadowRoot.querySelector('input[type="number"]'), 'the not-in-catalog numeric fallback is NOT shown while loading');
    // It then resolves to the real chooser.
    const sel = await waitFor(() => el.shadowRoot.querySelector('select:not([disabled])'));
    ok([...sel.options].some((o) => o.textContent.includes('reservePayment')), 'resolves to the catalog steps');
  });

  it('cannot skip forward from the last step', async () => {
    el = pickerWithMock({ 'workflow-id': 'adopt-pet-v1', cursor: '3', direction: 'forward' });
    mount(el);
    await waitFor(() => el.shadowRoot.querySelector('.empty'));
    equal(el.shadowRoot.querySelector('select'), null, 'no chooser is rendered');
    equal(el.value, null, 'no valid forward target');
  });

  it('forward fallback (uncatalogued) bounds the numeric input above the cursor', async () => {
    el = pickerWithMock({ 'workflow-id': 'ghost-v9', cursor: '3', direction: 'forward' });
    mount(el);
    const input = await waitFor(() => el.shadowRoot.querySelector('input[type="number"]'));
    equal(input.min, '4', 'min is one past the cursor');
    equal(el.value, 4, 'seeded with the next index');
  });

  it('falls back to a numeric input when the workflow is not in the catalog', async () => {
    el = pickerWithMock({ 'workflow-id': 'ghost-v9', cursor: '3' });
    mount(el);
    const input = await waitFor(() => el.shadowRoot.querySelector('input[type="number"]'));
    equal(input.value, '3', 'seeded with the current cursor');
    equal(el.value, 3, '.value still works for resume');
  });
});
