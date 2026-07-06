// <arazzo-scenario-panel> — the working copy's scenario suite (design §3.4/§4.2): list, run one,
// run all, and the per-expectation verdicts; a run's full trace hands off to the debug tray
// (`run-trace`) so scenario failures debug exactly like interactive sessions. Editing is the TYPED
// scenario form (<arazzo-scenario-editor>, driven by getWorkingCopySchemas) with JSON one toggle
// away; when the schemas cannot load, editing falls back to JSON-only.
//
//   const panel = document.createElement('arazzo-scenario-panel');
//   panel.client = client;
//   panel.workingCopyId = 'wc-…';
//   panel.addEventListener('run-trace', (e) => { tray.trace = e.detail.trace; });
//
// Properties : .client, .workingCopyId
// Methods    : refresh()
// Events     : run-trace {scenario, trace}, scenarios-changed {count}, error {problem}

import { ArazzoElement, SHARED_CSS, escapeHtml, define } from './base.js';
import './scenario-editor.js';

class ArazzoScenarioPanel extends ArazzoElement {
  constructor() {
    super();
    /** @private */ this._workingCopyId = null;
    /** @private */ this._scenarios = [];
    /** @private */ this._results = new Map(); // name → run result
    /** @private */ this._editing = null;      // name being edited (guarded JSON)
    /** @private */ this._busy = false;
  }

  get workingCopyId() { return this._workingCopyId; }
  set workingCopyId(value) {
    this._workingCopyId = value || null;
    this._results.clear();
    if (this.isConnected) this.refresh();
  }

  connectedCallback() {
    this.render();
    this.refresh();
  }

  /** Reload the scenario set. */
  async refresh() {
    if (!this.client || !this._workingCopyId) {
      this._scenarios = [];
      this.render();
      return;
    }

    try {
      // The scenario set and the typed form's descriptors (recomputed for the current document)
      // load together, so an edit opened right after scenarios-changed sees the schemas. A schema
      // failure is not fatal: the editor falls back to JSON-only.
      const [{ scenarios }, schemas] = await Promise.all([
        this.client.listScenarios(this._workingCopyId),
        this.client.getWorkingCopySchemas(this._workingCopyId).catch(() => null),
      ]);
      this._scenarios = scenarios;
      this._schemas = schemas;
      this.render();
      this.emit('scenarios-changed', { count: scenarios.length });
    } catch (err) {
      this.emit('error', { problem: err.problem, error: err });
    }
  }

