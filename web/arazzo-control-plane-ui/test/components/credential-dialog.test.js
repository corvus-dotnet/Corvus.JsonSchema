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
const $$ = (el, sel) => [...el.shadowRoot.querySelectorAll(sel)];

// Set the auth kind, which (re)builds the secret-reference slots for that kind.
function setAuthKind(el, kind) {
  const input = $(el, '#authKind');
  input.value = kind;
  input.dispatchEvent(new Event('change'));
}

// Drive a reference slot row: pick its store (rebuilds the per-store fields), then fill them.
function setRef(row, { scheme, fields = {} }) {
  if (scheme) {
    const sel = row.querySelector('.scheme');
    sel.value = scheme;
    sel.dispatchEvent(new Event('change'));
  }
  for (const [key, value] of Object.entries(fields)) {
    const input = scheme === 'raw' ? row.querySelector('.rawref') : row.querySelector(`[data-key="${key}"]`);
    input.value = value;
  }
  return row;
}

describe('<arazzo-credential-dialog>', () => {
  let el;
  afterEach(() => el?.remove());

  it('drives the reference slots from the auth kind (count + label, no free-text role, no "+ Add")', async () => {
    el = dialogWith(clientWithMock());
    mount(el);
    el.open();
    // Default apiKey → exactly one slot, labelled "API key".
    equal($$(el, '.refs .refrow').length, 1, 'apiKey has one secret slot');
    ok($(el, '.refs .refrow .slot-label').textContent.includes('API key'), 'the slot is labelled');
    ok(!$(el, '.refs .refrow .role'), 'no free-text role input');
    ok(!$(el, '.addref'), 'no open-ended "+ Add reference"');
    // Switch to basic → one slot, now labelled "Password", and the Config section shows the required username field.
    setAuthKind(el, 'basic');
    equal($$(el, '.refs .refrow').length, 1, 'basic has one secret slot');
    ok($(el, '.refs .refrow .slot-label').textContent.includes('Password'), 'relabelled for basic');
    ok($(el, '.config-fields [data-cfg="username"]'), 'the Config section shows basic\'s username field');
  });

  it('refuses an inline secret entered via the raw escape hatch (shows a banner)', async () => {
    el = dialogWith(clientWithMock());
    mount(el);
    el.open();
    $(el, '#sourceName').value = 'newsrc';
    $(el, '#environment').value = 'production';
    setRef($(el, '.refs .refrow'), { scheme: 'raw', fields: { raw: 'hunter2-the-actual-secret' } });
    el.submit();
    await waitFor(() => !$(el, '.error-banner').hidden);
    ok($(el, '.error-banner').textContent.toLowerCase().includes('reference'), 'explains it must be a reference');
  });

  it('composes a Key Vault reference from guided fields and emits credential-saved', async () => {
    el = dialogWith(clientWithMock());
    mount(el);
    el.open();
    $(el, '#sourceName').value = 'newsrc';
    $(el, '#environment').value = 'production'; // authKind defaults to apiKey → role "value"
    setRef($(el, '.refs .refrow'), { fields: { host: 'newsrc-kv', name: 'api-key' } });
    const saved = nextEvent(el, 'credential-saved');
    el.submit();
    const e = await saved;
    equal(e.detail.binding.sourceName, 'newsrc', 'created the binding');
    equal(e.detail.binding.secretRefs[0].name, 'value', 'the slot fixed the role to the kind it belongs to');
    equal(e.detail.binding.secretRefs[0].ref, 'keyvault://newsrc-kv/api-key', 'guided fields composed the canonical ref');
    equal(e.detail.binding.credentialStatus, 'valid', 'derived status comes back');
  });

  it('composes the canonical reference for each store (OAuth2 client secret via Vault)', async () => {
    el = dialogWith(clientWithMock());
    mount(el);
    el.open();
    $(el, '#sourceName').value = 'vaultsrc';
    $(el, '#environment').value = 'production';
    setAuthKind(el, 'oauth2ClientCredentials'); // → one "Client secret" slot, role "clientSecret"
    setRef($(el, '.refs .refrow'), { scheme: 'vault', fields: { mount: 'secret', path: 'arazzo/petstore', field: 'api-key' } });
    equal($(el, '.refs .refrow .refpreview code').textContent, 'vault://secret/arazzo/petstore#api-key', 'preview shows the composed ref');
    // OAuth2 requires tokenUrl + clientId in the guided Config (non-secret).
    $(el, '.config-fields [data-cfg="tokenUrl"]').value = 'https://idp.example.com/token';
    $(el, '.config-fields [data-cfg="clientId"]').value = 'my-client';
    const saved = nextEvent(el, 'credential-saved');
    el.submit();
    const e = await saved;
    equal(e.detail.binding.secretRefs[0].name, 'clientSecret', 'role fixed to the OAuth2 client secret');
    equal(e.detail.binding.secretRefs[0].ref, 'vault://secret/arazzo/petstore#api-key', 'composed the canonical vault KV-v2 ref');
    equal(e.detail.binding.config.find((c) => c.key === 'tokenUrl').value, 'https://idp.example.com/token', 'tokenUrl stored as config');
  });

  it('drives the Config fields from the auth kind (none for bearer, required username for basic)', async () => {
    el = dialogWith(clientWithMock());
    mount(el);
    el.open();
    // apiKey (default) exposes optional header-name + location.
    ok($(el, '.config-fields [data-cfg="parameterName"]'), 'apiKey shows the header/parameter-name field');
    ok($(el, '.config-fields [data-cfg="location"]'), 'apiKey shows the location select');
    setAuthKind(el, 'bearer');
    ok(!$(el, '.config-fields [data-cfg]'), 'bearer needs no config');
    setAuthKind(el, 'basic');
    ok($(el, '.config-fields [data-cfg="username"]'), 'basic shows a username field');

    // Submitting basic without the required username is rejected with a store-specific message.
    $(el, '#sourceName').value = 'svc';
    $(el, '#environment').value = 'production';
    setRef($(el, '.refs .refrow'), { scheme: 'env', fields: { var: 'SVC_PW' } });
    el.submit();
    await waitFor(() => !$(el, '.error-banner').hidden);
    ok($(el, '.error-banner').textContent.toLowerCase().includes('username'), 'requires the username');

    // Fill it → saves with the username as non-secret config.
    $(el, '.config-fields [data-cfg="username"]').value = 'svc-account';
    const saved = nextEvent(el, 'credential-saved');
    el.submit();
    const e = await saved;
    equal(e.detail.binding.config.find((c) => c.key === 'username').value, 'svc-account', 'the username is stored as non-secret config');
  });

  it('preserves unknown config keys as editable rows and lets you add new arbitrary entries', async () => {
    const client = clientWithMock();
    el = dialogWith(client);
    mount(el);
    // A binding carrying a config key this auth kind does not define (region) alongside a guided one.
    await client.createCredential({
      sourceName: 'analytics', environment: 'production', authKind: 'apiKey',
      secretRefs: [{ name: 'value', ref: 'keyvault://analytics-kv/api-key' }],
      config: [{ key: 'parameterName', value: 'X-Api-Key' }, { key: 'region', value: 'eu-west-1' }],
    });
    el.open(await client.getCredential('analytics', 'production'));

    // The known key lands in its guided slot; the unknown one is preserved as an editable key/value row.
    equal($(el, '.config-fields [data-cfg="parameterName"]').value, 'X-Api-Key', 'known config sits in its guided slot');
    const extra = $$(el, '.config-extra .row');
    equal(extra.length, 1, 'the unknown key is preserved as one extra row');
    equal(extra[0].querySelector('.a').value, 'region', 'the preserved key is editable');
    equal(extra[0].querySelector('.b').value, 'eu-west-1', 'the preserved value is editable');

    // Edit the preserved value, then add a brand-new arbitrary entry via the button.
    extra[0].querySelector('.b').value = 'us-east-1';
    $(el, '.addcfg').click();
    const added = $$(el, '.config-extra .row').at(-1);
    added.querySelector('.a').value = 'audience';
    added.querySelector('.b').value = 'svc://billing';

    const saved = nextEvent(el, 'credential-saved');
    el.submit();
    const e = await saved;
    const cfg = e.detail.binding.config;
    equal(cfg.find((c) => c.key === 'region').value, 'us-east-1', 'the edited preserved key round-trips');
    equal(cfg.find((c) => c.key === 'audience').value, 'svc://billing', 'the added arbitrary entry is saved');
    equal(cfg.find((c) => c.key === 'parameterName').value, 'X-Api-Key', 'the guided field is still saved');
  });

  it('rejects an incomplete guided slot with a store-specific message', async () => {
    el = dialogWith(clientWithMock());
    mount(el);
    el.open();
    $(el, '#sourceName').value = 'x';
    $(el, '#environment').value = 'y';
    setRef($(el, '.refs .refrow'), { scheme: 'vault', fields: { mount: 'secret' } }); // path + field missing
    el.submit();
    await waitFor(() => !$(el, '.error-banner').hidden);
    ok($(el, '.error-banner').textContent.toLowerCase().includes('incomplete'), 'flags the incomplete reference');
  });

  it('edit/rotate: a legacy single-segment ref opens in raw mode, re-points, and stamps rotatedAt', async () => {
    const client = clientWithMock();
    el = dialogWith(client);
    mount(el);
    const binding = await client.getCredential('petstore', 'production');
    el.open(binding);
    ok($(el, '#sourceName').readOnly, 'source is locked when editing');
    const row = $(el, '.refs .refrow');
    equal(row.dataset.role, 'value', 'the apiKey slot');
    // petstore's seeded `keyvault://petstore-key#3` has no host/name slash, so it cannot be represented by the
    // guided Key Vault fields — it falls back to raw, preserved verbatim (never silently mutated).
    equal(row.querySelector('.scheme').value, 'raw', 'an unrepresentable legacy ref opens in raw mode');
    equal(row.querySelector('.rawref').value, 'keyvault://petstore-key#3', 'the raw ref is preserved');
    row.querySelector('.rawref').value = 'keyvault://petstore-key#4';
    const saved = nextEvent(el, 'credential-saved');
    el.submit();
    const e = await saved;
    equal(e.detail.binding.secretRefs[0].ref, 'keyvault://petstore-key#4', 'reference re-pointed');
    ok(e.detail.binding.rotatedAt, 'a reference change stamped rotatedAt');
  });

  it('creates for a fixed source when opened locked (the add-workflow source-credential flow)', async () => {
    el = dialogWith(clientWithMock());
    mount(el);
    el.open(null, { sourceName: 'petstore', lockSource: true });
    equal($(el, '#sourceName').value, 'petstore', 'the source is pre-filled');
    ok($(el, '#sourceName').readOnly, 'the source is locked');
    ok(!$(el, '#environment').readOnly, 'the environment stays editable');
    $(el, '#environment').value = 'staging'; // petstore/production is already seeded; staging is fresh
    setRef($(el, '.refs .refrow'), { fields: { host: 'petstore-kv', name: 'api-key' } });
    const saved = nextEvent(el, 'credential-saved');
    el.submit();
    const e = await saved;
    equal(e.detail.binding.sourceName, 'petstore', 'created for the locked source');
    equal(e.detail.binding.secretRefs[0].ref, 'keyvault://petstore-kv/api-key', 'with the composed reference');
  });
});