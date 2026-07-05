// <arazzo-step-inspector> — the full editor for one selected step (design §5.3): the binding
// (operationId / operationPath / sub-workflow / AsyncAPI channel, switching prunes the others),
// parameters, requestBody, successCriteria, local onSuccess/onFailure actions, outputs, and the
// "localize workflow defaults" affordance for steps riding the inherited-defaults layer.
//
//   const insp = document.createElement('arazzo-step-inspector');
//   insp.stepIds = wf.steps.map((s) => s.stepId);      // goto targets
//   insp.workflowIds = ['place-order', …];             // sub-workflow targets
//   insp.workflowDefaults = { failureActions: wf.failureActions, successActions: wf.successActions };
//   insp.completionContext = { … };
//   insp.value = step;                                  // the step object (cloned in)
//   insp.addEventListener('step-changed', (e) => { wf.steps[i] = e.detail.step; });
//
// External `.value` sets rebuild the form; internal edits mutate and emit (focus preserved).
// stepId is displayed but not edited here: a rename rewrites goto targets and $steps expressions
// document-wide — that is the document model's job (§5.2), not a field-level edit.

import { ArazzoElement, SHARED_CSS, escapeHtml, define } from './base.js';
import { buildActionList, ACTION_LIST_CSS } from './action-list.js';
import { templatesFromResponses, payloadSkeletonFromSchema } from '../operation-templates.js';
import './expression-input.js';
import './criteria-editor.js';
import './outputs-editor.js';
import './payload-editor.js';

const KINDS = [
  ['operationId', 'operation (by id)'],
  ['operationPath', 'operation (by path)'],
  ['workflowId', 'sub-workflow'],
  ['channelPath', 'channel (AsyncAPI)'],
];
const PARAM_IN = ['path', 'query', 'querystring', 'header', 'cookie'];

class ArazzoStepInspector extends ArazzoElement {
  constructor() {
    super();
    /** @private */ this._step = { stepId: '' };
    /** @private */ this._stepIds = [];
    /** @private */ this._workflowIds = [];
    /** @private */ this._components = {};
    /** @private */ this._defaults = { successActions: [], failureActions: [] };
    /** @private */ this._completionContext = {};
  }

  connectedCallback() {
    if (!this._built) this.renderShell();
    this.renderForm();
  }

  /** The step object. Setting rebuilds the form. */
  get value() { return structuredClone(this._step); }
  set value(step) {
    this._step = structuredClone(step || { stepId: '' });
    if (this.isConnected) { if (!this._built) this.renderShell(); this.renderForm(); }
  }

  set stepIds(ids) { this._stepIds = [...(ids || [])]; }
  set workflowIds(ids) { this._workflowIds = [...(ids || [])]; }

  /** The document's components object — enables $components references and localize. */
  set components(value) {
    this._components = value || {};
    if (this.isConnected && this._built) this.renderForm();
  }
  /** The bound operation's documented response codes (from the operation surface); enables the
   *  "template criteria from responses" affordance. */
  set operationResponses(responses) {
    this._operationResponses = responses;
    if (this.isConnected && this._built) this._renderTemplateButton();
  }

  /** The binding's request surface — `{contentType?, schema}` for an OpenAPI request body, or the
   *  message-payload schema for an AsyncAPI send; enables the body-skeleton affordance. */
  set operationRequest(request) {
    this._operationRequest = request;
    if (this.isConnected && this._built) this._renderBodyTemplateButton();
  }
  /** The workflow-level actions (for the localize-defaults affordance). */
  set workflowDefaults(d) { this._defaults = { successActions: [], failureActions: [], ...(d || {}) }; }
  set completionContext(ctx) { this._completionContext = ctx || {}; }
  get completionContext() { return this._completionContext; }

