// <arazzo-debug-tray> — the debug session's tray (design §3.3): the trace viewer (every executed
// step, click-to-inspect), the paused-context explorer (exchanges, criterion truth table, outputs),
// and the time-travel scrubber. STATELESS by design (§8.2): the tray renders ONE complete trace
// from `simulateWorkingCopy`; scrubbing and stepping through it are pure cursor movement — no
// further calls. The HOST re-simulates (run / step-past-pause / re-run-after-edit) and assigns a
// fresh trace.
//
//   const tray = document.createElement('arazzo-debug-tray');
//   tray.trace = simulationTrace;          // the §4.3 SimulationTrace payload (null clears)
//   tray.cursor = tray.length;             // frame index: k = "about to run step k"; length = final state
//   tray.addEventListener('cursor-changed', (e) => { /* project frame e.detail.index onto the canvas */ });
//
// Properties : .trace, .cursor (clamped), .length (steps count)
// Methods    : frameAt(index) → {active, steps, edges} for <arazzo-design-surface>.debugState
// Events     : cursor-changed {index}, clear-requested, step-requested (cursor at end of a paused trace),
//              trigger-injected {channel, payload?, correlationId?} (a suspended run's message wait —
//              the host adds it to the session scenario and replays),
//              workflow-focus {workflowId|null} (stepping into/out of a sub-workflow's nested trace —
//              the host switches the canvas to that workflow; null = the run's own workflow),
//              output-override {stepId, outputs} (step over: the provided outputs, collected in the
//              typed inline editor; the host forces them — Skip on a debug run, replay otherwise)

import { ArazzoElement, SHARED_CSS, escapeHtml, define } from './base.js';
import { actionEdgeId } from '../workflow-graph.js';
import './value-editor.js';

const OUTCOME_LABEL = {
  completed: '✓ completed',
  faulted: '✗ faulted',
  paused: '⏸ paused',
  suspended: '⏳ suspended',
  budgetExhausted: '⏸ paused (budget)',
};

class ArazzoDebugTray extends ArazzoElement {
  constructor() {
    super();
    /** @private */ this._trace = null;
    /** @private */ this._cursor = 0;
    /** @private */ this._stack = []; // ancestor frames while stepped into a sub-workflow's trace
    /** stepId → {outputs?: descriptors} — types the step-over editor (host-fed from the schemas endpoint). */
    this.stepSchemas = {};
    /** channelPath → message payload schema — types the inject-trigger editor. */
    this.channelSchemas = {};
  }

  connectedCallback() {
    this.render();
  }

  /** The SimulationTrace payload; assigning re-renders with the cursor at the end. */
  get trace() { return this._trace; }
  set trace(value) {
    this._trace = value || null;
    this._stack = [];
    this._cursor = this.length;
    if (this.isConnected) this.render();
  }

  /** How many executed-step frames the trace holds. */
  get length() { return this._trace?.steps?.length ?? 0; }

  /** The frame index: k = "about to run step k"; length = the final state. */
  get cursor() { return this._cursor; }
  set cursor(value) {
    const next = Math.max(0, Math.min(this.length, Number(value) || 0));
    if (next === this._cursor) return;
    this._cursor = next;
    this.render();
    this.emit('cursor-changed', { index: next });
  }

  /** Projects frame `index` to the design surface's debugState shape. */
  frameAt(index) {
    const trace = this._trace;
    if (!trace) return null;
    const k = Math.max(0, Math.min(this.length, index));
    const steps = {};
    const edges = ['seq:#start'];
    let pausedParent = null;
    for (let i = 0; i < k; i++) {
      const record = trace.steps[i];
      const failedCriteria = (record.successCriteria ?? []).some((c) => !c.satisfied);
      // A step is on the FAILURE path when it faulted OR a success criterion was unsatisfied — the
      // single source for both the node status and the taken action's edge kind (design §10 bug 3);
      // the edge id itself is minted by the shared grammar so it matches the projection exactly.
      const onFailurePath = record.status === 'faulted' || failedCriteria;
      // An in-progress sub-workflow parent (its child paused or suspended) is not DONE (§3.5): it
      // stays out of the done map and pulses as the active node when the scrub reaches the end.
      if (record.status === 'paused' || record.status === 'suspended') {
        pausedParent = record.stepId;
        continue;
      }
      steps[record.stepId] = onFailurePath ? 'done-failure' : 'done-success';
      const kind = onFailurePath ? 'failure' : 'success';
      const action = record.actionTaken;
      if (!action || action.type === 'fallThrough') {
        edges.push(`seq:${record.stepId}`);
      } else if (action.type === 'goto' && action.target) {
        edges.push(actionEdgeId(record.stepId, kind, { type: 'goto', name: action.name, target: action.target }));
      } else if (action.type === 'end') {
        edges.push(actionEdgeId(record.stepId, kind, { type: 'end', name: action.name }));
      }
    }

    // At the end of a live paused trace (breakpoint, run-to-here, or a step's budget pause) the
    // active node is the step the run is paused BEFORE — trace.pausedBefore, whose first path
    // segment addresses this level's node (deeper segments belong to a focused sub-workflow).
    // Without it the halo only ever appeared when scrubbing BACK through a finished run.
    const pausedBefore = trace.pausedBefore ? trace.pausedBefore.split('/')[0] : null;
    const active = k < this.length ? trace.steps[k].stepId : (pausedParent ?? pausedBefore);
    if (active === null && trace.outcome === 'completed') steps['#end'] = 'done-success';
    return { active, steps, edges };
  }

