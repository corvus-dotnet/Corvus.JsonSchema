import { createMockControlPlane, DEMO_PERSONAS } from './mock-api.js';
import { DEMO_SEED } from './demo-seed.js';
import { ArazzoControlPlaneClient } from '../src/arazzo-client.js';
import '../src/arazzo-control-plane.js';
import '../src/arazzo-catalog.js';
import '../src/components/credentials.js';
import '../src/components/access-requests-panel.js';
import '../src/components/access-overview-panel.js';
import '../src/components/availability-requests-panel.js';
import '../src/components/rules-panel.js';
import '../src/components/grants-panel.js';
import '../src/components/environments-panel.js';
import '../src/components/runners-panel.js';
import '../src/components/runner-authorizations-panel.js';

const mock = createMockControlPlane(DEMO_SEED);
const panel = document.querySelector('arazzo-control-plane');
const catalog = document.querySelector('arazzo-catalog');
panel.fetch = mock.fetch; // inject the mock; a real host sets panel.authProvider instead
catalog.fetch = mock.fetch;
// base-url goes on after the fetch hook, never in the markup: a component that has a base-url and no hook yet builds a
// client over the browser's own fetch and loads through it, a request to the mock's made-up origin that the page's
// Content-Security-Policy refuses. The components below take a shared client instead and need no base-url at all.
panel.setAttribute('base-url', 'https://mock/arazzo/v1');
catalog.setAttribute('base-url', 'https://mock/arazzo/v1');

// The Layer-1 credential/administrator components take a Layer-0 client (not a fetch hook); share one
// built over the mock. A real host builds the client with its own auth (getAuthHeader/fetch).
const client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
const credentialsPanel = document.querySelector('arazzo-credentials');
const access = document.querySelector('arazzo-access-requests');
const overview = document.querySelector('arazzo-access-overview');
const promotions = document.querySelector('arazzo-availability-requests');
const reach = document.querySelector('arazzo-rules-panel');
const bindings = document.querySelector('arazzo-grants-panel');
const environments = document.querySelector('arazzo-environments');
const runnersPanel = document.querySelector('arazzo-runners');
const runnerAuth = document.querySelector('arazzo-runner-authorizations');
credentialsPanel.client = client;
access.client = client;
overview.client = client;
promotions.client = client;
reach.client = client;
bindings.client = client;
environments.client = client;
runnersPanel.client = client;
runnerAuth.client = client;

// Credentials: the Connections tab is a MANAGEMENT master-detail surface — select a row to open its record on the RHS and
// view/rotate/edit/duplicate/revoke there (no dialog on select). Creating a credential is rooted in the catalog's
// per-workflow Sources panel (where the source + its auth are known), not here. (§7.5)

// The §15 administrator set now lives on each workflow's detail page (the catalog's Security section), not a
// standalone tab — pick a workflow in the Catalog tab and open it to administer it.

// Theme drives the panels (their `theme` attribute) and the surrounding page (arazzo-kit.css honours
// data-theme on <html>); 'auto' falls back to the OS preference for both.
const applyTheme = (v) => {
  panel.setAttribute('theme', v);
  catalog.setAttribute('theme', v);
  access.setAttribute('theme', v);
  promotions.setAttribute('theme', v);
  runnerAuth.setAttribute('theme', v);
  if (v === 'auto') document.documentElement.removeAttribute('data-theme');
  else document.documentElement.setAttribute('data-theme', v);
};
const themeSel = document.getElementById('theme');
themeSel.addEventListener('change', (e) => applyTheme(e.target.value));
applyTheme(themeSel.value);

// The persona selector drives the WHOLE gating model from one source of truth: it sets each component's capability
// scopes (the real components hide the write controls they lack) AND tells the mock backend which scopes +
// administration the caller has (so it returns the same 401/403/elevation-required responses a real control plane
// would). Switch to Operator to watch direct actions become request-then-approve.
const prefixed = (value, prefix) => value.split(/\s+/).filter((s) => s.startsWith(prefix)).join(' ');
// The capability READ scope each tab requires to be shown at all. Reach (§14.2) narrows the domain-tagged ROWS a
// caller sees (runs + catalog); whole SURFACES are gated by capability scope instead — so a payments read-only
// caller (no security:read) has no Security tab, not just an empty one. Tabs governed by administration
// membership rather than a capability scope (access / promotions / runner-auth) carry no entry here and are always
// shown — their inbox is simply empty for a caller who administers nothing (non-disclosing, like an out-of-reach row).
const TAB_READ_SCOPE = {
  runs: 'runs:read', runners: 'runs:read', catalog: 'catalog:read', credentials: 'credentials:read',
  environments: 'environments:read', security: 'security:read',
};
const applyPersona = (name) => {
  const scopes = DEMO_PERSONAS[name].scopes;
  mock.setPersona(name);
  panel.setAttribute('scopes', prefixed(scopes, 'runs:'));
  // The catalog detail hosts the Security (administrators §15) section and the §7.8 promotion matrix.
  catalog.setAttribute('scopes', `${prefixed(scopes, 'catalog:')} ${prefixed(scopes, 'administrators:')} ${prefixed(scopes, 'availability:')}`.trim());
  credentialsPanel.setAttribute('scopes', prefixed(scopes, 'credentials:')); // gates the detail-pane actions + the guided editor (read-only view when it lacks credentials:write)
  environments.setAttribute('scopes', `${prefixed(scopes, 'environments:')} ${prefixed(scopes, 'availability:')}`.trim());
  reach.setAttribute('scopes', prefixed(scopes, 'security:'));
  bindings.setAttribute('scopes', prefixed(scopes, 'security:'));
  overview.setAttribute('scopes', prefixed(scopes, 'security:')); // the who-can-do-what overview (gates inline Revoke)
  runnersPanel.setAttribute('scopes', prefixed(scopes, 'runs:')); // runs:read; read-only observability
  // The acting subject (what a real host learns from its BFF /me): lets the queues render the
  // independent-decision rule on the caller's own rows instead of decisions that can only 403.
  access.setAttribute('acting-subject', mock.actingSubject());
  promotions.setAttribute('acting-subject', mock.actingSubject());
  // A persona change swaps the caller's scopes AND reach, so reset every surface to page 1 (a stale page/cursor from
  // the previous caller's data must not carry over, and an open detail may now be out of reach). Prefer reload().
  for (const c of [panel, catalog, credentialsPanel, environments, access, promotions, reach, bindings, runnersPanel, runnerAuth]) {
    (c.reload || c.refresh || c.requestRender)?.call(c);
  }
  // Nav honesty: hide any tab whose required read scope this persona lacks, so a surface the caller can't read is
  // absent rather than present-but-empty. If the change hid the active tab, fall back to the first still-visible one.
  const held = new Set(scopes.split(/\s+/).filter(Boolean));
  for (const key of tabNames) {
    const need = TAB_READ_SCOPE[key];
    tabs[key].hidden = need ? !held.has(need) : false;
  }
  const active = tabNames.find((n) => tabs[n].getAttribute('aria-selected') === 'true');
  if (active && tabs[active].hidden) selectTab(tabNames.find((n) => !tabs[n].hidden) || 'runs');
};
const personaSel = document.getElementById('persona');
personaSel.addEventListener('change', (e) => applyPersona(e.target.value));

