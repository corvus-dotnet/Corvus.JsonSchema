// KYC manual-recovery console — the <kyc-console> custom element.
//
// A build-free web component (Shadow DOM) rendering the KYC service's operator console: the paged list of identity
// verifications, and — for each PENDING one (an out-of-band review the onboard-customer-async workflow is suspended
// awaiting) — an inline "submit verdict" form. Submitting publishes the verdict onto the bus, which resumes the
// workflow run. Verified/blocked records show the resolved identity. It reads + writes the KYC service through
// KycClient. This is a separate app from the Arazzo control-plane UI; it merely consumes the workflow engine.
//
//   <link rel="stylesheet" href="kyc-kit.css">
//   <kyc-console poll="5"></kyc-console>
//
// Attributes: `base-url` (default '' — same origin), `limit` (page size, default 50), `poll` (auto-refresh seconds).

import { KycClient, ProblemError } from './kyc-client.js';

const STATUS_ORDER = ['pending', 'verified', 'blocked'];
const STATUS_LABEL = { pending: 'Pending', verified: 'Verified', blocked: 'Blocked' };

class KycConsole extends HTMLElement {
  constructor() {
    super();
    this.attachShadow({ mode: 'open' });
    /** @private */ this._client = null;
    /** @private @type {object[]} */ this._verifications = [];
    /** @private */ this._nextPageToken = null;
    /** @private @type {Set<string>} */ this._expanded = new Set();
    /** @private */ this._filter = 'all';
    /** @private */ this._error = null;
    /** @private */ this._loading = false;
    /** @private */ this._submitting = null;
    /** @private */ this._pollTimer = null;
    /** @private */ this._abort = null;
  }

