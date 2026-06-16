// Tier 3 — <arazzo-credential-dialog> mounted in a real browser against the in-memory mock.
import { ArazzoControlPlaneClient } from '../../src/arazzo-client.js';
import { createMockControlPlane } from '../../demo/mock-api.js';
import '../../src/components/credential-dialog.js';
import { ok, equal, nextEvent, waitFor, mount } from './helpers.js';

function clientWithMock() {
  const mock = createMockControlPlane({ latencyMs: 0 });
  return new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
}

function dialogWith(client) {
  const el = document.createElement('arazzo-credential-dialog');
  el.client = client;
  return el;
}

const $ = (el, sel) => el.shadowRoot.querySelector(sel);

describe('<arazzo-credential-dialog>', () => {
  let el;
  afterEach(() => el?.remove());

  it('refuses an inline secret before sending (shows a banner)', async () => {
    el = dialogWith(clientWithMock());
    mount(el);
    el.open();
    $(el, '#sourceName').value = 'newsrc';
    $(el, '#environment').value = 'production';
    $(el, '#authKind').value = 'apiKey';
    $(el, '.refs .row .b').value = 'hunter2-the-actual-secret'; // no scheme — not a reference
    el.submit();
    await waitFor(() => !$(el, '.error-banner').hidden);
    ok($(el, '.error-banner').textContent.toLowerCase().includes('reference'), 'explains it must be a reference');
  });

  it('creates a binding from references and emits credential-saved', async () => {
    el = dialogWith(clientWithMock());
    mount(el);
    el.open();
    $(el, '#sourceName').value = 'newsrc';
    $(el, '#environment').value = 'production';
    $(el, '#authKind').value = 'apiKey';
    $(el, '.refs .row .a').value = 'value';
    $(el, '.refs .row .b').value = 'keyvault://newsrc-key';
    const saved = nextEvent(el, 'credential-saved');
    el.submit();
    const e = await saved;
    equal(e.detail.binding.sourceName, 'newsrc', 'created the binding');
    equal(e.detail.binding.credentialStatus, 'valid', 'derived status comes back');
  });

  it('edit/rotate locks the identity, re-points a reference, and stamps rotatedAt', async () => {
    const client = clientWithMock();
    el = dialogWith(client);
    mount(el);
    const binding = await client.getCredential('petstore', 'production');
    el.open(binding);
    ok($(el, '#sourceName').readOnly, 'source is locked when editing');
    $(el, '.refs .row .b').value = 'keyvault://petstore-key#4';
    const saved = nextEvent(el, 'credential-saved');
    el.submit();
    const e = await saved;
    equal(e.detail.binding.secretRefs[0].ref, 'keyvault://petstore-key#4', 'reference re-pointed');
    ok(e.detail.binding.rotatedAt, 'a reference change stamped rotatedAt');
  });
});
