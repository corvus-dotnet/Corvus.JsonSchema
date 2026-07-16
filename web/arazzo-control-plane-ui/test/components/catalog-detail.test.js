// Tier 3 — <arazzo-catalog-detail> and the <arazzo-catalog> panel against the in-memory mock.
import { ArazzoControlPlaneClient } from '../../src/arazzo-client.js';
import { createMockControlPlane } from '../../demo/mock-api.js';
import '../../src/components/catalog-detail.js';
import '../../src/arazzo-catalog.js';
import { ok, equal, nextEvent, waitFor, mount } from './helpers.js';

function detailWithMock(attrs = {}) {
  const mock = createMockControlPlane({ latencyMs: 0 });
  const el = document.createElement('arazzo-catalog-detail');
  for (const [k, v] of Object.entries(attrs)) el.setAttribute(k, v);
  el.client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
  return el;
}

function panel(scopes) {
  const mock = createMockControlPlane({ latencyMs: 0 });
  const el = document.createElement('arazzo-catalog');
  el.setAttribute('base-url', 'https://mock/arazzo/v1');
  if (scopes != null) el.setAttribute('scopes', scopes);
  el.fetch = mock.fetch; // inject the mock; rebuilds the client
  return el;
}

describe('<arazzo-catalog-detail>', () => {
  let el;
  afterEach(() => el?.remove());

  it('loads a version and renders its metadata, owner, audit and sources', async () => {
    el = detailWithMock({ 'base-workflow-id': 'nightly-reconcile', 'version-number': '1', scopes: 'catalog:read' });
    mount(el);
    await waitFor(() => el.shadowRoot.querySelector('[part="hash"]'));
    ok(el.shadowRoot.querySelector('[part="owner"]'), 'owner block present');
    ok(el.shadowRoot.querySelector('[part="audit"]'), 'audit block present');
    ok(el.shadowRoot.querySelector('[part="sources"]'), 'sources block present');
    equal(el.shadowRoot.querySelector('header .badge').textContent, 'Obsolete', 'status badge reflects the version');
  });

  it('renders the evidence badge from the detail projection (green suite)', async () => {
    el = detailWithMock({ 'base-workflow-id': 'nightly-reconcile', 'version-number': '3', scopes: 'catalog:read' });
    mount(el);
    const dd = await waitFor(() => el.shadowRoot.querySelector('[part="evidence"]'));
    equal(dd.querySelector('.evd').textContent, '3/3 scenarios ✓', 'green badge counts the attested suite');
    ok(dd.querySelector('.evd').classList.contains('evd-ok'), 'a clean suite renders green');
  });

  it('renders a failing suite red', async () => {
    el = detailWithMock({ 'base-workflow-id': 'adopt-pet', 'version-number': '1', scopes: 'catalog:read' });
    mount(el);
    const dd = await waitFor(() => el.shadowRoot.querySelector('[part="evidence"]'));
    equal(dd.querySelector('.evd').textContent, '1/2 scenarios ✗', 'partial suite shows the failure');
    ok(dd.querySelector('.evd').classList.contains('evd-bad'), 'a failing suite renders red');
  });

  it('omits the evidence row for versions without attested evidence', async () => {
    el = detailWithMock({ 'base-workflow-id': 'nightly-reconcile', 'version-number': '1', scopes: 'catalog:read' });
    mount(el);
    await waitFor(() => el.shadowRoot.querySelector('[part="hash"]'));
    ok(!el.shadowRoot.querySelector('[part="evidence"]'), 'no badge without evidence');
  });

  // With catalog:write the management-tags block IS the inline editor, always open and seeded (W3 —
  // consistent with the other panels); there is no read-only chip list or "Edit…" toggle in write mode.
  const rowText = (row) => `${row.querySelector('.tk').value}=${row.querySelector('.tv').value}`;
  const hasRow = (editor, text) => [...editor.shadowRoot.querySelectorAll('.tag-row')].some((r) => rowText(r) === text);

  it('adds a security tag via the editor (Add tag) with catalog:write', async () => {
    el = detailWithMock({ 'base-workflow-id': 'adopt-pet', 'version-number': '1', scopes: 'catalog:read catalog:write' });
    mount(el);
    await waitFor(() => el.shadowRoot.querySelector('[part="security-tags"]'));
    const editor = await waitFor(() => el.shadowRoot.querySelector('#sectag-editor'));
    // Seeded with the existing tag as an editable row.
    await waitFor(() => editor.shadowRoot.querySelectorAll('.tag-row').length === 1);
    ok(hasRow(editor, 'domain=pets'), 'seeded security tag shown in the editor');
    // ADD a brand-new tag via the editor's Add button, then fill the new row.
    editor.shadowRoot.querySelector('.add').click();
    const rows = editor.shadowRoot.querySelectorAll('.tag-row');
    equal(rows.length, 2, 'Add tag appended a row');
    rows[1].querySelector('.tk').value = 'team';
    rows[1].querySelector('.tv').value = 'shelter';
    el.shadowRoot.querySelector('.sectag-save').click();
    // After save the version is re-tagged and the editor re-seeds with both tags.
    await waitFor(() => {
      const ed = el.shadowRoot.querySelector('#sectag-editor');
      return ed && hasRow(ed, 'team=shelter');
    });
    ok(hasRow(el.shadowRoot.querySelector('#sectag-editor'), 'domain=pets'), 'existing tag kept');
  });

  it('deletes a security tag via the editor (✕) with catalog:write', async () => {
    el = detailWithMock({ 'base-workflow-id': 'adopt-pet', 'version-number': '1', scopes: 'catalog:read catalog:write' });
    mount(el);
    await waitFor(() => el.shadowRoot.querySelector('[part="security-tags"]'));
    const editor = await waitFor(() => el.shadowRoot.querySelector('#sectag-editor'));
    await waitFor(() => editor.shadowRoot.querySelectorAll('.tag-row').length === 1);
    // Remove the only row, then save → the version ends up with no security tags.
    editor.shadowRoot.querySelector('.rm').click();
    equal(editor.shadowRoot.querySelectorAll('.tag-row').length, 0, 'row removed');
    el.shadowRoot.querySelector('.sectag-save').click();
    // Write mode always shows the editor; after the delete-save it re-seeds with no rows.
    await waitFor(() => {
      const ed = el.shadowRoot.querySelector('#sectag-editor');
      return ed && ed.shadowRoot.querySelectorAll('.tag-row').length === 0;
    });
    ok(true, 'no tags remain after delete');
  });

  it('shows security tags read-only without catalog:write', async () => {
    el = detailWithMock({ 'base-workflow-id': 'adopt-pet', 'version-number': '1', scopes: 'catalog:read' });
    mount(el);
    const dd = await waitFor(() => el.shadowRoot.querySelector('[part="security-tags"]'));
    ok([...dd.querySelectorAll('.tag')].some((t) => t.textContent === 'domain=pets'), 'the seeded tag renders as a read-only chip');
    ok(!dd.querySelector('#sectag-editor'), 'no editor without catalog:write');
    ok(!dd.querySelector('.sectag-edit'), 'no edit affordance read-only');
  });

  it('shows which environments the version is available in', async () => {
    el = detailWithMock({ 'base-workflow-id': 'adopt-pet', 'version-number': '1', scopes: 'catalog:read' });
    mount(el);
    await waitFor(() => el.shadowRoot.querySelector('[part="availability"]'));
    // adopt-pet v1 is seeded as available in production.
    await waitFor(() => [...el.shadowRoot.querySelectorAll('.avail-env')].some((c) => c.textContent === 'production'));
    ok(true, 'availability lists production');
    ok(!el.shadowRoot.querySelector('.request-promotion'), 'no promote button — it is ready only where it is already available');
  });

  it('offers Request promotion when the version is ready somewhere it is not yet available', async () => {
    el = detailWithMock({ 'base-workflow-id': 'onboard-customer', 'version-number': '1', scopes: 'catalog:read' });
    mount(el);
    // onboard-customer v1 is ready in staging (events credentialed there) and not yet available anywhere → promotable.
    const btn = await waitFor(() => el.shadowRoot.querySelector('.request-promotion'));
    btn.click();
    const dlg = await waitFor(() => el.shadowRoot.querySelector('arazzo-availability-request-dialog'));
    await waitFor(() => dlg.shadowRoot.querySelector('dialog')?.open);
    // Locked to this version; staging is the ready environment offered.
    const envSel = await waitFor(() => dlg.shadowRoot.querySelector('.env-in'));
    await waitFor(() => [...envSel.options].some((o) => o.value === 'staging'));
    ok(true, 'promotion dialog opened locked to the version with its ready environment');
  });

  it('offers a version switcher and loads another version when picked', async () => {
    el = detailWithMock({ 'base-workflow-id': 'nightly-reconcile', 'version-number': '3', scopes: 'catalog:read' });
    mount(el);
    // The switcher appears once the sibling versions load (nightly-reconcile has three).
    const sel = await waitFor(() => {
      const s = el.shadowRoot.querySelector('.version-switch');
      return s && s.options.length === 3 ? s : null;
    });
    equal(Number(sel.value), 3, 'starts on the selected (latest) version');
    // Switch to v1 (the Obsolete one) and confirm the detail reloads to it.
    sel.value = '1';
    sel.dispatchEvent(new Event('change', { bubbles: true }));
    await waitFor(() => el.shadowRoot.querySelector('header .badge')?.textContent === 'Obsolete');
    equal(el.getAttribute('version-number'), '1', 'version-number attribute followed the switch');
  });

  it('hides the switcher for a single-version workflow', async () => {
    el = detailWithMock({ 'base-workflow-id': 'adopt-pet', 'version-number': '1', scopes: 'catalog:read' });
    mount(el);
    await waitFor(() => el.shadowRoot.querySelector('[part="hash"]'));
    // Give loadVersions a moment; adopt-pet has one version so the switcher stays hidden.
    await new Promise((r) => setTimeout(r, 20));
    ok(el.shadowRoot.querySelector('.vswitch').hidden, 'switcher hidden with a single version');
  });

  it('shows a not-found banner for an unknown version', async () => {
    el = detailWithMock({ 'base-workflow-id': 'nightly-reconcile', 'version-number': '99' });
    // The element emits a composed `error` CustomEvent; stop it bubbling to window (the test runner's
    // global error handler would otherwise treat it as an uncaught error).
    el.addEventListener('error', (e) => e.stopPropagation());
    mount(el);
    await waitFor(() => el.shadowRoot.querySelector('.error-banner'));
    ok(el.shadowRoot.textContent.includes('not found'), 'reports not found');
  });

  it('hides governance actions without write/purge scope (scope honesty)', async () => {
    el = detailWithMock({ 'base-workflow-id': 'adopt-pet', 'version-number': '1', scopes: 'catalog:read' });
    mount(el);
    await waitFor(() => el.shadowRoot.querySelector('[part="actions"]'));
    ok(!el.shadowRoot.querySelector('.obsolete'), 'no obsolete button without catalog:write');
    ok(!el.shadowRoot.querySelector('.delete'), 'no delete button without catalog:purge');
  });

  it('shows an editable Security (administrators §15) section keyed by the base workflow', async () => {
    el = detailWithMock({ 'base-workflow-id': 'nightly-reconcile', 'version-number': '1', scopes: 'catalog:read administrators:read administrators:write' });
    mount(el);
    const sec = await waitFor(() => el.shadowRoot.querySelector('.security:not([hidden]) arazzo-administrators-panel'));
    equal(sec.getAttribute('base-workflow-id'), 'nightly-reconcile', 'the admin panel is keyed by the version’s base id');
    await waitFor(() => sec.shadowRoot.querySelector('.arow')); // the seeded administrator loads
    ok(!sec.shadowRoot.querySelector('.add').hidden, 'editable with administrators:write');
    ok(sec.shadowRoot.querySelector('.rm'), 'remove control present when editable');
  });

  it('keeps the Security section read-only without administrators:write', async () => {
    el = detailWithMock({ 'base-workflow-id': 'nightly-reconcile', 'version-number': '1', scopes: 'catalog:read' });
    mount(el);
    const sec = await waitFor(() => el.shadowRoot.querySelector('.security:not([hidden]) arazzo-administrators-panel'));
    await waitFor(() => sec.shadowRoot.querySelector('.arow'));
    ok(sec.shadowRoot.querySelector('.add').hidden, 'add form hidden without administrators:write');
    ok(!sec.shadowRoot.querySelector('.rm'), 'no remove buttons read-only');
  });

  it('offers Request access from the catalog entry, locked to this workflow, and emits access-requested', async () => {
    el = detailWithMock({ 'base-workflow-id': 'nightly-reconcile', 'version-number': '1', scopes: 'catalog:read' });
    mount(el);
    const btn = await waitFor(() => el.shadowRoot.querySelector('.request-access'));
    btn.click();
    const dlg = await waitFor(() => el.shadowRoot.querySelector('arazzo-access-request-dialog'));
    ok(dlg.shadowRoot.querySelector('.locked-wf')?.textContent.includes('nightly-reconcile'), 'the dialog is locked to this workflow');
    const requested = nextEvent(el, 'access-requested');
    dlg.shadowRoot.querySelector('.ok').click();
    const e = await requested;
    equal(e.detail.request.baseWorkflowId, 'nightly-reconcile', 'emits access-requested for the workflow');
  });

  it('obsoletes an active version and emits version-changed', async () => {
    el = detailWithMock({ 'base-workflow-id': 'adopt-pet', 'version-number': '1', scopes: 'catalog:read catalog:write' });
    mount(el);
    await waitFor(() => el.shadowRoot.querySelector('.obsolete'));
    const changed = nextEvent(el, 'version-changed');
    el.shadowRoot.querySelector('.obsolete').click();
    // confirmDialog renders into the shadow root; click its OK button.
    const okBtn = await waitFor(() => el.shadowRoot.querySelector('dialog.arazzo-confirm .ok'));
    okBtn.click();
    const e = await changed;
    equal(e.detail.version.status, 'Obsolete', 'version is now obsolete');
  });

  it('the + credential menu offers New and Copy-per-environment; Copy opens the dialog in duplicate mode', async () => {
    el = detailWithMock({ 'base-workflow-id': 'adopt-pet', 'version-number': '1', scopes: 'catalog:read credentials:read credentials:write' });
    mount(el);
    const plus = await waitFor(() => el.shadowRoot.querySelector('.src[data-name="petstore"] .setup-menu'));
    // Wait for the async binding list so the menu can offer "Copy production…" for the seeded binding.
    await waitFor(() => (el._creds || []).some((b) => b.sourceName === 'petstore'));
    plus.click();
    const menu = await waitFor(() => el.shadowRoot.querySelector('.src[data-name="petstore"] .cred-menu:not([hidden])'));
    const items = [...menu.querySelectorAll('.menu-item')].map((m) => m.textContent);
    ok(items.some((t) => /^New credential/i.test(t)), 'offers New credential…');
    ok(items.some((t) => /^Copy production/i.test(t)), 'offers Copy production…');
    // Copy → the dialog opens in duplicate mode cloned from that environment's binding.
    const dlg = el.shadowRoot.querySelector('arazzo-credential-dialog');
    let captured = null;
    dlg.open = (binding, opts) => { captured = { binding, opts }; };
    menu.querySelector('[data-action="copy"]').click();
    ok(captured?.opts?.duplicate === true, 'Copy opened the dialog in duplicate mode');
    equal(captured.binding?.environment, 'production', 'cloned from the production binding');
  });

  it('the + credential menu New option opens a blank guided setup locked to the source', async () => {
    el = detailWithMock({ 'base-workflow-id': 'adopt-pet', 'version-number': '1', scopes: 'catalog:read credentials:read credentials:write' });
    mount(el);
    const plus = await waitFor(() => el.shadowRoot.querySelector('.src[data-name="petstore"] .setup-menu'));
    plus.click();
    const menu = await waitFor(() => el.shadowRoot.querySelector('.src[data-name="petstore"] .cred-menu:not([hidden])'));
    const dlg = el.shadowRoot.querySelector('arazzo-credential-dialog');
    let captured = null;
    dlg.open = (binding, opts) => { captured = { binding, opts }; };
    menu.querySelector('[data-action="new"]').click();
    await waitFor(() => captured); // setupCredential derives the source doc asynchronously before opening
    ok(!captured.opts?.duplicate, 'New is not a duplicate');
    equal(captured.opts?.sourceName, 'petstore', 'New is locked to the source');
  });

  it('hides the + credential menu without credentials:write (scope honesty)', async () => {
    el = detailWithMock({ 'base-workflow-id': 'adopt-pet', 'version-number': '1', scopes: 'catalog:read credentials:read' });
    mount(el);
    await waitFor(() => el.shadowRoot.querySelector('.src[data-name="petstore"]'));
    equal(el.shadowRoot.querySelector('.setup-menu'), null, 'no + menu without credentials:write');
  });

  // ── "Compare with version…" catalog compare host (visual-diff §9.11) ──────────────────────────
  it('offers "Compare with version…" listing the OTHER versions of a multi-version base', async () => {
    el = detailWithMock({ 'base-workflow-id': 'nightly-reconcile', 'version-number': '3', scopes: 'catalog:read' });
    mount(el);
    const cmp = await waitFor(() => {
      const c = el.shadowRoot.querySelector('.compare-with');
      return c && !el.shadowRoot.querySelector('.vcompare').hidden ? c : null;
    });
    const opts = [...cmp.options].map((o) => o.value);
    ok(opts.includes('1') && opts.includes('2'), 'lists the sibling versions');
    ok(!opts.includes('3'), 'excludes the current version');
  });

  it('hides the compare affordance for a single-version base', async () => {
    el = detailWithMock({ 'base-workflow-id': 'onboard-customer', 'version-number': '1', scopes: 'catalog:read' });
    mount(el);
    await waitFor(() => el.shadowRoot.querySelector('[part="hash"]'));
    await waitFor(() => el.versions); // versions list resolved
    ok(el.shadowRoot.querySelector('.vcompare').hidden, 'no compare picker with one version');
  });

  it('selecting a version opens the read-only compare dialog with both documents diffed', async () => {
    el = detailWithMock({ 'base-workflow-id': 'nightly-reconcile', 'version-number': '3', scopes: 'catalog:read' });
    mount(el);
    const cmp = await waitFor(() => {
      const c = el.shadowRoot.querySelector('.compare-with');
      return c && [...c.options].some((o) => o.value === '1') ? c : null;
    });
    cmp.value = '1';
    cmp.dispatchEvent(new Event('change'));
    equal(cmp.value, '', 'the picker resets to its placeholder (acts as an action)');
    const compare = el.shadowRoot.querySelector('arazzo-workflow-compare');
    const dialog = await waitFor(() => compare.shadowRoot.querySelector('dialog[open]'));
    ok(dialog, 'the shared compare dialog opened');
    const legend = compare.shadowRoot.querySelector('.legend').textContent;
    ok(legend && legend !== 'No differences in this workflow', `the legend reports differences (${legend})`);
    // v1 (predates flagDiscrepancies + postCorrections) → v3 adds them: painted on the newer (right) side.
    const right = compare.shadowRoot.querySelector('.side-right arazzo-design-surface');
    await waitFor(() => right.shadowRoot.querySelector('.node.df-added'));
    ok(right.shadowRoot.querySelector('.node.df-added'), 'an added step is painted on the newer side');
    ok(right.hasAttribute('readonly'), 'the compared surface is read-only (no merge target)');
  });
});

