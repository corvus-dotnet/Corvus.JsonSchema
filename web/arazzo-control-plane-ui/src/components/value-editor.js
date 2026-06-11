// <arazzo-value-editor> — a strongly-typed form that builds a JSON value from a precomputed TypeDescriptor.
//
//   const b = document.querySelector('arazzo-value-editor');
//   b.descriptor = { type: 'object', properties: { amount: { type:'number' }, status:{ type:'string', enum:[...] } } };
//   const value = b.value;   // the assembled value (throws a friendly Error on invalid input)
//
// Properties : .descriptor (a TypeDescriptor — usually an object whose `properties` are the fields), .value,
//              .validator (optional `async (value) => { valid, errors[] }` — when set, the editor validates on
//              edit and shows each error next to the field its `instancePath` maps to, plus an unplaced summary)
// Events     : (none; the host reads .value on submit)
//
// It renders a control suited to each field's recognised type/format/enum/constraints (date/datetime/email/uri
// inputs, number inputs with min/max/step, enum dropdowns, checkboxes, nested objects, unions, tuples, maps,
// add/remove arrays), and falls back to a raw-JSON textarea for anything it can't type. Used as the output
// editor for Skip's `skipOutputs`; a future state-patch builder can reuse it to edit each operation's value.

import { ArazzoElement, SHARED_CSS, escapeHtml, define } from './base.js';

const VALIDATE_DEBOUNCE_MS = 350;

class ArazzoValueEditor extends ArazzoElement {
  constructor() {
    super();
    /** @private */ this._descriptor = null;
    /** @private */ this._read = null; // root value reader installed by render()
    /** @private */ this._validator = null;
    /** @private */ this._errorTargets = []; // { pathFn: () => (string|null), el: HTMLElement }
    /** @private */ this._validateSeq = 0;
    /** @private */ this._validateTimer = null;
  }

  connectedCallback() {
    if (!this._built) this.renderShell();
    this.renderForm();
  }

  /** The TypeDescriptor driving the form (typically an object descriptor; its `properties` become the fields). */
  get descriptor() { return this._descriptor; }

  set descriptor(value) {
    this._descriptor = value;
    if (this.isConnected) { if (!this._built) this.renderShell(); this.renderForm(); }
  }

  /** An optional `async (value) => { valid, errors }` validator; when set, the editor validates live on edit. */
  get validator() { return this._validator; }

  set validator(fn) {
    this._validator = typeof fn === 'function' ? fn : null;
    if (!this._validator) this._clearErrors();
  }

  /** The assembled value. Throws a friendly {@link Error} when a field's input is invalid. */
  get value() { return this._read ? this._read() : undefined; }

