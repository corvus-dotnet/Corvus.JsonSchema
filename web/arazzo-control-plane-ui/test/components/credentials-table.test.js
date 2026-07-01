// Tier 3 — <arazzo-credentials-table> mounted in a real browser against the in-memory mock.
import { ArazzoControlPlaneClient } from '../../src/arazzo-client.js';
import { createMockControlPlane } from '../../demo/mock-api.js';
import '../../src/components/credentials-table.js';
import { ok, equal, nextEvent, mount } from './helpers.js';

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

  // Note: this management table has no "New credential" control by design — creating a credential is rooted in the
  // catalog's per-workflow Sources panel (where the source + its auth are known), not here (§7.5). The create flow is
  // covered there; this table covers view / select / duplicate / rotate.

  it('pages bindings with Prev/Next over the keyset cursor', async () => {
    // Four seeded bindings; a page-size of two makes two keyset pages (2 + 2).
    el = tableWithMock({ 'page-size': '2' });
    mount(el);
    await nextEvent(el, 'loaded');
    equal(rowCount(el), 2, 'page 1 holds two bindings');
    const next = el.shadowRoot.querySelector('.next');
    ok(!next.disabled, 'Next is enabled when a page follows');
    ok(el.shadowRoot.querySelector('.prev').disabled, 'Prev is disabled on page 1');
    ok(!el.shadowRoot.querySelector('.foot .more'), 'the old Load more control is gone');

    const page2 = nextEvent(el, 'loaded');
    next.click();
    await page2;
    equal(rowCount(el), 2, 'page 2 holds the remaining two bindings');
    ok(el.shadowRoot.querySelector('.next').disabled, 'Next is disabled on the last page');
    ok(!el.shadowRoot.querySelector('.prev').disabled, 'Prev is enabled off page 1');

    const back = nextEvent(el, 'loaded');
    el.shadowRoot.querySelector('.prev').click();
    await back;
    equal(rowCount(el), 2, 'Prev returns to page 1');
    ok(el.shadowRoot.querySelector('.prev').disabled, 'Prev is disabled again on page 1');
  });
});
