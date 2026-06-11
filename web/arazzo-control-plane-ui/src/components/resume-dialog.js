// <arazzo-resume-dialog> — a modal for the four resume modes of a faulted run.
//
//   const dlg = document.querySelector('arazzo-resume-dialog');
//   dlg.client = client;
//   dlg.open(run);   // run is a WorkflowRunDetail (or at least { id, fault, cursor })
//
// Properties : .client
// Methods    : open(run), close()
// Events     : resume-submitted {run}, error {problem}
//
// Modes (ResumeRequest oneOf, selected by `mode`):
//   RetryFaultedStep · Rewind {targetCursor} · Skip {targetCursor?, skipOutputs?} · StatePatch {patch[]}
// The StatePatch editor is a validated raw RFC 6902 JSON array (a visual builder can come later).

import { ArazzoElement, SHARED_CSS, escapeHtml, define } from './base.js';
import './workflow-step-picker.js';
import './value-editor.js';

class ArazzoResumeDialog extends ArazzoElement {
  constructor() {
    super();
    /** @private */ this._run = null;
  }

  connectedCallback() {
    if (!this._built) this.render();
  }

  /** Open the dialog for a run. */
  open(run) {
    this._run = run;
    if (!this._built) this.render();
    this.$('.error-banner').hidden = true;
    // Point the step pickers at this run's workflow + current cursor (they resolve the step list from the catalog).
    for (const picker of this.$$('arazzo-workflow-step-picker')) {
      picker.client = this.client;
      if (run?.workflowId) picker.setAttribute('workflow-id', run.workflowId); else picker.removeAttribute('workflow-id');
      if (run?.cursor != null) picker.setAttribute('cursor', String(run.cursor)); else picker.removeAttribute('cursor');
    }
    this.loadSkipDescriptor(run);
    // Outputs are only recorded when explicitly opted into; reset the toggle + hide the builder each open.
    this.$('.record-outputs').checked = false;
    this.$('.skip-outputs').hidden = true;
    this.$('#patch').value = '[\n  { "op": "replace", "path": "/inputs/example", "value": 1 }\n]';
    this.setMode('RetryFaultedStep');
    this.$('.subhead').textContent = run?.fault
      ? `Faulted at step "${run.fault.stepId}" (attempt ${run.fault.attempt}).`
      : 'Resume this run.';
    this.$('dialog').showModal();
  }

  /**
   * Resolve the typed schema of the step being skipped (the step at the run's cursor) from the catalog so the
   * skip-outputs builder renders a strongly-typed form; falls back to a raw-JSON editor when unavailable.
   * @param {object} run
   */
  async loadSkipDescriptor(run) {
    const builder = this.$('.skip-builder');
    if (!builder) return;
    builder.descriptor = null; // raw-JSON fallback until/unless a typed schema resolves
    builder.validator = null;
    this._skipParsed = null;
    this._skipStepId = null;
    const parsed = parseVersionedWorkflowId(run?.workflowId);
    const cursor = run?.cursor;
    if (!this.client || !parsed || cursor == null) return;
    try {
      const [workflow, schemas] = await Promise.all([
        this.client.getCatalogWorkflow(parsed.base, parsed.version),
        this.client.getCatalogWorkflowSchemas(parsed.base, parsed.version),
      ]);
      const wf = (workflow.workflows || []).find((w) => w.workflowId === run.workflowId) || (workflow.workflows || [])[0];
      const stepId = wf?.steps?.[cursor]?.stepId;
      const outputs = schemas?.workflows?.[run.workflowId]?.steps?.[stepId]?.outputs;
      if (stepId) {
        // Remember the resolved target so submit() can validate the recorded outputs server-side.
        this._skipParsed = parsed;
        this._skipStepId = stepId;
        // Live, inline validation as the operator edits (mirrors the submit-time gate).
        if (this.client.validateCatalogValue) {
          builder.validator = (value) => this.client.validateCatalogValue(
            parsed.base, parsed.version, { kind: 'stepOutputs', workflowId: run.workflowId, stepId }, value);
        }
      }
      if (stepId && outputs && Object.keys(outputs).length) {
        builder.descriptor = { type: 'object', properties: outputs };
      }
    } catch {
      // No catalog metadata (or no catalog:read) → the builder stays a raw-JSON editor.
    }
  }

