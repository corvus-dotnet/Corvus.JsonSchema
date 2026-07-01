// <arazzo-runners> — the runner registry / health view (design §5.4).
//
//   <arazzo-runners base-url="/arazzo/v1" stale-after="90" poll="10000"></arazzo-runners>
//
// Attributes : base-url, stale-after (seconds before a runner with no fresh heartbeat is shown Stale; default 90),
//              poll (ms auto-refresh; omit/0 = off), scopes (informational — listRunners needs runs:read)
// Properties : .client
// Events     : loaded {count, hasMore}, error {problem}
// Parts      : panel, list, runner
//
// A READ-ONLY observability surface: the execution hosts that have registered and heartbeat. Runners self-register and
// refresh their `lastSeenAt` out of band (§5.4) — the control plane only observes them — so there are no mutating
// controls here. Each runner shows its liveness (Online / Stale, derived from the most recent heartbeat against
// `stale-after`), uptime, advertised concurrency, transports, and the workflow versions it hosts (loaded / loading).

import { ArazzoControlPlaneClient } from '../arazzo-client.js';
import { ArazzoElement, SHARED_CSS, escapeHtml, relativeTime, absoluteTime, define } from './base.js';

const DEFAULT_STALE_AFTER_SECONDS = 90;

class ArazzoRunners extends ArazzoElement {
  static get observedAttributes() {
    return ['base-url', 'stale-after', 'poll', 'scopes', 'page-size'];
  }

  constructor() {
    super();
    /** @private */ this._runners = [];
    /** @private */ this._loading = false;
    /** @private */ this._error = null;
    /** @private */ this._history = [];          // pageTokens of pages before the current one
    /** @private */ this._currentToken = undefined;
    /** @private */ this._nextPageToken = null;
    /** @private */ this._reqSeq = 0;
    /** @private */ this._timer = null;
  }

  connectedCallback() {
    this.renderShell();
    this.reload();
    this.startPolling();
  }

  disconnectedCallback() {
    this.stopPolling();
  }

  attributeChangedCallback(name) {
    if (!this.isConnected) return;
    if (name === 'poll') this.startPolling();
    else if (name === 'stale-after' || name === 'scopes') this.renderBody(); // scopes is informational here; never reset the client
    else { this._client = undefined; this.reload(); } // base-url, page-size
  }

  buildClient() {
    if (this._client) return this._client;
    const baseUrl = this.getAttribute('base-url');
    if (baseUrl) this._client = new ArazzoControlPlaneClient({ baseUrl, fetch: this._fetch, getAuthHeader: this._authProvider });
    return this._client;
  }

  set fetch(fn) { this._fetch = fn; this._client = undefined; this.reload(); }

  set authProvider(fn) { this._authProvider = fn; this._client = undefined; this.reload(); }

  requestRender() { this.reload(); }

  refresh() { this.reload(); }

  get pageSize() {
    return Number(this.getAttribute('page-size')) || 50;
  }

  get staleAfterMs() {
    const s = Number(this.getAttribute('stale-after'));
    return (Number.isFinite(s) && s > 0 ? s : DEFAULT_STALE_AFTER_SECONDS) * 1000;
  }

  startPolling() {
    this.stopPolling();
    const ms = Number(this.getAttribute('poll'));
    if (Number.isFinite(ms) && ms > 0) {
      // A silent reload (no skeleton flash) keeps the heartbeat ages + health current while the panel is open.
      this._timer = setInterval(() => this.load({ silent: true }), ms);
    }
  }

  stopPolling() {
    if (this._timer) { clearInterval(this._timer); this._timer = null; }
  }

  // ---- data -------------------------------------------------------------------------------------

  /** Reload from page 1 (resets the keyset cursor). */
  reload() {
    this._history = [];
    this._currentToken = undefined;
    this.load();
  }