  connectedCallback() {
    this._client = new KycClient({ baseUrl: this.getAttribute('base-url') ?? '' });
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
      const page = await this._client.listVerifications({ limit, signal: this._abort.signal });
      this._verifications = page.verifications;
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
      const page = await this._client.listVerifications({ limit, pageToken: this._nextPageToken });
      this._verifications = this._verifications.concat(page.verifications);
      this._nextPageToken = page.nextPageToken;
    } catch (err) {
      this._error = err instanceof ProblemError ? err.message : String(err?.message ?? err);
    } finally {
      this._loading = false;
      this._renderBody();
    }
  }

  /** @private Submits an operator verdict for a pending review, then reloads. */
  async _submitVerdict(accountId, verified, score, fullName) {
    if (this._submitting) {
      return;
    }

    this._submitting = accountId;
    this._error = null;
    this._renderBody();
    try {
      // Carry the applicant's name through the verdict so the resolved record keeps its context (the service would
      // otherwise fall back to a placeholder). Omit it when the pending review had no name rather than sending empty.
      await this._client.submitVerdict(accountId, fullName ? { verified, score, fullName } : { verified, score });
      this._expanded.delete(accountId);
      await this._loadFirstPage({ quiet: true });
    } catch (err) {
      this._error = err instanceof ProblemError ? err.message : String(err?.message ?? err);
    } finally {
      this._submitting = null;
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
        <h1>KYC console</h1>
        <p class="sub">Identity verifications. Resolve a <strong>pending</strong> review to resume the suspended <code>onboard-customer-async</code> run.</p>
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

  /** @private Re-renders the stats + verification list from current state. */
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
      err.textContent = `Could not complete request: ${this._error}`;
      body.appendChild(err);
    }

    const visible = this._filter === 'all'
      ? this._verifications
      : this._verifications.filter((v) => v.status === this._filter);

    if (visible.length === 0) {
      const empty = document.createElement('div');
      empty.className = 'notice';
      empty.textContent = this._verifications.length === 0 && !this._loading
        ? 'No verifications yet. An onboard-customer run creates one when it verifies identity.'
        : this._loading
          ? 'Loading…'
          : 'No verifications match this filter.';
      body.appendChild(empty);
      return;
    }

    const list = document.createElement('div');
    list.className = 'list';
    for (const verification of visible) {
      list.appendChild(this._verificationCard(verification));
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
    for (const v of this._verifications) {
      counts.set(v.status, (counts.get(v.status) ?? 0) + 1);
    }

    const total = document.createElement('span');
    total.className = 'stat total';
    total.textContent = `${this._verifications.length} verification${this._verifications.length === 1 ? '' : 's'}`;
    stats.appendChild(total);

    for (const status of STATUS_ORDER) {
      const n = counts.get(status);
      if (!n) {
        continue;
      }

      const chip = document.createElement('span');
      chip.className = 'stat';
      chip.style.setProperty('--dot', `var(--kyc-status-${status})`);
      chip.innerHTML = `<i class="dot"></i>${n} ${STATUS_LABEL[status].toLowerCase()}`;
      stats.appendChild(chip);
    }
  }

  /** @private One verification card: a summary row plus its expandable detail (identity, or a verdict form). */
  _verificationCard(verification) {
    const card = document.createElement('div');
    card.className = 'card';
    if (verification.status === 'pending') {
      card.classList.add('is-pending');
    }

    const summary = document.createElement('button');
    summary.className = 'summary';
    summary.type = 'button';
    summary.setAttribute('aria-expanded', String(this._expanded.has(verification.accountId)));

    const badge = document.createElement('span');
    badge.className = 'badge';
    badge.style.setProperty('--c', `var(--kyc-status-${verification.status})`);
    badge.textContent = STATUS_LABEL[verification.status] ?? verification.status;

    const name = document.createElement('span');
    name.className = 'name';
    name.textContent = verification.fullName || '(unnamed applicant)';

    const channel = document.createElement('span');
    channel.className = 'channel';
    channel.textContent = verification.channel === 'manual' ? 'manual' : 'synchronous';

    const id = document.createElement('span');
    id.className = 'id';
    id.textContent = shortId(verification.accountId);
    id.title = verification.accountId;

    const when = document.createElement('span');
    when.className = 'when';
    when.textContent = formatWhen(verification.verifiedAt ?? verification.submittedAt);
    when.title = verification.submittedAt ?? '';

    const chevron = document.createElement('span');
    chevron.className = 'chevron';
    chevron.setAttribute('aria-hidden', 'true');

    summary.append(badge, name, channel, id, when, chevron);

    const detail = document.createElement('div');
    detail.className = 'detail';
    detail.hidden = !this._expanded.has(verification.accountId);
    if (!detail.hidden) {
      detail.appendChild(this._detail(verification));
    }

    summary.addEventListener('click', () => {
      const open = this._expanded.has(verification.accountId);
      if (open) {
        this._expanded.delete(verification.accountId);
        detail.hidden = true;
        detail.textContent = '';
      } else {
        this._expanded.add(verification.accountId);
        detail.textContent = '';
        detail.appendChild(this._detail(verification));
        detail.hidden = false;
      }

      summary.setAttribute('aria-expanded', String(!open));
    });

    card.append(summary, detail);
    return card;
  }

  /** @private The card detail: a verdict form when pending, otherwise the resolved identity. */
  _detail(verification) {
    if (verification.status === 'pending') {
      return this._verdictForm(verification);
    }

    return this._identity(verification);
  }

  /** @private The manual-recovery verdict form for a pending review. */
  _verdictForm(verification) {
    const wrap = document.createElement('div');
    wrap.className = 'verdict';

    const lead = document.createElement('p');
    lead.className = 'verdict-lead';
    lead.textContent = 'This review is awaiting an operator verdict. Submitting publishes it onto the bus, which resumes the suspended workflow run.';
    wrap.appendChild(lead);

    const row = document.createElement('div');
    row.className = 'verdict-row';

    const verifiedLabel = document.createElement('label');
    verifiedLabel.className = 'verdict-field';
    const verifiedInput = document.createElement('input');
    verifiedInput.type = 'checkbox';
    verifiedInput.checked = true;
    verifiedLabel.append(verifiedInput, document.createTextNode(' Verified'));

    const scoreLabel = document.createElement('label');
    scoreLabel.className = 'verdict-field';
    const scoreInput = document.createElement('input');
    scoreInput.type = 'number';
    scoreInput.min = '0';
    scoreInput.max = '1';
    scoreInput.step = '0.01';
    scoreInput.value = '0.90';
    scoreLabel.append(document.createTextNode('Confidence '), scoreInput);

    const submit = document.createElement('button');
    submit.className = 'verdict-submit';
    submit.type = 'button';
    const busy = this._submitting === verification.accountId;
    submit.textContent = busy ? 'Submitting…' : 'Submit verdict';
    submit.disabled = busy;
    submit.addEventListener('click', () => {
      const score = Math.max(0, Math.min(1, Number(scoreInput.value) || 0));
      this._submitVerdict(verification.accountId, verifiedInput.checked, score, verification.fullName);
    });

    row.append(verifiedLabel, scoreLabel, submit);
    wrap.append(row, field('Account id', verification.accountId, true));
    return wrap;
  }

  /** @private The resolved identity result of a verified/blocked verification. */
  _identity(verification) {
    const identity = verification.identity ?? {};
    const flags = Array.isArray(identity.flags) ? identity.flags : [];
    const applicant = identity.applicant ?? {};
    const evidence = identity.evidence ?? {};
    const dl = document.createElement('dl');
    dl.className = 'fields';
    const rows = [
      field('Account id', verification.accountId, true),
      field('Outcome', verification.status === 'verified' ? 'Verified' : (flags.length ? `Blocked (${flags.join(', ')})` : 'Blocked')),
      field('Confidence', typeof identity.score === 'number' ? `${Math.round(identity.score * 100)}%` : '—'),
      field('Channel', verification.channel === 'manual' ? 'Manual recovery' : 'Synchronous'),
      field('Applicant', [applicant.fullName, applicant.country, applicant.email].filter(Boolean).join(' · ') || verification.fullName || '—'),
      field('Evidence', evidenceLabel(evidence)),
    ];
    for (const r of rows) {
      dl.appendChild(r);
    }

    return dl;
  }
}

