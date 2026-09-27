import { ArazzoControlPlaneClient } from '/ui/src/arazzo-client.js';
import '/ui/src/arazzo-control-plane.js';
import '/ui/src/arazzo-catalog.js';
import '/ui/src/components/environments-panel.js';
import '/ui/src/components/sources-panel.js';
import '/ui/src/components/credentials.js';
import '/ui/src/components/runners-panel.js';
import '/ui/src/components/schedules-panel.js';
import '/ui/src/components/runner-authorizations-panel.js';
import '/ui/src/components/grants-panel.js';
import '/ui/src/components/rules-panel.js';
import '/ui/src/components/access-overview-panel.js';
import '/ui/src/components/access-requests-panel.js';
import '/ui/src/components/availability-requests-panel.js';
import '/ui/src/components/auth-status.js';

// BFF auth (§16.3). When the host runs with authorization on, an API 401 means "not signed in" — bounce the
// browser to /login (the OIDC challenge). When the host runs open (the default), the API returns 200 and
// /me is absent (404), so this is inert and the sign-in bar stays hidden. The cookie travels same-origin.
const loginUrl = () => '/login?returnUrl=' + encodeURIComponent(location.pathname + location.search);
const authFetch = async (input, init = {}) => {
  // X-CSRF anti-forgery (§16.3): the server requires this header on cookie-authenticated state-changing
  // calls; sending it on every request is harmless and forces a CORS preflight that isolates cross-origin.
  const headers = new Headers(init.headers || {});
  headers.set('X-CSRF', '1');
  const res = await fetch(input, { credentials: 'include', ...init, headers });
  if (res.status === 401) { location.assign(loginUrl()); return new Promise(() => {}); }
  return res;
};

// Wire every mounted panel to the BFF fetch. Panels split into two families: the self-contained ones build their
// own client from a `fetch` property (Runs, Catalog, Environments, Sources, Credentials, Runners, Runner-auth,
// the two request inboxes); the security-authoring panels (Grants, Rules, Access overview) consume an injected
// `.client`. One shared client serves the latter. Both add the X-CSRF header + same-origin credentials.
const sharedClient = new ArazzoControlPlaneClient({ baseUrl: '/arazzo/v1', fetch: authFetch });
const FETCH_PANELS = [
  'arazzo-control-plane', 'arazzo-catalog', 'arazzo-environments', 'arazzo-sources', 'arazzo-credentials',
  'arazzo-runners', 'arazzo-schedules', 'arazzo-runner-authorizations', 'arazzo-access-requests', 'arazzo-availability-requests',
];
const CLIENT_PANELS = ['arazzo-grants-panel', 'arazzo-rules-panel', 'arazzo-access-overview'];
FETCH_PANELS.forEach((tag) => document.querySelectorAll(tag).forEach((el) => { el.fetch = authFetch; }));
CLIENT_PANELS.forEach((tag) => document.querySelectorAll(tag).forEach((el) => { el.client = sharedClient; }));
const allPanels = [...FETCH_PANELS, ...CLIENT_PANELS].flatMap((tag) => [...document.querySelectorAll(tag)]);

// The acting subject (the BFF's /me name is the same preferred_username the server keys requests on) lets the
// request queues render the independent-decision rule on the caller's own rows — disabled decisions with the
// reason — instead of buttons that can only 403. If /me is unavailable the attribute stays unset and the
// server's own-request refusal remains the (sole) backstop.
fetch('/me', { credentials: 'include' })
  .then((r) => (r.ok ? r.json() : null))
  .then((me) => {
    if (me?.name) {
      document.querySelectorAll('arazzo-access-requests, arazzo-availability-requests')
        .forEach((el) => el.setAttribute('acting-subject', me.name));
    }
  })
  .catch(() => { /* no BFF: server-side enforcement only */ });

