// Onboarding console — the <onboarding-console> custom element.
//
// A build-free web component (Shadow DOM) rendering the onboarding product's operator console: the paged list of
// onboarded customers and, per account, the expandable journey (created -> identity-verified/blocked -> provisioned
// -> welcomed). It reads the onboarding service through OnboardingClient. This is a separate app from the Arazzo
// control-plane UI; it merely consumes the workflow engine's effects.
//
//   <link rel="stylesheet" href="onboarding-kit.css">
//   <onboarding-console poll="5"></onboarding-console>
//
// Attributes: `base-url` (default '' — same origin), `limit` (page size, default 50), `poll` (auto-refresh seconds).

import { OnboardingClient, ProblemError } from './onboarding-client.js';

const STATUS_ORDER = ['created', 'verified', 'blocked', 'provisioned', 'welcomed'];
const STATUS_LABEL = {
  created: 'Created',
  verified: 'Verified',
  blocked: 'Blocked',
  provisioned: 'Provisioned',
  welcomed: 'Welcomed',
};

class OnboardingConsole extends HTMLElement {
  constructor() {
    super();
    this.attachShadow({ mode: 'open' });
    /** @private */ this._client = null;
    /** @private @type {object[]} */ this._accounts = [];
    /** @private */ this._nextPageToken = null;
    /** @private @type {Set<string>} */ this._expanded = new Set();
    /** @private */ this._filter = 'all';
    /** @private */ this._error = null;
    /** @private */ this._loading = false;
    /** @private */ this._pollTimer = null;
    /** @private */ this._abort = null;
  }

  connectedCallback() {
    this._client = new OnboardingClient({ baseUrl: this.getAttribute('base-url') ?? '' });
    this._renderShell();
    this._loadFirstPage();
    const poll = Number(this.getAttribute('poll'));
    if (Number.isFinite(poll) && poll > 0) {
      this._pollTimer = setInterval(() => this._loadFirstPage({ quiet: true }), poll * 1000);
    }
  }

  disconnectedCallback() {
    if (this._pollTimer) {
      clearInterval(this._pollTimer);
      this._pollTimer = null;
    }

    this._abort?.abort();
  }

  // ---- data ------------------------------------------------------------------------------------

  /** @private Loads (or reloads) the first page; `quiet` keeps the current view while a background poll refreshes. */
  async _loadFirstPage({ quiet = false } = {}) {
    if (this._loading) {
      return;
    }

    this._loading = true;
    this._abort?.abort();
    this._abort = new AbortController();
    if (!quiet) {
      this._error = null;
      this._renderBody();
    }

    try {
      const limit = Number(this.getAttribute('limit')) || 50;
      const page = await this._client.listAccounts({ limit, signal: this._abort.signal });
      this._accounts = page.accounts;
      this._nextPageToken = page.nextPageToken;
      this._error = null;
    } catch (err) {
      if (err?.name === 'AbortError') {
        return;
      }

      this._error = err instanceof ProblemError ? err.message : String(err?.message ?? err);
    } finally {
      this._loading = false;
      this._renderBody();
    }
  }

  /** @private Appends the next keyset page. */
  async _loadMore() {
    if (this._loading || !this._nextPageToken) {
      return;
    }

    this._loading = true;
    this._renderBody();
    try {
      const limit = Number(this.getAttribute('limit')) || 50;
      const page = await this._client.listAccounts({ limit, pageToken: this._nextPageToken });
      this._accounts = this._accounts.concat(page.accounts);
      this._nextPageToken = page.nextPageToken;
    } catch (err) {
      this._error = err instanceof ProblemError ? err.message : String(err?.message ?? err);
    } finally {
      this._loading = false;
      this._renderBody();
    }
  }

  // ---- rendering -------------------------------------------------------------------------------

