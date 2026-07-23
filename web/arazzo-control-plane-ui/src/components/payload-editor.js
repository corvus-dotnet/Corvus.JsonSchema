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
import './text-editor.js';
import './mode-toggle.js';

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
        .modes { display: block; margin-bottom: 6px; }
        /* The form seats in a delimited box mirroring the JSON text editor's bordered area, so Form
           and JSON modes read as the same panel in two views (compact: the editor's border + radius). */
        .fields { display: grid; gap: 10px; min-width: 0; border: 1px solid var(--_border); border-radius: var(--_radius); padding: 10px; }
        .fields:empty { display: none; }
        .fields .field, .fields .fieldset { min-width: 0; }
        label { font-size: 11px; color: var(--_muted); display: flex; gap: 6px; align-items: baseline; margin-bottom: 2px; }
        label .t { font-size: 10px; opacity: 0.8; }
        .fieldset { border: 1px solid var(--_border); border-radius: var(--_radius); padding: 8px; display: grid; gap: 8px; background: var(--_surface); }
        .fieldset > .fname { font: 600 11px var(--_font); color: var(--_muted); }
        arazzo-text-editor.payload { display: block; height: 180px; min-height: 0; }
        /* An enum leaf is a combo: the expression input plus a picker popping the declared values.
           Expressions stay first-class — the picker only offers the literal choices. */
        .leaf-combo { display: flex; gap: 4px; align-items: center; position: relative; }
        .leaf-combo arazzo-expression-input { flex: 1; min-width: 0; }
        .enum-pick { flex: none; font-size: 11px; padding: 4px 8px; }
        .enum-menu { position: absolute; top: 100%; right: 0; z-index: 30; margin-top: 4px; min-width: 160px; max-height: 220px; overflow-y: auto; background: var(--_bg); border: 1px solid var(--_border); border-radius: 6px; box-shadow: 0 6px 24px rgba(0,0,0,0.14); padding: 4px; display: grid; }
        .enum-menu[hidden] { display: none; }
        .enum-opt { display: block; width: 100%; text-align: left; border: none; background: none; color: inherit; padding: 4px 8px; border-radius: 4px; cursor: pointer; font: 12px ui-monospace, SFMono-Regular, Menlo, monospace; }
        .enum-opt:hover { background: var(--_surface); }
        .enum-opt[aria-selected="true"] { background: color-mix(in srgb, var(--_accent) 14%, transparent); font-weight: 600; }
        .hint { font-size: 11px; color: var(--_muted); margin-top: 3px; }
              arazzo-expression-input.invalid { outline: 1.5px solid var(--_danger, #d4351c); border-radius: 6px; }
      </style>
      <arazzo-mode-toggle class="modes" hidden></arazzo-mode-toggle>
      <div class="body"></div>
    `;
    const modes = this.$('.modes');
    modes.options = [{ value: 'form', label: 'Form' }, { value: 'json', label: 'JSON' }];
    modes.addEventListener('mode-changed', (e) => { this._mode = e.detail.value; this.render(); });
    // Component-scoped dismissal for the enum pickers: any click outside a combo, or Escape, closes.
    this.shadowRoot.addEventListener('click', () => this._closeEnumMenus());
    this.shadowRoot.addEventListener('keydown', (e) => { if (e.key === 'Escape') this._closeEnumMenus(); });
  }

  /** @private */
  render() {
    const hasForm = isObjectSchema(this._schema);
    this.$('.modes').hidden = !hasForm;
    this.$('.modes').value = (this._mode === 'json' || !hasForm) ? 'json' : 'form';
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
      const anType = type === 'integer' ? `an ${type}` : `a ${type}`;
      input.title = impossible ? `neither ${anType} nor a runtime expression — the schema requires ${anType}` : '';
    });

    // An enum leaf gains a picker: the declared values as a listbox (the current one marked), each
    // choice routing through the SAME coercion as typing, so a picked 3 lands as the number 3.
    // Expressions stay first-class — the input types them exactly as before.
    if (Array.isArray(schema?.enum) && schema.enum.length) {
      const combo = document.createElement('div');
      combo.className = 'leaf-combo';
      const pick = document.createElement('button');
      pick.type = 'button';
      pick.className = 'enum-pick ghost';
      pick.textContent = '▾';
      pick.title = `Choose one of the ${schema.enum.length} declared values`;
      pick.setAttribute('aria-haspopup', 'listbox');
      pick.setAttribute('aria-expanded', 'false');
      const menu = document.createElement('div');
      menu.className = 'enum-menu';
      menu.setAttribute('role', 'listbox');
      menu.hidden = true;
      const asText = (v) => (typeof v === 'string' ? v : JSON.stringify(v));
      pick.addEventListener('click', (e) => {
        e.stopPropagation();
        const open = menu.hidden;
        this._closeEnumMenus();
        if (!open) return;
        menu.replaceChildren(...schema.enum.map((v) => {
          const option = document.createElement('button');
          option.type = 'button';
          option.className = 'enum-opt';
          option.setAttribute('role', 'option');
          option.setAttribute('aria-selected', String(input.value === asText(v)));
          option.textContent = asText(v);
          option.addEventListener('click', () => {
            input.value = asText(v);
            this._setLeaf(path, asText(v), type);
            input.classList.remove('invalid');
            input.title = '';
            this._closeEnumMenus();
          });
          return option;
        }));
        menu.hidden = false;
        pick.setAttribute('aria-expanded', 'true');
      });
      combo.append(input, pick, menu);
      field.append(combo);
      return field;
    }

    field.append(input);
    return field;
  }

  /** @private — one enum menu open at a time; any outside interaction closes it (wired once in renderShell). */
  _closeEnumMenus() {
    for (const menu of this.$$('.enum-menu:not([hidden])')) menu.hidden = true;
    for (const pick of this.$$('.enum-pick[aria-expanded="true"]')) pick.setAttribute('aria-expanded', 'false');
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

  /** @private — the raw view: a syntax-highlighted, guarded JSON editor (full fidelity). */
  _buildJson() {
    const wrap = document.createElement('div');
    const ed = document.createElement('arazzo-text-editor');
    ed.standalone = true; // an inline editor with no document model — undo/redo stay local
    ed.className = 'payload';
    ed.value = this._value !== undefined ? JSON.stringify(this._value, null, 2) : '';
    const hint = document.createElement('div');
    hint.className = 'hint payload-hint';
    hint.textContent = 'JSON; runtime expressions allowed in strings';
    wrap.append(ed, hint);

    // Guarded, last-valid-wins: a parseable buffer commits and clears the problem; an unparseable one
    // shows the parse error under the editor while the last valid value stands (never emitted broken); a
    // blank buffer clears the payload.
    ed.addEventListener('text-changed', (e) => {
      const text = e.detail.text;
      if (!text.trim()) {
        ed.setProblem(null);
        this._value = undefined;
        this._emit();
        return;
      }
      try {
        const parsed = JSON.parse(text);
        ed.setProblem(null);
        this._value = parsed;
        this._emit();
      } catch (err) {
        ed.setProblem(`not JSON yet: ${String(err.message).slice(0, 80)}`);
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
