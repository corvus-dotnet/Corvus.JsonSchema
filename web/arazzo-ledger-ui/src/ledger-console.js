// Reconciliation console — the <reconciliation-console> custom element.
//
// A build-free web component (Shadow DOM) rendering the ledger product's operator console: the nightly-reconcile
// runs and, per run, its discrepancies (accounts whose ledger and bank balances disagree, with a severity), counts,
// and report. It reads the ledger service through LedgerClient. This is a separate app from the Arazzo control-plane
// UI; it merely consumes the workflow engine's effects.
//
//   <link rel="stylesheet" href="ledger-kit.css">
//   <reconciliation-console poll="5"></reconciliation-console>
//
// Attributes: `base-url` (default '' — same origin), `limit` (page size, default 50), `poll` (auto-refresh seconds).

import { LedgerClient, ProblemError } from './ledger-client.js';

const SEVERITY_ORDER = ['critical', 'warning', 'info'];
const SEVERITY_LABEL = { critical: 'Critical', warning: 'Warning', info: 'Info' };

class ReconciliationConsole extends HTMLElement {
  constructor() {
    super();
    this.attachShadow({ mode: 'open' });
    /** @private */ this._client = null;
    /** @private @type {object[]} */ this._runs = [];
    /** @private */ this._nextPageToken = null;
    /** @private @type {Set<string>} */ this._expanded = new Set();
    /** @private */ this._error = null;
    /** @private */ this._loading = false;
    /** @private */ this._pollTimer = null;
    /** @private */ this._abort = null;
  }

  connectedCallback() {
    this._client = new LedgerClient({ baseUrl: this.getAttribute('base-url') ?? '' });
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

  /** @private */
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
      const page = await this._client.listReconciliations({ limit, signal: this._abort.signal });
      this._runs = page.reconciliations;
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

  /** @private */
  async _loadMore() {
    if (this._loading || !this._nextPageToken) {
      return;
    }

    this._loading = true;
    this._renderBody();
    try {
      const limit = Number(this.getAttribute('limit')) || 50;
      const page = await this._client.listReconciliations({ limit, pageToken: this._nextPageToken });
      this._runs = this._runs.concat(page.reconciliations);
      this._nextPageToken = page.nextPageToken;
    } catch (err) {
      this._error = err instanceof ProblemError ? err.message : String(err?.message ?? err);
    } finally {
      this._loading = false;
      this._renderBody();
    }
  }

  // ---- rendering -------------------------------------------------------------------------------

  /** @private */
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
        <h1>Reconciliation console</h1>
        <p class="sub">Nightly reconciliations of the ledger against the bank, run by the <code>nightly-reconcile</code> workflow.</p>
      </div>
      <div class="toolbar">
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

    header.querySelector('.refresh').addEventListener('click', () => this._loadFirstPage());
    header.querySelector('.live').hidden = !(Number(this.getAttribute('poll')) > 0);

    this._statsEl = stats;
    this._bodyEl = body;
    this._renderBody();
  }

