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
    // Default the cursor inputs to the faulted cursor where it helps.
    const cursor = run?.cursor ?? 0;
    this.$('#rewindCursor').value = String(cursor);
    this.$('#skipCursor').value = '';
    this.$('#skipOutputs').value = '';
    this.$('#patch').value = '[\n  { "op": "replace", "path": "/inputs/example", "value": 1 }\n]';
    this.setMode('RetryFaultedStep');
    this.$('.subhead').textContent = run?.fault
      ? `Faulted at step "${run.fault.stepId}" (attempt ${run.fault.attempt}).`
      : 'Resume this run.';
    this.$('dialog').showModal();
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
                <div><label for="rewindCursor">Target cursor</label><input id="rewindCursor" type="number" min="0" step="1"></div>
              </div>
            </div>

            <div class="mode-fields" data-mode="Skip" hidden>
              <div class="fields">
                <div><label for="skipCursor">Target cursor (optional — defaults to faulted + 1)</label><input id="skipCursor" type="number" min="0" step="1"></div>
                <div><label for="skipOutputs">Skip outputs (optional JSON object)</label><textarea id="skipOutputs" rows="4" placeholder='{ "result": "manually-supplied" }'></textarea></div>
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
      const targetCursor = parseIntField(this.$('#rewindCursor').value, 'Target cursor');
      return { mode, targetCursor };
    }

    if (mode === 'Skip') {
      const req = { mode };
      const raw = this.$('#skipCursor').value.trim();
      if (raw !== '') req.targetCursor = parseIntField(raw, 'Target cursor');
      const outputs = this.$('#skipOutputs').value.trim();
      if (outputs !== '') {
        req.skipOutputs = parseJsonField(outputs, 'Skip outputs', 'object');
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

function parseIntField(value, label) {
  const n = Number(value);
  if (value === '' || !Number.isInteger(n) || n < 0) {
    throw new Error(`${label} must be a non-negative integer.`);
  }
  return n;
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