  renderShell() {
    this._built = true;
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        :host { display: block; }
        .fields { display: grid; gap: 10px; }
        .field > label { font-size: 12px; color: var(--_muted); display: block; margin-bottom: 3px; }
        .field > label.check { display: flex; gap: 8px; align-items: center; margin-bottom: 0; cursor: pointer; }
        .field > label.check input { width: auto; }
        .field .req { color: var(--_danger); }
        .field .desc { font-size: 11px; color: var(--_muted); margin-top: 2px; }
        input[type="text"], input[type="number"], input[type="date"], input[type="datetime-local"], input[type="time"],
        input[type="email"], input[type="url"], select, textarea {
          width: 100%; font: inherit; padding: 7px 8px; border: 1px solid var(--_border);
          border-radius: var(--_radius); background-color: var(--_bg); color: var(--_text);
        }
        select { padding-right: 30px; }
        textarea { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 12px; resize: vertical; }
        fieldset { border: 1px solid var(--_border); border-radius: var(--_radius); padding: 10px; margin: 0; display: grid; gap: 10px; }
        legend { font-size: 12px; font-weight: 600; color: var(--_muted); padding: 0 4px; }
        .array-items { display: grid; gap: 6px; }
        .array-row { display: grid; grid-template-columns: 1fr auto; gap: 6px; align-items: start; }
        .array-row .rm { padding: 4px 8px; }
        /* Collapsible item: a one-line summary (compressed) that expands into the edit form. */
        .item { border: 1px solid var(--_border); border-radius: var(--_radius); overflow: hidden; }
        .item-head { display: grid; grid-template-columns: 1fr auto auto; gap: 6px; align-items: center; padding: 6px 8px; background-color: var(--_surface); }
        .item-summary { font-size: 13px; color: var(--_text); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; min-width: 0; }
        .item-summary.placeholder { color: var(--_muted); font-style: italic; }
        .item-head button { padding: 3px 9px; font-size: 12px; }
        .item-body { padding: 10px; border-top: 1px solid var(--_border); display: grid; gap: 10px; }
        .map-row { display: grid; grid-template-columns: minmax(72px, 1fr) 2fr auto; gap: 6px; align-items: start; }
        .map-row .rm, .array-row .rm { padding: 4px 8px; }
        .union { display: grid; gap: 8px; }
        .union-slot:empty { display: none; }
        .union-slot { display: grid; gap: 10px; padding-left: 10px; border-left: 2px solid var(--_border); }
        .empty { color: var(--_muted); font-size: 12px; }
        .err { color: var(--_danger); font-size: 11px; margin-top: 3px; }
        .array-row .err, .map-row .err, .item .err { grid-column: 1 / -1; }
        .item .err { padding: 0 8px 8px; }
        .validation-summary {
          border: 1px solid var(--_danger); border-radius: var(--_radius);
          background: color-mix(in srgb, var(--_danger) 8%, transparent);
          color: var(--_text); padding: 8px 10px; margin-bottom: 10px; font-size: 12px;
        }
        .validation-summary ul { margin: 4px 0 0; padding-left: 18px; }
      </style>
      <div class="validation-summary" part="validation-summary" hidden></div>
      <div class="root"></div>
    `;
    // Validate (debounced) whenever a control's value changes anywhere in the form.
    this.shadowRoot.addEventListener('input', () => this._scheduleValidation());
    this.shadowRoot.addEventListener('change', () => this._scheduleValidation());
  }

  renderForm() {
    const root = this.$('.root');
    if (!root) return;
    root.replaceChildren();
    this._read = null;
    this._errorTargets = [];
    this._clearErrors();

    const descriptor = this._descriptor;
    const noFields = !descriptor || typeof descriptor !== 'object'
      || (descriptor.type === 'object'
        && (!descriptor.properties || Object.keys(descriptor.properties).length === 0)
        && !descriptor.additionalProperties);

    if (noFields) {
      // No typed schema → a raw-JSON object editor so the field still works.
      const hint = document.createElement('div');
      hint.className = 'empty';
      hint.textContent = 'No typed schema available — enter a JSON object.';
      const built = unknownField({ type: 'unknown' }, { name: null, required: false, path: rootPath, editor: this });
      root.append(hint, built.node);
      this._read = built.read;
      return;
    }

    const built = buildField(descriptor, { name: null, required: false, path: rootPath, editor: this });
    root.appendChild(built.node);
    this._read = built.read;
  }

  /** Register an error slot: `pathFn` returns the JSON Pointer this field currently maps to (or null). */
  _registerError(pathFn, el) {
    this._errorTargets.push({ pathFn, el });
  }

  /** @private */ _scheduleValidation() {
    if (!this._validator) return;
    if (this._validateTimer) clearTimeout(this._validateTimer);
    this._validateTimer = setTimeout(() => this._runValidation(), VALIDATE_DEBOUNCE_MS);
  }

  /**
   * Validate the current value and surface the results inline. Returns the result (or null when there's no
   * validator or the value can't be assembled). Safe to call directly (e.g. from a host before submit).
   */
  async validate() {
    if (!this._validator) return null;
    let value;
    try {
      value = this.value;
    } catch (err) {
      this._clearFieldErrors();
      this._setSummary([err.message]);
      return { valid: false, errors: [{ message: err.message }] };
    }
    const seq = ++this._validateSeq;
    let result;
    try {
      result = await this._validator(value);
    } catch {
      this._clearErrors(); // validation unavailable → don't show stale/false errors
      return null;
    }
    if (seq !== this._validateSeq) return result; // superseded by a newer run
    this._applyValidation(result?.errors || []);
    return result;
  }

  /** @private */ _runValidation() { void this.validate(); }

  /** @private */ _applyValidation(errors) {
    // Drop slots whose field was removed (a deleted row, or a switched-away union variant).
    this._errorTargets = this._errorTargets.filter((t) => t.el.isConnected);
    const byPath = new Map();
    for (const e of errors) {
      const p = normalizePath(e.instancePath);
      if (!byPath.has(p)) byPath.set(p, []);
      byPath.get(p).push(e.message);
    }

    const placed = new Set();
    for (const target of this._errorTargets) {
      const path = target.pathFn();
      const msgs = path != null ? byPath.get(normalizePath(path)) : null;
      if (msgs && msgs.length) {
        target.el.textContent = msgs.join('; ');
        target.el.hidden = false;
        placed.add(normalizePath(path));
      } else {
        target.el.textContent = '';
        target.el.hidden = true;
      }
    }

    const summary = [];
    for (const [path, msgs] of byPath) {
      if (!placed.has(path)) {
        for (const m of msgs) summary.push(`${path || '(root)'}: ${m}`);
      }
    }
    this._setSummary(summary);
  }

  /** @private */ _clearFieldErrors() {
    for (const target of this._errorTargets) { target.el.textContent = ''; target.el.hidden = true; }
  }

  /** @private */ _clearErrors() {
    this._clearFieldErrors();
    this._setSummary([]);
  }

  /** @private */ _setSummary(messages) {
    const el = this.$('.validation-summary');
    if (!el) return;
    if (!messages.length) { el.hidden = true; el.replaceChildren(); return; }
    el.hidden = false;
    el.replaceChildren();
    const head = document.createElement('strong');
    head.textContent = messages.length === 1 ? '1 problem' : `${messages.length} problems`;
    const list = document.createElement('ul');
    for (const m of messages) {
      const li = document.createElement('li');
      li.textContent = m;
      list.appendChild(li);
    }
    el.append(head, list);
  }
}

/** The root field's path function — the empty JSON Pointer. */
function rootPath() { return ''; }

/** Escape a JSON Pointer reference token (RFC 6901). */
function escapePointer(token) { return String(token).replaceAll('~', '~0').replaceAll('/', '~1'); }

/** Normalise a JSON Pointer for comparison (treat the root as ""). */
function normalizePath(path) { return (path === '/' || path == null) ? '' : path; }

/** Append an error slot to a field container and register it under `pathFn` with the editor. */
function attachError(container, ctx, pathFn) {
  if (!ctx.editor) return;
  const el = document.createElement('div');
  el.className = 'err';
  el.hidden = true;
  container.appendChild(el);
  ctx.editor._registerError(pathFn, el);
}

/**
 * Build one field for a descriptor. Returns its DOM node and a `read()` that returns the field's value (or
 * `undefined` when blank/optional), throwing a friendly Error on invalid input.
 * @param {object} d The TypeDescriptor.
 * @param {{ name: (string|null), required: boolean }} ctx
 * @returns {{ node: HTMLElement, read: () => any }}
 */
function buildField(d, ctx) {
  const type = d?.type;

  if (d && d.const !== undefined) {
    return constField(d, ctx);
  }

  if (type === 'union' || Array.isArray(d?.variants)) {
    return unionField(d, ctx);
  }

  if (Array.isArray(d?.enum) && d.enum.length) {
    return enumField(d, ctx);
  }

  switch (type) {
    case 'object': return objectField(d, ctx);
    case 'array': return Array.isArray(d?.prefixItems) ? tupleField(d, ctx) : arrayField(d, ctx);
    case 'boolean': return booleanField(d, ctx);
    case 'integer':
    case 'number': return numberField(d, ctx);
    case 'string': return stringField(d, ctx);
    default: return unknownField(d, ctx);
  }
}

function labelText(d, ctx) {
  return ctx.name == null ? '' : `${escapeHtml(ctx.name)}${ctx.required ? ' <span class="req">*</span>' : ''}`;
}

function descHtml(d) {
  return d?.description ? `<div class="desc">${escapeHtml(d.description)}</div>` : '';
}

function objectField(d, ctx) {
  const props = (d.properties && typeof d.properties === 'object') ? d.properties : {};
  const required = new Set(Array.isArray(d.required) ? d.required : []);
  const wrap = document.createElement(ctx.name == null ? 'div' : 'fieldset');
  if (ctx.name != null) {
    const legend = document.createElement('legend');
    legend.innerHTML = labelText(d, ctx);
    wrap.appendChild(legend);
  }
  wrap.classList.add('fields');

  const names = Object.keys(props);
  const mapValue = (d.additionalProperties && typeof d.additionalProperties === 'object') ? d.additionalProperties : null;
  if (names.length === 0 && !mapValue) {
    const empty = document.createElement('div');
    empty.className = 'empty';
    empty.textContent = 'No fields.';
    wrap.appendChild(empty);
    return { node: wrap, read: () => undefined };
  }

  const readers = [];
  for (const name of names) {
    const childPath = () => { const p = ctx.path(); return p == null ? null : `${p}/${escapePointer(name)}`; };
    const childCtx = { name, required: required.has(name), path: childPath, editor: ctx.editor };
    const field = document.createElement('div');
    field.className = 'field';
    const built = buildField(props[name], childCtx);
    if (props[name]?.type === 'boolean') {
      // A checkbox reads best inline with its label, not stacked beneath it.
      const label = document.createElement('label');
      label.className = 'check';
      const text = document.createElement('span');
      text.innerHTML = labelText(props[name], childCtx);
      label.append(built.node, text);
      field.appendChild(label);
    } else {
      // Scalars/enums get a label here; nested objects (incl. maps) render their own legend.
      if (props[name]?.type !== 'object') {
        const label = document.createElement('label');
        label.innerHTML = labelText(props[name], childCtx);
        field.appendChild(label);
      }
      field.appendChild(built.node);
    }
    field.insertAdjacentHTML('beforeend', descHtml(props[name]));
    attachError(field, ctx, childPath);
    wrap.appendChild(field);
    readers.push([name, built.read, childCtx.required]);
  }

  // A free-form map (additionalProperties): arbitrary user-named keys whose values follow one schema.
  let readMap = () => ({});
  if (mapValue) {
    const map = mapField(mapValue, ctx);
    wrap.appendChild(map.node);
    readMap = map.read;
  }

  const read = () => {
    const out = {};
    for (const [name, r, req] of readers) {
      const v = r();
      if (v !== undefined) out[name] = v;
      else if (req) throw new Error(`"${name}" is required.`);
    }
    const entries = readMap();
    for (const k of Object.keys(entries)) out[k] = entries[k];
    return Object.keys(out).length || ctx.name != null ? out : undefined;
  };
  return { node: wrap, read };
}

/** A map editor for an object's `additionalProperties`: rows of a user-typed key plus a typed value control. */
function mapField(valueDesc, ctx) {
  const wrap = document.createElement('div');
  wrap.className = 'map';
  const head = document.createElement('div');
  head.className = 'desc';
  head.textContent = 'Additional entries (key → value)';
  const items = document.createElement('div');
  items.className = 'array-items';
  const add = document.createElement('button');
  add.type = 'button';
  add.className = 'ghost';
  add.textContent = '+ Add entry';
  const rows = [];

  const addRow = () => {
    const row = document.createElement('div');
    row.className = 'map-row';
    const key = document.createElement('input');
    key.type = 'text';
    key.placeholder = 'key';
    key.className = 'map-key';
    // The value's path follows the live key (a map entry is `<parent>/<key>`).
    const valuePath = () => { const p = ctx.path(); const k = key.value.trim(); return (p == null || !k) ? null : `${p}/${escapePointer(k)}`; };
    const built = buildField(valueDesc, { name: null, required: false, path: valuePath, editor: ctx.editor });
    const rm = removeButton();
    const entry = { key, read: built.read, row };
    rm.addEventListener('click', () => { row.remove(); const i = rows.indexOf(entry); if (i >= 0) rows.splice(i, 1); ctx.editor?._scheduleValidation(); });
    row.append(key, built.node, rm);
    attachError(row, ctx, valuePath);
    items.appendChild(row);
    rows.push(entry);
  };

  add.addEventListener('click', addRow);
  wrap.append(head, items, add);
  return {
    node: wrap,
    read: () => {
      const out = {};
      for (const e of rows) {
        const k = e.key.value.trim();
        if (!k) continue;
        const v = e.read();
        if (v !== undefined) out[k] = v;
      }
      return out;
    },
  };
}

/** A polymorphic union: a type chooser whose selection reveals the chosen variant's form. */
function unionField(d, ctx) {
  const variants = Array.isArray(d.variants) ? d.variants : [];
  const wrap = document.createElement('div');
  wrap.className = 'union';
  const select = document.createElement('select');
  select.innerHTML = `<option value="">${ctx.required ? '— choose type —' : '— none —'}</option>`
    + variants.map((v, i) => `<option value="${i}">${escapeHtml(variantLabel(v, i, d.discriminator))}</option>`).join('');
  const slot = document.createElement('div');
  slot.className = 'union-slot';
  let current = null;

  const rebuild = () => {
    slot.replaceChildren();
    current = null;
    if (select.value === '') return;
    const variant = variants[Number(select.value)];
    // A union adds no path segment — the chosen variant's value lives at the union's own path.
    current = buildField(variant, { name: null, required: true, path: ctx.path, editor: ctx.editor });
    slot.appendChild(current.node);
  };
  select.addEventListener('change', rebuild);
  wrap.append(select, slot);
  return {
    node: wrap,
    read: () => {
      if (!current) {
        if (ctx.required) throw new Error(`"${ctx.name ?? 'value'}" requires a choice.`);
        return undefined;
      }
      return current.read();
    },
  };
}

/** A label for a union variant: its title, else the discriminator's const value, else its type. */
function variantLabel(v, i, discriminator) {
  if (v?.title) return v.title;
  const disc = discriminator && v?.properties?.[discriminator];
  if (disc && disc.const != null) return String(disc.const);
  if (Array.isArray(disc?.enum) && disc.enum.length === 1) return String(disc.enum[0]);
  if (v?.type && v.type !== 'unknown') return v.type;
  return `Option ${i + 1}`;
}

/** A tuple: a fixed positional slot per `prefixItems` entry, plus optional trailing items when `items` is set. */
function tupleField(d, ctx) {
  const prefix = Array.isArray(d.prefixItems) ? d.prefixItems : [];
  const wrap = document.createElement('div');
  wrap.className = 'tuple fields';
  const slotReaders = [];

  prefix.forEach((pd, i) => {
    const field = document.createElement('div');
    field.className = 'field';
    const slotPath = () => { const p = ctx.path(); return p == null ? null : `${p}/${i}`; };
    const childCtx = { name: pd?.title || `#${i + 1}`, required: false, path: slotPath, editor: ctx.editor };
    const built = buildField(pd, childCtx);
    if (pd?.type !== 'object') {
      const label = document.createElement('label');
      label.textContent = pd?.title || `Item ${i + 1}`;
      field.appendChild(label);
    }
    field.appendChild(built.node);
    field.insertAdjacentHTML('beforeend', descHtml(pd));
    attachError(field, ctx, slotPath);
    wrap.appendChild(field);
    slotReaders.push(built.read);
  });

  let readExtra = () => undefined;
  if (d.items && typeof d.items === 'object') {
    const extra = arrayField({ type: 'array', items: d.items }, { name: null, required: false, path: ctx.path, editor: ctx.editor });
    const label = document.createElement('div');
    label.className = 'desc';
    label.textContent = 'Additional items';
    wrap.append(label, extra.node);
    readExtra = extra.read;
  }

  return {
    node: wrap,
    read: () => {
      const head = slotReaders.map((r) => r()); // propagates per-slot validation errors
      const tail = readExtra() || [];
      const anyHead = head.some((v) => v !== undefined);
      if (!anyHead && tail.length === 0) return ctx.required ? [] : undefined;
      // Preserve tuple positions: blank fixed slots become null rather than shifting later items.
      return [...head.map((v) => (v === undefined ? null : v)), ...tail];
    },
  };
}

