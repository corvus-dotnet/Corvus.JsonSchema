// <arazzo-scenario-editor> — the typed scenario form (design §4.2): inputs typed by the workflow's
// inputs descriptor, mocks addressed by the document's own operations with bodies typed by the
// DECLARED response status, expectations picked from the workflow's steps, and triggers/clock —
// all driven by the working copy's recomputed schema metadata (getWorkingCopySchemas). A JSON view
// stays one toggle away (hand-editing remains available), and every edit overlays the original
// scenario object, so fields the form doesn't know survive a round-trip.
//
//   const editor = document.createElement('arazzo-scenario-editor');
//   editor.schemas = schemas;                 // {formatVersion, workflows} — null falls back to JSON-only
//   editor.scenario = scenario;               // the scenario value being edited
//
// Properties : .schemas, .scenario
// Events     : scenario-changed {scenario} (Save) · cancel
//
// The status picker offers only the operation's DECLARED responses — correct by construction: the
// simulator faults on undeclared mock statuses.

import { ArazzoElement, SHARED_CSS, escapeHtml, define } from './base.js';
import './value-editor.js';

const OUTCOMES = ['completed', 'faulted', 'suspended', 'paused', 'budgetExhausted'];

class ArazzoScenarioEditor extends ArazzoElement {
  constructor() {
    super();
    /** @private */ this._working = {};
    /** @private */ this._schemas = null;
    /** @private */ this._json = false;
  }

  /** The schema-metadata document ({formatVersion, workflows}); null renders the JSON view only. */
  set schemas(value) {
    this._schemas = value;
    this._json = this._json || !value;
    if (this.isConnected) this.render();
  }

  get schemas() { return this._schemas; }

  /** The scenario value under edit (cloned; Save emits the edited copy). */
  set scenario(value) {
    this._working = structuredClone(value ?? {});
    this._json = !this._schemas;
    if (this.isConnected) this.render();
  }

  get scenario() { return this._working; }

  connectedCallback() {
    this.render();
  }

  // ---- schema lookups -----------------------------------------------------------------------------

  /** @private The workflow scenarios run against — the document's first (the run seam compiles it first). */
  firstWorkflow() {
    const workflows = this._schemas?.workflows ?? {};
    const first = Object.keys(workflows)[0];
    return first ? workflows[first] : null;
  }

  /** @private (source, operationId) → {kind, responses} from the steps' resolved operations. */
  operations() {
    const ops = new Map();
    for (const step of Object.values(this.firstWorkflow()?.steps ?? {})) {
      const op = step.operation;
      if (op?.source && op?.operationId && !ops.has(`${op.source}::${op.operationId}`)) {
        ops.set(`${op.source}::${op.operationId}`, { source: op.source, operationId: op.operationId, kind: op.kind, responses: step.responses ?? {} });
      }
    }

    return ops;
  }

  /** @private */
  stepIds() { return Object.keys(this.firstWorkflow()?.steps ?? {}); }

  // ---- rendering ----------------------------------------------------------------------------------

