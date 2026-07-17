// Tier 3 — <arazzo-sources>: the registry list; the row-level credential entry point is the job a
// connections admin brings to this page (creation was previously only reachable via the catalog).
import { ArazzoControlPlaneClient } from '../../src/arazzo-client.js';
import { createMockControlPlane } from '../../demo/mock-api.js';
import '../../src/components/sources-panel.js';
import { ok, equal, nextEvent, waitFor, mount } from './helpers.js';

function panel(scopes = 'sources:read sources:write') {
  const mock = createMockControlPlane({ latencyMs: 0 });
  const el = document.createElement('arazzo-sources');
  el.setAttribute('scopes', scopes);
  el.client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
  return el;
}

describe('<arazzo-sources> credential entry point', () => {
  let el;
  afterEach(() => el?.remove());

  it('the detail shows the structured operation surface AND the highlighted document, not just raw text (#843)', async () => {
    el = panel();
    mount(el);
    await nextEvent(el, 'loaded');
    // Open the first registered source's detail.
    el.shadowRoot.querySelector('tbody tr[data-name]').click();
    await waitFor(() => el.shadowRoot.querySelector('arazzo-source-operations'));

    // The standard filterable operations view renders the source's real surface (method+path rows).
    const opsView = el.shadowRoot.querySelector('arazzo-source-operations');
    await waitFor(() => opsView.shadowRoot.querySelectorAll('.op').length > 0);
    ok(opsView.shadowRoot.querySelector('.op .badge'), 'rows carry the binding badge (method/action)');
    const before = opsView.shadowRoot.querySelectorAll('.op').length;
    const filter = opsView.shadowRoot.querySelector('.filter');
    filter.value = 'zzz-no-such-operation';
    filter.dispatchEvent(new Event('input'));
    ok(opsView.shadowRoot.querySelector('.empty'), 'the filter narrows to the empty state');
    filter.value = '';
    filter.dispatchEvent(new Event('input'));
    equal(opsView.shadowRoot.querySelectorAll('.op').length, before, 'clearing the filter restores the surface');

    // The document renders through the read-only JSON view (highlighted when CM lands; themed pre until then).
    const doc = el.shadowRoot.querySelector('arazzo-json-view');
    ok(doc, 'the document section uses the shared JSON view');
    ok(doc.value.includes('"openapi"') || doc.value.includes('"asyncapi"'), 'it carries the registered document text');

    // The master-detail split is resizable (#844): opening a detail arms the shared splitbar, targeting
    // the layout's --detail-w that the has-selection grid consumes.
    const layout = el.shadowRoot.querySelector('.layout');
    ok(layout.classList.contains('has-selection'), 'the layout is in its two-pane mode');
    const bar = el.shadowRoot.querySelector('.layout arazzo-splitbar');
    ok(bar, 'a draggable splitbar sits between the list and the detail');
    equal(bar.getAttribute('prop'), '--detail-w', 'the bar drives the width property the grid reads');
    equal(bar.getAttribute('target'), '.layout', 'targeting the layout it lives in');
  });

  it('every source row offers ＋ credential, opening the dialog locked to that source', async () => {
    el = panel();
    mount(el);
    await nextEvent(el, 'loaded');
    const btn = await waitFor(() => el.shadowRoot.querySelector('.cred-add[data-source="billing"]'));
    btn.click();
    const dlg = el.shadowRoot.querySelector('arazzo-credential-dialog');
    const opened = await waitFor(() => dlg.shadowRoot.querySelector('dialog[open]'));
    ok(opened, 'the dialog opened');
    const src = dlg.shadowRoot.querySelector('#sourceName');
    equal(src.value, 'billing', 'prefilled with the row source');
    ok(src.disabled || src.readOnly, 'and locked to it — the environment is the choice being made');
  });
});