/** A fixed `const` value: shown read-only and always contributed (e.g. a oneOf variant's discriminator). */
function constField(d, ctx) {
  void ctx;
  const input = document.createElement('input');
  input.type = 'text';
  input.value = (d.const !== null && typeof d.const === 'object') ? JSON.stringify(d.const) : String(d.const);
  input.readOnly = true;
  input.setAttribute('aria-readonly', 'true');
  return { node: input, read: () => d.const };
}

function enumField(d, ctx) {
  const select = document.createElement('select');
  select.innerHTML = `<option value="">${ctx.required ? '— choose —' : '— none —'}</option>`
    + d.enum.map((v, i) => `<option value="${i}">${escapeHtml(String(v))}</option>`).join('');
  return {
    node: select,
    read: () => (select.value === '' ? undefined : d.enum[Number(select.value)]),
  };
}

function booleanField(d, ctx) {
  void ctx;
  const input = document.createElement('input');
  input.type = 'checkbox';
  return { node: input, read: () => input.checked };
}

function numberField(d, ctx) {
  const input = document.createElement('input');
  input.type = 'number';
  if (d.type === 'integer') input.step = String(d.multipleOf ?? 1);
  else if (d.multipleOf != null) input.step = String(d.multipleOf);
  else input.step = 'any';
  if (d.minimum != null) input.min = String(d.minimum);
  if (d.maximum != null) input.max = String(d.maximum);
  return {
    node: input,
    read: () => {
      const raw = input.value.trim();
      if (raw === '') { if (ctx.required) throw new Error(`"${ctx.name}" is required.`); return undefined; }
      const n = Number(raw);
      if (Number.isNaN(n)) throw new Error(`"${ctx.name}" must be a number.`);
      return n;
    },
  };
}

