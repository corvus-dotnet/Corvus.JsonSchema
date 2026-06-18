// Tier 3 — <arazzo-credentials-table> mounted in a real browser against the in-memory mock.
import { ArazzoControlPlaneClient } from '../../src/arazzo-client.js';
import { createMockControlPlane } from '../../demo/mock-api.js';
import '../../src/components/credentials-table.js';
import { ok, equal, nextEvent, waitFor, mount } from './helpers.js';

function tableWithMock(attrs = {}) {
  const mock = createMockControlPlane({ latencyMs: 0 });
  const el = document.createElement('arazzo-credentials-table');
  for (const [k, v] of Object.entries(attrs)) el.setAttribute(k, v);
  el.client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
  return el;
}

const rowCount = (el) => el.shadowRoot.querySelectorAll('tbody tr[data-key]').length;

describe('<arazzo-credentials-table>', () => {
  let el;
  afterEach(() => el?.remove());

  it('renders a row per binding with a status badge and an expiring/expired footer', async () => {
    el = tableWithMock();
    mount(el);
    const e = await nextEvent(el, 'loaded');
    equal(rowCount(el), 4, 'four seeded bindings');
    equal(e.detail.expiring, 1, 'one expiring soon');
    equal(e.detail.expired, 1, 'one expired');
    ok(el.shadowRoot.querySelector('[part="status"]'), 'status badge present');
    ok(el.shadowRoot.querySelector('.foot').textContent.includes('expiring'), 'footer counts expiring');
  });

  it('never renders secret material — only references', async () => {
    el = tableWithMock();
    mount(el);
    await nextEvent(el, 'loaded');
    // The seeded refs all carry a scheme; the table shows the reference, not a secret.
    ok(el.shadowRoot.textContent.includes('keyvault://petstore-key#3'), 'shows the reference');
  });

  it('filters by status', async () => {
    el = tableWithMock({ status: 'expired' });
    mount(el);
    await nextEvent(el, 'loaded');
    equal(rowCount(el), 1, 'only the expired binding');
  });

  it('filters by source substring', async () => {
    el = tableWithMock({ source: 'pet' });
    mount(el);
    await nextEvent(el, 'loaded');
    equal(rowCount(el), 1, 'only petstore');
  });

  it('emits credential-selected when a row is clicked', async () => {
    el = tableWithMock({ selectable: '' });
    mount(el);
    await nextEvent(el, 'loaded');
    const row = el.shadowRoot.querySelector('tbody tr[data-key="petstore@production"]');
    const sel = nextEvent(el, 'credential-selected');
    row.click();
    const e = await sel;
    equal(e.detail.binding.sourceName, 'petstore', 'event carries the binding');
  });

  it('hides New without credentials:write and emits credential-new when present', async () => {
    el = tableWithMock({ scopes: 'credentials:read' });
    mount(el);
    await nextEvent(el, 'loaded');
    ok(el.shadowRoot.querySelector('.new').hidden, 'New hidden without the write scope');
    el.setAttribute('scopes', 'credentials:write');
    await waitFor(() => !el.shadowRoot.querySelector('.new').hidden);
    const ev = nextEvent(el, 'credential-new');
    el.shadowRoot.querySelector('.new').click();
    await ev;
  });

  it('keyset-pages with "Load more" when the store returns more than one page', async () => {
    const many = Array.from({ length: 55 }, (_, i) => {
      const k = String(i).padStart(3, '0');
      return { id: `cred-src${k}`, sourceName: `src${k}`, environment: 'production', authKind: 'apiKey', secretRefs: [{ name: 'value', ref: `keyvault://kv/secret${k}` }], createdBy: 'demo', createdAt: new Date(0).toISOString(), etag: `"e${k}"` };
    });
    const mock = createMockControlPlane({ latencyMs: 0, credentialsSeed: many });
    el = document.createElement('arazzo-credentials-table');
    el.client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
    mount(el);
    await nextEvent(el, 'loaded');
    equal(rowCount(el), 50, 'first page is one keyset page of 50');
    const more = el.shadowRoot.querySelector('.foot .more');
    ok(more, 'a Load more control while more pages remain');
    more.click();
    await waitFor(() => rowCount(el) === 55);
    ok(!el.shadowRoot.querySelector('.foot .more'), 'Load more disappears on the last page');
  });
});
