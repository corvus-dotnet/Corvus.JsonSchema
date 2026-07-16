// <arazzo-operation-browser> — the designer's left rail (design §3.2/§5.3): the working copy's
// attached sources and each one's operation surface, searchable, with click-to-add step creation.
//
//   const browser = document.createElement('arazzo-operation-browser');
//   browser.client = client;                 // an ArazzoControlPlaneClient
//   browser.workingCopyId = 'wc-…';          // loads attachments + their operation surfaces
//
// Properties : .client, .workingCopyId
// Methods    : refresh()
// Events     : operation-selected {sourceName, operation}   (KEYBOARD Enter on a row — pointer adds
//                                                             go through drag-and-drop onto the surface,
//                                                             which emits operation-dropped THERE; a
//                                                             stray click must never create a step)
//              add-source-requested                          ("Add source…" — the host opens the acquisition dialog)
//              source-detached {name}                        (after a successful detach — the working copy's etag advanced;
//                                                             the host refreshes its copy of the etag)
//              loaded {sources, operations}, error {problem}
//
// Every operation row renders its binding identity (method+path or channel+action), id, and summary,
// and carries the FULL descriptor (raw-JSON-Schema request/parameters/responses) in the
// `operation-selected` event — exactly what step creation and the step inspector's templates consume.

import { ArazzoElement, SHARED_CSS, escapeHtml, define } from './base.js';
import './input-dialog.js';

const METHOD_COLOR = {
  GET: 'var(--arazzo-status-completed, #2a8a4a)',
  POST: 'var(--arazzo-accent, #3b6cf6)',
  PUT: '#b57706',
  PATCH: '#b57706',
  DELETE: 'var(--arazzo-status-failed, #c33)',
};

class ArazzoOperationBrowser extends ArazzoElement {
  constructor() {
    super();
    /** @private */ this._workingCopyId = null;
    /** @private */ this._sources = [];
    /** @private */ this._operations = new Map(); // source name → descriptor[]
    /** @private */ this._errors = new Map();     // source name → problem detail (per-source surface failures)
    /** @private */ this._loading = false;
    /** @private */ this._error = null;
    /** @private */ this._filter = '';
    /** @private */ this._expanded = new Set(); // source names whose operations are rendered
    /** @private */ this._reqSeq = 0;
  }

  /** The working copy whose attachments the rail shows; setting it (re)loads. */
  get workingCopyId() { return this._workingCopyId; }
  set workingCopyId(value) {
    this._workingCopyId = value || null;
    if (this.isConnected) this.refresh();
  }

  connectedCallback() {
    this.renderShell();
    this.refresh();
  }

  /** The loaded operation surfaces (source name → descriptor array), a read-only copy. */
  get surfaces() { return new Map(this._operations); }

  /** The OPEN document's other workflows — draggable step sources like any operation (a step
   *  bound by workflowId runs them as sub-workflows; the debugger steps into them). */
  get documentWorkflows() { return this._documentWorkflows ?? []; }
  set documentWorkflows(value) {
    this._documentWorkflows = value ?? [];
    if (this.isConnected) this.renderBody();
  }

  /** Reload the attachments and every source's operation surface. */
  async refresh() {
    const client = this.client;
    if (!client || !this._workingCopyId) {
      this._sources = [];
      this._operations.clear();
      this.renderBody();
      return;
    }

    const seq = ++this._reqSeq;
    this._loading = true;
    this._error = null;
    this.renderBody();

    try {
      const { sources } = await client.listWorkingCopySources(this._workingCopyId);
      if (seq !== this._reqSeq) return;
      this._sources = sources;
      this._operations.clear();
      this._errors.clear();

      // Load every source's surface concurrently; a single source failing (e.g. a dangling registry
      // reference) reports on ITS group without wiping the rail.
      await Promise.all(sources.map(async (s) => {
        try {
          const { operations } = await client.listWorkingCopySourceOperations(this._workingCopyId, s.name);
          if (seq === this._reqSeq) this._operations.set(s.name, operations);
        } catch (err) {
          if (seq === this._reqSeq) this._errors.set(s.name, err.problem?.detail || err.problem?.title || err.message);
        }
      }));
      if (seq !== this._reqSeq) return;
      this._loading = false;
      this.renderBody();
      this.emit('loaded', { sources: this._sources.length, operations: [...this._operations.values()].reduce((n, ops) => n + ops.length, 0) });
    } catch (err) {
      if (seq !== this._reqSeq) return;
      this._loading = false;
      this._error = err.problem || { title: err.message };
      this.renderBody();
      this.emit('error', { problem: this._error, error: err });
    }
  }