  async load({ silent = false } = {}) {
    const client = this.buildClient();
    if (!client) {
      this._error = { title: 'Not configured', detail: 'Set a base-url or .client.' };
      this._runners = [];
      this.renderBody();
      return;
    }
    const seq = ++this._reqSeq;
    if (!silent) { this._loading = true; this._runners = []; }
    this._error = null;
    this.renderBody();
    try {
      const page = await client.listRunners({ pageToken: this._currentToken, limit: this.pageSize });
      if (seq !== this._reqSeq) return;
      this._runners = page.runners;
      this._nextPageToken = page.nextPageToken;
      this._loading = false;
      this.renderBody();
      this.emit('loaded', { count: this._runners.length, hasMore: !!this._nextPageToken });
    } catch (err) {
      if (seq !== this._reqSeq) return;
      this._loading = false;
      this._error = err.problem || { title: err.message };
      this.renderBody();
      this.emit('error', { problem: this._error, error: err });
    }
  }

  nextPage() {
    if (!this._nextPageToken) return;
    this._history.push(this._currentToken);
    this._currentToken = this._nextPageToken;
    this.load();
  }

  prevPage() {
    if (this._history.length === 0) return;
    this._currentToken = this._history.pop();
    this.load();
  }

  /** A runner is Stale once its most recent heartbeat is older than `stale-after` (§5.4: missed-intervals → Stale). */
  isStale(runner, now = Date.now()) {
    const last = Date.parse(runner.lastSeenAt);
    return Number.isNaN(last) ? true : (now - last) > this.staleAfterMs;
  }

  // ---- rendering --------------------------------------------------------------------------------