// Deep links FROM the access overview's Administers lists: Open navigates to the thing itself.
overview.addEventListener('open-workflow', (e) => { selectTab('catalog'); catalog.openWorkflow(e.detail.baseWorkflowId); });
overview.addEventListener('open-environment', (e) => { selectTab('environments'); environments.select(e.detail.environment); });

// Tabs.
const tabNames = ['runs', 'runners', 'catalog', 'credentials', 'environments', 'security', 'access', 'promotions', 'runner-auth'];
const tabs = Object.fromEntries(tabNames.map((n) => [n, document.getElementById(`tab-${n}`)]));
const views = Object.fromEntries(tabNames.map((n) => [n, document.getElementById(`view-${n}`)]));
const selectTab = (name) => {
  for (const key of tabNames) {
    tabs[key].setAttribute('aria-selected', String(key === name));
    views[key].hidden = key !== name;
  }
  // Re-fetch the activated view's panels so a change made under another tab (e.g. a credential set up in the
  // Catalog tab's Sources section) is reflected here — the tabs share one backend but each panel caches its data.
  for (const el of views[name].querySelectorAll('*')) el.refresh?.();
};
for (const name of tabNames) tabs[name].addEventListener('click', () => selectTab(name));

// Secondary tab bars inside a grouped view (the Security area's Grants / Rules / Access overview). One helper drives
// any `.subtabs` bar: a subtab's aria-controls names the subview it toggles; selecting it shows that subview, hides
// its siblings, and refreshes its panels (so a change made elsewhere is reflected when the subtab is re-activated).
for (const tablist of document.querySelectorAll('.subtabs[role="tablist"]')) {
  const buttons = [...tablist.querySelectorAll(':scope > [role="tab"]')];
  const subviews = buttons.map((b) => document.getElementById(b.getAttribute('aria-controls')));
  const selectSub = (active) => buttons.forEach((b, i) => {
    b.setAttribute('aria-selected', String(b === active));
    if (!subviews[i]) return;
    subviews[i].hidden = b !== active;
    if (b === active) for (const el of subviews[i].querySelectorAll('*')) el.refresh?.();
  });
  buttons.forEach((b) => b.addEventListener('click', () => selectSub(b)));
}

// Apply the initial persona now that the tabs exist (applyPersona gates them by the caller's read scopes).
applyPersona(personaSel.value);

// Surface kit events in the console so you can see the composition contract at work.
for (const type of ['run-selected', 'run-changed', 'run-deleted', 'purge-completed', 'error']) {
  panel.addEventListener(type, (e) => console.log(`[arazzo] ${type}`, e.detail));
}
for (const type of ['version-selected', 'workflow-added', 'version-changed', 'version-deleted', 'purge-completed', 'error']) {
  catalog.addEventListener(type, (e) => console.log(`[arazzo:catalog] ${type}`, e.detail));
}
for (const type of ['credential-selected', 'credential-saved', 'credential-deleted', 'error']) {
  credentialsPanel.addEventListener(type, (e) => console.log(`[arazzo:credentials] ${type}`, e.detail));
}
for (const type of ['access-request-submitted', 'access-request-decided', 'loaded', 'error']) {
  access.addEventListener(type, (e) => console.log(`[arazzo:access] ${type}`, e.detail));
}
for (const type of ['availability-request-submitted', 'availability-request-decided', 'loaded', 'error']) {
  promotions.addEventListener(type, (e) => console.log(`[arazzo:promotions] ${type}`, e.detail));
}
for (const type of ['environment-selected', 'environment-created', 'environment-changed', 'environment-deleted', 'loaded', 'error']) {
  environments.addEventListener(type, (e) => console.log(`[arazzo:environments] ${type}`, e.detail));
}
