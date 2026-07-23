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
  /** The bound operation's documented response codes (from the operation surface); enables the */
  set operationResponses(responses) {
    this._operationResponses = responses;
  }

  /** The binding's request surface — `{contentType?, schema}` for an OpenAPI request body, or the
   *  message-payload schema for an AsyncAPI send; enables the body-skeleton affordance. */
  /** The operation's declared parameters ({name, in, schema}[]) — object-schema'd ones get the
   *  structured editor; scalars coerce by their declared type. */
  set operationParameters(parameters) {
    this._operationParameters = parameters;
    if (this.isConnected && this._built) this._renderParams();
  }

  set operationRequest(request) {
    this._operationRequest = request;
    // A full re-render: the request presence decides whether the whole request body section shows, and
    // its schema drives the replacement targets — so this is not just a replacements refresh.
    if (this.isConnected && this._built) this.renderForm();
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
        .prow .pfixed { font: 12px ui-monospace, SFMono-Regular, Menlo, monospace; overflow-wrap: anywhere; }
        .prow .phead:has(.pfixed) { grid-template-columns: minmax(0, 1fr) auto; }
        .pmissing { display: flex; flex-wrap: wrap; gap: 4px; }
        .pmissing .ghost { font-size: 11px; }
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
        /* The add action sits on its OWN line below the pills/empty-state text — inline it read as a
           run-on sentence and misaligned the control (backlog #854). */
        .add-dep { font-size: 12px; max-width: max-content; min-width: 0; flex-basis: 100%; margin-top: 4px; }
        .chip.on { border-color: var(--_accent); color: var(--_accent); font-weight: 600; }
      </style>
      <div class="form" part="form"></div>
    `;
  }

  /** @private */
  renderForm() {
    const s = this._step;
    const kind = KINDS.find(([k]) => s[k] != null)?.[0] ?? 'operationId';
    // The request body section appears only when the operation actually takes a body (its resolved
    // request carries a schema) or the step already carries one; a GET-style operation with no request
    // body must not prompt for one.
    const showRequestBody = !!this._operationRequest?.schema
      || !!(s.requestBody && Object.keys(s.requestBody).length);
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
        <button class="addp ghost" type="button">+ Custom parameter (undeclared)</button>
        <select class="addpref" hidden style="font-size:12px;"></select>
      </div>

      ${showRequestBody ? `
      <h3>request body</h3>
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
      </div>` : ''}

      <h3>success criteria</h3>
      <div class="hint">the verdict: ALL must match for the step to succeed — failing them is what makes it fail</div>
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
    if (showRequestBody) this._renderReplacements();

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
    // The request body controls exist only when the section is shown (an operation that takes a body).
    if (showRequestBody) {
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
        this._emit();
      });
    }
  }

  /** @private — binding fields per kind; switching prunes the other binding's fields. */
  _renderBinding(kind) {
    const s = this._step;
    const box = this.$('.binding');
    if (kind === 'operationId' || kind === 'operationPath') {
      box.innerHTML = `
        <input class="bval" type="text" placeholder="${kind === 'operationId' ? 'e.g. authorizePayment' : '{$sourceDescriptions.payments.url}#/paths/…'}"
               value="${escapeHtml(s[kind] || '')}">
        <div class="hint">drop an operation from the Sources rail onto the step, or type the id</div>
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
      // A DECLARED parameter's name and location come from the source binding — fixed, not typed
      // in. Only undeclared (custom) entries get editable name/in fields.
      const declaredHead = (this._operationParameters ?? []).find((d) => d.name === p.name && (!d.in || d.in === p.in))
        ?? (this._operationParameters ?? []).find((d) => d.name === p.name);
      if (declaredHead) {
        const headType = Array.isArray(declaredHead.schema?.type) ? declaredHead.schema.type[0] : declaredHead.schema?.type;
        row.innerHTML = `
          <div class="phead">
            <span class="pfixed">${escapeHtml(p.name)} <span class="muted">${escapeHtml(p.in ?? declaredHead.in ?? 'query')}${headType ? ` · ${escapeHtml(headType)}` : ''}${declaredHead.required ? ' · required' : ''}</span></span>
            <button class="close" type="button" title="${declaredHead.required ? 'Remove (the operation REQUIRES this parameter — validation will flag it)' : 'Remove parameter'}">✕</button>
          </div>
          <span class="pval-slot"></span>
        `;
      } else {
        row.innerHTML = `
          <div class="phead">
            <input class="pname" type="text" placeholder="name" value="${escapeHtml(p.name || '')}">
            <select class="pin">${PARAM_IN.map((v) => `<option ${v === p.in ? 'selected' : ''}>${v}</option>`).join('')}</select>
            <button class="close" type="button" title="Remove parameter">✕</button>
          </div>
          <span class="pval-slot"></span>
        `;
      }
      // The declared schema (from the operation surface) picks the editor: object schemas — and
      // object values, declared or not — get the STRUCTURED payload editor (expression-capable
      // leaves); scalars stay a single expression input, literals coercing to the declared type.
      const declared = (this._operationParameters ?? []).find((d) => d.name === p.name && (!d.in || d.in === p.in))
        ?? (this._operationParameters ?? []).find((d) => d.name === p.name);
      const declaredType = Array.isArray(declared?.schema?.type) ? declared.schema.type[0] : declared?.schema?.type;
      const isComplex = declaredType === 'object' || (p.value !== null && typeof p.value === 'object');
      let val;
      if (isComplex) {
        val = document.createElement('arazzo-payload-editor');
        val.schema = declaredType === 'object' ? declared.schema : null;
        val.completionContext = this._completionContext;
        val.value = (p.value !== null && typeof p.value === 'object') ? p.value : undefined;
        val.addEventListener('payload-changed', (e) => {
          e.stopPropagation();
          if (e.detail.payload === undefined) delete p.value; else p.value = e.detail.payload;
          this._emit();
        });
      } else {
        val = document.createElement('arazzo-expression-input');
        val.setAttribute('placeholder', declaredType && declaredType !== 'string' ? `${declaredType} or $inputs.…` : '$inputs.orderId');
        val.value = typeof p.value === 'string' ? p.value : p.value === undefined ? '' : JSON.stringify(p.value);
        val.completionContext = this._completionContext;
        val.addEventListener('value-changed', (e) => {
          e.stopPropagation();
          const text = e.detail.value;
          if (text === '') { p.value = ''; }
          else if (text.startsWith('$') || text.includes('{$') || !declaredType) { p.value = text; }
          else if (declaredType === 'number' || declaredType === 'integer') { const n = Number(text); p.value = Number.isFinite(n) ? n : text; }
          else if (declaredType === 'boolean') { p.value = text === 'true' ? true : text === 'false' ? false : text; }
          else { p.value = text; }
          this._emit();
        });
      }

      row.querySelector('.pval-slot').replaceWith(val);

      row.querySelector('.pname')?.addEventListener('input', (e) => { p.name = e.target.value; this._emit(); });
      row.querySelector('.pin')?.addEventListener('change', (e) => { p.in = e.target.value; this._emit(); });
      row.querySelector('.close').addEventListener('click', () => {
        s.parameters.splice(i, 1);
        if (!s.parameters.length) delete s.parameters;
        this._renderParams();
        this._emit();
      });
      box.append(row);
    });
    // Declared parameters the step does not carry yet offer themselves as one-click adds.
    const present = new Set((s.parameters || []).map((p) => p?.name).filter(Boolean));
    const missing = (this._operationParameters ?? []).filter((d) => d.name && !present.has(d.name));
    if (missing.length) {
      const offer = document.createElement('div');
      offer.className = 'pmissing';
      for (const d of missing) {
        const add = document.createElement('button');
        add.type = 'button';
        add.className = 'ghost';
        add.textContent = `+ ${d.name} (${d.in ?? 'query'})`;
        add.title = d.description ?? `Add the declared '${d.name}' parameter`;
        add.addEventListener('click', () => {
          (this._step.parameters ??= []).push({ name: d.name, in: d.in ?? 'query', value: '' });
          this._renderParams();
          this._emit();
        });
        offer.append(add);
      }

      box.append(offer);
    }

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

  /** @private — actual dependencies render as removable pills; new ones add through an explicit
   *  select. Offering every step as a toggle chip read as "this step depends on all of these". */
  _renderDependsOn() {
    const box = this.$('.dependson');
    const others = this._stepIds.filter((id) => id !== this._step.stepId);
    const current = this._step.dependsOn || [];
    box.innerHTML = '';

    if (!current.length) {
      const none = document.createElement('span');
      none.className = 'hint';
      none.textContent = 'none — this step waits only for its position in the flow';
      box.append(none);
    }

    for (const id of current) {
      const pill = document.createElement('button');
      pill.type = 'button';
      pill.className = 'chip on';
      pill.title = 'Remove this dependency';
      pill.textContent = `${id} ✕`;
      pill.addEventListener('click', () => {
        const next = current.filter((x) => x !== id);
        if (next.length) this._step.dependsOn = next;
        else delete this._step.dependsOn;
        this._renderDependsOn();
        this._emit();
      });
      box.append(pill);
    }

    const remaining = others.filter((id) => !current.includes(id));
    if (remaining.length) {
      const select = document.createElement('select');
      select.className = 'add-dep';
      select.innerHTML = `<option value="">+ Add dependency…</option>`
        + remaining.map((id) => `<option value="${escapeHtml(id)}">${escapeHtml(id)}</option>`).join('');
      select.addEventListener('change', () => {
        if (!select.value) return;
        const set = new Set([...current, select.value]);
        // Preserve step order (dependencies read best in execution order).
        this._step.dependsOn = this._stepIds.filter((id) => set.has(id));
        this._renderDependsOn();
        this._emit();
      });
      box.append(select);
    }
  }

  /** @private — the requestBody.replacements list: target (JSON Pointer) → expression-capable value. */
  /** @private — every JSON Pointer the request schema can address (bounded), for autocomplete. */
  _requestPointerPaths() {
    const paths = [];
    const walk = (schema, prefix, depth) => {
      if (!schema || typeof schema !== 'object' || depth > 6) return;
      for (const [name, propSchema] of Object.entries(schema.properties || {})) {
        const path = `${prefix}/${name.replaceAll('~', '~0').replaceAll('/', '~1')}`;
        paths.push(path);
        walk(propSchema, path, depth + 1);
      }
      if (schema.items && typeof schema.items === 'object') {
        const path = `${prefix}/0`;
        paths.push(path);
        walk(schema.items, path, depth + 1);
      }
    };
    walk(this._operationRequest?.schema, '', 0);
    return paths;
  }

  /** @private — the schema node a pointer addresses (array indices resolve through items). */
  _schemaAt(pointer) {
    let node = this._operationRequest?.schema;
    if (!pointer) return node ?? null;
    if (!node || !pointer.startsWith('/')) return null;
    for (const token of pointer.slice(1).split('/')) {
      if (!node || typeof node !== 'object') return null;
      const key = token.replaceAll('~1', '/').replaceAll('~0', '~');
      node = /^\d+$/.test(key) && node.items ? node.items : node.properties?.[key];
    }
    return node ?? null;
  }

  _renderReplacements() {
    const box = this.$('.repls');
    if (!box) return; // the request body section is hidden (a body-less operation) — nothing to render
    const replacements = this._step.requestBody?.replacements || [];
    box.innerHTML = '';

    // The target pointers autocomplete from the operation's request schema — through the SAME
    // CM6 completion popup every other editor uses (a native datalist looked foreign here).
    const pointerPaths = this._requestPointerPaths();
    replacements.forEach((r, i) => {
      const row = document.createElement('div');
      row.className = 'rrow';
      row.style.cssText = 'display:grid; grid-template-columns: 1fr 1.2fr auto; gap:6px; align-items:start; margin-bottom:6px;';
      row.innerHTML = `
        <div class="rtarget-slot"></div>
        <div class="rvalue"></div>
        <button class="rdel ghost" type="button" title="Remove">✕</button>`;
      const targetInput = document.createElement('arazzo-expression-input');
      targetInput.className = 'rtarget';
      targetInput.setAttribute('placeholder', '/card/number');
      targetInput.staticCompletions = pointerPaths.map((path) => {
        const at = this._schemaAt(path);
        const type = Array.isArray(at?.type) ? at.type[0] : at?.type;
        return { label: path, ...(type ? { detail: type } : {}) };
      });
      // A replacement target is a JSON Pointer into the payload (Arazzo `PayloadReplacement.target`), NOT a
      // runtime expression — so reject a `$…` here. Empty is the whole body; otherwise it must start with `/`.
      targetInput.validator = (value) => {
        const v = (value ?? '').trim();
        return v === '' || v.startsWith('/')
          ? { valid: true }
          : { valid: false, errors: [{ message: 'the target is a JSON Pointer into the payload (e.g. /card/number), not a runtime expression' }] };
      };
      targetInput.value = r.target ?? '';
      row.querySelector('.rtarget-slot').replaceWith(targetInput);

      // The VALUE editor follows the schema at the target pointer: an object target (or an object
      // value) edits structurally, constrained to that sub-schema; scalars stay expression inputs,
      // literals coercing to the target's type.
      const buildValue = () => {
        const slot = row.querySelector('.rvalue');
        slot.replaceChildren();
        const target = this._schemaAt(r.target ?? '');
        const type = Array.isArray(target?.type) ? target.type[0] : target?.type;
        if (type === 'object' || (r.value !== null && typeof r.value === 'object')) {
          const ed = document.createElement('arazzo-payload-editor');
          ed.schema = type === 'object' ? target : null;
          ed.completionContext = this._completionContext;
          ed.value = (r.value !== null && typeof r.value === 'object') ? r.value : undefined;
          ed.addEventListener('payload-changed', (e) => {
            e.stopPropagation();
            r.value = e.detail.payload === undefined ? '' : e.detail.payload;
            this._emit();
          });
          slot.append(ed);
          return;
        }

        const value = document.createElement('arazzo-expression-input');
        value.setAttribute('placeholder', type && type !== 'string' ? `${type} or $inputs.…` : '$inputs.…');
        value.completionContext = this._completionContext;
        value.value = typeof r.value === 'string' ? r.value : JSON.stringify(r.value ?? '');
        value.addEventListener('value-changed', (e) => {
          e.stopPropagation();
          const text = e.detail.value;
          if (text === '' || text.startsWith('$') || text.includes('{$') || !type) r.value = text;
          else if (type === 'number' || type === 'integer') { const n = Number(text); r.value = Number.isFinite(n) ? n : text; }
          else if (type === 'boolean') r.value = text === 'true' ? true : text === 'false' ? false : text;
          else r.value = text;
          this._emit();
        });
        slot.append(value);
      };

      buildValue();
      targetInput.addEventListener('value-changed', (e) => {
        e.stopPropagation();
        r.target = e.detail.value;
        buildValue(); // the value editor tracks the schema at the new target
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
      onComponentChange: (name, action) => {
        const kindKey = kind === 'success' ? 'successActions' : 'failureActions';
        (this._components ??= {})[kindKey] ??= {};
        this._components[kindKey][name] = action;
        this.emit('component-changed', { kind: kindKey, name, action });
      },
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
