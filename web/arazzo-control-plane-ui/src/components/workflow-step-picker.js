// <arazzo-workflow-step-picker> — choose a target step of a workflow by name instead of a raw cursor index.
//
//   <arazzo-workflow-step-picker workflow-id="adopt-pet-v1" cursor="2" direction="forward"></arazzo-workflow-step-picker>
//   picker.client = client;   // an ArazzoControlPlaneClient (needs catalog:read)
//   const targetCursor = picker.value;   // the chosen step index
//
// Attributes : workflow-id (the run's versioned workflow id), cursor (the run's current step index),
//              direction ("forward" = only steps after the cursor — Skip; "backward" = only steps before
//              the cursor — Rewind; omitted = all steps)
// Properties : .client, .value (the selected step index, a number), .steps (read-only)
// Events     : change {index, stepId}
//
// It resolves the workflow's step list from the catalog (getCatalogWorkflow on the base id + version parsed
// from `workflow-id`) and renders a step chooser mapping each step to its cursor index, constrained by
// `direction` so Skip can only move forward and Rewind can only move backward. If the workflow isn't in the
// catalog (or there's no catalog access), it falls back to a plain numeric input (bounded by direction) so
// resume still works.

import { ArazzoElement, SHARED_CSS, escapeHtml, define } from './base.js';

class ArazzoWorkflowStepPicker extends ArazzoElement {
  static get observedAttributes() {
    return ['workflow-id', 'cursor', 'base-url', 'direction'];
  }

  constructor() {
    super();
    /** @private */ this._steps = null;
    /** @private */ this._stepsKey = null;
    /** @private */ this._reqSeq = 0;
  }

  connectedCallback() {
    this.renderShell();
    this.renderBody();
    this.load();
  }

  attributeChangedCallback(name, oldValue, newValue) {
    if (!this.isConnected || oldValue === newValue) return;
    if (name === 'workflow-id' || name === 'base-url') this.load();
    else this.renderBody();
  }

  get workflowId() { return this.getAttribute('workflow-id') || null; }
  set workflowId(value) { if (value) this.setAttribute('workflow-id', value); else this.removeAttribute('workflow-id'); }

  get cursor() { const c = this.getAttribute('cursor'); return c == null || c === '' ? null : Number(c); }
  set cursor(value) { if (value == null) this.removeAttribute('cursor'); else this.setAttribute('cursor', String(value)); }

  /** "forward" (Skip — steps after the cursor), "backward" (Rewind — steps before the cursor), or null. */
  get direction() { const d = this.getAttribute('direction'); return d === 'forward' || d === 'backward' ? d : null; }
  set direction(value) { if (value) this.setAttribute('direction', value); else this.removeAttribute('direction'); }

  /** The resolved steps (`[{ index, stepId }]`), or `null` while loading / when unavailable. */
  get steps() { return this._steps; }

  /** The steps selectable given `direction` relative to `cursor` (all steps when unconstrained). */
  visibleSteps() {
    if (!this._steps) return null;
    const cursor = this.cursor;
    const dir = this.direction;
    if (cursor == null || !dir) return this._steps;
    return this._steps.filter((s) => (dir === 'forward' ? s.index > cursor : s.index < cursor));
  }

  /** The selected target step index (a number), or `null` when there's no valid target. */
  get value() {
    const control = this.$('select') || this.$('input');
    if (!control) return null;
    return control.value === '' ? null : Number(control.value);
  }

  set value(v) {
    const control = this.$('select') || this.$('input');
    if (control) control.value = v == null ? '' : String(v);
  }

  requestRender() { this.load(); }

  // ---- loading ----------------------------------------------------------------------------------