const STRING_FORMAT_INPUT = {
  date: 'date', 'date-time': 'datetime-local', time: 'time', email: 'email', 'idn-email': 'email',
  uri: 'url', 'uri-reference': 'url', iri: 'url', url: 'url',
};

function stringField(d, ctx) {
  const input = document.createElement('input');
  input.type = STRING_FORMAT_INPUT[d.format] || 'text';
  if (d.maxLength != null) input.maxLength = Number(d.maxLength);
  if (d.pattern) input.pattern = d.pattern;
  if (d.format && !STRING_FORMAT_INPUT[d.format]) input.placeholder = d.format;
  return {
    node: input,
    read: () => {
      let raw = input.value;
      // datetime-local lacks a timezone; normalise to an ISO instant for date-time.
      if (d.format === 'date-time' && raw) raw = new Date(raw).toISOString();
      if (raw === '') { if (ctx.required) throw new Error(`"${ctx.name}" is required.`); return undefined; }
      return raw;
    },
  };
}

function arrayField(d, ctx) {
  const itemDesc = d.items || { type: 'unknown' };
  // Object/array items are bulky, so each gets a collapsed summary row that expands to edit; scalar
  // items stay as a single compact inline control with a remove button.
  const collapsible = itemDesc.type === 'object' || itemDesc.type === 'array';
  const wrap = document.createElement('div');
  const items = document.createElement('div');
  items.className = 'array-items';
  const add = document.createElement('button');
  add.type = 'button';
  add.className = 'ghost';
  add.textContent = '+ Add item';
  const rowReaders = [];
  const removeEntry = (entry) => { const i = rowReaders.indexOf(entry); if (i >= 0) rowReaders.splice(i, 1); ctx.editor?._scheduleValidation(); };
  // A row's path is its index within the *built* array (blank/undefined rows are excluded, as in read()).
  const rowPath = (entry) => () => {
    const p = ctx.path();
    if (p == null) return null;
    let idx = 0;
    for (const e of rowReaders) {
      let v;
      try { v = e.read(); } catch { v = undefined; }
      if (v === undefined) { if (e === entry) return null; continue; }
      if (e === entry) return `${p}/${idx}`;
      idx++;
    }
    return null;
  };

  const addInlineRow = () => {
    const row = document.createElement('div');
    row.className = 'array-row';
    const entry = { read: null, row };
    const built = buildField(itemDesc, { name: null, required: false, path: rowPath(entry), editor: ctx.editor });
    entry.read = built.read;
    const rm = removeButton();
    rm.addEventListener('click', () => { row.remove(); removeEntry(entry); });
    row.append(built.node, rm);
    attachError(row, ctx, rowPath(entry));
    items.appendChild(row);
    rowReaders.push(entry);
  };

  const addCollapsibleRow = (startEditing) => {
    const row = document.createElement('div');
    row.className = 'item';
    const head = document.createElement('div');
    head.className = 'item-head';
    const summary = document.createElement('span');
    summary.className = 'item-summary';
    const edit = document.createElement('button');
    edit.type = 'button';
    edit.className = 'ghost edit';
    const rm = removeButton();
    const body = document.createElement('div');
    body.className = 'item-body';
    const entry = { read: null, row };
    const built = buildField(itemDesc, { name: null, required: false, path: rowPath(entry), editor: ctx.editor });
    entry.read = built.read;
    body.appendChild(built.node);
    head.append(summary, edit, rm);
    row.append(head, body);
    attachError(row, ctx, rowPath(entry));

    let editing = false;
    const setEditing = (v) => {
      editing = v;
      body.hidden = !v;
      edit.textContent = v ? 'Done' : 'Edit';
      edit.setAttribute('aria-expanded', String(v));
      if (!v) applySummary(summary, built.read);
    };
    edit.addEventListener('click', () => setEditing(!editing));
    rm.addEventListener('click', () => { row.remove(); removeEntry(entry); });
    items.appendChild(row);
    rowReaders.push(entry);
    setEditing(startEditing);
  };

  const addRow = collapsible ? addCollapsibleRow : addInlineRow;
  add.addEventListener('click', () => addRow(true));
  wrap.append(items, add);
  return {
    node: wrap,
    read: () => {
      const arr = rowReaders.map((e) => e.read()).filter((v) => v !== undefined);
      return arr.length ? arr : (ctx.required ? [] : undefined);
    },
  };
}

