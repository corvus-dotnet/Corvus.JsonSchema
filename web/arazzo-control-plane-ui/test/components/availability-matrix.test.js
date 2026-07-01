// Tier 3 — <arazzo-availability-matrix> mounted in a real browser against the in-memory mock.
import { ArazzoControlPlaneClient } from '../../src/arazzo-client.js';
import { createMockControlPlane } from '../../demo/mock-api.js';
import '../../src/components/availability-matrix.js';
import { ok, equal, nextEvent, waitFor, mount } from './helpers.js';

const FULL = 'availability:read availability:write';

// Build a matrix over a fresh mock. `setup(client)` runs before the element loads (e.g. to add a credential-less env).
async function matrix(attrs, setup) {
  const mock = createMockControlPlane({ latencyMs: 0 });
  const el = document.createElement('arazzo-availability-matrix');
  for (const [k, v] of Object.entries(attrs)) el.setAttribute(k, v);
  el.client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
  if (setup) await setup(el.client);
  mount(el);
  return el;
}

const q = (el, sel) => el.shadowRoot.querySelectorAll(sel);

describe('<arazzo-availability-matrix>', () => {
  let el;
  afterEach(() => el?.remove());

  it('renders a versions × environments grid with available / ready / not-ready cells', async () => {
    // nightly-reconcile has 3 versions; add a credential-less 'qa' env → a deterministic not-ready column.
    el = await matrix({ 'base-workflow-id': 'nightly-reconcile', scopes: FULL }, (c) => c.createEnvironment({ name: 'qa' }));
    await nextEvent(el, 'loaded');
    equal(q(el, 'tbody tr').length, 3, 'one row per version');
    equal(q(el, 'thead th').length, 4, 'Version + production + staging + qa');
    ok(q(el, 'button[data-action="make"]').length > 0, 'a ready, not-available cell offers Make available');
    ok(q(el, 'button[data-action="withdraw"]').length > 0, 'a seeded available cell offers Withdraw');
    ok(q(el, '.badge.notready').length > 0, 'the credential-less qa column is not ready');
  });

  it('shows only the selected version row in single-version mode (embedded in the detail)', async () => {
    // nightly-reconcile has 3 versions; single-version scopes the grid to just the selected one's row.
    el = await matrix({ 'base-workflow-id': 'nightly-reconcile', 'selected-version': '2', 'single-version': '', scopes: FULL });
    await nextEvent(el, 'loaded');
    equal(q(el, 'tbody tr').length, 1, 'only the selected version row is shown');
    ok(el.shadowRoot.querySelector('td.ver').textContent.includes('v2'), 'and it is the selected version');
    // The environment columns are still all present (it is a single-version × all-environments row).
    equal(q(el, 'thead th').length, 3, 'Version + production + staging');
  });

  it('makes a version available in a cell and emits availability-changed', async () => {
    el = await matrix({ 'base-workflow-id': 'nightly-reconcile', scopes: FULL });
    await nextEvent(el, 'loaded');
    const make = el.shadowRoot.querySelector('button[data-action="make"]');
    ok(make, 'a Make available action exists');
    const version = Number(make.dataset.version);
    const env = make.dataset.env;
    const changed = nextEvent(el, 'availability-changed');
    make.click();
    const e = await changed;
    equal(e.detail.available, true, 'reported available');
    equal(e.detail.environment, env);
    equal(e.detail.versionNumber, version);
    // The cell flips to available (a Withdraw action for that (version, env)).
    await waitFor(() => el.shadowRoot.querySelector(`button[data-action="withdraw"][data-version="${version}"][data-env="${env}"]`));
  });

  it('withdraws an available cell after confirmation and emits availability-changed', async () => {
    el = await matrix({ 'base-workflow-id': 'nightly-reconcile', scopes: FULL });
    await nextEvent(el, 'loaded');
    const wd = el.shadowRoot.querySelector('button[data-action="withdraw"]');
    ok(wd, 'a seeded available cell offers Withdraw');
    const version = Number(wd.dataset.version);
    const env = wd.dataset.env;
    const changed = nextEvent(el, 'availability-changed');
    wd.click();
    // Drive the themed confirm dialog appended to the shadow root.
    const okBtn = await waitFor(() => el.shadowRoot.querySelector('dialog.arazzo-confirm .ok'));
    okBtn.click();
    const e = await changed;
    equal(e.detail.available, false, 'reported withdrawn');
    await waitFor(() => el.shadowRoot.querySelector(`button[data-action="make"][data-version="${version}"][data-env="${env}"]`));
  });

  it('offers Request promotion (not a direct Make) without availability:write', async () => {
    el = await matrix({ 'base-workflow-id': 'nightly-reconcile', scopes: 'availability:read' });
    await nextEvent(el, 'loaded');
    equal(q(el, 'button[data-action="make"]').length, 0, 'no direct make without availability:write');
    ok(q(el, 'button[data-action="request"]').length > 0, 'a ready cell offers Request…');
  });
});