  close() {
    this.$('dialog')?.close();
  }

  setMode(mode) {
    this.$$('input[name="mode"]').forEach((r) => { r.checked = r.value === mode; });
    this.$$('.mode-fields').forEach((el) => { el.hidden = el.dataset.mode !== mode; });
  }

  get mode() {
    return this.$('input[name="mode"]:checked')?.value || 'RetryFaultedStep';
  }

  render() {
    this._built = true;
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        dialog {
          border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_bg);
          color: var(--_text); padding: 0; width: min(560px, 94vw);
        }
        dialog::backdrop { background: rgba(0,0,0,0.4); }
        .head { padding: 14px 16px; border-bottom: 1px solid var(--_border); }
        .head .title { font-weight: 700; font-size: 15px; }
        .subhead { color: var(--_muted); font-size: 12px; margin-top: 2px; }
        .content { padding: 16px; display: grid; gap: 14px; max-height: 60vh; overflow: auto; }
        .modes { display: grid; gap: 6px; }
        .mode {
          display: grid; grid-template-columns: auto 1fr; gap: 8px; align-items: start;
          border: 1px solid var(--_border); border-radius: var(--_radius); padding: 8px 10px; cursor: pointer;
        }
        .mode:has(input:checked) { border-color: var(--_accent); background: color-mix(in srgb, var(--_accent) 8%, transparent); }
        .mode .name { font-weight: 600; }
        .mode .desc { font-size: 12px; color: var(--_muted); }
        .fields { display: grid; gap: 10px; }
        label { font-size: 12px; color: var(--_muted); display: block; margin-bottom: 4px; }
        input[type="number"], textarea {
          width: 100%; font: inherit; padding: 8px; border: 1px solid var(--_border);
          border-radius: var(--_radius); background: var(--_bg); color: var(--_text);
        }
        textarea { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 12px; resize: vertical; }
        label.check { display: flex; gap: 8px; align-items: center; color: var(--_text); font-size: 13px; cursor: pointer; margin: 0; }
        label.check input { width: auto; }
        .foot { display: flex; gap: 8px; justify-content: flex-end; padding: 12px 16px; border-top: 1px solid var(--_border); }
      </style>
      <dialog part="dialog">
        <form method="dialog">
          <div class="head">
            <div class="title">Resume run</div>
            <div class="subhead"></div>
          </div>
          <div class="content">
            <div class="modes" part="modes">
              ${this.modeOption('RetryFaultedStep', 'Retry faulted step', 'Re-run the step that faulted (the common case).')}
              ${this.modeOption('Rewind', 'Rewind', 'Reset the cursor and re-run forward, overwriting re-executed outputs.')}
              ${this.modeOption('Skip', 'Skip', 'Advance past the faulted step, optionally recording outputs for it.')}
              ${this.modeOption('StatePatch', 'State patch', 'Apply an RFC 6902 JSON Patch to the run context, then retry.')}
            </div>

            <div class="mode-fields" data-mode="Rewind" hidden>
              <div class="fields">
                <div><label>Target step (an earlier step to re-run from)</label><arazzo-workflow-step-picker class="rewind-picker" direction="backward"></arazzo-workflow-step-picker></div>
              </div>
            </div>

            <div class="mode-fields" data-mode="Skip" hidden>
              <div class="fields">
                <div><label>Target step (a later step to advance to)</label><arazzo-workflow-step-picker class="skip-picker" direction="forward"></arazzo-workflow-step-picker></div>
                <label class="check"><input type="checkbox" class="record-outputs"> Record outputs for skipped step</label>
                <div class="skip-outputs" hidden><arazzo-value-editor class="skip-builder"></arazzo-value-editor></div>
              </div>
            </div>

            <div class="mode-fields" data-mode="StatePatch" hidden>
              <div class="fields">
                <div><label for="patch">RFC 6902 JSON Patch over { inputs, stepOutputs }</label><textarea id="patch" rows="7"></textarea></div>
              </div>
            </div>