  /** @private Builds the static shell once (styles, header, body container). */
  _renderShell() {
    const root = this.shadowRoot;
    root.textContent = '';

    const style = document.createElement('style');
    style.textContent = STYLES;
    root.appendChild(style);

    const header = document.createElement('header');
    header.className = 'app-header';
    header.innerHTML = `
      <div class="titles">
        <h1>Onboarding console</h1>
        <p class="sub">Customers onboarded through the <code>onboard-customer</code> workflow.</p>
      </div>
      <div class="toolbar">
        <label class="filter">Status
          <select class="status-filter" aria-label="Filter by status"></select>
        </label>
        <button class="refresh" type="button" title="Refresh">Refresh</button>
        <span class="live" hidden>live</span>
      </div>`;
    root.appendChild(header);

    const stats = document.createElement('div');
    stats.className = 'stats';
    root.appendChild(stats);

    const body = document.createElement('div');
    body.className = 'body';
    root.appendChild(body);

    // Wire the toolbar.
    const filter = header.querySelector('.status-filter');
    for (const value of ['all', ...STATUS_ORDER]) {
      const opt = document.createElement('option');
      opt.value = value;
      opt.textContent = value === 'all' ? 'All' : STATUS_LABEL[value];
      filter.appendChild(opt);
    }

    filter.value = this._filter;
    filter.addEventListener('change', () => {
      this._filter = filter.value;
      this._renderBody();
    });
    header.querySelector('.refresh').addEventListener('click', () => this._loadFirstPage());
    header.querySelector('.live').hidden = !(Number(this.getAttribute('poll')) > 0);

    this._statsEl = stats;
    this._bodyEl = body;
    this._renderBody();
  }

  /** @private Re-renders the stats + account list from current state. */
  _renderBody() {
    if (!this._bodyEl) {
      return;
    }

    this._renderStats();

    const body = this._bodyEl;
    body.textContent = '';

    if (this._error) {
      const err = document.createElement('div');
      err.className = 'notice error';
      err.textContent = `Could not load accounts: ${this._error}`;
      body.appendChild(err);
      return;
    }

    const visible = this._filter === 'all'
      ? this._accounts
      : this._accounts.filter((a) => a.status === this._filter);

    if (visible.length === 0) {
      const empty = document.createElement('div');
      empty.className = 'notice';
      empty.textContent = this._accounts.length === 0 && !this._loading
        ? 'No accounts yet. Trigger an onboard-customer run to onboard a customer.'
        : this._loading
          ? 'Loading…'
          : 'No accounts match this filter.';
      body.appendChild(empty);
      return;
    }

    const list = document.createElement('div');
    list.className = 'list';
    for (const account of visible) {
      list.appendChild(this._accountCard(account));
    }

    body.appendChild(list);

    if (this._nextPageToken) {
      const more = document.createElement('button');
      more.className = 'load-more';
      more.type = 'button';
      more.textContent = this._loading ? 'Loading…' : 'Load more';
      more.disabled = this._loading;
      more.addEventListener('click', () => this._loadMore());
      body.appendChild(more);
    }
  }

  /** @private A summary line of counts per status. */
  _renderStats() {
    const stats = this._statsEl;
    if (!stats) {
      return;
    }

    stats.textContent = '';
    const counts = new Map();
    for (const a of this._accounts) {
      counts.set(a.status, (counts.get(a.status) ?? 0) + 1);
    }

    const total = document.createElement('span');
    total.className = 'stat total';
    total.textContent = `${this._accounts.length} account${this._accounts.length === 1 ? '' : 's'}`;
    stats.appendChild(total);

    for (const status of STATUS_ORDER) {
      const n = counts.get(status);
      if (!n) {
        continue;
      }

      const chip = document.createElement('span');
      chip.className = 'stat';
      chip.style.setProperty('--dot', `var(--onb-status-${status})`);
      chip.innerHTML = `<i class="dot"></i>${n} ${STATUS_LABEL[status].toLowerCase()}`;
      stats.appendChild(chip);
    }
  }

