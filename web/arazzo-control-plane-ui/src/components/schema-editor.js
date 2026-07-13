// <arazzo-schema-editor> — a typed JSON Schema 2020-12 authoring form for workflow `inputs` and the
// components library (schema-form design §3). The document IS the model: it mutates the schema object IN
// PLACE through the pure `schema-authoring.js` helpers (never hand-rolled), so an edit is an ordinary,
// lossless model op. Unrendered keywords, advanced constructs, and key order elsewhere are preserved
// (the round-trip guarantee). Combiners (oneOf/anyOf/allOf) are first-class (§3.2a); a Form|JSON toggle
// exposes the raw JSON via the shared guarded-JSON helper for anything the form does not author.
//
//   const ed = document.createElement('arazzo-schema-editor');
//   ed.value = workflow.inputs ?? { type: 'object' };   // the live schema object
//   ed.library = doc.components?.inputs;                 // optional: for a later slice's $ref picker
//   ed.addEventListener('schema-changed', (e) => { workflow.inputs = e.detail.schema; });
//
// Attributes : readonly
// Properties : .value (the schema object; get returns the live lossless object, set replaces), .library, .title
// Events     : schema-changed { schema }
// Parts      : editor, row, json
//
// | Row kind    | Shows | Edits |
// |-------------|-------|-------|
// | object prop | name · type · required ★ · ✕ · description · more… · ▲▼ | rename/type/required/remove/reorder/constraints |
// | combiner    | kind select · variant cards (nested editors) · + Add variant | kind/add/remove/reorder/label |
// | advanced    | summary (`$defs (2) · not`) · edit as JSON | routes to the JSON tier; never mutated by form edits |
// | boolean     | a read-only true/false switch | JSON tier only (the engine does not enforce boolean input schemas) |
// | +N more     | preserved unrendered keyword names | preserved verbatim |

import { ArazzoElement, SHARED_CSS, escapeHtml, confirmDialog, define } from './base.js';
import { wireGuardedJson } from './guarded-json.js';
import {
  classifyNode, unrenderedKeywords, RENDERABLE_TYPES, COMBINER_KINDS,
  addProperty, renameProperty, reorderProperty, removeProperty, setRequired,
  setType, constraintsDroppedOnType, addVariant, removeVariant, reorderVariant, setConstraint,
} from '../schema-authoring.js';
import './value-editor.js';

const PREVIEW_DEBOUNCE_MS = 350;
const TYPE_LABEL = { object: 'object', array: 'array', string: 'string', number: 'number', integer: 'integer', boolean: 'boolean', null: 'null' };
const COMBINER_LABEL = { oneOf: 'one of', anyOf: 'any of', allOf: 'all of' };
// Per-type "more…" constraints (title/enum/const/default are added for every type).
const CONSTRAINTS = {
  string: [['format', 'text'], ['pattern', 'text'], ['minLength', 'number'], ['maxLength', 'number']],
  number: [['minimum', 'number'], ['maximum', 'number'], ['multipleOf', 'number']],
  integer: [['minimum', 'number'], ['maximum', 'number'], ['multipleOf', 'number']],
  array: [['minItems', 'number'], ['maxItems', 'number'], ['uniqueItems', 'checkbox']],
  object: [],
  boolean: [], null: [],
};

class ArazzoSchemaEditor extends ArazzoElement {
  constructor() {
    super();
    /** @private */ this._schema = { type: 'object' };
    /** @private */ this._mode = 'form';
    /** @private */ this._library = null;
    /** @private */ this._title = '';
    /** @private */ this._previewTimer = null;
    /** @private */ this._previewOpen = false;
  }

  get value() { return this._schema; }
  set value(v) {
    // A JSON Schema is an object OR a boolean (true/false); anything else defaults to an empty object schema.
    this._schema = (v === true || v === false || (v && typeof v === 'object')) ? v : { type: 'object' };
    if (this.isConnected) this._render();
  }

  get library() { return this._library; }
  set library(v) { this._library = v || null; }

  get title() { return this._title; }
  set title(v) { this._title = v || ''; if (this._built) this._render(); }

  get readonly() { return this.hasAttribute('readonly'); }

  connectedCallback() {
    if (!this._built) this._renderShell();
    this._render();
  }