  /** @private */
  renderShell() {
    this._built = true;
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        ${ACTION_LIST_CSS}
        :host { display: block; }
        .form { display: grid; gap: 12px; }
        .form > *, .pair > *, .prow > *, .binding { min-width: 0; }
        label { font-size: 11px; color: var(--_muted); display: block; margin-bottom: 2px; }
        h3 { font-size: 11px; letter-spacing: 0.05em; text-transform: uppercase; color: var(--_muted); margin: 6px 0 0; border-top: 1px solid var(--_border); padding-top: 10px; }
        input[type="text"], input[type="number"], textarea {
          width: 100%; box-sizing: border-box; font: inherit; padding: 6px 9px;
          border: 1px solid var(--_border); border-radius: var(--_radius);
          background-color: var(--_bg); color: var(--_text);
        }
        textarea { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 12px; resize: vertical; }
        textarea.invalid { border-color: var(--_danger); }
        .pair { display: grid; grid-template-columns: 1fr 1fr; gap: 8px; }
        /* A parameter is a small card: name + in + remove on the head line, the value expression
           full-width beneath — a narrow inspector rail never squeezes the editor. */
        .prow { border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_surface); padding: 8px; display: grid; gap: 6px; }
        .prow .phead { display: grid; grid-template-columns: minmax(80px, 1fr) auto auto; gap: 6px; align-items: center; }
        .prow .phead > * { min-width: 0; }
        .prow input.pname { font: 12.5px ui-monospace, SFMono-Regular, Menlo, monospace; }
        .params { display: grid; gap: 8px; }
        .addp { font-size: 12px; justify-self: start; }
        .localize { font-size: 12px; }
        .hint { font-size: 11px; color: var(--_muted); }
        .chips { display: flex; flex-wrap: wrap; gap: 6px; }
        .repls { min-width: 0; }
        .rrow > * { min-width: 0; }
        .rrow arazzo-expression-input { display: block; min-width: 0; }
        .addpref { max-width: 100%; min-width: 0; }
        .chip { font-size: 12px; padding: 3px 10px; border-radius: 999px; }
        .chip.on { border-color: var(--_accent); color: var(--_accent); font-weight: 600; }
      </style>
      <div class="form" part="form"></div>
    `;
  }

  /** @private */
  renderForm() {
    const s = this._step;
    const kind = KINDS.find(([k]) => s[k] != null)?.[0] ?? 'operationId';
    const form = this.$('.form');
    form.innerHTML = `
      <div>
        <label>description</label>
        <input class="desc" type="text" value="${escapeHtml(s.description || '')}">
      </div>

      <div>
        <label>binding</label>
        <select class="kind">
          ${KINDS.map(([k, label]) => `<option value="${k}" ${k === kind ? 'selected' : ''}>${label}</option>`).join('')}
        </select>
      </div>
      <div class="binding"></div>

      <h3>depends on</h3>
      <div class="hint">steps that must complete before this one runs (Arazzo 1.1 async ordering)</div>
      <div class="dependson chips"></div>

      <h3>parameters</h3>
      <div class="params"></div>
      <div style="display:flex; gap:8px; align-items:center;">
        <button class="addp ghost" type="button">+ Add parameter</button>
        <select class="addpref" hidden style="font-size:12px;"></select>
      </div>

      <h3>request body</h3>
      <div class="body-template-slot"></div>
      <div class="pair">
        <div>
          <label>contentType</label>
          <input class="ctype" type="text" placeholder="application/json" value="${escapeHtml(s.requestBody?.contentType || '')}">
        </div>
      </div>
      <div>
        <label>payload</label>
        <div class="payload-slot"></div>
      </div>
      <div>
        <label>replacements (JSON Pointer → value)</label>
        <div class="repls"></div>
        <button class="addr ghost" type="button">+ Add replacement</button>
      </div>

      <h3>success criteria</h3>
      <div class="hint">the verdict: ALL must match for the step to succeed — failing them is what makes it fail</div>
      <div class="template-slot"></div>
      <div class="crit"></div>

      <h3>on success</h3>
      <div class="hint">routing when it succeeded — first match wins; nothing matching falls through to the next step</div>
      <div class="onsuccess"></div>
      <h3>on failure</h3>
      <div class="hint">routing when it failed — first match wins; nothing matching falls back to the workflow defaults, then faults</div>
      <div class="onfailure"></div>
      <div class="localize-slot"></div>