// Approvals badges: poll the three approver queues' bounded /count endpoints for outstanding (Pending) work and badge
// the Approvals tab + its sub-tabs, so the user sees at a glance whether there is anything to action. Auto-refreshes;
// a 403 (the user administers nothing) just yields 0 and hides the badge. The count is bounded by the server's cap —
// when it is hit the response's `capped` flag renders "N+" (rather than fetching rows just to count them).
const badges = {};
document.querySelectorAll('.tab-badge[data-badge]').forEach((b) => { badges[b.dataset.badge] = b; });
const setBadge = (b, count, capped) => { if (b) { b.textContent = capped ? `${count}+` : String(count); b.hidden = !(count > 0); } };
const countQueue = async (call) => { try { return await call(); } catch { return { count: 0, capped: false }; } };
const refreshApprovalBadges = async () => {
  const [access, avail, runners] = await Promise.all([
    countQueue(() => sharedClient.countAccessRequests({ scope: 'queue', status: 'Pending' })),
    countQueue(() => sharedClient.countAvailabilityRequests({ scope: 'queue', status: 'Pending' })),
    countQueue(() => sharedClient.countRunnerAuthorizations({ status: 'Pending' })),
  ]);
  setBadge(badges['approvals-access'], access.count, access.capped);
  setBadge(badges['approvals-availability'], avail.count, avail.capped);
  setBadge(badges['approvals-runners'], runners.count, runners.capped);
  // The tab total sums the three; it is "N+" if the sum overflows or any queue was itself capped.
  setBadge(badges['approvals'], access.count + avail.count + runners.count, access.capped || avail.capped || runners.capped);
};
refreshApprovalBadges();
setInterval(refreshApprovalBadges, 15000);

// Sign-in / sign-out chrome is the shared <arazzo-auth-status> element in the title bar (it self-discovers
// /me and stays invisible when authorization is off). The same element serves the designer (§16.3).

// Tri-state theme toggle: one title-bar button cycling System → Light → Dark, showing the current mode's icon.
// Theme drives the panels (their `theme` attribute) and the page (arazzo-kit.css honours data-theme on <html>);
// 'auto' clears data-theme so the kit follows the OS preference. The choice persists across reloads.
const THEME_ORDER = ['auto', 'light', 'dark'];
const THEME_META = {
  auto: { label: 'System', icon: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="2" y="4" width="20" height="14" rx="2"/><path d="M8 21h8M12 18v3"/></svg>' },
  light: { label: 'Light', icon: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4"/></svg>' },
  dark: { label: 'Dark', icon: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 12.8A9 9 0 1 1 11.2 3 7 7 0 0 0 21 12.8z"/></svg>' },
};
const themeToggle = document.getElementById('theme-toggle');
const applyTheme = (v) => {
  allPanels.forEach((el) => el.setAttribute('theme', v));
  if (v === 'auto') document.documentElement.removeAttribute('data-theme');
  else document.documentElement.setAttribute('data-theme', v);
  const meta = THEME_META[v];
  themeToggle.innerHTML = meta.icon;
  themeToggle.setAttribute('aria-label', `Theme: ${meta.label} (click to change)`);
  themeToggle.title = `Theme: ${meta.label} — click to switch`;
};
let theme = localStorage.getItem('arazzo-theme');
if (!THEME_ORDER.includes(theme)) { theme = 'auto'; }
applyTheme(theme);
themeToggle.addEventListener('click', () => {
  theme = THEME_ORDER[(THEME_ORDER.indexOf(theme) + 1) % THEME_ORDER.length];
  localStorage.setItem('arazzo-theme', theme);
  applyTheme(theme);
});

// Tabs. One helper drives both the primary tab bar and each grouped view's secondary bar: a tab's
// aria-controls names the view element it toggles; selecting a tab shows its view and hides its siblings.
const wireTabGroup = (tablist) => {
  const buttons = [...tablist.querySelectorAll(':scope > [role="tab"]')];
  const views = buttons.map((b) => document.getElementById(b.getAttribute('aria-controls')));
  const select = (active) => buttons.forEach((b, i) => {
    b.setAttribute('aria-selected', String(b === active));
    if (views[i]) views[i].hidden = b !== active;
  });
  buttons.forEach((b) => b.addEventListener('click', () => select(b)));
};
document.querySelectorAll('[role="tablist"]').forEach(wireTabGroup);

// Deep links FROM the access overview's Administers lists: Open navigates to the thing itself —
// the catalog detail for a workflow, the environment detail for an environment.
const clickTab = (viewId) => document.querySelector(`[role="tab"][aria-controls="${viewId}"]`)?.click();
const overviewPanel = document.querySelector('arazzo-access-overview');
overviewPanel?.addEventListener('open-workflow', (e) => {
  clickTab('view-catalog');
  document.querySelector('arazzo-catalog')?.openWorkflow(e.detail.baseWorkflowId);
});
overviewPanel?.addEventListener('open-environment', (e) => {
  clickTab('view-environments');
  document.querySelector('arazzo-environments')?.select(e.detail.environment);
});