  /** @private One account card: a summary row plus its expandable journey. */
  _accountCard(account) {
    const card = document.createElement('div');
    card.className = 'card';

    const summary = document.createElement('button');
    summary.className = 'summary';
    summary.type = 'button';
    summary.setAttribute('aria-expanded', String(this._expanded.has(account.accountId)));

    const badge = document.createElement('span');
    badge.className = 'badge';
    badge.style.setProperty('--c', `var(--onb-status-${account.status})`);
    badge.textContent = STATUS_LABEL[account.status] ?? account.status;

    const name = document.createElement('span');
    name.className = 'name';
    name.textContent = applicantName(account) ?? '(no applicant yet)';

    const id = document.createElement('span');
    id.className = 'id';
    id.textContent = shortId(account.accountId);
    id.title = account.accountId;

    const when = document.createElement('span');
    when.className = 'when';
    when.textContent = formatWhen(account.welcomedAt ?? account.provisionedAt ?? account.verifiedAt ?? account.submittedAt);
    when.title = account.submittedAt ?? '';

    const chevron = document.createElement('span');
    chevron.className = 'chevron';
    chevron.setAttribute('aria-hidden', 'true');

    summary.append(badge, name, id, when, chevron);

    const detail = document.createElement('div');
    detail.className = 'detail';
    detail.hidden = !this._expanded.has(account.accountId);
    if (!detail.hidden) {
      detail.appendChild(this._journey(account));
    }

    summary.addEventListener('click', () => {
      const open = this._expanded.has(account.accountId);
      if (open) {
        this._expanded.delete(account.accountId);
        detail.hidden = true;
        detail.textContent = '';
      } else {
        this._expanded.add(account.accountId);
        detail.textContent = '';
        detail.appendChild(this._journey(account));
        detail.hidden = false;
      }

      summary.setAttribute('aria-expanded', String(!open));
    });

    card.append(summary, detail);
    return card;
  }

  /** @private The account's journey through the onboarding stages. */
  _journey(account) {
    const wrap = document.createElement('div');
    wrap.className = 'journey';

    // Stage 1 — account created.
    wrap.appendChild(stage('created', 'Account created', account.submittedAt, [
      field('Account id', account.accountId, true),
    ]));

    // Stage 2 — identity verification (present once verifyIdentity has run).
    const identity = account.identity;
    if (identity) {
      const verified = identity.verified === true;
      const flags = Array.isArray(identity.flags) ? identity.flags : [];
      const applicant = identity.applicant ?? account.applicant ?? {};
      const evidence = identity.evidence ?? {};
      const rows = [
        field('Outcome', verified ? 'Verified' : (flags.length ? `Blocked (${flags.join(', ')})` : 'Not verified')),
        field('Confidence', typeof identity.score === 'number' ? `${Math.round(identity.score * 100)}%` : '—'),
        field('Method', identity.method ?? '—'),
        field('Applicant', [applicant.fullName, applicant.country, applicant.email].filter(Boolean).join(' · ') || '—'),
        field('Evidence', evidenceLabel(evidence)),
      ];
      wrap.appendChild(stage(verified ? 'verified' : 'blocked', 'Identity verification', account.verifiedAt, rows));
    }

    // Stage 3 — provisioning.
    const provisioning = account.provisioning;
    if (provisioning) {
      const resources = Array.isArray(provisioning.resources) ? provisioning.resources : [];
      const rows = [
        field('Account URL', provisioning.accountUrl ?? '—'),
        field('Quota', provisioning.quotaGb != null ? `${provisioning.quotaGb} GB` : '—'),
        field('Resources', resources.map((r) => `${r.kind} “${r.name}” (${r.region ?? '—'})`).join('; ') || '—'),
        field('Tags', formatTags(provisioning.tags)),
      ];
      wrap.appendChild(stage('provisioned', 'Resources provisioned', account.provisionedAt, rows));
    }

    // Stage 4 — welcome.
    if (account.welcomedAt) {
      wrap.appendChild(stage('welcomed', 'Welcome sent', account.welcomedAt, []));
    }

    return wrap;
  }
}

// ---- small DOM builders ------------------------------------------------------------------------

