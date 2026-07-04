// Tier 3 — <arazzo-credentials>: the Connections master-detail surface (list + RHS detail pane) over the in-memory mock.
import { ArazzoControlPlaneClient } from '../../src/arazzo-client.js';
import { createMockControlPlane } from '../../demo/mock-api.js';
import '../../src/components/credentials.js';
import { ok, equal, nextEvent, waitFor, mount } from './helpers.js';

function panel(scopes = 'credentials:read credentials:write') {
  const mock = createMockControlPlane({ latencyMs: 0 });
  const el = document.createElement('arazzo-credentials');
  el.setAttribute('base-url', 'https://mock/arazzo/v1');
  el.setAttribute('scopes', scopes);
  el.client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
  return el;
}

const table = (el) => el.shadowRoot.querySelector('arazzo-credentials-table');
const detail = (el) => el.shadowRoot.querySelector('arazzo-credential-detail');
const row = (el, key) => table(el).shadowRoot.querySelector(`tbody tr[data-key="${key}"]`);

async function selectRow(el, key = 'petstore@production') {
  mount(el);
  await nextEvent(table(el), 'loaded');
  row(el, key).click();
  return waitFor(() => detail(el) && detail(el).shadowRoot.querySelector('[part="meta"]'));
}

describe('<arazzo-credentials> (Connections master-detail)', () => {
  let el;
  afterEach(() => el?.remove());

  it('selecting a row opens its record in the RHS detail pane, not a dialog', async () => {
    el = panel();
    await selectRow(el);
    ok(detail(el), 'a detail pane is shown on the RHS');
    ok(el.shadowRoot.querySelector('.layout').classList.contains('has-selection'), 'the layout switches to two columns');
    ok(detail(el).shadowRoot.querySelector('header .src').textContent.includes('petstore'), 'the pane shows the selected binding');
    // The dialog is NOT popped on select — it exists but its <dialog> is not open.
    const dlg = el.shadowRoot.querySelector('arazzo-credential-dialog');
    ok(!dlg.shadowRoot.querySelector('dialog')?.open, 'no modal dialog on select');
  });

  it('a write-capable caller sees Edit / Duplicate / Revoke actions in the pane', async () => {
    el = panel('credentials:read credentials:write');
    await selectRow(el);
    const actions = detail(el).shadowRoot.querySelector('.actions');
    ok(actions.querySelector('.edit'), 'Edit action present');
    ok(actions.querySelector('.duplicate'), 'Duplicate action present');
    ok(actions.querySelector('.revoke'), 'Revoke action present');
  });

  it('a read-only caller sees the record but no write actions (scope honesty)', async () => {
    el = panel('credentials:read');
    await selectRow(el);
    const actions = detail(el).shadowRoot.querySelector('.actions');
    ok(!actions.querySelector('.edit'), 'no Edit action without credentials:write');
    ok(!actions.querySelector('.revoke'), 'no Revoke action without credentials:write');
    ok(actions.textContent.toLowerCase().includes('read-only'), 'explains it is read-only');
  });

  it('Edit opens the guided dialog (the heavy secret-ref editor stays a dialog)', async () => {
    el = panel();
    await selectRow(el);
    detail(el).shadowRoot.querySelector('.edit').click();
    const dlg = el.shadowRoot.querySelector('arazzo-credential-dialog');
    await waitFor(() => dlg.shadowRoot.querySelector('dialog')?.open);
    ok(dlg.shadowRoot.querySelector('dialog').open, 'the guided editor dialog opened for Edit');
  });

  it('Revoke deletes the binding and clears the detail pane', async () => {
    el = panel();
    await selectRow(el);
    const d = detail(el);
    const deleted = nextEvent(el, 'credential-deleted');
    d.shadowRoot.querySelector('.revoke').click();
    // Confirm in the themed confirmation dialog (never window.confirm).
    const ok2 = await waitFor(() => d.shadowRoot.querySelector('dialog.arazzo-confirm .ok'));
    ok2.click();
    await deleted;
    await waitFor(() => !detail(el), 'the pane clears after revoke');
    ok(!detail(el), 'detail pane cleared');
    await waitFor(() => !row(el, 'petstore@production'), 'the revoked row is gone from the list');
  });
});