  /** @private */
  render() {
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        :host { display: block; font-size: 12px; }
        .bar { display: flex; align-items: center; gap: 8px; padding: 8px 10px; border-bottom: 1px solid var(--_border); }
        .bar .chip { font-weight: 600; }
        .bar button { font-size: 12px; padding: 2px 10px; }
        .empty-note { padding: 10px; color: var(--_muted); }
        .sc { border-bottom: 1px solid var(--_border); }
        .sc-head { display: flex; align-items: center; gap: 6px; padding: 6px 10px; }
        .sc-head .name { font: 600 12px ui-monospace, SFMono-Regular, Menlo, monospace; flex: 1; min-width: 0; overflow-wrap: anywhere; }
        .sc-head button { font-size: 11px; padding: 1px 7px; }
        .verdicts { padding: 0 10px 8px 22px; display: grid; gap: 2px; }
        .verdicts button { text-align: left; border: none; background: none; color: inherit; font: 11px ui-monospace, SFMono-Regular, Menlo, monospace; cursor: pointer; padding: 0; }
        .ok { color: var(--arazzo-status-completed, #2a8a4a); }
        .bad { color: var(--arazzo-status-faulted, #d4351c); }
        textarea { width: calc(100% - 20px); margin: 0 10px 8px; box-sizing: border-box; font: 11px ui-monospace, Menlo, monospace; min-height: 130px; border: 1px solid var(--_border); border-radius: 6px; background: var(--_bg); color: inherit; }
        textarea.invalid { border-color: var(--_danger); }
        .desc { color: var(--_muted); padding: 0 10px 6px 22px; }
      </style>
      <div class="bar" part="controls">
        <span class="chip">${this._scenarios.length} scenario${this._scenarios.length === 1 ? '' : 's'}</span>
        <button class="runall" type="button" ${this._scenarios.length && !this._busy ? '' : 'disabled'} title="Run every scenario as a suite">▶ Run all</button>
        <button class="new" type="button" title="Author a scenario from scratch — no run needed">+ New</button>
        <span class="suite muted"></span>
      </div>
      ${this._creating ? '<div class="editor-slot" data-name=""></div>' : ''}
      ${this._scenarios.length === 0 && !this._creating
        ? '<div class="empty-note">No scenarios yet — + New authors one from scratch, or run a debug session and "Save as scenario…".</div>'
        : this._scenarios.map((s) => this.renderScenario(s)).join('')}
    `;

    this.$('.runall')?.addEventListener('click', () => this.runAll());
    this.$('.new')?.addEventListener('click', () => { this._creating = true; this._editing = null; this.render(); });
    this.$$('.sc [data-run]').forEach((b) => b.addEventListener('click', () => this.runOne(b.dataset.run)));
    this.$$('.sc [data-del]').forEach((b) => b.addEventListener('click', () => this.delete(b.dataset.del)));
    this.$$('.sc [data-edit]').forEach((b) => b.addEventListener('click', () => { this._editing = this._editing === b.dataset.edit ? null : b.dataset.edit; this.render(); }));
    this.$$('.sc [data-trace]').forEach((b) => b.addEventListener('click', () => {
      const result = this._results.get(b.dataset.trace);
      if (result?.trace) this.emit('run-trace', { scenario: b.dataset.trace, trace: result.trace });
    }));
    this.$$('.editor-slot').forEach((slot) => {
      const editor = document.createElement('arazzo-scenario-editor');
      editor.schemas = this._schemas;
      editor.scenario = slot.dataset.name ? this._scenarios.find((x) => x.name === slot.dataset.name) : {};
      editor.addEventListener('scenario-changed', async (e) => {
        try {
          await this.client.putScenario(this._workingCopyId, e.detail.scenario);
          this._editing = null;
          this._creating = false;
          this.refresh();
        } catch (err) {
          this.emit('error', { problem: err.problem ?? { title: err.message }, error: err });
        }
      });
      editor.addEventListener('cancel', () => { this._editing = null; this._creating = false; this.render(); });
      slot.appendChild(editor);
    });
  }

  /** @private */
  renderScenario(s) {
    const result = this._results.get(s.name);
    const status = result ? (result.passed ? '<span class="ok">✓ passed</span>' : '<span class="bad">✗ failed</span>') : '';
    return `<div class="sc" part="scenario">
      <div class="sc-head">
        <span class="name">${escapeHtml(s.name)}</span>
        ${status}
        ${result?.trace ? `<button type="button" data-trace="${escapeHtml(s.name)}" title="Open this run's trace in the debug tray">🐞</button>` : ''}
        <button type="button" data-run="${escapeHtml(s.name)}" title="Run this scenario" ${this._busy ? 'disabled' : ''}>▶</button>
        <button type="button" data-edit="${escapeHtml(s.name)}" title="Edit (typed form; JSON one toggle away)">✎</button>
        <button type="button" class="ghost" data-del="${escapeHtml(s.name)}" title="Delete">✕</button>
      </div>
      ${s.description ? `<div class="desc">${escapeHtml(s.description)}</div>` : ''}
      ${result ? `<div class="verdicts">${result.expectations.map((v) =>
        `<button type="button" data-trace="${escapeHtml(s.name)}" title="Open the trace">
          <span class="${v.passed ? 'ok' : 'bad'}">${v.passed ? '✓' : '✗'}</span> [${escapeHtml(v.kind)}] ${escapeHtml(v.detail ?? '')}</button>`).join('')}</div>` : ''}
      ${this._editing === s.name ? `<div class="editor-slot" data-name="${escapeHtml(s.name)}"></div>` : ''}
    </div>`;
  }

  /** @private */
  async runOne(name) {
    this._busy = true;
    this.render();
    try {
      const result = await this.client.runScenario(this._workingCopyId, name);
      this._results.set(name, result);
    } catch (err) {
      this.emit('error', { problem: err.problem, error: err });
    }

    this._busy = false;
    this.render();
  }

  /** @private */
  async runAll() {
    this._busy = true;
    this.render();
    try {
      const suite = await this.client.runAllScenarios(this._workingCopyId);
      for (const result of suite.results) this._results.set(result.scenario, result);
      this._busy = false;
      this.render();
      this.$('.suite').textContent = `${suite.passed}/${suite.total} passed`;
    } catch (err) {
      this._busy = false;
      this.render();
      this.emit('error', { problem: err.problem, error: err });
    }
  }

  /** @private */
  async delete(name) {
    try {
      await this.client.deleteScenario(this._workingCopyId, name);
      this._results.delete(name);
      this.refresh();
    } catch (err) {
      this.emit('error', { problem: err.problem, error: err });
    }
  }
}

define('arazzo-scenario-panel', ArazzoScenarioPanel);
export { ArazzoScenarioPanel };