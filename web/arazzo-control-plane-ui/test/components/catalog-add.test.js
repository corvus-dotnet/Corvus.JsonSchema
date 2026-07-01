// Tier 3 — <arazzo-catalog-add-dialog> (the guided wizard) and its <arazzo-catalog> panel wiring against the mock.
import { ArazzoControlPlaneClient } from '../../src/arazzo-client.js';
import { createMockControlPlane } from '../../demo/mock-api.js';
import '../../src/components/catalog-add-dialog.js';
import '../../src/arazzo-catalog.js';
import { ok, equal, nextEvent, waitFor, mount } from './helpers.js';

function dialogWithMock() {
  const mock = createMockControlPlane({ latencyMs: 0 });
  const el = document.createElement('arazzo-catalog-add-dialog');
  el.client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
  return el;
}

function panel(scopes) {
  const mock = createMockControlPlane({ latencyMs: 0 });
  const el = document.createElement('arazzo-catalog');
  el.setAttribute('base-url', 'https://mock/arazzo/v1');
  if (scopes != null) el.setAttribute('scopes', scopes);
  el.fetch = mock.fetch;
  return el;
}

/** Attach a File to a file input the way a user picking a file would. */
function setFile(input, name, text) {
  const dt = new DataTransfer();
  dt.items.add(new File([text], name, { type: 'application/json' }));
  input.files = dt.files;
  input.dispatchEvent(new Event('change', { bubbles: true }));
}

const next = (el) => el.$('.next').click();

const workflowJson = (id, title, sources = [{ name: 'petstore', type: 'openapi' }]) => JSON.stringify({
  arazzo: '1.1.0', info: { title }, sourceDescriptions: sources,
  workflows: [{ workflowId: id, steps: [] }],
});

/** Fill the Details step (build mode) with a workflow + owner, ready to advance. */
async function fillDetails(el, id, title, sources) {
  setFile(el.$('#workflowFile'), 'workflow.json', workflowJson(id, title, sources));
  await waitFor(() => el.$('.wf-status')?.classList.contains('ok'));
  el.$('#ownerName').value = 'Reconciliation Team';
  el.$('#ownerEmail').value = 'team@example.com';
}