  render() {
    const s = this._working;
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        :host { display: block; font-size: 12px; }
        .frame { border: 1px solid var(--_border); border-radius: 8px; margin: 0 10px 8px; padding: 8px 10px; display: grid; gap: 10px; }
        h5 { margin: 0; font-size: 11px; color: var(--_muted); text-transform: uppercase; letter-spacing: 0.03em; }
        section { display: grid; gap: 6px; }
        label.inline { display: grid; gap: 3px; font-size: 11px; color: var(--_muted); }
        input, select, textarea { font: inherit; font-size: 12px; padding: 4px 6px; border: 1px solid var(--_border); border-radius: 6px; background: var(--_bg); color: var(--arazzo-text, inherit); }
        textarea.json { width: 100%; box-sizing: border-box; font: 11px ui-monospace, SFMono-Regular, Menlo, monospace; min-height: 220px; }
        .row { border: 1px solid var(--_border); border-radius: 6px; padding: 6px 8px; display: grid; gap: 6px; }
        .row-head { display: flex; gap: 6px; align-items: center; }
        .row-head select, .row-head input { flex: 1; min-width: 0; }
        .row-head .del { flex: 0; }
        .resp { display: grid; grid-template-columns: auto 1fr auto; gap: 6px; align-items: start; }
        .path-steps { display: flex; flex-wrap: wrap; gap: 4px; align-items: center; }
        .path-steps select { max-width: 160px; }
        .kv { display: grid; grid-template-columns: 1fr auto auto auto; gap: 6px; align-items: center; }
        .add { justify-self: start; font-size: 11px; }
        .foot { display: flex; gap: 8px; align-items: center; }
        .spacer { flex: 1; }
        .error-banner[hidden] { display: none; }
        label.check { display: flex; gap: 6px; align-items: center; color: var(--_text); cursor: pointer; font-size: 12px; }
        label.check input { width: auto; }
        button { font-size: 12px; }
      </style>
      <div class="frame" part="editor">
        <div class="error-banner" hidden></div>
        ${this._json ? this.renderJson() : this.renderForm(s)}
        <div class="foot">
          <button class="save" type="button">Save</button>
          <button class="cancel ghost" type="button">Cancel</button>
          <span class="spacer"></span>
          <button class="toggle ghost" type="button" ${this._schemas ? '' : 'disabled title="No schema metadata — JSON only"'}>${this._json ? 'Form' : '{ } JSON'}</button>
        </div>
      </div>`;

    this.$('.save').addEventListener('click', () => this.save());
    this.$('.cancel').addEventListener('click', () => this.emit('cancel'));
    this.$('.toggle').addEventListener('click', () => this.toggleView());
    if (this._json) return;

    // Typed sub-editors get their seeds THEN descriptors after the shell exists (the descriptor
    // setter renders, consuming the seed).
    const inputsEditor = this.$('.inputs-editor');
    if (inputsEditor) {
      inputsEditor.seed = s.inputs;
      inputsEditor.descriptor = this.firstWorkflow()?.inputs ?? { type: 'unknown' };
    }

    const ops = this.operations();
    this.$$('.resp').forEach((resp) => {
      const mock = s.mocks?.[Number(resp.dataset.i)];
      const response = mock?.responses?.[Number(resp.dataset.j)];
      const op = ops.get(`${mock?.source}::${mock?.operationId}`);
      const body = resp.querySelector('arazzo-value-editor');
      body.seed = response?.body;
      body.descriptor = op?.responses?.[String(response?.status)]?.body ?? { type: 'unknown' };
    });

    // Structural edits collect the current form state first, then mutate and re-render.
    this.$$('.m-op').forEach((sel) => sel.addEventListener('change', () => this.restructure((w) => {
      const op = ops.get(sel.value);
      const mock = w.mocks[Number(sel.closest('.row').dataset.i)];
      mock.source = op?.source;
      mock.operationId = op?.operationId;
      const declared = Object.keys(op?.responses ?? {});
      for (const response of mock.responses ?? []) {
        if (!declared.includes(String(response.status))) response.status = Number(declared[0] ?? 200);
      }
    })));
    this.$$('.r-status').forEach((sel) => sel.addEventListener('change', () => this.restructure((w) => {
      const resp = sel.closest('.resp');
      w.mocks[Number(resp.dataset.i)].responses[Number(resp.dataset.j)].status = Number(sel.value);
    })));
    this.$('.add-mock')?.addEventListener('click', () => this.restructure((w) => {
      const first = [...ops.values()][0];
      (w.mocks ??= []).push({ source: first?.source, operationId: first?.operationId, responses: [{ status: Number(Object.keys(first?.responses ?? {})[0] ?? 200) }] });
    }));
    this.$$('[data-del-mock]').forEach((b) => b.addEventListener('click', () => this.restructure((w) => w.mocks.splice(Number(b.dataset.delMock), 1))));
    this.$$('[data-add-resp]').forEach((b) => b.addEventListener('click', () => this.restructure((w) => {
      const mock = w.mocks[Number(b.dataset.addResp)];
      const op = ops.get(`${mock.source}::${mock.operationId}`);
      (mock.responses ??= []).push({ status: Number(Object.keys(op?.responses ?? {})[0] ?? 200) });
    })));
    this.$$('[data-del-resp]').forEach((b) => b.addEventListener('click', () => this.restructure((w) => {
      const [i, j] = b.dataset.delResp.split(',').map(Number);
      w.mocks[i].responses.splice(j, 1);
    })));
    this.$('.add-path')?.addEventListener('click', () => this.restructure((w) => {
      w.expect ??= {};
      (w.expect.path ??= []).push(this.stepIds()[0] ?? '');
    }));
    this.$$('[data-del-path]').forEach((b) => b.addEventListener('click', () => this.restructure((w) => w.expect.path.splice(Number(b.dataset.delPath), 1))));
    this.$('.add-output')?.addEventListener('click', () => this.restructure((w) => {
      w.expect ??= {};
      (w.expect.outputs ??= []).push({ condition: '' });
    }));
    this.$$('[data-del-output]').forEach((b) => b.addEventListener('click', () => this.restructure((w) => w.expect.outputs.splice(Number(b.dataset.delOutput), 1))));
    this.$('.add-stepx')?.addEventListener('click', () => this.restructure((w) => {
      w.expect ??= {};
      const free = this.stepIds().find((id) => !(id in (w.expect.steps ?? {}))) ?? this.stepIds()[0];
      if (free) (w.expect.steps ??= {})[free] = { reached: true };
    }));
    this.$$('[data-del-stepx]').forEach((b) => b.addEventListener('click', () => this.restructure((w) => delete w.expect.steps[b.dataset.delStepx])));
    this.$('.add-trigger')?.addEventListener('click', () => this.restructure((w) => (w.triggers ??= []).push({ channel: '' })));
    this.$$('[data-del-trigger]').forEach((b) => b.addEventListener('click', () => this.restructure((w) => w.triggers.splice(Number(b.dataset.delTrigger), 1))));
  }

  /** @private */
  renderJson() {
    return `<section>
      <h5>Scenario (JSON)</h5>
      <textarea class="json" spellcheck="false">${escapeHtml(JSON.stringify(this._working, null, 2))}</textarea>
    </section>`;
  }

  /** @private */
  renderForm(s) {
    const ops = this.operations();
    const steps = this.stepIds();
    const expect = s.expect ?? {};
    const opOptions = (mock) => {
      const current = `${mock?.source}::${mock?.operationId}`;
      const entries = [...ops.entries()];
      if (mock?.source && !ops.has(current)) entries.push([current, { source: mock.source, operationId: mock.operationId }]);
      return entries.map(([key, op]) =>
        `<option value="${escapeHtml(key)}"${key === current ? ' selected' : ''}>${escapeHtml(`${op.source} · ${op.operationId}`)}</option>`).join('');
    };
    const statusOptions = (mock, status) => {
      const declared = Object.keys(ops.get(`${mock?.source}::${mock?.operationId}`)?.responses ?? {});
      if (status !== undefined && !declared.includes(String(status))) declared.push(String(status));
      return declared.map((code) => `<option value="${escapeHtml(code)}"${String(status) === code ? ' selected' : ''}>${escapeHtml(code)}</option>`).join('');
    };
    const stepOptions = (picked) => steps.map((id) => `<option value="${escapeHtml(id)}"${id === picked ? ' selected' : ''}>${escapeHtml(id)}</option>`).join('');

    return `
      <section>
        <h5>Scenario</h5>
        <label class="inline">Description <input class="f-desc" type="text" value="${escapeHtml(s.description ?? '')}" placeholder="(optional)"></label>
      </section>
      <section>
        <h5>Inputs — typed by the workflow's inputs schema</h5>
        <arazzo-value-editor class="inputs-editor"></arazzo-value-editor>
      </section>
      <section>
        <h5>Mocks — the document's own operations; bodies typed by the declared status</h5>
        ${(s.mocks ?? []).map((mock, i) => `
          <div class="row" data-i="${i}">
            <div class="row-head">
              <select class="m-op">${opOptions(mock)}</select>
              <button type="button" class="ghost del" data-del-mock="${i}" title="Remove mock">✕</button>
            </div>
            ${(mock.responses ?? []).map((response, j) => `
              <div class="resp" data-i="${i}" data-j="${j}">
                <select class="r-status" title="Declared response statuses only — the simulator faults on undeclared ones">${statusOptions(mock, response.status)}</select>
                <arazzo-value-editor class="r-body"></arazzo-value-editor>
                <button type="button" class="ghost" data-del-resp="${i},${j}" title="Remove response">✕</button>
              </div>`).join('')}
            <button type="button" class="add" data-add-resp="${i}">+ response</button>
          </div>`).join('')}
        <button type="button" class="add add-mock">+ mock</button>
      </section>
      <section>
        <h5>Expectations</h5>
        <label class="inline">Outcome
          <select class="x-outcome">
            <option value="">(implicit: completed)</option>
            ${OUTCOMES.map((o) => `<option value="${o}"${expect.outcome === o ? ' selected' : ''}>${o}</option>`).join('')}
          </select>
        </label>
        <label class="inline">Path — visited steps, in order
          <span class="path-steps">
            ${(expect.path ?? []).map((id, i) => `<span><select class="x-path" data-i="${i}">${stepOptions(id)}</select><button type="button" class="ghost" data-del-path="${i}" title="Remove">✕</button></span>`).join('')}
            <button type="button" class="add add-path">+ step</button>
            <label class="check"><input type="checkbox" class="x-exact"${expect.pathMode === 'exact' ? ' checked' : ''}> exact</label>
          </span>
        </label>
        <label class="inline">$outputs conditions
          ${(expect.outputs ?? []).map((o, i) => `<span class="row-head"><input class="x-output" data-i="${i}" type="text" value="${escapeHtml(o.condition ?? '')}" placeholder="$outputs.name == 'Fido'"><button type="button" class="ghost del" data-del-output="${i}" title="Remove">✕</button></span>`).join('')}
          <button type="button" class="add add-output">+ condition</button>
        </label>
        <label class="inline">Per-step
          ${Object.entries(expect.steps ?? {}).map(([stepId, x]) => `
            <span class="kv" data-step="${escapeHtml(stepId)}">
              <select class="x-step">${stepOptions(stepId)}</select>
              <select class="x-reached">
                <option value=""${x.reached === undefined ? ' selected' : ''}>reached: any</option>
                <option value="true"${x.reached === true ? ' selected' : ''}>reached</option>
                <option value="false"${x.reached === false ? ' selected' : ''}>not reached</option>
              </select>
              <input class="x-attempts" type="number" min="0" placeholder="attempts" value="${x.attempts ?? ''}" style="width:80px">
              <button type="button" class="ghost" data-del-stepx="${escapeHtml(stepId)}" title="Remove">✕</button>
            </span>`).join('')}
          <button type="button" class="add add-stepx">+ step expectation</button>
        </label>
      </section>
      <section>
        <h5>Triggers &amp; clock</h5>
        ${(s.triggers ?? []).map((t, i) => `
          <span class="row-head" data-i="${i}">
            <input class="t-channel" data-i="${i}" type="text" value="${escapeHtml(t.channel ?? '')}" placeholder="channel">
            <input class="t-corr" data-i="${i}" type="text" value="${escapeHtml(t.correlationId ?? '')}" placeholder="correlationId (optional)">
            <button type="button" class="ghost del" data-del-trigger="${i}" title="Remove">✕</button>
          </span>`).join('')}
        <button type="button" class="add add-trigger">+ trigger</button>
        <label class="check"><input type="checkbox" class="c-auto"${(s.clock?.autoAdvance ?? true) ? ' checked' : ''}> auto-advance the virtual clock past timers</label>
      </section>`;
  }

  // ---- collect / save -----------------------------------------------------------------------------

  /** @private Reads the form back into the working object (overlay: unknown fields survive). */
  collect() {
    if (this._json) {
      this._working = JSON.parse(this.$('.json').value);
      return;
    }

    const w = this._working;
    const description = this.$('.f-desc').value.trim();
    if (description) w.description = description; else delete w.description;

    const inputs = this.$('.inputs-editor').value;
    if (inputs !== undefined) w.inputs = inputs; else delete w.inputs;

    this.$$('.resp').forEach((resp) => {
      const response = w.mocks[Number(resp.dataset.i)].responses[Number(resp.dataset.j)];
      response.status = Number(resp.querySelector('.r-status').value);
      const body = resp.querySelector('arazzo-value-editor').value;
      if (body !== undefined) response.body = body; else delete response.body;
    });
    if (w.mocks && w.mocks.length === 0) delete w.mocks;

    const expect = w.expect ?? {};
    const outcome = this.$('.x-outcome').value;
    if (outcome) expect.outcome = outcome; else delete expect.outcome;
    const path = this.$$('.x-path').map((sel) => sel.value).filter(Boolean);
    if (path.length > 0) expect.path = path; else delete expect.path;
    if (this.$('.x-exact').checked && path.length > 0) expect.pathMode = 'exact'; else delete expect.pathMode;
    const outputs = this.$$('.x-output').map((input, i) => {
      const base = expect.outputs?.[i] ?? {};
      base.condition = input.value.trim();
      return base;
    }).filter((o) => o.condition);
    if (outputs.length > 0) expect.outputs = outputs; else delete expect.outputs;
    const stepEntries = this.$$('.kv[data-step]').map((row) => {
      const base = expect.steps?.[row.dataset.step] ?? {};
      const reached = row.querySelector('.x-reached').value;
      if (reached) base.reached = reached === 'true'; else delete base.reached;
      const attempts = row.querySelector('.x-attempts').value;
      if (attempts !== '') base.attempts = Number(attempts); else delete base.attempts;
      return [row.querySelector('.x-step').value, base];
    });
    if (stepEntries.length > 0) expect.steps = Object.fromEntries(stepEntries); else delete expect.steps;
    if (Object.keys(expect).length > 0) w.expect = expect; else delete w.expect;

    this.$$('.t-channel').forEach((input) => {
      const trigger = w.triggers[Number(input.dataset.i)];
      trigger.channel = input.value.trim();
    });
    this.$$('.t-corr').forEach((input) => {
      const trigger = w.triggers[Number(input.dataset.i)];
      const value = input.value.trim();
      if (value) trigger.correlationId = value; else delete trigger.correlationId;
    });
    if (w.triggers) {
      w.triggers = w.triggers.filter((t) => t.channel);
      if (w.triggers.length === 0) delete w.triggers;
    }

    const clock = w.clock ?? {};
    if (this.$('.c-auto').checked) delete clock.autoAdvance; else clock.autoAdvance = false;
    if (Object.keys(clock).length > 0) w.clock = clock; else delete w.clock;
  }

  /** @private A structural change: capture the form, mutate, re-render. */
  restructure(mutate) {
    try {
      this.collect();
    } catch {
      // A momentarily-invalid field shouldn't block adding/removing rows; the value re-renders as held.
    }

    mutate(this._working);
    this.render();
  }

  /** @private */
  toggleView() {
    try {
      this.collect();
    } catch (err) {
      this.showError(err.message);
      return;
    }

    this._json = !this._json;
    this.render();
  }

  /** @private */
  save() {
    try {
      this.collect();
    } catch (err) {
      this.showError(err.message);
      return;
    }

    if (!this._working.name) {
      this.showError('The scenario has no name.');
      return;
    }

    this.emit('scenario-changed', { scenario: this._working });
  }

  /** @private */
  showError(message) {
    const banner = this.$('.error-banner');
    banner.textContent = message;
    banner.hidden = false;
  }
}

define('arazzo-scenario-editor', ArazzoScenarioEditor);
export { ArazzoScenarioEditor };