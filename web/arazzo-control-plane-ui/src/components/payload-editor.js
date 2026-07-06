// <arazzo-payload-editor> — the strongly-typed request-body / message-payload editor
// (design §5.3): structure comes from the binding's JSON Schema, but every scalar leaf is an
// <arazzo-expression-input>, because Arazzo payloads legitimately carry runtime expressions where
// the schema says number/boolean ("amount": "$inputs.amount"). On edit, a literal coerces to the
// schema's type (42 in a number field emits 42); anything expression-shaped stays a string.
// Unknown value keys the schema does not describe are PRESERVED verbatim — never dropped.
//
//   const ed = document.createElement('arazzo-payload-editor');
//   ed.schema = operation.request.schema;         // optional — without it, JSON mode only
//   ed.completionContext = { … };
//   ed.value = step.requestBody?.payload;
//   ed.addEventListener('payload-changed', (e) => { step.requestBody.payload = e.detail.payload; });
//
// A Form | JSON toggle keeps full fidelity: arrays and anything beyond the schema's shape are
// edited as guarded raw JSON (unparseable input never emits; the last valid payload stands).

import { ArazzoElement, SHARED_CSS, escapeHtml, define } from './base.js';
import './expression-input.js';

class ArazzoPayloadEditor extends ArazzoElement {
  constructor() {
    super();
    /** @private */ this._schema = null;
    /** @private */ this._value = undefined;
    /** @private */ this._completionContext = {};
    /** @private */ this._mode = 'form';
  }

  connectedCallback() {
    if (!this._built) this.renderShell();
    this.render();
  }

  /** The payload value. Setting rebuilds the editor. */
  get value() { return structuredClone(this._value); }
  set value(v) {
    this._value = structuredClone(v);
    if (this.isConnected) { if (!this._built) this.renderShell(); this.render(); }
  }

  /** The binding's JSON Schema; enables the typed form (JSON mode remains available). */
  get schema() { return this._schema; }
  set schema(s) {
    this._schema = s || null;
    if (this.isConnected && this._built) this.render();
  }

  set completionContext(ctx) {
    this._completionContext = ctx || {};
    for (const x of this.$$('arazzo-expression-input')) x.completionContext = this._completionContext;
  }

  get completionContext() { return this._completionContext; }