function stage(status, title, when, rows) {
  const el = document.createElement('div');
  el.className = 'stage';
  el.style.setProperty('--c', `var(--onb-status-${status})`);

  const head = document.createElement('div');
  head.className = 'stage-head';
  const dot = document.createElement('i');
  dot.className = 'stage-dot';
  const h = document.createElement('span');
  h.className = 'stage-title';
  h.textContent = title;
  const t = document.createElement('span');
  t.className = 'stage-when';
  t.textContent = when ? formatFull(when) : '';
  head.append(dot, h, t);
  el.appendChild(head);

  if (rows.length) {
    const dl = document.createElement('dl');
    dl.className = 'fields';
    for (const row of rows) {
      dl.appendChild(row);
    }

    el.appendChild(dl);
  }

  return el;
}

function field(label, value, mono = false) {
  const frag = document.createDocumentFragment();
  const dt = document.createElement('dt');
  dt.textContent = label;
  const dd = document.createElement('dd');
  dd.textContent = value == null || value === '' ? '—' : String(value);
  if (mono) {
    dd.className = 'mono';
  }

  frag.append(dt, dd);
  return frag;
}

// ---- data helpers ------------------------------------------------------------------------------

function applicantName(account) {
  return account.applicant?.fullName ?? account.identity?.applicant?.fullName ?? null;
}

function shortId(id) {
  return typeof id === 'string' && id.length > 8 ? id.slice(0, 8) : (id ?? '');
}

function evidenceLabel(evidence) {
  switch (evidence.kind) {
    case 'document':
      return `Document · ${evidence.documentType ?? 'document'} ${evidence.documentNumber ?? ''}`.trim();
    case 'biometric':
      return `Biometric · ${evidence.modality ?? ''}`.trim();
    case 'knowledge-based':
      return `Knowledge-based · ${evidence.questionsPassed ?? 0} passed`;
    default:
      return '—';
  }
}

function formatTags(tags) {
  if (!tags || typeof tags !== 'object') {
    return '—';
  }

  const entries = Object.entries(tags);
  return entries.length ? entries.map(([k, v]) => `${k}=${v}`).join(', ') : '—';
}

function formatWhen(iso) {
  if (!iso) {
    return '';
  }

  const then = new Date(iso);
  if (Number.isNaN(then.getTime())) {
    return '';
  }

  const secs = Math.round((Date.now() - then.getTime()) / 1000);
  if (secs < 60) {
    return 'just now';
  }

  if (secs < 3600) {
    return `${Math.floor(secs / 60)}m ago`;
  }

  if (secs < 86400) {
    return `${Math.floor(secs / 3600)}h ago`;
  }

  return then.toLocaleDateString();
}

function formatFull(iso) {
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? '' : d.toLocaleString();
}