describe('<arazzo-catalog> panel', () => {
  let el;
  afterEach(() => el?.remove());

  it('composes a catalog-table and loads versions', async () => {
    el = panel('catalog:read catalog:write catalog:purge');
    mount(el);
    const table = el.shadowRoot.querySelector('arazzo-catalog-table');
    ok(table, 'embeds the catalog table');
    await nextEvent(table, 'loaded');
    ok(table.shadowRoot.querySelectorAll('tbody tr[data-key]').length > 0, 'rows loaded');
  });

  it('hides Purge unless catalog:purge is granted (scope honesty)', async () => {
    el = panel('catalog:read catalog:write');
    mount(el);
    ok(el.shadowRoot.querySelector('.purge-btn').hidden, 'purge hidden without catalog:purge');
    el.setAttribute('scopes', 'catalog:read catalog:write catalog:purge');
    await waitFor(() => !el.shadowRoot.querySelector('.purge-btn').hidden);
    ok(true, 'purge shown once granted');
  });

  it('selecting a row opens the detail pane', async () => {
    el = panel('catalog:read');
    mount(el);
    const table = el.shadowRoot.querySelector('arazzo-catalog-table');
    await nextEvent(table, 'loaded');
    table.shadowRoot.querySelector('tbody tr[data-key]').click();
    const detail = await waitFor(() => el.shadowRoot.querySelector('arazzo-catalog-detail'));
    ok(detail, 'detail pane populated');
    await waitFor(() => detail.shadowRoot.querySelector('[part="hash"]'));
    ok(detail.shadowRoot.querySelector('[part="hash"]'), 'authoritative detail loaded');
  });

  it('the search input sets the table q attribute', async () => {
    el = panel('catalog:read');
    mount(el);
    const table = el.shadowRoot.querySelector('arazzo-catalog-table');
    await nextEvent(table, 'loaded');
    const input = el.shadowRoot.querySelector('.q-search');
    input.value = 'reconcile';
    input.dispatchEvent(new Event('input', { bubbles: true }));
    await waitFor(() => table.getAttribute('q') === 'reconcile');
    ok(true);
  });
});