// ---- small DOM builders ------------------------------------------------------------------------

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

const STYLES = `
:host { display: block; box-sizing: border-box; max-width: 960px; margin: 0 auto; padding: 24px 20px 64px; color: var(--kyc-text, #1a1d21); }
:host *, :host *::before, :host *::after { box-sizing: border-box; }
h1 { font-size: 1.4rem; margin: 0; }
.sub { margin: 4px 0 0; color: var(--kyc-muted, #6b7280); font-size: .9rem; }
.sub code { font-family: var(--kyc-mono, monospace); font-size: .85em; }
.app-header { display: flex; align-items: flex-start; justify-content: space-between; gap: 16px; flex-wrap: wrap; margin-bottom: 12px; }
.toolbar { display: flex; align-items: center; gap: 10px; }
.filter { display: flex; align-items: center; gap: 6px; font-size: .8rem; color: var(--kyc-muted, #6b7280); }
select, button, input { font: inherit; color: inherit; }
select { background: var(--kyc-surface, #fff); border: 1px solid var(--kyc-border, #e1e5ea); border-radius: 8px; padding: 5px 8px; }
.refresh { background: var(--kyc-surface, #fff); border: 1px solid var(--kyc-border, #e1e5ea); border-radius: 8px; padding: 6px 12px; cursor: pointer; }
.refresh:hover { border-color: var(--kyc-accent, #3b6cf6); color: var(--kyc-accent, #3b6cf6); }
.live { font-size: .7rem; text-transform: uppercase; letter-spacing: .06em; color: var(--kyc-status-verified, #2a8a4a); display: inline-flex; align-items: center; gap: 5px; }
.live::before { content: ""; width: 7px; height: 7px; border-radius: 50%; background: currentColor; animation: pulse 2s ease-in-out infinite; }
@keyframes pulse { 0%, 100% { opacity: 1; } 50% { opacity: .3; } }
.stats { display: flex; flex-wrap: wrap; gap: 8px 14px; margin-bottom: 14px; font-size: .82rem; color: var(--kyc-muted, #6b7280); }
.stat { display: inline-flex; align-items: center; gap: 6px; }
.stat.total { font-weight: 600; color: var(--kyc-text, #1a1d21); }
.stat .dot { width: 8px; height: 8px; border-radius: 50%; background: var(--dot, currentColor); }
.list { display: flex; flex-direction: column; gap: 8px; }
.card { background: var(--kyc-surface, #fff); border: 1px solid var(--kyc-border, #e1e5ea); border-radius: var(--kyc-radius, 10px); overflow: hidden; }
.card.is-pending { border-color: var(--kyc-status-pending, #c98a12); }
.summary { width: 100%; display: grid; grid-template-columns: auto minmax(0, 1fr) auto auto auto auto; align-items: center; gap: 12px; padding: 12px 14px; background: none; border: 0; cursor: pointer; text-align: left; }
.summary:hover { background: var(--kyc-surface-2, #f2f4f8); }
.badge { font-size: .72rem; font-weight: 600; padding: 3px 9px; border-radius: 999px; color: #fff; background: var(--c, #888); white-space: nowrap; }
.name { font-weight: 550; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.channel { font-size: .72rem; text-transform: capitalize; padding: 2px 8px; border-radius: 999px; border: 1px solid var(--kyc-border, #e1e5ea); color: var(--kyc-muted, #6b7280); white-space: nowrap; }
.id { font-family: var(--kyc-mono, monospace); font-size: .78rem; color: var(--kyc-muted, #6b7280); }
.when { font-size: .78rem; color: var(--kyc-muted, #6b7280); white-space: nowrap; }
.chevron { width: 8px; height: 8px; border-right: 2px solid var(--kyc-muted, #6b7280); border-bottom: 2px solid var(--kyc-muted, #6b7280); transform: rotate(45deg); transition: transform .15s; }
.summary[aria-expanded="true"] .chevron { transform: rotate(225deg); }
.detail { padding: 12px 14px 14px; border-top: 1px solid var(--kyc-border, #e1e5ea); }
.fields { display: grid; grid-template-columns: minmax(90px, auto) 1fr; gap: 3px 14px; margin: 0; font-size: .84rem; }
.fields dt { color: var(--kyc-muted, #6b7280); }
.fields dd { margin: 0; overflow-wrap: anywhere; }
.fields dd.mono { font-family: var(--kyc-mono, monospace); font-size: .82em; }
.verdict-lead { margin: 0 0 10px; font-size: .84rem; color: var(--kyc-muted, #6b7280); }
.verdict-row { display: flex; align-items: center; gap: 14px; flex-wrap: wrap; margin-bottom: 10px; }
.verdict-field { display: inline-flex; align-items: center; gap: 6px; font-size: .84rem; }
.verdict-field input[type="number"] { width: 72px; background: var(--kyc-surface, #fff); border: 1px solid var(--kyc-border, #e1e5ea); border-radius: 8px; padding: 5px 8px; }
.verdict-submit { background: var(--kyc-accent, #3b6cf6); color: #fff; border: 0; border-radius: 8px; padding: 8px 16px; cursor: pointer; font-weight: 550; }
.verdict-submit:hover:not(:disabled) { filter: brightness(1.05); }
.verdict-submit:disabled { opacity: .6; cursor: default; }
.notice { padding: 32px 16px; text-align: center; color: var(--kyc-muted, #6b7280); background: var(--kyc-surface, #fff); border: 1px dashed var(--kyc-border, #e1e5ea); border-radius: var(--kyc-radius, 10px); margin-bottom: 8px; }
.notice.error { color: var(--kyc-danger, #d4351c); border-color: var(--kyc-danger, #d4351c); border-style: solid; }
.load-more { display: block; margin: 14px auto 0; background: var(--kyc-surface, #fff); border: 1px solid var(--kyc-border, #e1e5ea); border-radius: 8px; padding: 8px 18px; cursor: pointer; }
.load-more:hover:not(:disabled) { border-color: var(--kyc-accent, #3b6cf6); }
`;

customElements.define('kyc-console', KycConsole);

export { KycConsole };