  // ---- rendering --------------------------------------------------------------------------------

  renderShell() {
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        :host { display: flex; flex-direction: column; min-height: 0; }
        .bar { display: flex; justify-content: space-between; align-items: center; gap: 8px; padding: 8px 10px; border-bottom: 1px solid var(--_border); }
        .bar h2 { margin: 0; font-size: 11px; letter-spacing: 0.05em; text-transform: uppercase; color: var(--_muted); }
        .bar button { font-size: 12px; padding: 3px 9px; }
        .search { padding: 8px 10px; border-bottom: 1px solid var(--_border); }
        .search input { width: 100%; box-sizing: border-box; font: inherit; font-size: 12px; padding: 5px 8px; border: 1px solid var(--_border); border-radius: 6px; background: var(--_bg); color: inherit; }
        .body { overflow-y: auto; overscroll-behavior: contain; flex: 1; min-height: 0; }
        .group { border-bottom: 1px solid var(--_border); }
        .group-head { display: flex; align-items: center; gap: 6px; padding: 7px 10px; font-size: 12px; font-weight: 600; }
        .group-head .type { font-size: 10px; font-weight: 600; padding: 0 6px; border-radius: 999px; border: 1px solid var(--_border); color: var(--_muted); text-transform: uppercase; }
        .group-head .spacer { flex: 1; }
        .group-head button.detach { font-size: 11px; padding: 1px 7px; }
        .group-head.toggle { cursor: pointer; user-select: none; }
        .group-head .twist { color: var(--_muted); width: 1em; flex-shrink: 0; }
        .group-head .count { font-size: 10.5px; color: var(--_muted); flex-shrink: 0; }
        .op { display: grid; grid-template-columns: auto minmax(0, 1fr); gap: 3px 8px; width: 100%; box-sizing: border-box;
              text-align: left; border: none; background: none; color: inherit; font: inherit; cursor: pointer; padding: 6px 10px 6px 16px; }
        .op:hover, .op:focus-visible { background: var(--_surface); outline: none; }
        .op { cursor: grab; }
        .op:active { cursor: grabbing; }
        .op .badge { font-size: 10px; font-weight: 700; padding: 1px 6px; border-radius: 4px; color: #fff; align-self: start; white-space: nowrap; }
        .op .id { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 12px; overflow-wrap: anywhere; }
        .op .summary { grid-column: 2; font-size: 11px; color: var(--_muted); overflow-wrap: anywhere; }
        .op .deprecated { text-decoration: line-through; }
        .note { padding: 8px 10px; font-size: 12px; color: var(--_muted); }
        .group .error-banner { margin: 6px 10px; font-size: 11px; }
      </style>
      <div class="bar" part="actions">
        <h2>Sources</h2>
        <button class="add" type="button">Add source…</button>
      </div>
      <div class="search"><input type="search" placeholder="Filter operations…" aria-label="Filter operations"></div>
      <div class="body" part="body"></div>
    `;
    const ask = document.createElement('arazzo-input-dialog');
    ask.className = 'ask';
    this.shadowRoot.append(ask);
    this.$('button.add').addEventListener('click', () => this.emit('add-source-requested', {}));
    this.$('.search input').addEventListener('input', (e) => {
      this._filter = e.target.value.trim().toLowerCase();
      this.renderBody();
    });
  }

  renderBody() {
    const body = this.$('.body');
    if (!body) return;

    if (this._error) {
      body.innerHTML = `<div class="error-banner" style="margin:10px">
        <span><strong>${escapeHtml(this._error.title || 'Request failed')}</strong>${this._error.detail ? ' — ' + escapeHtml(this._error.detail) : ''}</span>
        <button class="retry" type="button">Retry</button></div>`;
      body.querySelector('.retry').addEventListener('click', () => this.refresh());
      return;
    }

    if (this._loading) {
      body.innerHTML = `<div class="note">Loading sources…</div>`;
      return;
    }

    if (!this._workingCopyId) {
      body.innerHTML = `<div class="note">Open a working copy to browse its sources.</div>`;
      return;
    }

    if (this._sources.length === 0) {
      body.innerHTML = `<div class="empty" style="margin:10px">No sources attached yet — add one to browse its operations and create steps.</div>`;
      return;
    }

    const docWorkflows = (this._documentWorkflows ?? []).filter((w) => !this._filter
      || `${w.workflowId} ${w.summary ?? ''}`.toLowerCase().includes(this._filter));
    const docSection = (this._documentWorkflows ?? []).length === 0 ? '' : `
      <div class="group" part="group">
        <div class="group-head"><span>This document</span><span class="type">workflows</span></div>
        ${docWorkflows.length === 0 ? '<div class="note">No workflows match the filter.</div>' : docWorkflows.map((w, i) => `
          <button class="op wfop" type="button" part="operation" draggable="${w.current ? 'false' : 'true'}" data-wf="${i}"
            ${w.current ? 'disabled title="The workflow being edited — a step cannot run its own workflow directly"' : 'title="Drag onto the canvas to run this workflow as a sub-workflow step"'}>
            <span class="badge" style="background:var(--arazzo-status-running, #7048b7)">WF</span>
            <span class="id">${escapeHtml(w.workflowId)}</span>
            ${w.summary ? `<span class="summary">${escapeHtml(w.summary)}</span>` : ''}
          </button>`).join('')}
      </div>`;
    body.innerHTML = docSection + this._sources.map((s) => this.renderGroup(s)).join('');

    body.querySelectorAll('button.wfop').forEach((button) => {
      const payload = () => {
        const w = docWorkflows[Number(button.dataset.wf)];
        return w && !w.current ? { sourceName: null, operation: { kind: 'workflow', workflowId: w.workflowId, summary: w.summary } } : null;
      };
      button.addEventListener('click', (e) => {
        if (e.detail > 0) return;
        const data = payload();
        if (data) this.emit('operation-selected', data);
      });
      button.addEventListener('dragstart', (e) => {
        const data = payload();
        if (!data) return;
        e.dataTransfer.setData('application/x-arazzo-operation', JSON.stringify(data));
        e.dataTransfer.effectAllowed = 'copy';
      });
    });

    body.querySelectorAll('button.detach').forEach((button) => {
      button.addEventListener('click', (e) => { e.stopPropagation(); this.detach(button.dataset.name); });
    });
    body.querySelectorAll('.group-head.toggle').forEach((head) => {
      const toggle = () => {
        const name = head.dataset.name;
        if (this._expanded.has(name)) this._expanded.delete(name);
        else this._expanded.add(name);
        this.renderBody();
      };
      head.addEventListener('click', toggle);
      head.addEventListener('keydown', (e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); toggle(); } });
    });
    body.querySelectorAll('button.op').forEach((button) => {
      const payload = () => {
        const sourceName = button.dataset.source;
        const operation = this._operations.get(sourceName)?.[Number(button.dataset.index)];
        return operation ? { sourceName, operation } : null;
      };
      // Pointer clicks deliberately do NOT create steps (too easy to fire by accident) — dragging
      // onto the surface is the pointer gesture. Keyboard activation (click with detail 0) stays,
      // so non-pointer users still have a deliberate path.
      button.addEventListener('click', (e) => {
        if (e.detail > 0) return;
        const data = payload();
        if (data) this.emit('operation-selected', data);
      });
      button.addEventListener('dragstart', (e) => {
        const data = payload();
        if (!data) return;
        e.dataTransfer.setData('application/x-arazzo-operation', JSON.stringify(data));
        e.dataTransfer.effectAllowed = 'copy';
      });
    });
  }

  renderGroup(source) {
    const operations = this._operations.get(source.name) ?? [];
    const failure = this._errors.get(source.name);

    // Sources list COLLAPSED: a big spec can carry hundreds of operations, and rendering them all
    // eagerly floods the pane. Expanding renders that source's rows (a live filter auto-expands
    // every group so search stays cross-source); a lone source opens itself.
    const expanded = !!this._filter || this._expanded.has(source.name) || this._sources.length === 1;
    const shown = this._filter
      ? operations.filter((op) => `${op.operationId ?? ''} ${op.path ?? ''} ${op.channelPath ?? ''} ${op.summary ?? ''}`.toLowerCase().includes(this._filter))
      : operations;

    const CAP = 100;
    const rows = !expanded
      ? ''
      : failure
        ? `<div class="error-banner">${escapeHtml(failure)}</div>`
        : shown.length === 0
          ? `<div class="note">${operations.length === 0 ? 'No operations in this source.' : 'No operations match the filter.'}</div>`
          : shown.slice(0, CAP).map((op) => this.renderOperation(source.name, op, operations.indexOf(op))).join('')
            + (shown.length > CAP ? `<div class="note">${shown.length - CAP} more — filter to narrow</div>` : '');

    return `
      <div class="group" part="group">
        <div class="group-head toggle" data-name="${escapeHtml(source.name)}" role="button" tabindex="0"
             title="${expanded ? 'Collapse' : 'Expand'} ${escapeHtml(source.name)}">
          <span class="twist">${expanded ? '▾' : '▸'}</span>
          <span>${escapeHtml(source.name)}</span>
          ${source.type ? `<span class="type">${escapeHtml(source.type)}</span>` : ''}
          <span class="count">${operations.length} op${operations.length === 1 ? '' : 's'}</span>
          <span class="spacer"></span>
          <button class="detach" type="button" data-name="${escapeHtml(source.name)}" title="Detach this source">✕</button>
        </div>
        ${rows}
      </div>`;
  }

  renderOperation(sourceName, op, index) {
    const isHttp = op.kind === 'openapi';
    const badge = isHttp ? (op.method ?? '?') : (op.action ?? 'channel');
    const badgeColor = isHttp ? (METHOD_COLOR[op.method] ?? 'var(--_muted)') : 'var(--arazzo-status-running, #7048b7)';
    const id = op.operationId ?? (isHttp ? op.path : op.channelPath) ?? '(unnamed)';
    const where = isHttp ? op.path : op.channelPath;
    return `
      <button class="op" type="button" part="operation" draggable="true" data-source="${escapeHtml(sourceName)}" data-index="${index}" title="Drag onto the canvas to create a step bound to this operation">
        <span class="badge" style="background:${badgeColor}">${escapeHtml(badge)}</span>
        <span class="id${op.deprecated ? ' deprecated' : ''}">${escapeHtml(id)}${where && where !== id ? ` <span class="muted">${escapeHtml(where)}</span>` : ''}</span>
        ${op.summary ? `<span class="summary">${escapeHtml(op.summary)}</span>` : ''}
      </button>`;
  }

  // ---- actions ----------------------------------------------------------------------------------

  async detach(name) {
    const client = this.client;
    if (!client || !this._workingCopyId) return;
    try {
      // Detach is destructive (the declaration goes with it), so: say what happens, and STASH the
      // full attachment first — the host offers restore (a detach cannot ride the document's
      // undo stack: attachments are workspace state with their own etag lifecycle).
      const attachment = await client.getWorkingCopySource(this._workingCopyId, name).catch(() => null);
      const inline = attachment?.kind !== 'registry';
      const sure = await this.$('.ask').ask({
        title: `Detach '${name}'?`,
        message: inline
          ? 'Steps bound to its operations lose their surface; the sourceDescriptions declaration stays in the document (remove it there if it is no longer wanted). The stored document is kept for restore until you leave this working copy.'
          : 'Steps bound to its operations lose their surface; the sourceDescriptions declaration stays in the document. The source stays registered — it can be re-attached from the registry at any time.',
        confirmLabel: 'Detach',
        danger: true,
      });
      if (!sure) return;

      await client.detachWorkingCopySource(this._workingCopyId, name);
      this.emit('source-detached', { name, attachment });
      this.refresh();
    } catch (err) {
      this._error = err.problem || { title: err.message };
      this.renderBody();
      this.emit('error', { problem: this._error, error: err });
    }
  }
}

define('arazzo-operation-browser', ArazzoOperationBrowser);
export { ArazzoOperationBrowser };