  _renderShell() {
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        :host { display: block; }
        .head { display: flex; align-items: center; gap: 8px; margin-bottom: 8px; }
        .head .legend { font-weight: 600; font-size: 13px; margin-right: auto; }
        .toggle { display: inline-flex; border: 1px solid var(--_border); border-radius: 6px; overflow: hidden; }
        .toggle button { border: none; background: var(--_bg); color: inherit; font: 12px var(--_font); padding: 3px 10px; cursor: pointer; }
        .toggle button[aria-pressed="true"] { background: var(--_surface); font-weight: 600; }
        .rows { display: flex; flex-direction: column; gap: 4px; }
        .node { border: 1px solid var(--_border); border-radius: 8px; padding: 6px 8px; background: var(--_bg); }
        .node.child { margin-left: 14px; }
        .rowline { display: flex; align-items: center; gap: 6px; }
        .rowline .name { flex: 1; min-width: 60px; font: 12px var(--_font); padding: 3px 6px; border: 1px solid var(--_border); border-radius: 5px; background: var(--_bg); color: inherit; }
        .rowline .name.invalid { border-color: var(--arazzo-status-faulted, #d4351c); }
        select.type, select.kind { font: 12px var(--_font); padding: 3px 6px; border: 1px solid var(--_border); border-radius: 5px; background: var(--_bg); color: inherit; }
        .req { cursor: pointer; font-size: 15px; line-height: 1; background: none; border: none; color: var(--_muted); padding: 0 2px; }
        .req[aria-pressed="true"] { color: var(--arazzo-status-suspended, #b07d18); }
        .iconbtn { border: none; background: none; color: var(--_muted); cursor: pointer; font-size: 13px; padding: 0 3px; }
        .iconbtn:hover { color: inherit; }
        .desc { width: 100%; box-sizing: border-box; font: 12px var(--_font); padding: 3px 6px; margin-top: 4px; border: 1px solid var(--_border); border-radius: 5px; background: var(--_bg); color: inherit; }
        .more { margin-top: 6px; }
        .more summary { cursor: pointer; font-size: 11px; color: var(--_muted); }
        .more-body { display: grid; grid-template-columns: max-content 1fr; gap: 4px 8px; align-items: center; padding: 6px 0 2px; }
        .more-body label { font-size: 11px; color: var(--_muted); }
        .more-body input, .more-body select { font: 12px var(--_font); padding: 2px 5px; border: 1px solid var(--_border); border-radius: 5px; background: var(--_bg); color: inherit; }
        .more-body input.invalid { border-color: var(--arazzo-status-faulted, #d4351c); }
        .full { grid-column: 1 / -1; }
        .variant { border: 1px dashed var(--_border); border-radius: 8px; padding: 6px 8px; margin: 6px 0; }
        .variant .vhead { display: flex; align-items: center; gap: 6px; }
        .variant .vlabel { flex: 1; font: 12px var(--_font); padding: 2px 6px; border: 1px solid var(--_border); border-radius: 5px; background: var(--_bg); color: inherit; }
        .add { font: 12px var(--_font); padding: 3px 10px; border: 1px dashed var(--_border); border-radius: 6px; background: none; color: inherit; cursor: pointer; margin-top: 4px; }
        .advanced { color: var(--_muted); font-size: 12px; display: flex; align-items: center; gap: 8px; }
        .chip { display: inline-block; font-size: 10px; padding: 1px 6px; border-radius: 999px; background: var(--_surface); color: var(--_muted); }
        .chips { display: flex; flex-wrap: wrap; gap: 4px; margin-top: 4px; }
        .ghost { color: var(--arazzo-status-faulted, #d4351c); font-size: 12px; display: flex; align-items: center; gap: 8px; }
        .empty { color: var(--_muted); font-size: 12px; padding: 4px 0; }
        textarea.json { width: 100%; box-sizing: border-box; min-height: 160px; font: 12px ui-monospace, Menlo, monospace; padding: 6px; border: 1px solid var(--_border); border-radius: 6px; background: var(--_bg); color: inherit; }
        textarea.json.invalid { border-color: var(--arazzo-status-faulted, #d4351c); }
        .json-hint { font-size: 11px; color: var(--_muted); margin-top: 4px; }
        .preview { margin-top: 10px; }
        .preview summary { cursor: pointer; font-size: 12px; color: var(--_muted); }
        .preview .pv-body { border: 1px solid var(--_border); border-radius: 8px; padding: 8px; margin-top: 6px; }
        [hidden] { display: none !important; }
      </style>
      <div class="head">
        <span class="legend" part="editor"></span>
        <div class="toggle" role="group" aria-label="Editing tier">
          <button class="t-form" type="button" aria-pressed="true">Form</button>
          <button class="t-json" type="button" aria-pressed="false">JSON</button>
        </div>
      </div>
      <div class="form" part="editor"></div>
      <div class="json" part="json" hidden>
        <textarea class="json" spellcheck="false"></textarea>
        <div class="json-hint">the raw JSON Schema; edits apply when it parses</div>
      </div>
      <details class="preview">
        <summary>Preview input form</summary>
        <div class="pv-body"><arazzo-value-editor></arazzo-value-editor></div>
      </details>`;
    this.$('.t-form').addEventListener('click', () => this._setMode('form'));
    this.$('.t-json').addEventListener('click', () => this._setMode('json'));
    this.$('.preview').addEventListener('toggle', (e) => { this._previewOpen = e.target.open; if (this._previewOpen) this._updatePreview(); });
    this._built = true;
  }

  _setMode(mode) {
    if (mode === this._mode) return;
    // JSON → Form re-derives from the (parseable) buffer; an unparseable buffer blocks the switch.
    if (mode === 'form' && this._mode === 'json') {
      const ta = this.$('textarea.json');
      try { this._schema = JSON.parse(ta.value); } catch { ta.classList.add('invalid'); return; }
    }
    this._mode = mode;
    this._render();
    this._commit();
  }

  _render() {
    if (!this._built) return;
    this.$('.legend').textContent = this._title || '';
    this.$('.t-form').setAttribute('aria-pressed', String(this._mode === 'form'));
    this.$('.t-json').setAttribute('aria-pressed', String(this._mode === 'json'));
    this.$('.form').hidden = this._mode !== 'form';
    this.$('.json').hidden = this._mode !== 'json';
    if (this._mode === 'json') this._renderJson();
    else this._renderForm();
    if (this._previewOpen) this._updatePreview();
  }

  _renderJson() {
    const ta = this.$('textarea.json');
    ta.value = JSON.stringify(this._schema, null, 2);
    ta.classList.remove('invalid');
    if (!ta._wired) {
      ta._wired = true;
      ta.readOnly = this.readonly;
      wireGuardedJson(ta, {
        hint: this.$('.json-hint'),
        baseHint: 'the raw JSON Schema; edits apply when it parses',
        emptyDeletes: false,
        onCommit: (value) => { this._schema = value; this._commit(); },
      });
    }
  }

  _renderForm() {
    const form = this.$('.form');
    form.replaceChildren();
    form.appendChild(this._renderNode(this._schema, { root: true }));
  }

  // ── node rendering (recursive) ────────────────────────────────────────────────────────────────
  _renderNode(schema, ctx = {}) {
    const cls = classifyNode(schema);
    const node = document.createElement('div');
    node.className = 'node' + (ctx.root ? '' : ' child');
    node.dataset.kind = cls.kind;
    if (cls.kind === 'boolean-schema') { this._renderBoolean(node, schema, ctx); return node; }
    if (cls.kind === 'advanced') { this._renderAdvanced(node, schema, cls, ctx); return node; }
    if (cls.kind === 'combiner') { this._renderCombiner(node, schema, cls, ctx); return node; }
    this._renderRenderable(node, schema, ctx);
    return node;
  }

  _typeSelect(schema, onChange) {
    const sel = document.createElement('select');
    sel.className = 'type';
    sel.disabled = this.readonly;
    let html = '<optgroup label="Type">';
    for (const t of RENDERABLE_TYPES) html += `<option value="${t}">${TYPE_LABEL[t]}</option>`;
    html += '</optgroup><optgroup label="Combiner">';
    for (const k of COMBINER_KINDS) html += `<option value="${k}">${COMBINER_LABEL[k]}</option>`;
    html += '</optgroup>';
    sel.innerHTML = html;
    const cls = classifyNode(schema);
    sel.value = cls.kind === 'combiner' ? cls.combiner : (schema.type ?? 'object');
    sel.addEventListener('change', () => onChange(sel.value, sel));
    return sel;
  }

  async _changeType(schema, node, newValue, sel) {
    const cls = classifyNode(schema);
    let msg = null;
    if (cls.kind === 'combiner' && !COMBINER_KINDS.includes(newValue)) {
      const list = schema[cls.combiner];
      if (Array.isArray(list) && list.length > 1) msg = `Keeping only variant 1 — ${list.length - 1} other variant(s) will be dropped.`;
    } else if (!COMBINER_KINDS.includes(newValue)) {
      const dropped = constraintsDroppedOnType(schema, newValue);
      if (dropped.length) msg = `Changing the type drops: ${dropped.join(', ')}.`;
    }
    if (msg && !(await confirmDialog(this, { message: msg, confirmLabel: 'Change type' }))) {
      if (sel) sel.value = cls.kind === 'combiner' ? cls.combiner : (schema.type ?? 'object');
      return;
    }
    setType(schema, newValue);
    this._renderForm();
    this._commit();
  }

  _renderRenderable(node, schema, ctx) {
    const line = document.createElement('div');
    line.className = 'rowline';
    // A named property row leads with its name + required toggle; the root leads with just its type.
    if (ctx.name != null) {
      const name = document.createElement('input');
      name.className = 'name';
      name.value = ctx.name;
      name.disabled = this.readonly;
      name.addEventListener('change', () => this._renameRow(ctx, name));
      line.appendChild(name);
    }
    line.appendChild(this._typeSelect(schema, (v, sel) => this._changeType(schema, node, v, sel)));
    if (ctx.name != null) {
      const req = document.createElement('button');
      req.className = 'req';
      req.type = 'button';
      req.textContent = '★';
      req.title = 'Required';
      req.setAttribute('aria-pressed', String(!!ctx.isRequired));
      req.disabled = this.readonly;
      req.addEventListener('click', () => { setRequired(ctx.parent, ctx.name, req.getAttribute('aria-pressed') !== 'true'); this._renderForm(); this._commit(); });
      line.appendChild(req);
      line.appendChild(this._reorderRemove(ctx));
    }
    node.appendChild(line);

    // description
    const desc = document.createElement('input');
    desc.className = 'desc';
    desc.placeholder = 'description';
    desc.value = schema.description ?? '';
    desc.disabled = this.readonly;
    desc.addEventListener('input', () => { setConstraint(schema, 'description', desc.value); this._commit(); });
    node.appendChild(desc);

    // more… constraints
    node.appendChild(this._moreSection(schema));
    // +N more preserved keywords
    const more = unrenderedKeywords(schema).filter((k) => k !== 'default');
    if (more.length) node.appendChild(this._chips(more));

    // nested structure
    const type = schema.type ?? (schema.properties ? 'object' : (schema.items ? 'array' : undefined));
    if (type === 'object') node.appendChild(this._objectBody(schema));
    else if (type === 'array') node.appendChild(this._arrayBody(schema));
  }

  _renameRow(ctx, input) {
    const name = input.value.trim();
    input.classList.remove('invalid');
    if (!name || (name !== ctx.name && ctx.parent.properties && name in ctx.parent.properties)) {
      input.classList.add('invalid'); // blank or duplicate — leave the model untouched
      input.value = ctx.name;
      return;
    }
    if (name !== ctx.name) { renameProperty(ctx.parent, ctx.name, name); this._renderForm(); this._commit(); }
  }

  _reorderRemove(ctx) {
    const wrap = document.createElement('span');
    const up = document.createElement('button'); up.className = 'iconbtn'; up.type = 'button'; up.textContent = '▲'; up.title = 'Move up'; up.disabled = this.readonly;
    const down = document.createElement('button'); down.className = 'iconbtn'; down.type = 'button'; down.textContent = '▼'; down.title = 'Move down'; down.disabled = this.readonly;
    const del = document.createElement('button'); del.className = 'iconbtn'; del.type = 'button'; del.textContent = '✕'; del.title = 'Remove'; del.disabled = this.readonly;
    const idx = () => Object.keys(ctx.parent.properties || {}).indexOf(ctx.name);
    up.addEventListener('click', () => { reorderProperty(ctx.parent, ctx.name, idx() - 1); this._renderForm(); this._commit(); });
    down.addEventListener('click', () => { reorderProperty(ctx.parent, ctx.name, idx() + 1); this._renderForm(); this._commit(); });
    del.addEventListener('click', () => { removeProperty(ctx.parent, ctx.name); this._renderForm(); this._commit(); });
    wrap.append(up, down, del);
    return wrap;
  }

  _moreSection(schema) {
    const type = schema.type ?? (schema.properties ? 'object' : (schema.items ? 'array' : 'string'));
    const details = document.createElement('details');
    details.className = 'more';
    const body = document.createElement('div');
    body.className = 'more-body';
    const rows = [['title', 'text'], ...(CONSTRAINTS[type] || [])];
    for (const [key, kind] of rows) this._constraintRow(body, schema, key, kind);
    // typed default (§9.4) — value-editor typed by the row's schema.
    this._defaultRow(body, schema);
    details.innerHTML = '<summary>more…</summary>';
    details.appendChild(body);
    return details;
  }

  _constraintRow(body, schema, key, kind) {
    const label = document.createElement('label'); label.textContent = key;
    const input = document.createElement('input');
    input.type = kind === 'number' ? 'number' : (kind === 'checkbox' ? 'checkbox' : 'text');
    input.disabled = this.readonly;
    if (kind === 'checkbox') input.checked = !!schema[key];
    else input.value = schema[key] ?? '';
    input.addEventListener('input', () => {
      input.classList.remove('invalid');
      let value;
      if (kind === 'checkbox') value = input.checked ? true : undefined;
      else if (kind === 'number') value = input.value === '' ? undefined : Number(input.value);
      else value = input.value;
      if (key === 'pattern' && value) { try { RegExp(value); } catch { input.classList.add('invalid'); return; } }
      setConstraint(schema, key, value);
      this._commit();
    });
    body.append(label, input);
  }

  _defaultRow(body, schema) {
    const label = document.createElement('label'); label.textContent = 'default';
    const ve = document.createElement('arazzo-value-editor');
    ve.className = 'full';
    if (schema.default !== undefined) ve.seed = schema.default; // seed BEFORE descriptor (value-editor contract)
    ve.descriptor = schema;
    // Native input events are composed, so an edit inside value-editor's shadow root reaches here.
    ve.addEventListener('input', () => {
      try { const v = ve.value; if (v === undefined) delete schema.default; else schema.default = v; this._commit(); } catch { /* invalid default held */ }
    });
    body.append(label, ve);
  }

  _objectBody(schema) {
    const wrap = document.createElement('div');
    const props = (schema.properties && typeof schema.properties === 'object') ? schema.properties : {};
    const required = new Set(Array.isArray(schema.required) ? schema.required : []);
    const names = Object.keys(props);
    // ghost rows: required names with no property (§3.2).
    for (const r of required) if (!(r in props)) wrap.appendChild(this._ghostRow(schema, r));
    if (!names.length) { const e = document.createElement('div'); e.className = 'empty'; e.textContent = 'No properties.'; wrap.appendChild(e); }
    for (const name of names) {
      wrap.appendChild(this._renderNode(props[name], { name, parent: schema, isRequired: required.has(name) }));
    }
    const add = document.createElement('button');
    add.className = 'add'; add.type = 'button'; add.textContent = '+ Add property'; add.disabled = this.readonly;
    add.addEventListener('click', () => {
      let n = 'newProperty'; let i = 1;
      while (n in (schema.properties || {})) n = `newProperty${++i}`;
      addProperty(schema, n, { type: 'string' });
      this._renderForm(); this._commit();
      const input = [...this.$$('.name')].find((x) => x.value === n);
      input?.focus(); input?.select();
    });
    wrap.appendChild(add);
    return wrap;
  }

  _ghostRow(schema, name) {
    const row = document.createElement('div');
    row.className = 'ghost';
    row.innerHTML = `<span>required but not defined: <strong>${escapeHtml(name)}</strong></span>`;
    const addBtn = document.createElement('button'); addBtn.className = 'iconbtn'; addBtn.type = 'button'; addBtn.textContent = 'add property'; addBtn.disabled = this.readonly;
    addBtn.addEventListener('click', () => { addProperty(schema, name, { type: 'string' }); this._renderForm(); this._commit(); });
    const dropBtn = document.createElement('button'); dropBtn.className = 'iconbtn'; dropBtn.type = 'button'; dropBtn.textContent = 'drop requirement'; dropBtn.disabled = this.readonly;
    dropBtn.addEventListener('click', () => { setRequired(schema, name, false); this._renderForm(); this._commit(); });
    row.append(addBtn, dropBtn);
    return row;
  }

  _arrayBody(schema) {
    const wrap = document.createElement('div');
    const label = document.createElement('div'); label.className = 'empty'; label.textContent = 'items:';
    wrap.appendChild(label);
    if (!schema.items || typeof schema.items !== 'object') schema.items = { type: 'string' };
    wrap.appendChild(this._renderNode(schema.items, { name: null, parent: schema, itemsOf: schema }));
    return wrap;
  }

  _renderCombiner(node, schema, cls, ctx) {
    const line = document.createElement('div');
    line.className = 'rowline';
    if (ctx.name != null) {
      const name = document.createElement('input'); name.className = 'name'; name.value = ctx.name; name.disabled = this.readonly;
      name.addEventListener('change', () => this._renameRow(ctx, name));
      line.appendChild(name);
    }
    line.appendChild(this._typeSelect(schema, (v, sel) => this._changeType(schema, node, v, sel)));
    if (ctx.name != null) {
      const req = document.createElement('button'); req.className = 'req'; req.type = 'button'; req.textContent = '★'; req.title = 'Required';
      req.setAttribute('aria-pressed', String(!!ctx.isRequired)); req.disabled = this.readonly;
      req.addEventListener('click', () => { setRequired(ctx.parent, ctx.name, req.getAttribute('aria-pressed') !== 'true'); this._renderForm(); this._commit(); });
      line.appendChild(req);
      line.appendChild(this._reorderRemove(ctx));
    }
    node.appendChild(line);

    const kind = cls.combiner;
    const variants = Array.isArray(schema[kind]) ? schema[kind] : [];
    variants.forEach((variant, i) => {
      const card = document.createElement('div'); card.className = 'variant';
      const vhead = document.createElement('div'); vhead.className = 'vhead';
      const vlabel = document.createElement('input'); vlabel.className = 'vlabel'; vlabel.placeholder = 'label (title)'; vlabel.value = (variant && variant.title) ?? ''; vlabel.disabled = this.readonly;
      vlabel.addEventListener('input', () => { if (variant && typeof variant === 'object') setConstraint(variant, 'title', vlabel.value); this._commit(); });
      const up = document.createElement('button'); up.className = 'iconbtn'; up.type = 'button'; up.textContent = '▲'; up.disabled = this.readonly;
      const down = document.createElement('button'); down.className = 'iconbtn'; down.type = 'button'; down.textContent = '▼'; down.disabled = this.readonly;
      const del = document.createElement('button'); del.className = 'iconbtn'; del.type = 'button'; del.textContent = '✕'; del.disabled = this.readonly;
      up.addEventListener('click', () => { reorderVariant(schema, i, i - 1); this._renderForm(); this._commit(); });
      down.addEventListener('click', () => { reorderVariant(schema, i, i + 1); this._renderForm(); this._commit(); });
      del.addEventListener('click', () => { removeVariant(schema, i); this._renderForm(); this._commit(); });
      vhead.append(vlabel, up, down, del);
      card.appendChild(vhead);
      if (variant && typeof variant === 'object') card.appendChild(this._renderNode(variant, {}));
      node.appendChild(card);
    });
    const add = document.createElement('button'); add.className = 'add'; add.type = 'button'; add.textContent = '+ Add variant'; add.disabled = this.readonly;
    add.addEventListener('click', () => { addVariant(schema, { type: 'string' }); this._renderForm(); this._commit(); });
    node.appendChild(add);
    // sibling +N more (description etc. edit via the more-body; keep it simple: chips for unrendered)
    const more = unrenderedKeywords(schema);
    if (more.length) node.appendChild(this._chips(more));
  }

  _renderAdvanced(node, schema, cls, ctx) {
    const line = document.createElement('div'); line.className = 'rowline';
    if (ctx.name != null) {
      const name = document.createElement('input'); name.className = 'name'; name.value = ctx.name; name.disabled = this.readonly;
      name.addEventListener('change', () => this._renameRow(ctx, name));
      line.appendChild(name);
      const req = document.createElement('button'); req.className = 'req'; req.type = 'button'; req.textContent = '★';
      req.setAttribute('aria-pressed', String(!!ctx.isRequired)); req.disabled = this.readonly;
      req.addEventListener('click', () => { setRequired(ctx.parent, ctx.name, req.getAttribute('aria-pressed') !== 'true'); this._renderForm(); this._commit(); });
      line.appendChild(req);
      line.appendChild(this._reorderRemove(ctx));
    }
    node.appendChild(line);
    const adv = document.createElement('div'); adv.className = 'advanced';
    const keys = Object.keys(schema).filter((k) => k !== 'type' && k !== 'title' && k !== 'description');
    adv.innerHTML = `<span>advanced: ${escapeHtml(summarize(schema, keys))}</span>`;
    const edit = document.createElement('button'); edit.className = 'iconbtn'; edit.type = 'button'; edit.textContent = 'edit as JSON';
    edit.addEventListener('click', () => this._setMode('json'));
    adv.appendChild(edit);
    node.appendChild(adv);
  }

  _renderBoolean(node, schema, ctx) {
    const row = document.createElement('div'); row.className = 'advanced';
    row.innerHTML = `<span>boolean schema: <strong>${schema === true ? 'true (accept all)' : 'false (reject all)'}</strong> — edit in the JSON tier</span>`;
    const edit = document.createElement('button'); edit.className = 'iconbtn'; edit.type = 'button'; edit.textContent = 'edit as JSON';
    edit.addEventListener('click', () => this._setMode('json'));
    row.appendChild(edit);
    node.appendChild(row);
  }

  _chips(names) {
    const wrap = document.createElement('div'); wrap.className = 'chips';
    wrap.innerHTML = `<span class="chip">+${names.length} more</span>` + names.map((n) => `<span class="chip">${escapeHtml(n)}</span>`).join('');
    return wrap;
  }

  _commit() {
    this.emit('schema-changed', { schema: this._schema });
    if (this._previewOpen) {
      clearTimeout(this._previewTimer);
      this._previewTimer = setTimeout(() => this._updatePreview(), PREVIEW_DEBOUNCE_MS);
    }
  }

  _updatePreview() {
    const ve = this.$('.preview arazzo-value-editor');
    if (!ve) return;
    // The renderer normalizes raw combiners at its own boundary (slice C); substitute local library refs so a
    // reference-rooted schema previews the resolved typed form (§3.5) rather than the raw fallback.
    ve.descriptor = this._resolveLibraryRefs(this._schema);
  }

  _resolveLibraryRefs(schema, seen = new Set()) {
    if (!schema || typeof schema !== 'object') return schema;
    if (typeof schema.$ref === 'string') {
      const m = /^#\/components\/inputs\/(.+)$/.exec(schema.$ref);
      const target = m && this._library && this._library[m[1]];
      if (target && !seen.has(schema.$ref)) return this._resolveLibraryRefs(target, new Set([...seen, schema.$ref]));
      return schema;
    }
    if (Array.isArray(schema)) return schema.map((s) => this._resolveLibraryRefs(s, seen));
    const out = {};
    for (const [k, v] of Object.entries(schema)) out[k] = (v && typeof v === 'object') ? this._resolveLibraryRefs(v, seen) : v;
    return out;
  }
}

// A one-line advanced summary: `$defs (2) · not · patternProperties (1)`.
function summarize(schema, keys) {
  return keys.map((k) => {
    const v = schema[k];
    if (Array.isArray(v)) return `${k} (${v.length})`;
    if (v && typeof v === 'object') return `${k} (${Object.keys(v).length})`;
    return k;
  }).join(' · ') || 'schema';
}

define('arazzo-schema-editor', ArazzoSchemaEditor);
export { ArazzoSchemaEditor };