function removeButton() {
  const rm = document.createElement('button');
  rm.type = 'button';
  rm.className = 'rm ghost danger';
  rm.textContent = '✕';
  rm.setAttribute('aria-label', 'Remove item');
  return rm;
}

/** Set a collapsed item's one-line summary from its current (soft-read) value. */
function applySummary(node, read) {
  let value;
  let ok = true;
  try { value = read(); } catch { ok = false; }
  const text = !ok ? 'Incomplete — click Edit' : summarize(value);
  node.textContent = text;
  node.classList.toggle('placeholder', !ok || value === undefined);
}

/** A compact human summary of an assembled item value for the collapsed row. */
function summarize(value) {
  if (value === undefined || value === null) return 'Empty item';
  if (Array.isArray(value)) return `${value.length} item${value.length === 1 ? '' : 's'}`;
  if (typeof value === 'object') {
    const entries = Object.entries(value);
    if (entries.length === 0) return 'Empty item';
    const shown = entries.slice(0, 3).map(([k, v]) => `${k}: ${scalarStr(v)}`);
    const extra = entries.length - shown.length;
    return shown.join(', ') + (extra > 0 ? `, +${extra} more` : '');
  }
  return scalarStr(value);
}

function scalarStr(v) {
  if (v === undefined || v === null) return '∅';
  if (Array.isArray(v)) return `[${v.length}]`;
  if (typeof v === 'object') return '{…}';
  const s = String(v);
  return s.length > 32 ? `${s.slice(0, 31)}…` : s;
}

function unknownField(d, ctx) {
  void d;
  const textarea = document.createElement('textarea');
  textarea.rows = 3;
  textarea.placeholder = 'JSON value';
  return {
    node: textarea,
    read: () => {
      const raw = textarea.value.trim();
      if (raw === '') { if (ctx.required) throw new Error(`"${ctx.name}" is required.`); return undefined; }
      try {
        return JSON.parse(raw);
      } catch {
        throw new Error(`"${ctx.name ?? 'value'}" is not valid JSON.`);
      }
    },
  };
}

define('arazzo-value-editor', ArazzoValueEditor);
export { ArazzoValueEditor };