      <h3>outputs</h3>
      <div class="outs"></div>
    `;

    this._renderBinding(kind);
    this._renderDependsOn();
    this._renderParams();
    this._renderReplacements();

    // Success criteria (shared editor).
    const criteria = document.createElement('arazzo-criteria-editor');
    criteria.completionContext = this._completionContext;
    criteria.value = s.successCriteria || [];
    form.querySelector('.crit').append(criteria);
    criteria.addEventListener('criteria-changed', (e) => {
      e.stopPropagation();
      if (e.detail.criteria.length) this._step.successCriteria = e.detail.criteria;
      else delete this._step.successCriteria;
      this._emit();
    });

    // Local action lists.
    this._mountActionList('onsuccess', 'onSuccess', 'success');
    this._mountActionList('onfailure', 'onFailure', 'failure');
    this._renderLocalize();
    this._renderTemplateButton();
    this._renderBodyTemplateButton();

    // Outputs.
    const outs = document.createElement('arazzo-outputs-editor');
    outs.completionContext = this._completionContext;
    outs.value = s.outputs || {};
    form.querySelector('.outs').append(outs);
    outs.addEventListener('outputs-changed', (e) => {
      e.stopPropagation();
      if (Object.keys(e.detail.outputs).length) this._step.outputs = e.detail.outputs;
      else delete this._step.outputs;
      this._emit();
    });

    // Scalar fields.
    form.querySelector('.desc').addEventListener('input', (e) => {
      if (e.target.value) this._step.description = e.target.value;
      else delete this._step.description;
      this._emit();
    });
    form.querySelector('.kind').addEventListener('change', (e) => {
      this._setKind(e.target.value);
    });
    form.querySelector('.ctype').addEventListener('input', (e) => {
      this._ensureRequestBody();
      if (e.target.value) this._step.requestBody.contentType = e.target.value;
      else delete this._step.requestBody.contentType;
      this._pruneRequestBody();
      this._emit();
    });
    // The strongly-typed payload editor: structure from the binding's schema, expression-capable
    // leaves, JSON view for full fidelity (schema-less bindings get JSON only).
    const payload = document.createElement('arazzo-payload-editor');
    payload.schema = this._operationRequest?.schema || null;
    payload.completionContext = this._completionContext;
    payload.value = s.requestBody?.payload;
    form.querySelector('.payload-slot').append(payload);
    payload.addEventListener('payload-changed', (e) => {
      e.stopPropagation();
      if (e.detail.payload === undefined) {
        if (this._step.requestBody) { delete this._step.requestBody.payload; this._pruneRequestBody(); }
      } else {
        this._ensureRequestBody();
        this._step.requestBody.payload = e.detail.payload;
      }
      this._renderBodyTemplateButton(); // the skeleton affordance tracks payload presence
      this._emit();
    });
  }

  /** @private — binding fields per kind; switching prunes the other binding's fields. */
  _renderBinding(kind) {
    const s = this._step;
    const box = this.$('.binding');
    if (kind === 'operationId' || kind === 'operationPath') {
      box.innerHTML = `
        <input class="bval" type="text" placeholder="${kind === 'operationId' ? 'e.g. authorizePayment' : '{$sourceDescriptions.payments.url}#/paths/…'}"
               value="${escapeHtml(s[kind] || '')}">
        <div class="hint">the operation browser will fill this in the full designer</div>
      `;
      box.querySelector('.bval').addEventListener('input', (e) => {
        s[kind] = e.target.value;
        this._emit();
      });
    } else if (kind === 'workflowId') {
      box.innerHTML = `
        <select class="bval">
          ${this._workflowIds.map((id) => `<option ${id === s.workflowId ? 'selected' : ''}>${escapeHtml(id)}</option>`).join('')}
        </select>
      `;
      box.querySelector('.bval').addEventListener('change', (e) => {
        s.workflowId = e.target.value;
        this._emit();
      });
    } else {
      box.innerHTML = `
        <div class="pair">
          <div>
            <label>channelPath</label>
            <input class="bval" type="text" placeholder="/channels/orderConfirmations" value="${escapeHtml(s.channelPath || '')}">
          </div>
          <div>
            <label>action</label>
            <select class="chaction">
              <option ${s.action !== 'receive' ? 'selected' : ''}>send</option>
              <option ${s.action === 'receive' ? 'selected' : ''}>receive</option>
            </select>
          </div>
        </div>
        <div class="pair" style="margin-top:6px;">
          <div>
            <label>correlationId (runtime expression)</label>
            <span class="corr-slot"></span>
          </div>
          <div>
            <label>timeout (ms)</label>
            <input class="timeout" type="number" min="0" step="1" value="${s.timeout ?? ''}">
          </div>
        </div>
      `;
      const corr = document.createElement('arazzo-expression-input');
      corr.setAttribute('placeholder', '$inputs.orderId');
      corr.value = s.correlationId || '';
      corr.completionContext = this._completionContext;
      box.querySelector('.corr-slot').replaceWith(corr);
      box.querySelector('.bval').addEventListener('input', (e) => { s.channelPath = e.target.value; this._emit(); });
      box.querySelector('.chaction').addEventListener('change', (e) => { s.action = e.target.value; this._emit(); });
      corr.addEventListener('value-changed', (e) => {
        e.stopPropagation();
        if (e.detail.value) s.correlationId = e.detail.value;
        else delete s.correlationId;
        this._emit();
      });
      box.querySelector('.timeout').addEventListener('input', (e) => {
        if (e.target.value === '') delete s.timeout;
        else s.timeout = Number(e.target.value);
        this._emit();
      });
    }
  }

  /** @private — one binding at a time (the schema's oneOf): switching kinds prunes the rest. */
  _setKind(kind) {
    const s = this._step;
    for (const [k] of KINDS) delete s[k];
    delete s.action; delete s.correlationId; delete s.timeout;
    if (kind === 'workflowId') s.workflowId = this._workflowIds[0] || '';
    else if (kind === 'channelPath') { s.channelPath = ''; s.action = 'receive'; }
    else s[kind] = '';
    this._renderBinding(kind);
    this._emit();
  }

  /** @private */
  _renderParams() {
    const s = this._step;
    const box = this.$('.params');
    box.replaceChildren();
    (s.parameters || []).forEach((p, i) => {
      if (p && typeof p.reference === 'string') {
        const row = document.createElement('div');
        row.className = 'hint refrow';
        row.style.cssText = 'display:flex; align-items:center; gap:6px;';
        const label = document.createElement('span');
        label.style.cssText = 'flex:1; min-width:0;';
        label.textContent = `↺ ${p.reference} (reusable — edit in components)`;
        row.append(label);
        const key = p.reference.split('.').pop();
        const resolved = this._components?.parameters?.[key];
        if (resolved) {
          const localize = document.createElement('button');
          localize.type = 'button';
          localize.className = 'ghost';
          localize.style.fontSize = '11px';
          localize.textContent = 'localize';
          localize.title = 'Copy the reusable parameter inline so this step can edit it independently';
          localize.addEventListener('click', () => {
            s.parameters[i] = structuredClone(resolved);
            this._renderParams();
            this._emit();
          });
          row.append(localize);
        }

        const drop = document.createElement('button');
        drop.type = 'button';
        drop.className = 'ghost';
        drop.style.fontSize = '11px';
        drop.textContent = '✕';
        drop.title = 'Remove the reference';
        drop.addEventListener('click', () => {
          s.parameters.splice(i, 1);
          if (!s.parameters.length) delete s.parameters;
          this._renderParams();
          this._emit();
        });
        row.append(drop);
        box.append(row);
        return;
      }
      const row = document.createElement('div');
      row.className = 'prow';
      row.innerHTML = `
        <div class="phead">
          <input class="pname" type="text" placeholder="name" value="${escapeHtml(p.name || '')}">
          <select class="pin">${PARAM_IN.map((v) => `<option ${v === p.in ? 'selected' : ''}>${v}</option>`).join('')}</select>
          <button class="close" type="button" title="Remove parameter">✕</button>
        </div>
        <span class="pval-slot"></span>
      `;
      const val = document.createElement('arazzo-expression-input');
      val.setAttribute('placeholder', '$inputs.orderId');
      val.value = typeof p.value === 'string' ? p.value : JSON.stringify(p.value ?? '');
      val.completionContext = this._completionContext;
      row.querySelector('.pval-slot').replaceWith(val);

      row.querySelector('.pname').addEventListener('input', (e) => { p.name = e.target.value; this._emit(); });
      row.querySelector('.pin').addEventListener('change', (e) => { p.in = e.target.value; this._emit(); });
      val.addEventListener('value-changed', (e) => { e.stopPropagation(); p.value = e.detail.value; this._emit(); });
      row.querySelector('.close').addEventListener('click', () => {
        s.parameters.splice(i, 1);
        if (!s.parameters.length) delete s.parameters;
        this._renderParams();
        this._emit();
      });
      box.append(row);
    });
    this.$('.addp').onclick = () => {
      (this._step.parameters ??= []).push({ name: '', in: 'query', value: '' });
      this._renderParams();
      this.$$('.prow .pname').at(-1)?.focus();
      this._emit();
    };

    const refSelect = this.$('.addpref');
    const keys = Object.keys(this._components?.parameters || {});
    refSelect.hidden = !keys.length;
    if (keys.length) {
      refSelect.innerHTML = `<option value="">+ Add reference…</option>`
        + keys.map((k) => `<option value="${escapeHtml(k)}">$components.parameters.${escapeHtml(k)}</option>`).join('');
      refSelect.onchange = () => {
        if (!refSelect.value) return;
        (s.parameters ??= []).push({ reference: `$components.parameters.${refSelect.value}` });
        this._renderParams();
        this._emit();
      };
    }
  }

  /** @private — toggle chips over the workflow's other steps; empty prunes the property. */
  _renderDependsOn() {
    const box = this.$('.dependson');
    const others = this._stepIds.filter((id) => id !== this._step.stepId);
    if (!others.length) {
      box.innerHTML = '<span class="hint">no other steps yet</span>';
      return;
    }

    const current = new Set(this._step.dependsOn || []);
    box.innerHTML = others.map((id) => `
      <button type="button" class="chip ghost${current.has(id) ? ' on' : ''}" data-id="${escapeHtml(id)}"
        aria-pressed="${current.has(id)}">${escapeHtml(id)}</button>`).join('');
    box.querySelectorAll('.chip').forEach((chip) => chip.addEventListener('click', () => {
      const set = new Set(this._step.dependsOn || []);
      if (set.has(chip.dataset.id)) set.delete(chip.dataset.id);
      else set.add(chip.dataset.id);

      // Preserve step order (dependencies read best in execution order).
      const ordered = this._stepIds.filter((id) => set.has(id));
      if (ordered.length) this._step.dependsOn = ordered;
      else delete this._step.dependsOn;
      this._renderDependsOn();
      this._emit();
    }));
  }

  /** @private — the requestBody.replacements list: target (JSON Pointer) → expression-capable value. */
  _renderReplacements() {
    const box = this.$('.repls');
    const replacements = this._step.requestBody?.replacements || [];
    box.innerHTML = '';
    replacements.forEach((r, i) => {
      const row = document.createElement('div');
      row.className = 'rrow';
      row.style.cssText = 'display:grid; grid-template-columns: 1fr 1fr auto; gap:6px; align-items:center; margin-bottom:6px;';
      row.innerHTML = `
        <input class="rtarget" type="text" placeholder="/card/number" value="${escapeHtml(r.target ?? '')}">
        <div class="rvalue-slot"></div>
        <button class="rdel ghost" type="button" title="Remove">✕</button>`;
      const value = document.createElement('arazzo-expression-input');
      value.completionContext = this._completionContext;
      value.value = typeof r.value === 'string' ? r.value : JSON.stringify(r.value ?? '');
      row.querySelector('.rvalue-slot').append(value);
      row.querySelector('.rtarget').addEventListener('input', (e) => {
        r.target = e.target.value;
        this._emit();
      });
      value.addEventListener('commit', (e) => {
        e.stopPropagation();
        r.value = e.detail.value;
        this._emit();
      });
      row.querySelector('.rdel').addEventListener('click', () => {
        replacements.splice(i, 1);
        if (!replacements.length) { delete this._step.requestBody.replacements; this._pruneRequestBody(); }
        this._renderReplacements();
        this._emit();
      });
      box.append(row);
    });
    this.$('.addr').onclick = () => {
      this._ensureRequestBody();
      (this._step.requestBody.replacements ??= []).push({ target: '', value: '' });
      this._renderReplacements();
      box.querySelector('.rrow:last-child .rtarget')?.focus();
      this._emit();
    };
  }

  /** @private */
  _mountActionList(slotClass, listName, kind) {
    const actions = (this._step[listName] ??= []);
    const el = buildActionList({
      actions,
      kind,
      stepIds: this._stepIds.filter((id) => id !== this._step.stepId),
      workflowIds: this._workflowIds,
      components: this._components?.[kind === 'success' ? 'successActions' : 'failureActions'],
      completionContext: this._completionContext,
      onChange: () => {
        if (!actions.length) delete this._step[listName];
        else this._step[listName] = actions;
        this._renderLocalize();
        this._emit();
      },
    });
    this.$(`.${slotClass}`).replaceChildren(el);
    if (!actions.length) delete this._step[listName]; // ??= above must not leave an empty list behind
  }

  /** @private — offer response-derived templates: success criteria from the documented success
   *  codes, one failure action per documented error (plus the catch-all — a documented `default`,
   *  or an explicit unexpected-failure fallback when the operation documents none). Success
   *  criteria fill only when empty; failure actions append without duplicating names. */
  _renderTemplateButton() {
    const slot = this.$('.template-slot');
    if (!slot) return;
    slot.replaceChildren();
    const templates = templatesFromResponses(this._operationResponses);
    if (!templates) return;
    const btn = document.createElement('button');
    btn.type = 'button';
    btn.className = 'ghost template';
    btn.style.fontSize = '12px';
    btn.textContent = '⚡ Template from the operation’s responses';
    btn.title = 'Derives success criteria and per-response failure actions from the documented responses.';
    btn.addEventListener('click', () => {
      if (!this._step.successCriteria?.length && templates.successCriteria.length) {
        this._step.successCriteria = templates.successCriteria;
      }
      // First-match-wins: criteria'd templates insert ABOVE any existing catch-all (else they
      // could never fire), and the template's own fallback is skipped when a catch-all exists.
      const list = [...(this._step.onFailure || [])];
      const names = new Set(list.map((a) => a?.name));
      const isCatchAll = (a) => a && typeof a.reference !== 'string' && !(a.criteria?.length);
      const hasCatchAll = list.some(isCatchAll);
      const fresh = templates.failureActions.filter((a) =>
        !names.has(a.name) && (a.criteria?.length || !hasCatchAll));
      if (fresh.length) {
        const pin = list.findIndex(isCatchAll);
        const at = pin < 0 ? list.length : pin;
        list.splice(at, 0, ...fresh.filter((a) => a.criteria?.length));
        list.push(...fresh.filter((a) => !(a.criteria?.length)));
        this._step.onFailure = list;
      }
      this.renderForm();
      this._emit();
    });
    slot.append(btn);
  }

  /** @private — offer a request-body skeleton derived from the binding's schema: structure typed
   *  for you, values (usually runtime expressions) yours to fill. Only fills an empty payload. */
  _renderBodyTemplateButton() {
    const slot = this.$('.body-template-slot');
    if (!slot) return;
    slot.replaceChildren();
    const request = this._operationRequest;
    const skeleton = payloadSkeletonFromSchema(request?.schema);
    if (skeleton === undefined || this._step.requestBody?.payload !== undefined) return;
    const btn = document.createElement('button');
    btn.type = 'button';
    btn.className = 'ghost body-template';
    btn.style.fontSize = '12px';
    btn.textContent = '⚡ Build body from the operation’s schema';
    btn.title = 'Fills the payload with the schema’s structure; replace the stub values with runtime expressions.';
    btn.addEventListener('click', () => {
      this._ensureRequestBody();
      this._step.requestBody.payload = skeleton;
      if (!this._step.requestBody.contentType && request.contentType) {
        this._step.requestBody.contentType = request.contentType;
      }
      this.renderForm();
      this._emit();
    });
    slot.append(btn);
  }

  /** @private — the §3.2 affordance: copy the inherited workflow defaults into local actions. */
  _renderLocalize() {
    const slot = this.$('.localize-slot');
    slot.replaceChildren();
    const inheritsFailure = !this._step.onFailure?.length && this._defaults.failureActions.length;
    const inheritsSuccess = !this._step.onSuccess?.length && this._defaults.successActions.length;
    if (!inheritsFailure && !inheritsSuccess) return;
    const btn = document.createElement('button');
    btn.type = 'button';
    btn.className = 'localize ghost';
    btn.textContent = '⌁ Localize workflow defaults onto this step';
    btn.title = 'Copies the inherited workflow-level actions here for local editing.';
    btn.addEventListener('click', () => {
      if (inheritsSuccess) this._step.onSuccess = structuredClone(this._defaults.successActions);
      if (inheritsFailure) this._step.onFailure = structuredClone(this._defaults.failureActions);
      this.renderForm();
      this._emit();
    });
    slot.append(btn);
  }

  /** @private */
  _ensureRequestBody() { this._step.requestBody ??= {}; }

  /** @private */
  _pruneRequestBody() {
    if (this._step.requestBody && !Object.keys(this._step.requestBody).length) {
      delete this._step.requestBody;
    }
  }

  /** @private */
  _emit() {
    this.emit('step-changed', { step: this.value });
  }
}

define('arazzo-step-inspector', ArazzoStepInspector);
export { ArazzoStepInspector };