  /** @private */
  renderShell() {
    this._built = true;
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        :host { display: block; }
        .modes { display: flex; gap: 4px; margin-bottom: 6px; }
        .modes button { font-size: 11px; padding: 2px 10px; opacity: 0.7; }
        .modes button.active { opacity: 1; border-color: var(--_accent); font-weight: 600; }
        .fields { display: grid; gap: 8px; min-width: 0; }
        .fields .field, .fields .fieldset { min-width: 0; }
        label { font-size: 11px; color: var(--_muted); display: flex; gap: 6px; align-items: baseline; margin-bottom: 2px; }
        label .t { font-size: 10px; opacity: 0.8; }
        .fieldset { border: 1px solid var(--_border); border-radius: var(--_radius); padding: 8px; display: grid; gap: 8px; background: var(--_surface); }
        .fieldset > .fname { font: 600 11px var(--_font); color: var(--_muted); }
        textarea {
          width: 100%; box-sizing: border-box; font: 12px ui-monospace, SFMono-Regular, Menlo, monospace;
          padding: 7px 9px; border: 1px solid var(--_border); border-radius: var(--_radius);
          background-color: var(--_bg); color: var(--_text); resize: vertical;
        }
        textarea.invalid { border-color: var(--_danger); }
        .hint { font-size: 11px; color: var(--_muted); margin-top: 3px; }
              arazzo-expression-input.invalid { outline: 1.5px solid var(--_danger, #d4351c); border-radius: 6px; }
      </style>
      <div class="modes" hidden>
        <button type="button" class="m-form">Form</button>
        <button type="button" class="m-json">JSON</button>
      </div>
      <div class="body"></div>
    `;
    this.$('.m-form').addEventListener('click', () => { this._mode = 'form'; this.render(); });
    this.$('.m-json').addEventListener('click', () => { this._mode = 'json'; this.render(); });
  }

  /** @private */
  render() {
    const hasForm = isObjectSchema(this._schema);
    this.$('.modes').hidden = !hasForm;
    this.$('.m-form').classList.toggle('active', this._mode === 'form' && hasForm);
    this.$('.m-json').classList.toggle('active', this._mode === 'json' || !hasForm);
    const body = this.$('.body');
    body.replaceChildren();
    if (hasForm && this._mode === 'form') body.append(this._buildFieldset(this._schema, [], null));
    else body.append(this._buildJson());
  }

  /** @private — one object level: a field per schema property; unknown value keys are kept as-is. */
  _buildFieldset(schema, path, name) {
    const box = document.createElement('div');
    box.className = name ? 'fieldset' : 'fields';
    if (name) {
      const label = document.createElement('div');
      label.className = 'fname';
      label.textContent = name;
      box.append(label);
    }
    for (const [prop, propSchema] of Object.entries(schema.properties || {})) {
      const fieldPath = [...path, prop];
      if (isObjectSchema(propSchema)) {
        box.append(this._buildFieldset(propSchema, fieldPath, prop));
      } else {
        box.append(this._buildLeaf(prop, propSchema, fieldPath));
      }
    }
    return box;
  }

  /** @private — a scalar/array leaf: an expression-capable input with schema-typed coercion. */
  _buildLeaf(name, schema, path) {
    const field = document.createElement('div');
    field.className = 'field';
    const type = Array.isArray(schema?.type) ? schema.type[0] : schema?.type || 'any';
    field.innerHTML = `<label>${escapeHtml(name)} <span class="t">${escapeHtml(type)}${schema?.format ? ` (${escapeHtml(schema.format)})` : ''}</span></label>`;

    const current = getAt(this._value, path);
    const input = document.createElement('arazzo-expression-input');
    input.setAttribute('placeholder', type === 'string' ? '$inputs.… or a literal' : `$inputs.… or a ${type}`);
    input.value = current === undefined ? '' : (typeof current === 'string' ? current : JSON.stringify(current));
    input.completionContext = this._completionContext;
    input.addEventListener('value-changed', (e) => {
      e.stopPropagation();
      this._setLeaf(path, e.detail.value, type);
      // Inline truth while typing: a non-expression literal that can never satisfy the schema's
      // type shows invalid (t → tr → tru clears at "true"); Validate catches what gets committed.
      const text = e.detail.value;
      const impossible = text !== '' && !text.startsWith('$') && !text.includes('{$') && (
        (type === 'boolean' && text !== 'true' && text !== 'false')
        || ((type === 'number' || type === 'integer') && !Number.isFinite(Number(text))));
      input.classList.toggle('invalid', impossible);
      input.title = impossible ? `neither a ${type} nor a runtime expression — the schema requires a ${type}` : '';
    });
    field.append(input);
    return field;
  }

  /** @private — coercion: expressions stay strings; literals adopt the schema's type when they parse. */
  _setLeaf(path, text, type) {
    let next;
    if (text === '') {
      next = undefined; // an emptied field unsets the key
    } else if (text.startsWith('$') || text.includes('{$')) {
      next = text; // a runtime expression, whatever the schema says
    } else if (type === 'number' || type === 'integer') {
      const n = Number(text);
      next = Number.isFinite(n) ? n : text;
    } else if (type === 'boolean') {
      next = text === 'true' ? true : text === 'false' ? false : text;
    } else if (type === 'array' || type === 'object' || type === 'any') {
      try { next = JSON.parse(text); } catch { next = text; }
    } else {
      next = text;
    }
    const working = isPlainObject(this._value) ? structuredClone(this._value) : {};
    setAt(working, path, next);
    this._value = working;
    this._emit();
  }

  /** @private — the raw view: guarded JSON, full fidelity. */
  _buildJson() {
    const wrap = document.createElement('div');
    wrap.innerHTML = `
      <textarea class="payload" rows="5" spellcheck="false">${this._value !== undefined ? escapeHtml(JSON.stringify(this._value, null, 2)) : ''}</textarea>
      <div class="hint payload-hint">JSON; runtime expressions allowed in strings</div>
    `;
    const ta = wrap.querySelector('textarea');
    ta.addEventListener('input', () => {
      const hint = wrap.querySelector('.payload-hint');
      if (!ta.value.trim()) {
        ta.classList.remove('invalid');
        hint.textContent = 'JSON; runtime expressions allowed in strings';
        this._value = undefined;
        this._emit();
        return;
      }
      try {
        this._value = JSON.parse(ta.value);
        ta.classList.remove('invalid');
        hint.textContent = 'JSON; runtime expressions allowed in strings';
        this._emit();
      } catch (err) {
        ta.classList.add('invalid');
        hint.textContent = `not JSON yet: ${String(err.message).slice(0, 80)}`;
        // Never emit a broken payload; the last valid one stands.
      }
    });
    return wrap;
  }

  /** @private */
  _emit() {
    this.emit('payload-changed', { payload: this.value });
  }
}

function isObjectSchema(s) {
  if (!s || typeof s !== 'object') return false;
  const type = Array.isArray(s.type) ? s.type[0] : s.type;
  return type === 'object' || (!type && !!s.properties);
}

function isPlainObject(v) {
  return v !== null && typeof v === 'object' && !Array.isArray(v);
}

function getAt(value, path) {
  let node = value;
  for (const key of path) {
    if (!isPlainObject(node)) return undefined;
    node = node[key];
  }
  return node;
}

function setAt(obj, path, value) {
  let node = obj;
  for (const key of path.slice(0, -1)) {
    if (!isPlainObject(node[key])) node[key] = {};
    node = node[key];
  }
  const last = path[path.length - 1];
  if (value === undefined) delete node[last];
  else node[last] = value;
}

define('arazzo-payload-editor', ArazzoPayloadEditor);
export { ArazzoPayloadEditor };