  renderShell() {
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        :host { display: block; }
        .panel { border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_bg); overflow: hidden; }
        .head { padding: 10px 12px; background: var(--_surface); border-bottom: 1px solid var(--_border); display: flex; align-items: center; gap: 8px; }
        .head .title { font-weight: 700; }
        .head .grow { flex: 1; }
        .head .count { color: var(--_muted); font-size: 12px; }
        .err { margin: 10px 12px; }
        .list { display: grid; }
        .runner { padding: 11px 12px; border-bottom: 1px solid var(--_border); }
        .runner:last-child { border-bottom: none; }
        .rhead { display: flex; align-items: baseline; gap: 8px; flex-wrap: wrap; }
        .rid { font-weight: 600; }
        .renv { font-size: 11px; padding: 1px 8px; border-radius: 999px; background: var(--_surface); border: 1px solid var(--_border); color: var(--_text); font-weight: 600; }
        .raddr { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 12px; color: var(--_muted); }
        .health { flex: none; font-size: 11px; padding: 1px 8px; border-radius: 999px; border: 1px solid currentColor; display: inline-flex; align-items: center; gap: 5px; }
        .health.online { color: #1a7f37; }
        .health.stale { color: #b45309; }
        .health .dot { width: 7px; height: 7px; border-radius: 50%; background: currentColor; }
        .rgrow { flex: 1; }
        .rmeta { display: flex; flex-wrap: wrap; gap: 6px 16px; margin-top: 5px; color: var(--_muted); font-size: 12px; }
        .rmeta b { color: var(--_text); font-weight: 600; }
        .badges { display: inline-flex; gap: 4px; flex-wrap: wrap; }
        .badge { font-size: 11px; padding: 1px 7px; border-radius: 999px; background: var(--_surface); border: 1px solid var(--_border); color: var(--_muted); }
        .hosted { margin-top: 7px; display: grid; gap: 3px; }
        .hv { font-size: 12px; display: flex; align-items: baseline; gap: 8px; }
        .hv .wf { font-weight: 600; }
        .hv .ver { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; color: var(--_muted); }
        .hv .lstate { font-size: 11px; padding: 0 6px; border-radius: 999px; border: 1px solid var(--_border); color: var(--_muted); }
        .hv .lstate.loading { color: #b45309; border-color: currentColor; }
        .pager { display: flex; gap: 8px; justify-content: center; padding: 10px; border-top: 1px solid var(--_border); }
        .skl { height: 16px; border-radius: 4px; background: var(--_surface); animation: pulse 1.2s ease-in-out infinite; margin: 11px 12px; }
        @keyframes pulse { 50% { opacity: 0.45; } }
      </style>
      <div class="panel" part="panel">
        <div class="head">
          <span class="title">Runners</span>
          <span class="count"></span>
          <span class="grow"></span>
          <button class="refresh ghost" type="button" title="Refresh">↻</button>
        </div>
        <div class="err"></div>
        <div class="list" part="list"></div>
      </div>
    `;
    this.$('.refresh').addEventListener('click', () => this.reload());
  }

  renderBody() {
    const err = this.$('.err');
    const list = this.$('.list');
    if (!list) return;

    err.innerHTML = this._error
      ? `<div class="error-banner"><span><strong>${escapeHtml(this._error.title || 'Request failed')}</strong>${this._error.detail ? ' — ' + escapeHtml(this._error.detail) : ''}</span></div>`
      : '';

    if (this._loading && this._runners.length === 0) {
      this.$('.count').textContent = '';
      list.innerHTML = '<div class="skl"></div><div class="skl"></div>';
      return;
    }
    if (this._runners.length === 0) {
      this.$('.count').textContent = '';
      list.innerHTML = '<div class="empty">No runners are registered.</div>';
      return;
    }

    const now = Date.now();
    const stale = this._runners.filter((r) => this.isStale(r, now)).length;
    this.$('.count').textContent = stale > 0 ? `${this._runners.length} registered · ${stale} stale` : `${this._runners.length} registered`;

    const rows = this._runners.map((r) => this.runnerHtml(r, now)).join('');
    const pager = (this._history.length > 0 || this._nextPageToken)
      ? `<div class="pager">
           <button class="prev ghost" type="button"${this._history.length === 0 || this._loading ? ' disabled' : ''}>‹ Prev</button>
           <button class="next ghost" type="button"${!this._nextPageToken || this._loading ? ' disabled' : ''}>Next ›</button>
         </div>`
      : '';
    list.innerHTML = rows + pager;
    const prevBtn = this.$('.prev');
    if (prevBtn) prevBtn.addEventListener('click', () => this.prevPage());
    const nextBtn = this.$('.next');
    if (nextBtn) nextBtn.addEventListener('click', () => this.nextPage());
  }

  runnerHtml(r, now) {
    const stale = this.isStale(r, now);
    const transports = Array.isArray(r.transports) ? r.transports : [];
    const hosted = Array.isArray(r.hostedVersions) ? r.hostedVersions : [];
    const hostedHtml = hosted.length === 0
      ? '<span class="muted" style="font-size:12px">No workflow versions loaded.</span>'
      : hosted.map((h) => `
        <div class="hv">
          <span class="wf">${escapeHtml(h.baseWorkflowId)}</span><span class="ver">v${escapeHtml(h.versionNumber)}</span>
          <span class="lstate${h.loaded ? '' : ' loading'}">${h.loaded ? 'loaded' : 'loading'}</span>
        </div>`).join('');
    return `
      <div class="runner" part="runner">
        <div class="rhead">
          <span class="rid">${escapeHtml(r.runnerId)}</span>
          ${r.environment ? `<span class="renv" title="Serves the ${escapeHtml(r.environment)} environment">${escapeHtml(r.environment)}</span>` : ''}
          ${r.address ? `<span class="raddr">${escapeHtml(r.address)}</span>` : ''}
          <span class="rgrow"></span>
          <span class="health ${stale ? 'stale' : 'online'}" title="Last heartbeat ${escapeHtml(absoluteTime(r.lastSeenAt))}"><span class="dot"></span>${stale ? 'Stale' : 'Online'}</span>
        </div>
        <div class="rmeta">
          <span>heartbeat <b>${escapeHtml(relativeTime(r.lastSeenAt))}</b></span>
          <span>up <b title="${escapeHtml(absoluteTime(r.startedAt))}">${escapeHtml(relativeTime(r.startedAt))}</b></span>
          <span>concurrency <b>${escapeHtml(r.maxConcurrency)}</b></span>
          ${transports.length ? `<span class="badges">${transports.map((t) => `<span class="badge">${escapeHtml(t)}</span>`).join('')}</span>` : ''}
        </div>
        <div class="hosted">${hostedHtml}</div>
      </div>`;
  }
}

define('arazzo-runners', ArazzoRunners);
export { ArazzoRunners };