describe('<arazzo-catalog-add-dialog> wizard', () => {
  let el;
  afterEach(() => el?.remove());

  it('the catalog panel reloads its list on a persona change, so the list matches the caller’s reach (no stale full list)', async () => {
    const mock = createMockControlPlane({ latencyMs: 0 });
    el = document.createElement('arazzo-catalog');
    el.setAttribute('base-url', 'https://mock/arazzo/v1');
    el.setAttribute('scopes', 'catalog:read');
    el.fetch = mock.fetch;
    mount(el);
    const table = el.shadowRoot.querySelector('arazzo-catalog-table');
    await nextEvent(table, 'loaded');
    equal(table.shadowRoot.querySelectorAll('tbody tr[data-key]').length, 3, 'full reach: all three seeded workflows');

    // Switch to the reach-scoped reader and reload the wrapper, as the demo's persona selector does. Without the
    // wrapper's reload() the list would stay stale (all three) while the detail view 404s the out-of-reach ones.
    mock.setPersona('team-reader');
    const reloaded = nextEvent(table, 'loaded');
    el.reload();
    await reloaded;
    const keys = [...table.shadowRoot.querySelectorAll('tbody tr[data-key]')].map((tr) => tr.dataset.key);
    equal(keys.length, 1, 'reach-scoped: only the payments-domain workflow remains');
    equal(keys[0], 'nightly-reconcile');
  });

  it('reuses an already-registered source (no re-upload) and adds the version through the steps', async () => {
    el = dialogWithMock();
    mount(el);
    el.open();
    // Step 1 — Details.
    await fillDetails(el, 'nightly-reconcile', 'Nightly Reconcile');
    el.$('#tags').value = 'prod billing';
    next(el);
    // Step 2 — Sources: petstore is a registered source → resolved, no upload input.
    await waitFor(() => el.shadowRoot.querySelector('.src-badge'));
    ok(el.shadowRoot.querySelector('.src-badge.registered'), 'petstore shown as a registered source');
    ok(!el.$('.src-file[data-name="petstore"]'), 'a registered source asks for no upload');
    next(el);
    // Step 3 — Administrators (optional). Step 4 — Review.
    next(el);
    await waitFor(() => el.$('.next').textContent === 'Add workflow');
    const added = nextEvent(el, 'workflow-added');
    next(el);
    const e = await added;
    equal(e.detail.version.baseWorkflowId, 'nightly-reconcile', 'base id read from the built package');
    equal(e.detail.version.versionNumber, 4, 'catalog assigned the next version');
  });

  it('asks for a new source document, BLOCKS until it is configured in an environment, then registers it on commit', async () => {
    el = dialogWithMock();
    mount(el);
    el.open();
    await fillDetails(el, 'inventory-sync', 'Inventory Sync', [{ name: 'inventory', type: 'openapi' }]);
    next(el);
    // inventory is NOT registered → it asks for a document.
    const input = await waitFor(() => el.$('.src-file[data-name="inventory"]'));
    ok(el.shadowRoot.querySelector('.src-badge.new'), 'inventory shown as a new source');
    // Advancing without the document is refused.
    next(el);
    ok(!el.$('.error-banner').hidden && /inventory/.test(el.$('.error-banner').textContent), 'requires the new source document');
    // Supply the document — still NOT ready (no credential), so adding is still refused.
    setFile(input, 'inventory.json', JSON.stringify({ openapi: '3.1.0' }));
    await waitFor(() => el.shadowRoot.querySelector('.src-upload-status.ok'));
    next(el);
    ok(!el.$('.error-banner').hidden && /ready in any environment/.test(el.$('.error-banner').textContent), 'blocked until ready in an environment');
    // Configure inventory's credential inline (production) → the workflow becomes ready.
    el.$('.setup-cred-btn[data-name="inventory"]').click();
    const cred = await waitFor(() => el.shadowRoot.querySelector('arazzo-credential-dialog'));
    await waitFor(() => cred.shadowRoot.querySelector('dialog')?.open);
    cred.shadowRoot.querySelector('#environment').value = 'production';
    const row = cred.shadowRoot.querySelector('.refs .refrow');
    row.querySelector('.scheme').value = 'raw';
    row.querySelector('.scheme').dispatchEvent(new Event('change'));
    row.querySelector('.rawref').value = 'keyvault://inventory-kv/key';
    const saved = nextEvent(cred, 'credential-saved');
    cred.shadowRoot.querySelector('.confirm').click();
    await saved;
    await waitFor(() => el.shadowRoot.querySelector('.readiness.ok'));
    // Now ready → advance to the end and commit.
    next(el); next(el);
    await waitFor(() => el.$('.next').textContent === 'Add workflow');
    const added = nextEvent(el, 'workflow-added');
    next(el);
    await added;
    // The new source was registered in the §7.6 registry, so it is now findable.
    const reg = await el.client.getSource('inventory');
    equal(reg.name, 'inventory', 'the new source was registered');
    ok(reg.document, 'with its document');
  });

  it('validates required owner fields before leaving the first step', async () => {
    el = dialogWithMock();
    mount(el);
    el.open();
    setFile(el.$('#workflowFile'), 'workflow.json', workflowJson('thing', 'Thing'));
    await waitFor(() => el.$('.wf-status')?.classList.contains('ok'));
    next(el);
    ok(!el.$('.error-banner').hidden && /owner/i.test(el.$('.error-banner').textContent), 'reports the missing owner');
  });

  it('uploads a pre-built package in upload mode (no sources step)', async () => {
    el = dialogWithMock();
    mount(el);
    el.open();
    el.$('input[name="mode"][value="upload"]').click();
    await waitFor(() => el.$('#packageFile'));
    const dt = new DataTransfer();
    dt.items.add(new File(['not-a-real-zip'], 'pkg.zip', { type: 'application/zip' }));
    el.$('#packageFile').files = dt.files;
    el.$('#packageFile').dispatchEvent(new Event('change', { bubbles: true }));
    el.$('#ownerName').value = 'Me';
    el.$('#ownerEmail').value = 'me@example.com';
    next(el); // → admins (upload mode skips the sources step)
    ok(el.$('.admin-picker'), 'upload mode goes straight to administrators');
    next(el); // → review
    await waitFor(() => el.$('.next').textContent === 'Add workflow');
    const added = nextEvent(el, 'workflow-added');
    next(el);
    const e = await added;
    equal(e.detail.version.status, 'Active');
  });

  it('establishes the creator as administrator and applies an added person/team/role grantee', async () => {
    el = dialogWithMock();
    mount(el);
    el.open();
    await fillDetails(el, 'fresh-flow', 'Fresh Flow'); // a brand-new base → creator becomes admin
    next(el); // → sources (petstore registered, nothing to do)
    await waitFor(() => el.shadowRoot.querySelector('.src-badge'));
    next(el); // → admins
    await waitFor(() => el.$('.admin-picker'));
    ok(el.shadowRoot.textContent.includes('You (the creator)'), 'states the creator administers');
    // Resolve a real team grantee via the picker and stage it.
    const picker = el.$('.admin-picker');
    const q = picker.shadowRoot.querySelector('.q');
    q.value = 'payments';
    q.dispatchEvent(new Event('input'));
    await waitFor(() => picker.shadowRoot.querySelectorAll('.results li[data-index]').length === 1);
    picker.shadowRoot.querySelector('.results li[data-index]').click();
    el.$('.add-admin').click();
    ok([...el.shadowRoot.querySelectorAll('.admins .admin-row')].some((r) => r.textContent.includes('Payments')), 'team administrator staged');
    next(el); // → review
    await waitFor(() => el.$('.next').textContent === 'Add workflow');
    const added = nextEvent(el, 'workflow-added');
    next(el);
    await added;
    const { administrators } = await el.client.listAdministrators('fresh-flow');
    const has = (dim, val) => administrators.some((a) => (a.identity || []).some((g) => g.dimension === dim && g.value === val));
    ok(administrators.some((a) => /creator/i.test(a.label || '')), 'the creator is an administrator');
    ok(has('team', 'payments'), 'the added team administrator was applied');
  });

  it('does not offer a workflow as an administrator (only person/team/role)', async () => {
    el = dialogWithMock();
    mount(el);
    el.open();
    await fillDetails(el, 'fresh-flow-2', 'Fresh Flow 2');
    next(el);
    await waitFor(() => el.shadowRoot.querySelector('.src-badge'));
    next(el);
    const picker = await waitFor(() => el.$('.admin-picker'));
    const opts = [...picker.shadowRoot.querySelectorAll('.kind option')].map((o) => o.value);
    ok(!opts.includes('workflow'), 'the admin picker does not offer the workflow kind');
  });

  it('shows environment readiness and sets up a credential INLINE during the wizard', async () => {
    el = dialogWithMock();
    mount(el);
    el.open();
    await fillDetails(el, 'nightly-reconcile', 'Nightly Reconcile'); // references petstore (credentialed in production)
    next(el);
    // The workflow is already ready in production (petstore has a credential there).
    await waitFor(() => el.shadowRoot.querySelector('.readiness.ok'));
    ok(el.shadowRoot.querySelector('.readiness.ok').textContent.includes('production'), 'ready in production');
    // "Set up credential…" opens the guided dialog INLINE, locked to the source with auth derived from its registry doc.
    el.$('.setup-cred-btn[data-name="petstore"]').click();
    const cred = await waitFor(() => el.shadowRoot.querySelector('arazzo-credential-dialog'));
    await waitFor(() => cred.shadowRoot.querySelector('dialog')?.open);
    equal(cred.shadowRoot.querySelector('#sourceName').value, 'petstore', 'locked to the source');
    ok(cred.shadowRoot.querySelector('#sourceName').readOnly, 'source is locked');
    equal(cred.shadowRoot.querySelector('#authKind').value, 'apiKey', 'auth derived from the registered source document');
    // Configure a staging credential via the raw-reference escape hatch and save.
    cred.shadowRoot.querySelector('#environment').value = 'staging';
    const row = cred.shadowRoot.querySelector('.refs .refrow');
    row.querySelector('.scheme').value = 'raw';
    row.querySelector('.scheme').dispatchEvent(new Event('change'));
    row.querySelector('.rawref').value = 'keyvault://petstore-kv/api-key';
    const saved = nextEvent(cred, 'credential-saved');
    cred.shadowRoot.querySelector('.confirm').click();
    await saved;
    // The wizard reflects the new binding inline — petstore now has a credential in staging too.
    await waitFor(() => [...el.shadowRoot.querySelectorAll('.src-creds .env-chip')].some((c) => c.textContent === 'staging'));
    ok(true, 'inline-created credential reflected in the source row');
  });

  it('does not count a credential scoped to another workflow toward readiness (§13 usability)', async () => {
    el = dialogWithMock();
    mount(el);
    el.open();
    // 'rival' references billing, whose only credential (production) is usage-scoped to nightly-reconcile.
    await fillDetails(el, 'rival', 'Rival', [{ name: 'billing', type: 'openapi' }]);
    next(el);
    await waitFor(() => el.shadowRoot.querySelector('.src-badge'));
    ok(el.shadowRoot.querySelector('.readiness.warn'), 'not ready — billing’s credential is restricted to another workflow');
    ok(![...el.shadowRoot.querySelectorAll('.src-creds .env-chip')].length, 'no usable credential environment shown for billing');
  });

});

describe('<arazzo-catalog> add wiring', () => {
  let el;
  afterEach(() => el?.remove());

  it('hides Add version unless catalog:write is granted', async () => {
    el = panel('catalog:read');
    mount(el);
    ok(el.shadowRoot.querySelector('.add-btn').hidden, 'add hidden without catalog:write');
    el.setAttribute('scopes', 'catalog:read catalog:write');
    await waitFor(() => !el.shadowRoot.querySelector('.add-btn').hidden);
    ok(true, 'add shown once granted');
  });

  it('opens the add dialog from the toolbar', async () => {
    el = panel('catalog:read catalog:write');
    mount(el);
    const table = el.shadowRoot.querySelector('arazzo-catalog-table');
    await nextEvent(table, 'loaded');
    el.shadowRoot.querySelector('.add-btn').click();
    const dlg = el.shadowRoot.querySelector('arazzo-catalog-add-dialog');
    await waitFor(() => dlg.shadowRoot.querySelector('dialog')?.open);
    ok(dlg.shadowRoot.querySelector('dialog').open, 'dialog opened');
  });
});