  /** @private */
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
      err.textContent = `Could not load reconciliations: ${this._error}`;
      body.appendChild(err);
      return;
    }

    if (this._runs.length === 0) {
      const empty = document.createElement('div');
      empty.className = 'notice';
      empty.textContent = this._loading
        ? 'Loading…'
        : 'No reconciliations yet. Trigger a nightly-reconcile run to reconcile the ledger.';
      body.appendChild(empty);
      return;
    }

    const list = document.createElement('div');
    list.className = 'list';
    for (const run of this._runs) {
      list.appendChild(this._runCard(run));
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

  /** @private A summary line: run count, latest unmatched, and the latest total delta. */
  _renderStats() {
    const stats = this._statsEl;
    if (!stats) {
      return;
    }

    stats.textContent = '';
    const total = document.createElement('span');
    total.className = 'stat total';
    total.textContent = `${this._runs.length} reconciliation${this._runs.length === 1 ? '' : 's'}`;
    stats.appendChild(total);

    const latest = this._runs[0];
    if (latest) {
      const unmatched = document.createElement('span');
      unmatched.className = 'stat';
      unmatched.textContent = `latest: ${latest.unmatched} unmatched, ${discrepancyCount(latest)} discrepanc${discrepancyCount(latest) === 1 ? 'y' : 'ies'}`;
      stats.appendChild(unmatched);

      const delta = document.createElement('span');
      delta.className = `stat delta ${signClass(latest.totalDelta)}`;
      delta.textContent = `Δ ${money(latest.totalDelta)}`;
      stats.appendChild(delta);
    }
  }

  /** @private One reconciliation run: a summary row plus its expandable detail. */
  _runCard(run) {
    const card = document.createElement('div');
    card.className = 'card';

    const summary = document.createElement('button');
    summary.className = 'summary';
    summary.type = 'button';
    summary.setAttribute('aria-expanded', String(this._expanded.has(run.runId)));

    const badge = document.createElement('span');
    badge.className = 'badge';
    badge.style.setProperty('--c', run.published ? 'var(--ldg-published)' : 'var(--ldg-pending)');
    badge.textContent = run.published ? 'Published' : 'Pending';

    const title = document.createElement('span');
    title.className = 'title';
    title.textContent = `Reconciliation ${shortId(run.runId)}`;

    const meta = document.createElement('span');
    meta.className = 'meta';
    const n = discrepancyCount(run);
    meta.textContent = `${run.matched} matched · ${run.unmatched} unmatched · ${n} discrepanc${n === 1 ? 'y' : 'ies'}`;

    const delta = document.createElement('span');
    delta.className = `card-delta ${signClass(run.totalDelta)}`;
    delta.textContent = `Δ ${money(run.totalDelta)}`;

    const when = document.createElement('span');
    when.className = 'when';
    when.textContent = formatWhen(run.startedAt);
    when.title = run.startedAt ?? '';

    const chevron = document.createElement('span');
    chevron.className = 'chevron';
    chevron.setAttribute('aria-hidden', 'true');

    summary.append(badge, title, meta, delta, when, chevron);

    const detail = document.createElement('div');
    detail.className = 'detail';
    detail.hidden = !this._expanded.has(run.runId);
    if (!detail.hidden) {
      detail.appendChild(this._detail(run));
    }

    summary.addEventListener('click', () => {
      const open = this._expanded.has(run.runId);
      if (open) {
        this._expanded.delete(run.runId);
        detail.hidden = true;
        detail.textContent = '';
      } else {
        this._expanded.add(run.runId);
        detail.textContent = '';
        detail.appendChild(this._detail(run));
        detail.hidden = false;
      }

      summary.setAttribute('aria-expanded', String(!open));
    });

    card.append(summary, detail);
    return card;
  }

  /** @private The run's counts, discrepancy table, and report footer. */
  _detail(run) {
    const wrap = document.createElement('div');
    wrap.className = 'run-detail';

    // Counts.
    const counts = document.createElement('div');
    counts.className = 'counts';
    counts.append(
      countChip('Ledger entries', integer(run.ledgerEntries)),
      countChip('Bank transactions', integer(run.bankTransactions)),
      countChip('Matched', integer(run.matched)),
      countChip('Unmatched', integer(run.unmatched)),
    );
    wrap.appendChild(counts);

    // Discrepancies.
    const discrepancies = Array.isArray(run.discrepancies) ? run.discrepancies : [];
    if (discrepancies.length === 0) {
      const none = document.createElement('p');
      none.className = 'no-discrepancies';
      none.textContent = 'No discrepancies — the ledger fully reconciles with the bank.';
      wrap.appendChild(none);
    } else {
      const table = document.createElement('table');
      table.className = 'discrepancies';
      const thead = document.createElement('thead');
      thead.innerHTML = '<tr><th>Account</th><th class="num">Delta (bank − ledger)</th><th>Currency</th><th>Severity</th><th>First seen</th></tr>';
      table.appendChild(thead);
      const tbody = document.createElement('tbody');
      for (const d of discrepancies) {
        const tr = document.createElement('tr');

        const account = document.createElement('td');
        account.className = 'mono';
        account.textContent = d.account ?? '—';

        const delta = document.createElement('td');
        delta.className = `num ${signClass(d.delta)}`;
        delta.textContent = money(d.delta);

        const currency = document.createElement('td');
        currency.textContent = d.currency ?? '—';

        const severity = document.createElement('td');
        const sev = document.createElement('span');
        sev.className = 'sev';
        sev.style.setProperty('--c', `var(--ldg-sev-${d.severity ?? 'info'})`);
        sev.textContent = SEVERITY_LABEL[d.severity] ?? (d.severity ?? '—');
        severity.appendChild(sev);

        const firstSeen = document.createElement('td');
        firstSeen.className = 'muted';
        firstSeen.textContent = d.firstSeen ?? '—';

        tr.append(account, delta, currency, severity, firstSeen);
        tbody.appendChild(tr);
      }

      table.appendChild(tbody);
      const scroller = document.createElement('div');
      scroller.className = 'table-scroll';
      scroller.appendChild(table);
      wrap.appendChild(scroller);
    }

    // Report footer.
    const footer = document.createElement('div');
    footer.className = 'report';
    footer.append(
      reportItem('Total delta', money(run.totalDelta), signClass(run.totalDelta)),
      reportItem('Corrections posted', integer(run.correctionsPosted ?? 0)),
      reportItem('Published', run.published ? (run.publishedAt ? formatFull(run.publishedAt) : 'yes') : 'no'),
    );
    if (run.reportUrl) {
      const link = document.createElement('a');
      link.className = 'report-link';
      link.href = run.reportUrl;
      link.target = '_blank';
      link.rel = 'noreferrer noopener';
      link.textContent = 'Open report';
      footer.appendChild(link);
    }

    wrap.appendChild(footer);
    return wrap;
  }
}

