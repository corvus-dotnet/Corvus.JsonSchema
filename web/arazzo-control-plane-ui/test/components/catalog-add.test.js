// Tier 3 — <arazzo-catalog-add-dialog> and its <arazzo-catalog> panel wiring against the in-memory mock.
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

const workflowJson = (id, title) => JSON.stringify({
  arazzo: '1.1.0', info: { title }, sourceDescriptions: [{ name: 'petstore', type: 'openapi' }],
  workflows: [{ workflowId: id, steps: [] }],
});

describe('<arazzo-catalog-add-dialog>', () => {
  let el;
  afterEach(() => el?.remove());

  it('derives the required sources from the workflow and builds the package on submit', async () => {
    el = dialogWithMock();
    mount(el);
    el.open();
    setFile(el.$('#workflowFile'), 'workflow.json', workflowJson('nightly-reconcile', 'Nightly Reconcile'));
    // The dialog reads sourceDescriptions and renders a required file input named "petstore".
    const input = await waitFor(() => el.$('.src-file[data-name="petstore"]'));
    setFile(input, 'petstore.json', JSON.stringify({ openapi: '3.1.0' }));
    el.$('#ownerName').value = 'Reconciliation Team';
    el.$('#ownerEmail').value = 'team@example.com';
    el.$('#tags').value = 'prod billing';

    const added = nextEvent(el, 'workflow-added');
    el.$('.confirm').click();
    const e = await added;
    equal(e.detail.version.baseWorkflowId, 'nightly-reconcile', 'base id read from the built package');
    equal(e.detail.version.versionNumber, 4, 'catalog assigned the next version');
  });

  it('requires a document for every declared source', async () => {
    el = dialogWithMock();
    mount(el);
    el.open();
    setFile(el.$('#workflowFile'), 'workflow.json', workflowJson('nightly-reconcile', 'Nightly Reconcile'));
    await waitFor(() => el.$('.src-file[data-name="petstore"]'));
    el.$('#ownerName').value = 'Me';
    el.$('#ownerEmail').value = 'me@example.com';
    // Leave the petstore source file unset.
    el.$('.confirm').click();
    const banner = await waitFor(() => { const b = el.$('.error-banner'); return b && !b.hidden ? b : null; });
    ok(/petstore/.test(banner.textContent), 'reports the missing source document');
  });

  it('validates required owner fields before submitting', async () => {
    el = dialogWithMock();
    mount(el);
    el.open();
    setFile(el.$('#workflowFile'), 'workflow.json', workflowJson('thing', 'Thing'));
    el.$('.confirm').click();
    const banner = await waitFor(() => { const b = el.$('.error-banner'); return b && !b.hidden ? b : null; });
    ok(/owner/i.test(banner.textContent), 'reports the missing owner');
  });

  it('uploads a pre-built package in upload mode', async () => {
    el = dialogWithMock();
    mount(el);
    el.open();
    el.setMode('upload');
    // A non-package upload still posts; the mock falls back to the generic base id.
    const dt = new DataTransfer();
    dt.items.add(new File(['not-a-real-zip'], 'pkg.zip', { type: 'application/zip' }));
    el.$('#packageFile').files = dt.files;
    el.$('#ownerName').value = 'Me';
    el.$('#ownerEmail').value = 'me@example.com';
    const added = nextEvent(el, 'workflow-added');
    el.$('.confirm').click();
    const e = await added;
    equal(e.detail.version.status, 'Active');
  });

  it('stages the workflow administrator (locked, deletable) and applies the set after the version lands', async () => {
    el = dialogWithMock();
    mount(el);
    el.open();
    setFile(el.$('#workflowFile'), 'workflow.json', workflowJson('nightly-reconcile', 'Nightly Reconcile'));
    await waitFor(() => el.$('.src-file[data-name="petstore"]'));
    setFile(el.$('.src-file[data-name="petstore"]'), 'petstore.json', JSON.stringify({ openapi: '3.1.0' }));

    // The Administrators section seeds the workflow identity (read-only value, deletable) and pins the grant input.
    const grant = await waitFor(() => el.$('arazzo-admin-grant-input'));
    ok([...el.shadowRoot.querySelectorAll('.admins .admin-row')].some((r) => r.textContent.includes('workflow=nightly-reconcile')), 'workflow identity staged by default');
    equal(grant.getAttribute('fixed-workflow'), 'nightly-reconcile', 'the workflow value is pinned to the new workflow');

    // Add a tenant administrator and stage it.
    const dim = grant.shadowRoot.querySelector('.dim');
    dim.value = 'tenant';
    dim.dispatchEvent(new Event('change'));
    grant.shadowRoot.querySelector('.val-text').value = 'growth';
    el.$('.add-admin').click();
    ok([...el.shadowRoot.querySelectorAll('.admins .admin-row')].some((r) => r.textContent.includes('tenant=growth')), 'tenant administrator staged');

    el.$('#ownerName').value = 'Team';
    el.$('#ownerEmail').value = 'team@example.com';
    const added = nextEvent(el, 'workflow-added');
    el.$('.confirm').click();
    await added;

    // The staged set was applied to the landed base id via the administrators API; each administrator is a resolved
    // identity whose `identity` carries the {dimension,value} grant(s) it resolves from.
    const { administrators } = await el.client.listAdministrators('nightly-reconcile');
    const hasGrant = (dimension, value) => administrators.some((a) => (a.identity || []).some((g) => g.dimension === dimension && g.value === value));
    ok(hasGrant('tenant', 'growth'), 'applied the tenant administrator');
    ok(hasGrant('workflow', 'nightly-reconcile'), 'applied the workflow administrator');
  });

  it('opens a guided credential dialog locked to each source ticked for credential setup', async () => {
    el = dialogWithMock();
    mount(el);
    el.open();
    setFile(el.$('#workflowFile'), 'workflow.json', workflowJson('nightly-reconcile', 'Nightly Reconcile'));
    await waitFor(() => el.$('.src-file[data-name="petstore"]'));
    setFile(el.$('.src-file[data-name="petstore"]'), 'petstore.json', JSON.stringify({ openapi: '3.1.0' }));
    el.$('#ownerName').value = 'Team';
    el.$('#ownerEmail').value = 'team@example.com';
    el.$('.setup-cred[data-name="petstore"]').checked = true; // set up this source's credentials after adding

    const added = nextEvent(el, 'workflow-added');
    el.$('.confirm').click();
    await added;

    // After the version lands, a guided credential dialog opens locked to the petstore source.
    const cred = await waitFor(() => el.shadowRoot.querySelector('arazzo-credential-dialog'));
    await waitFor(() => cred.shadowRoot.querySelector('dialog')?.open);
    equal(cred.shadowRoot.querySelector('#sourceName').value, 'petstore', 'pre-filled with the source name');
    ok(cred.shadowRoot.querySelector('#sourceName').readOnly, 'source is locked');
    ok(!cred.shadowRoot.querySelector('#environment').readOnly, 'environment stays editable');
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