  /** @private */
  render() {
    const trace = this._trace;
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        :host { display: block; font-size: 12px; }
        .bar { display: flex; align-items: center; gap: 8px; padding: 8px 10px; border-bottom: 1px solid var(--_border); }
        .bar input[type="range"] { flex: 1; min-width: 60px; accent-color: var(--_accent); }
        .bar .chip { font-weight: 600; }
        .bar button { font-size: 12px; padding: 2px 8px; }
        .empty-note { padding: 10px; color: var(--_muted); }
        .body { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1.2fr); grid-template-rows: minmax(0, 1fr); gap: 0; max-height: var(--arazzo-tray-body-max, 34vh); overflow: hidden; }
        .steps { overflow-y: auto; overflow-x: hidden; border-right: 1px solid var(--_border); }
        .step { display: flex; gap: 6px; width: 100%; text-align: left; border: none; background: none; color: inherit;
                font: 12px ui-monospace, SFMono-Regular, Menlo, monospace; padding: 5px 8px; cursor: pointer; align-items: baseline; }
        .step:hover { background: var(--_surface); }
        .step.at { background: color-mix(in srgb, var(--_accent) 14%, transparent); }
        .step .n { color: var(--_muted); width: 2ch; text-align: right; flex-shrink: 0; }
        .step .nm { flex: 1 1 auto; min-width: 0; overflow-wrap: break-word; }
        .step .ok { color: var(--arazzo-status-completed, #2a8a4a); }
        .step .bad { color: var(--arazzo-status-faulted, #d4351c); }
        .step .act { color: var(--_muted); margin-left: auto; font-size: 11px; flex: 0 1 auto; min-width: 0;
                     overflow: hidden; text-overflow: ellipsis; white-space: nowrap; max-width: 45%; }
        .ctx { overflow-y: auto; overflow-x: hidden; padding: 8px 10px; display: grid; gap: 8px; align-content: start; }
        .ctx h4 { margin: 0; font-size: 10.5px; letter-spacing: 0.05em; text-transform: uppercase; color: var(--_muted); }
        .ctx pre { margin: 0; font: 11px ui-monospace, SFMono-Regular, Menlo, monospace; white-space: pre-wrap; overflow-wrap: anywhere; }
        table { border-collapse: collapse; width: 100%; }
        td { padding: 2px 6px; border-top: 1px solid var(--_border); font: 11px ui-monospace, SFMono-Regular, Menlo, monospace; vertical-align: top; overflow-wrap: anywhere; }
        td.v { width: 1%; white-space: nowrap; overflow-wrap: normal; }
        td.sent { color: var(--_muted); border-top: none; padding-top: 0; }
        .chip.warn { color: var(--arazzo-status-suspended, #b45309); }
        .crumb { display: flex; gap: 8px; align-items: center; padding: 5px 10px; border-bottom: 1px dashed var(--_border); font: 11px ui-monospace, SFMono-Regular, Menlo, monospace; }
        .crumb .up { font-size: 11px; padding: 1px 8px; }
        .step .into { font-size: 11px; padding: 0 5px; flex-shrink: 0; }
        .inject { display: grid; gap: 4px; border: 1px solid var(--_border); border-radius: 6px; padding: 6px; }
        .inject input, .inject textarea { font: 11px ui-monospace, SFMono-Regular, Menlo, monospace; padding: 3px 5px; border: 1px solid var(--_border); border-radius: 4px; background: var(--_bg); color: inherit; }
        .inject textarea { min-height: 44px; }
        .inject button { justify-self: start; font-size: 11px; }
        .inject .why { color: var(--_muted); font-size: 10.5px; }
        .override { font-size: 11px; justify-self: start; }
        .ovr-form { display: grid; gap: 6px; border: 1px solid var(--arazzo-status-suspended, #b45309); border-radius: 6px; padding: 8px; margin-top: 4px; }
        .ovr-form[hidden] { display: none; }
        .ovr-form .why { color: var(--_muted); font-size: 10.5px; }
        .ovr-actions { display: flex; gap: 6px; }
        .ovr-actions button { font-size: 11px; }
        .ctx-into { font-size: 11px; justify-self: start; }
        .inj-payload-slot { display: grid; gap: 3px; }
      </style>
      ${!trace ? '<div class="empty-note">No debug session — ▶ Run simulates the working copy against its scripted mocks.</div>' : `
      ${this._stack.length ? `<div class="crumb" part="crumb">
        <button class="up" type="button" title="Step out to the calling workflow">⬅ back</button>
        <span class="muted">${this._stack.map((f) => escapeHtml(f.label)).join(' ⤵ ')} ⤵</span>
      </div>` : ''}
      <div class="bar" part="controls">
        ${this.renderOutcomeChip(trace)}
        <button class="prev" type="button" title="Scrub back one step" ${this._cursor === 0 ? 'disabled' : ''}>⏮</button>
        <input class="scrub" type="range" min="0" max="${this.length}" step="1" value="${this._cursor}" title="Time-travel: the whole run is recorded — scrub freely">
        <button class="next" type="button" title="Step forward (client-side over the recorded trace)">⏭</button>
        <span class="muted">${this._cursor}/${this.length}</span>
        <button class="clear ghost" type="button" title="Clear the session and the canvas overlay">✕ Clear</button>
      </div>
      <div class="body">
        <div class="steps" part="steps">${trace.steps.map((s, i) => this.renderStepRow(s, i)).join('')}</div>
        <div class="ctx" part="context">${this.renderContext()}</div>
      </div>`}
    `;
    if (!trace) return;

    this.$('.scrub').addEventListener('input', (e) => { this.cursor = Number(e.target.value); });
    this.$('.prev').addEventListener('click', () => { this.cursor = this._cursor - 1; });
    this.$('.next').addEventListener('click', () => {
      if (this._cursor < this.length) this.cursor = this._cursor + 1;
      else this.emit('step-requested', {}); // at the end of a paused trace: the host replays one step further
    });
    this.$('.clear').addEventListener('click', () => this.emit('clear-requested', {}));
    this.$$('.step').forEach((row) => row.addEventListener('click', () => { this.cursor = Number(row.dataset.index) + 1; }));
    this.$$('[data-into]').forEach((b) => b.addEventListener('click', (e) => {
      e.stopPropagation(); // not a cursor move
      const record = this._trace.steps[Number(b.dataset.into)];
      this._stack.push({ trace: this._trace, cursor: this._cursor, label: record.stepId });
      this._trace = record.subTrace;
      this._cursor = this.length;
      this.render();
      this.emit('workflow-focus', { workflowId: record.subTrace.workflowId ?? null, path: this._stack.map((f) => f.label) });
      this.emit('cursor-changed', { index: this._cursor });
    }));
    this.$('.crumb .up')?.addEventListener('click', () => {
      const frame = this._stack.pop();
      this._trace = frame.trace;
      this._cursor = frame.cursor;
      this.render();
      this.emit('workflow-focus', { workflowId: this._trace.workflowId ?? null, path: this._stack.map((f) => f.label) });
      this.emit('cursor-changed', { index: this._cursor });
    });
    this.$('.override')?.addEventListener('click', () => {
      const form = this.$('.ovr-form');
      form.hidden = !form.hidden;
      if (!form.hidden && !form.dataset.built) {
        form.dataset.built = '1';
        const record = this._trace.steps[this._cursor - 1];
        const declared = this.stepSchemas?.[record.stepId]?.outputs;
        const editor = form.querySelector('.ovr-editor');
        editor.seed = record.outputs;
        editor.descriptor = declared && Object.keys(declared).length ? { type: 'object', properties: declared } : { type: 'object' };
      }
    });
    this.$('.ovr-apply')?.addEventListener('click', () => {
      const record = this._trace.steps[this._cursor - 1];
      try {
        this.emit('output-override', { stepId: record.stepId, outputs: this.$('.ovr-editor').value });
      } catch { /* unparseable JSON in the fallback editor: stay open */ }
    });
    this.$('.ovr-cancel')?.addEventListener('click', () => { this.$('.ovr-form').hidden = true; });

    // The inject payload edits TYPED when the awaited channel's message schema is known.
    const injSlot = this.$('.inj-payload-slot');
    if (injSlot) {
      const schema = this.channelSchemas?.[this._trace.wait?.channel];
      const editor = document.createElement('arazzo-value-editor');
      editor.className = 'inj-payload';
      editor.descriptor = schema && typeof schema === 'object' ? schema : { type: 'object' };
      injSlot.append(editor);
      if (!schema) injSlot.insertAdjacentHTML('beforeend', '<span class="why">no message schema declared for this channel — free-form payload</span>');
    }
    this.$('.inj-go')?.addEventListener('click', () => {
      const channel = this.$('.inj-channel').value.trim();
      if (!channel) return;
      const correlationId = this.$('.inj-corr').value.trim();
      let payload;
      try {
        payload = this.$('.inj-payload').value; // the typed editor parses/validates
      } catch {
        return; // unparseable free-form JSON: the editor shows the error, stay put
      }

      this.emit('trigger-injected', {
        channel,
        ...(payload !== undefined ? { payload } : {}),
        ...(correlationId ? { correlationId } : {}),
      });
    });
  }

  /** @private The engine outcome, tempered by judged health: completing while criteria failed is a warning. */
  renderOutcomeChip(trace) {
    const failed = (trace.steps ?? []).filter((s) => s.status === 'faulted' || (s.successCriteria ?? []).some((c) => !c.satisfied)).length;
    const base = `${escapeHtml(OUTCOME_LABEL[trace.outcome] ?? trace.outcome)}${trace.pausedBefore ? ` before ${escapeHtml(trace.pausedBefore)}` : ''}`;
    if (trace.outcome === 'completed' && failed > 0) {
      return `<span class="chip warn" title="The run reached an end, but ${failed} step${failed === 1 ? "'s" : 's’'} success criteria failed along the way">✓ completed — ${failed} failed criteri${failed === 1 ? 'on' : 'a'}</span>`;
    }

    return `<span class="chip">${base}</span>`;
  }

  /** @private */
  renderStepRow(record, index) {
    const failed = record.status === 'faulted' || (record.successCriteria ?? []).some((c) => !c.satisfied);
    const at = this._cursor === index + 1;
    const action = record.actionTaken ? record.actionTaken.type + (record.actionTaken.target ? `→${record.actionTaken.target}` : '') : '';
    // An in-progress sub-workflow parent shows its child's state (⏸ paused / ⏱ suspended), never a green ✓ (§3.5).
    const waiting = record.status === 'paused' || record.status === 'suspended';
    const glyph = record.status === 'paused' ? '⏸' : record.status === 'suspended' ? '⏱' : failed ? '✗' : '✓';
    return `<button class="step${at ? ' at' : ''}" type="button" data-index="${index}" title="Inspect after this step">
      <span class="n">${index + 1}</span>
      <span class="${waiting ? 'muted' : failed ? 'bad' : 'ok'}">${glyph}</span>
      <span class="nm">${escapeHtml(record.stepId)}${record.skipped ? ' <span class="muted" title="stepped over — outputs provided">⏭</span>' : ''}${record.attempt ? ` <span class="muted">↻${record.attempt}</span>` : ''}</span>
      ${record.subTrace ? `<span class="into ghost" data-into="${index}" title="Step into ${escapeHtml(record.subTrace.workflowId ?? 'the sub-workflow')} — its own trace, scrubbable">⤵</span>` : ''}
      <span class="act">${escapeHtml(action)}</span>
    </button>`;
  }

  /** @private — the paused-context explorer for the record BEFORE the cursor. */
  renderContext() {
    const trace = this._trace;
    if (this._cursor === 0) {
      return `<h4>before the first step</h4><pre class="muted">nothing has executed — inputs and mocks are staged</pre>`;
    }

    const record = trace.steps[this._cursor - 1];
    const parts = [];
    if (record.requests?.length) {
      parts.push('<h4>exchanges — as sent</h4><table>' + record.requests.map((x) =>
        `<tr><td>${escapeHtml(x.method.toUpperCase())} ${escapeHtml(x.path)}</td><td class="v">${x.status}</td>
         <td>${x.responseBody !== undefined ? escapeHtml(JSON.stringify(x.responseBody)) : '<span class="muted">—</span>'}</td></tr>`
        + (x.requestBody !== undefined ? `<tr><td colspan="3" class="sent">→ ${escapeHtml(JSON.stringify(x.requestBody))}</td></tr>` : '')).join('') + '</table>');
    }

    if (record.successCriteria?.length) {
      parts.push('<h4>success criteria (truth table)</h4><table>' + record.successCriteria.map((c) =>
        `<tr><td class="v ${c.satisfied ? 'ok' : 'bad'}">${c.satisfied ? '✓' : '✗'}</td><td>${escapeHtml(c.condition)}</td></tr>`).join('') + '</table>');
    }

    if (record.outputs !== undefined) {
      parts.push(`<h4>outputs — $steps.${escapeHtml(record.stepId)}.outputs</h4><pre>${escapeHtml(JSON.stringify(record.outputs, null, 2))}</pre>`);
    }

    if (this._cursor === this.length) {
      if (trace.outcome === 'completed' && trace.outputs !== undefined) {
        parts.push(`<h4>workflow outputs</h4><pre>${escapeHtml(JSON.stringify(trace.outputs, null, 2))}</pre>`);
      }

      if (trace.fault) {
        parts.push(`<h4>fault</h4><pre class="bad">${escapeHtml(trace.fault.stepId)} (attempt ${trace.fault.attempt}): ${escapeHtml(trace.fault.error)}</pre>`);
      }

      if (trace.wait) {
        parts.push(`<h4>waiting on</h4><pre>${escapeHtml(trace.wait.kind)}${trace.wait.dueAt ? ` due ${escapeHtml(trace.wait.dueAt)}` : ''}${trace.wait.channel ? ` channel ${escapeHtml(trace.wait.channel)}` : ''}</pre>`);
        if (trace.outcome === 'suspended' && trace.wait.kind === 'message') {
          // Inject the message the run is waiting for: the trigger joins the session scenario and
          // the host replays (stateless stepping, §8.2) — past the suspension this time.
          parts.push(`<div class="inject" part="inject">
            <h4>inject trigger</h4>
            <input class="inj-channel" type="text" placeholder="channel" value="${escapeHtml(trace.wait.channel ?? '')}">
            <input class="inj-corr" type="text" placeholder="correlationId (optional)" value="${escapeHtml(trace.wait.correlationId ?? '')}">
            <div class="inj-payload-slot"></div>
            <button class="inj-go" type="button" title="Add this message to the session scenario and replay past the suspension">⚡ Inject &amp; continue</button>
            <span class="why">joins the session's scenario; the replay delivers it at this wait</span>
          </div>`);
        }
      }

      if (trace.clockAdvances?.length) {
        parts.push(`<h4>virtual clock</h4><pre>${trace.clockAdvances.map((a) => `→ ${escapeHtml(a.to)} (${escapeHtml(a.reason)})`).join('\n')}</pre>`);
      }
    }

    if (record.subTrace) {
      parts.push(`<button class="ghost ctx-into" data-into="${this._cursor - 1}"
        title="Open ${escapeHtml(record.subTrace.workflowId ?? 'the sub-workflow')}'s own trace — scrubbable, with ⬅ back in the breadcrumb">⤵ step into ${escapeHtml(record.subTrace.workflowId ?? 'subworkflow')}</button>`);
    }

    const declared = this.stepSchemas?.[record.stepId]?.outputs;
    const typed = declared && Object.keys(declared).length;
    const overrideBlock = `<button class="override ghost" type="button" data-step="${escapeHtml(record.stepId)}"
      title="Provide this step's outputs yourself and replay — the step will NOT execute (the runs-view Skip, here). Rewind first to try a what-if from any point.">⏭ step over — provide outputs & replay…</button>
      <div class="ovr-form" hidden>
        <span class="why">${typed
          ? `typed by ${escapeHtml(record.stepId)}'s declared outputs`
          : 'this step declares no outputs — provide an object to stand in as its outputs'}</span>
        <arazzo-value-editor class="ovr-editor"></arazzo-value-editor>
        <span class="ovr-actions"><button class="ovr-apply" type="button">⏭ Apply &amp; replay</button><button class="ovr-cancel ghost" type="button">Cancel</button></span>
      </div>`;
    return `<h4>after step ${this._cursor}: ${escapeHtml(record.stepId)}${record.skipped ? ' <span class="muted">(stepped over — outputs provided)</span>' : ''}</h4>`
      + (parts.join('') || '<pre class="muted">no captured context</pre>') + overrideBlock;
  }
}

define('arazzo-debug-tray', ArazzoDebugTray);
export { ArazzoDebugTray };