            <div class="error-banner" hidden></div>
          </div>
          <div class="foot">
            <button value="dismiss" class="ghost" type="submit">Cancel</button>
            <button value="confirm" class="primary confirm" type="submit">Resume</button>
          </div>
        </form>
      </dialog>
    `;

    this.$$('input[name="mode"]').forEach((r) => r.addEventListener('change', () => this.setMode(r.value)));
    this.$('.record-outputs').addEventListener('change', (e) => { this.$('.skip-outputs').hidden = !e.target.checked; });
    this.$('form').addEventListener('submit', (e) => {
      if (e.submitter?.value === 'confirm') {
        e.preventDefault();
        this.submit();
      }
    });
  }

  modeOption(value, name, desc) {
    return `<label class="mode">
      <input type="radio" name="mode" value="${value}">
      <span><span class="name">${escapeHtml(name)}</span><br><span class="desc">${escapeHtml(desc)}</span></span>
    </label>`;
  }

  /** Build the ResumeRequest body for the selected mode, or throw a friendly Error for invalid input. */
  buildRequest() {
    const mode = this.mode;
    if (mode === 'RetryFaultedStep') return { mode };

    if (mode === 'Rewind') {
      const targetCursor = this.$('.rewind-picker').value;
      if (targetCursor == null) throw new Error('Choose a target step to rewind to.');
      return { mode, targetCursor };
    }

    if (mode === 'Skip') {
      const req = { mode };
      const targetCursor = this.$('.skip-picker').value;
      if (targetCursor != null) req.targetCursor = targetCursor;
      // Only record outputs for the skipped step when the operator has opted in.
      if (this.$('.record-outputs').checked) {
        const outputs = this.$('.skip-builder').value; // may throw on invalid input — surfaced as the banner
        if (outputs !== undefined) {
          req.skipOutputs = outputs;
        }
      }
      return req;
    }

    // StatePatch
    const patch = parseJsonField(this.$('#patch').value, 'JSON Patch', 'array');
    if (patch.length === 0) throw new Error('Provide at least one JSON Patch operation.');
    return { mode, patch };
  }

  async submit() {
    const banner = this.$('.error-banner');
    banner.hidden = true;
    let request;
    try {
      request = this.buildRequest();
    } catch (err) {
      banner.textContent = err.message;
      banner.hidden = false;
      return;
    }

    const confirmBtn = this.$('.confirm');
    confirmBtn.disabled = true;
    try {
      // Before recording skip outputs, validate them server-side against the step's true output schema.
      if (request.mode === 'Skip' && request.skipOutputs !== undefined
        && this._skipParsed && this._skipStepId && this.client.validateCatalogValue) {
        try {
          const result = await this.client.validateCatalogValue(
            this._skipParsed.base, this._skipParsed.version,
            { kind: 'stepOutputs', workflowId: this._run.workflowId, stepId: this._skipStepId },
            request.skipOutputs);
          if (result && result.valid === false) {
            banner.textContent = 'Recorded outputs are invalid — '
              + (result.errors || []).map((e) => `${e.instancePath || '/'}: ${e.message}`).join('; ');
            banner.hidden = false;
            return;
          }
        } catch {
          // Validation unavailable (no endpoint / no catalog:read) → don't block the resume.
        }
      }

      const run = await this.client.resumeRun(this._run.id, request);
      this.close();
      this.emit('resume-submitted', { run, mode: request.mode });
    } catch (err) {
      const problem = err.problem || { title: err.message };
      banner.textContent = `${problem.title || 'Resume failed'}${problem.detail ? ' — ' + problem.detail : ''}`;
      banner.hidden = false;
      this.emit('error', { problem, error: err });
    } finally {
      confirmBtn.disabled = false;
    }
  }
}

/** Split a versioned workflow id (`base-vN`) into `{ base, version }`, or null when not versioned. */
function parseVersionedWorkflowId(workflowId) {
  const m = /^(.*)-v(\d+)$/.exec(workflowId || '');
  return m ? { base: m[1], version: Number(m[2]) } : null;
}

function parseJsonField(value, label, expect) {
  let parsed;
  try {
    parsed = JSON.parse(value);
  } catch {
    throw new Error(`${label} is not valid JSON.`);
  }
  if (expect === 'array' && !Array.isArray(parsed)) throw new Error(`${label} must be a JSON array.`);
  if (expect === 'object' && (typeof parsed !== 'object' || parsed === null || Array.isArray(parsed))) {
    throw new Error(`${label} must be a JSON object.`);
  }
  return parsed;
}

define('arazzo-resume-dialog', ArazzoResumeDialog);
export { ArazzoResumeDialog };