// ---- small DOM builders ------------------------------------------------------------------------

function countChip(label, value) {
  const el = document.createElement('div');
  el.className = 'count';
  const v = document.createElement('span');
  v.className = 'count-value';
  v.textContent = value;
  const l = document.createElement('span');
  l.className = 'count-label';
  l.textContent = label;
  el.append(v, l);
  return el;
}

function reportItem(label, value, cls = '') {
  const el = document.createElement('span');
  el.className = `report-item ${cls}`.trim();
  el.innerHTML = `<i>${label}</i> `;
  el.append(document.createTextNode(value));
  return el;
}

// ---- helpers -----------------------------------------------------------------------------------

function discrepancyCount(run) {
  return Array.isArray(run.discrepancies) ? run.discrepancies.length : 0;
}

function shortId(id) {
  return typeof id === 'string' && id.length > 8 ? id.slice(0, 8) : (id ?? '');
}

function money(n) {
  const v = Number(n);
  return Number.isFinite(v) ? v.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 }) : '—';
}

function integer(n) {
  const v = Number(n);
  return Number.isFinite(v) ? v.toLocaleString() : '—';
}

function signClass(n) {
  const v = Number(n);
  return !Number.isFinite(v) || v === 0 ? '' : v < 0 ? 'neg' : 'pos';
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
:host { display: block; box-sizing: border-box; max-width: 1000px; margin: 0 auto; padding: 24px 20px 64px; color: var(--ldg-text, #1a1d21); }
:host *, :host *::before, :host *::after { box-sizing: border-box; }
h1 { font-size: 1.4rem; margin: 0; }
.sub { margin: 4px 0 0; color: var(--ldg-muted, #6b7280); font-size: .9rem; }
.sub code { font-family: var(--ldg-mono, monospace); font-size: .85em; }
.app-header { display: flex; align-items: flex-start; justify-content: space-between; gap: 16px; flex-wrap: wrap; margin-bottom: 12px; }
.toolbar { display: flex; align-items: center; gap: 10px; }
button { font: inherit; color: inherit; }
.refresh { background: var(--ldg-surface, #fff); border: 1px solid var(--ldg-border, #e1e5ea); border-radius: 8px; padding: 6px 12px; cursor: pointer; }
.refresh:hover { border-color: var(--ldg-accent, #3b6cf6); color: var(--ldg-accent, #3b6cf6); }
.live { font-size: .7rem; text-transform: uppercase; letter-spacing: .06em; color: var(--ldg-published, #2a8a4a); display: inline-flex; align-items: center; gap: 5px; }
.live::before { content: ""; width: 7px; height: 7px; border-radius: 50%; background: currentColor; animation: pulse 2s ease-in-out infinite; }
@keyframes pulse { 0%, 100% { opacity: 1; } 50% { opacity: .3; } }
.stats { display: flex; flex-wrap: wrap; gap: 8px 16px; margin-bottom: 14px; font-size: .82rem; color: var(--ldg-muted, #6b7280); }
.stat.total { font-weight: 600; color: var(--ldg-text, #1a1d21); }
.stat.delta { font-family: var(--ldg-mono, monospace); }
.list { display: flex; flex-direction: column; gap: 8px; }
.card { background: var(--ldg-surface, #fff); border: 1px solid var(--ldg-border, #e1e5ea); border-radius: var(--ldg-radius, 10px); overflow: hidden; }
.summary { width: 100%; display: grid; grid-template-columns: auto auto minmax(0, 1fr) auto auto auto; align-items: center; gap: 12px; padding: 12px 14px; background: none; border: 0; cursor: pointer; text-align: left; }
.summary:hover { background: var(--ldg-surface-2, #f2f4f8); }
.badge { font-size: .72rem; font-weight: 600; padding: 3px 9px; border-radius: 999px; color: #fff; background: var(--c, #888); white-space: nowrap; }
.title { font-weight: 600; white-space: nowrap; }
.meta { color: var(--ldg-muted, #6b7280); font-size: .82rem; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.card-delta { font-family: var(--ldg-mono, monospace); font-size: .82rem; white-space: nowrap; }
.pos { color: var(--ldg-pos, #2a8a4a); } .neg { color: var(--ldg-neg, #d4351c); }
.when { font-size: .78rem; color: var(--ldg-muted, #6b7280); white-space: nowrap; }
.chevron { width: 8px; height: 8px; border-right: 2px solid var(--ldg-muted, #6b7280); border-bottom: 2px solid var(--ldg-muted, #6b7280); transform: rotate(45deg); transition: transform .15s; }
.summary[aria-expanded="true"] .chevron { transform: rotate(225deg); }
.detail { padding: 12px 14px 14px; border-top: 1px solid var(--ldg-border, #e1e5ea); }
.counts { display: flex; flex-wrap: wrap; gap: 10px; margin-bottom: 12px; }
.count { background: var(--ldg-surface-2, #f2f4f8); border-radius: 8px; padding: 8px 12px; min-width: 120px; display: flex; flex-direction: column; }
.count-value { font-size: 1.05rem; font-weight: 600; font-variant-numeric: tabular-nums; }
.count-label { font-size: .72rem; color: var(--ldg-muted, #6b7280); }
.no-discrepancies { color: var(--ldg-pos, #2a8a4a); font-size: .88rem; margin: 6px 0 12px; }
.table-scroll { overflow-x: auto; margin-bottom: 12px; }
.discrepancies { width: 100%; border-collapse: collapse; font-size: .85rem; }
.discrepancies th { text-align: left; font-weight: 600; color: var(--ldg-muted, #6b7280); border-bottom: 1px solid var(--ldg-border, #e1e5ea); padding: 6px 10px; white-space: nowrap; }
.discrepancies td { padding: 6px 10px; border-bottom: 1px solid var(--ldg-border, #e1e5ea); white-space: nowrap; }
.discrepancies tr:last-child td { border-bottom: 0; }
.discrepancies .num { text-align: right; font-variant-numeric: tabular-nums; }
.discrepancies .mono { font-family: var(--ldg-mono, monospace); }
.discrepancies .muted { color: var(--ldg-muted, #6b7280); }
.sev { font-size: .72rem; font-weight: 600; padding: 2px 8px; border-radius: 999px; color: #fff; background: var(--c, #888); }
.report { display: flex; flex-wrap: wrap; align-items: center; gap: 8px 16px; font-size: .82rem; color: var(--ldg-muted, #6b7280); }
.report-item i { font-style: normal; opacity: .8; }
.report-item.pos { color: var(--ldg-pos, #2a8a4a); } .report-item.neg { color: var(--ldg-neg, #d4351c); }
.report-link { color: var(--ldg-accent, #3b6cf6); text-decoration: none; }
.report-link:hover { text-decoration: underline; }
.notice { padding: 32px 16px; text-align: center; color: var(--ldg-muted, #6b7280); background: var(--ldg-surface, #fff); border: 1px dashed var(--ldg-border, #e1e5ea); border-radius: var(--ldg-radius, 10px); }
.notice.error { color: var(--ldg-neg, #d4351c); border-color: var(--ldg-neg, #d4351c); border-style: solid; }
.load-more { display: block; margin: 14px auto 0; background: var(--ldg-surface, #fff); border: 1px solid var(--ldg-border, #e1e5ea); border-radius: 8px; padding: 8px 18px; cursor: pointer; }
.load-more:hover:not(:disabled) { border-color: var(--ldg-accent, #3b6cf6); }
`;

customElements.define('reconciliation-console', ReconciliationConsole);

export { ReconciliationConsole };
