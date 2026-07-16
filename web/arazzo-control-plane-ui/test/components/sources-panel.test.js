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