  async load() {
    const client = this.client;
    const workflowId = this.workflowId;
    const parsed = parseVersionedId(workflowId);
    if (!client || !parsed) { this._steps = null; this._stepsKey = null; this.renderBody(); return; }
    if (this._stepsKey === workflowId && this._steps) { this.renderBody(); return; }

    const seq = ++this._reqSeq;
    try {
      const doc = await client.getCatalogWorkflow(parsed.base, parsed.version);
      if (seq !== this._reqSeq) return;
      const workflows = Array.isArray(doc?.workflows) ? doc.workflows : [];
      const wf = workflows.find((w) => w.workflowId === workflowId) || workflows[0];
      const steps = Array.isArray(wf?.steps) ? wf.steps : [];
      this._steps = steps.length ? steps.map((s, i) => ({ index: i, stepId: s?.stepId || `step ${i}` })) : null;
      this._stepsKey = workflowId;
      this.renderBody();
    } catch {
      if (seq !== this._reqSeq) return;
      this._steps = null; // no catalog access / not catalogued → numeric fallback
      this.renderBody();
    }
  }

  // ---- rendering --------------------------------------------------------------------------------

  renderShell() {
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        :host { display: block; }
        select, input {
          width: 100%; font: inherit; padding: 8px; border: 1px solid var(--_border);
          border-radius: var(--_radius); background-color: var(--_bg); color: var(--_text);
        }
        select { padding-right: 30px; }
        .hint { font-size: 11px; color: var(--_muted); margin-top: 4px; }
        .current { font-weight: 600; }
      </style>
      <div class="body"></div>
    `;
  }

  renderBody() {
    const body = this.$('.body');
    if (!body) return;
    const cursor = this.cursor;
    const dir = this.direction;
    const visible = this.visibleSteps();

    if (this._steps && this._steps.length) {
      if (!visible.length) {
        // The workflow has steps, but none in the requested direction (e.g. faulted at the last/first step).
        const which = dir === 'forward' ? 'after' : 'before';
        body.innerHTML = `<div class="hint empty">No steps ${which} the current one — nothing to ${dir === 'forward' ? 'skip to' : 'rewind to'}.</div>`;
        return;
      }
      // Default to the step nearest the cursor in the chosen direction (the next step for Skip, the
      // immediately preceding step for Rewind); with no direction, default to the current cursor.
      const selected = dir === 'forward' ? visible[0].index
        : dir === 'backward' ? visible[visible.length - 1].index
        : cursor;
      const options = visible.map((s) => {
        const isCurrent = s.index === cursor;
        return `<option value="${s.index}"${s.index === selected ? ' selected' : ''}>${s.index}: ${escapeHtml(s.stepId)}${isCurrent ? ' (current)' : ''}</option>`;
      }).join('');
      const hint = dir === 'forward' ? 'Choose a later step to skip to.'
        : dir === 'backward' ? 'Choose an earlier step to rewind to.'
        : 'Choose the step to resume at.';
      body.innerHTML = `<select part="select" aria-label="Target step">${options}</select>
        <div class="hint">${hint}</div>`;
      this.$('select').addEventListener('change', (e) => {
        const index = Number(e.target.value);
        this.emit('change', { index, stepId: this._steps[index]?.stepId });
      });
    } else {
      // Fallback: the workflow isn't in the catalog (or no catalog access) — a plain step-index input,
      // bounded by direction so it can't cross the cursor.
      const min = dir === 'forward' && cursor != null ? cursor + 1 : 0;
      const maxAttr = dir === 'backward' && cursor != null ? ` max="${cursor - 1}"` : '';
      const seed = dir === 'forward' && cursor != null ? cursor + 1
        : dir === 'backward' && cursor != null ? Math.max(0, cursor - 1)
        : cursor;
      body.innerHTML = `<input part="input" type="number" min="${min}"${maxAttr} step="1" value="${seed == null ? '' : escapeHtml(String(seed))}" aria-label="Target step index">
        <div class="hint">Step index (the workflow isn't in the catalog, so steps can't be listed).</div>`;
      this.$('input').addEventListener('change', (e) => this.emit('change', { index: e.target.value === '' ? null : Number(e.target.value) }));
    }
  }
}

/** Split a versioned workflow id (`base-vN`) into `{ base, version }`, or return null if it isn't versioned. */
function parseVersionedId(workflowId) {
  const m = /^(.*)-v(\d+)$/.exec(workflowId || '');
  return m ? { base: m[1], version: Number(m[2]) } : null;
}

define('arazzo-workflow-step-picker', ArazzoWorkflowStepPicker);
export { ArazzoWorkflowStepPicker };
