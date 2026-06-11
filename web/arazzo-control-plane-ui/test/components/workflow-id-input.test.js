// Tier 3 — <arazzo-workflow-id-input> autocomplete against the in-memory mock.
import { ArazzoControlPlaneClient } from '../../src/arazzo-client.js';
import { createMockControlPlane } from '../../demo/mock-api.js';
import '../../src/components/workflow-id-input.js';
import { ok, equal, waitFor, mount } from './helpers.js';

function inputWithMock(catalogSeed) {
  const mock = createMockControlPlane(catalogSeed ? { latencyMs: 0, catalogSeed } : { latencyMs: 0 });
  const el = document.createElement('arazzo-workflow-id-input');
  el.client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
  return el;
}

const options = (el) => [...(el.shadowRoot.querySelector('#wf-id-options')?.options ?? [])].map((o) => o.value);

describe('<arazzo-workflow-id-input>', () => {
  let el;
  afterEach(() => el?.remove());

  it('seeds suggestions (base + versioned ids) from the catalog on connect', async () => {
    el = inputWithMock();
    mount(el);
    await waitFor(() => el.shadowRoot.querySelector('#wf-id-options')?.options.length);
    const values = options(el);
    ok(values.includes('adopt-pet'), 'offers base ids');
    ok(values.includes('nightly-reconcile-v3'), 'offers versioned ids');
  });

  it('caps at 10 and narrows to the typed prefix', async () => {
    const seed = Array.from({ length: 20 }, (_, i) => {
      const base = `wf-${String(i).padStart(2, '0')}`;
      return {
        baseWorkflowId: base, versionNumber: 1, workflowId: `${base}-v1`, title: `Workflow ${i}`,
        status: 'Active', tags: [], owner: { name: 'Team', email: 'team@example.com' }, sources: [],
        createdBy: 'a', createdAt: new Date().toISOString(),
      };
    });
    el = inputWithMock(seed);
    mount(el);
    await waitFor(() => el.shadowRoot.querySelector('#wf-id-options')?.options.length);
    ok(options(el).length <= 10, 'capped at 10 when empty');

    const inner = el.shadowRoot.querySelector('input');
    inner.value = 'wf-01';
    inner.dispatchEvent(new Event('input', { bubbles: true }));
    await waitFor(() => { const o = options(el); return o.length > 0 && o.every((v) => v.includes('wf-01')) ? o : null; });
    ok(options(el).length <= 10 && options(el).every((v) => v.includes('wf-01')), 'narrowed + still capped');
  });

  it('bubbles input events retargeted so the host reads e.target.value', async () => {
    el = inputWithMock();
    mount(el);
    let seen = null;
    el.addEventListener('input', (e) => { seen = e.target.value; });
    const inner = el.shadowRoot.querySelector('input');
    inner.value = 'adopt';
    inner.dispatchEvent(new Event('input', { bubbles: true, composed: true }));
    equal(seen, 'adopt', 'host sees the typed value via e.target.value');
    equal(el.value, 'adopt', '.value reflects the input');
  });
});