const STYLES = `
:host { display: block; box-sizing: border-box; max-width: 960px; margin: 0 auto; padding: 24px 20px 64px; color: var(--onb-text, #1a1d21); }
:host *, :host *::before, :host *::after { box-sizing: border-box; }
h1 { font-size: 1.4rem; margin: 0; }
.sub { margin: 4px 0 0; color: var(--onb-muted, #6b7280); font-size: .9rem; }
.sub code { font-family: var(--onb-mono, monospace); font-size: .85em; }
.app-header { display: flex; align-items: flex-start; justify-content: space-between; gap: 16px; flex-wrap: wrap; margin-bottom: 12px; }
.toolbar { display: flex; align-items: center; gap: 10px; }
.filter { display: flex; align-items: center; gap: 6px; font-size: .8rem; color: var(--onb-muted, #6b7280); }
select, button { font: inherit; color: inherit; }
select { background: var(--onb-surface, #fff); border: 1px solid var(--onb-border, #e1e5ea); border-radius: 8px; padding: 5px 8px; }
.refresh { background: var(--onb-surface, #fff); border: 1px solid var(--onb-border, #e1e5ea); border-radius: 8px; padding: 6px 12px; cursor: pointer; }
.refresh:hover { border-color: var(--onb-accent, #3b6cf6); color: var(--onb-accent, #3b6cf6); }
.live { font-size: .7rem; text-transform: uppercase; letter-spacing: .06em; color: var(--onb-status-welcomed, #2a8a4a); display: inline-flex; align-items: center; gap: 5px; }
.live::before { content: ""; width: 7px; height: 7px; border-radius: 50%; background: currentColor; animation: pulse 2s ease-in-out infinite; }
@keyframes pulse { 0%, 100% { opacity: 1; } 50% { opacity: .3; } }
.stats { display: flex; flex-wrap: wrap; gap: 8px 14px; margin-bottom: 14px; font-size: .82rem; color: var(--onb-muted, #6b7280); }
.stat { display: inline-flex; align-items: center; gap: 6px; }
.stat.total { font-weight: 600; color: var(--onb-text, #1a1d21); }
.stat .dot { width: 8px; height: 8px; border-radius: 50%; background: var(--dot, currentColor); }
.list { display: flex; flex-direction: column; gap: 8px; }
.card { background: var(--onb-surface, #fff); border: 1px solid var(--onb-border, #e1e5ea); border-radius: var(--onb-radius, 10px); overflow: hidden; }
.summary { width: 100%; display: grid; grid-template-columns: auto minmax(0, 1fr) auto auto auto; align-items: center; gap: 12px; padding: 12px 14px; background: none; border: 0; cursor: pointer; text-align: left; }
.summary:hover { background: var(--onb-surface-2, #f2f4f8); }
.badge { font-size: .72rem; font-weight: 600; padding: 3px 9px; border-radius: 999px; color: #fff; background: var(--c, #888); white-space: nowrap; }
.name { font-weight: 550; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.id { font-family: var(--onb-mono, monospace); font-size: .78rem; color: var(--onb-muted, #6b7280); }
.when { font-size: .78rem; color: var(--onb-muted, #6b7280); white-space: nowrap; }
.chevron { width: 8px; height: 8px; border-right: 2px solid var(--onb-muted, #6b7280); border-bottom: 2px solid var(--onb-muted, #6b7280); transform: rotate(45deg); transition: transform .15s; }
.summary[aria-expanded="true"] .chevron { transform: rotate(225deg); }
.detail { padding: 4px 14px 14px; border-top: 1px solid var(--onb-border, #e1e5ea); }
.journey { display: flex; flex-direction: column; }
.stage { position: relative; padding: 12px 0 12px 22px; }
.stage:not(:last-child)::before { content: ""; position: absolute; left: 4px; top: 20px; bottom: -2px; width: 2px; background: var(--onb-border, #e1e5ea); }
.stage-dot { position: absolute; left: 0; top: 15px; width: 10px; height: 10px; border-radius: 50%; background: var(--c, #888); }
.stage-head { display: flex; align-items: baseline; gap: 10px; flex-wrap: wrap; }
.stage-title { font-weight: 600; font-size: .9rem; }
.stage-when { font-size: .75rem; color: var(--onb-muted, #6b7280); }
.fields { display: grid; grid-template-columns: minmax(90px, auto) 1fr; gap: 3px 14px; margin: 8px 0 0; font-size: .84rem; }
.fields dt { color: var(--onb-muted, #6b7280); }
.fields dd { margin: 0; overflow-wrap: anywhere; }
.fields dd.mono { font-family: var(--onb-mono, monospace); font-size: .82em; }
.notice { padding: 32px 16px; text-align: center; color: var(--onb-muted, #6b7280); background: var(--onb-surface, #fff); border: 1px dashed var(--onb-border, #e1e5ea); border-radius: var(--onb-radius, 10px); }
.notice.error { color: var(--onb-danger, #d4351c); border-color: var(--onb-danger, #d4351c); border-style: solid; }
.load-more { display: block; margin: 14px auto 0; background: var(--onb-surface, #fff); border: 1px solid var(--onb-border, #e1e5ea); border-radius: 8px; padding: 8px 18px; cursor: pointer; }
.load-more:hover:not(:disabled) { border-color: var(--onb-accent, #3b6cf6); }
`;

customElements.define('onboarding-console', OnboardingConsole);

export { OnboardingConsole };
