// <arazzo-patch-builder> — a strongly-typed form built from a precomputed TypeDescriptor.
//
//   const b = document.querySelector('arazzo-patch-builder');
//   b.descriptor = { type: 'object', properties: { amount: { type:'number' }, status:{ type:'string', enum:[...] } } };
//   const value = b.value;   // the assembled object (throws a friendly Error on invalid input)
//
// Properties : .descriptor (a TypeDescriptor — usually an object whose `properties` are the fields), .value
// Events     : (none; the host reads .value on submit)
//
// It renders a control suited to each field's recognised type/format/enum/constraints (date/datetime/email/uri
// inputs, number inputs with min/max/step, enum dropdowns, checkboxes, nested objects, add/remove arrays), and
// falls back to a raw-JSON textarea for anything it can't type. Used for Skip `skipOutputs` and Rewind output
// overrides; a `patch` mode (RFC 6902) builds on the same form.

import { ArazzoElement, SHARED_CSS, escapeHtml, define } from './base.js';

class ArazzoPatchBuilder extends ArazzoElement {
  constructor() {
    super();
    /** @private */ this._descriptor = null;
    /** @private */ this._read = null; // root value reader installed by render()
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
        .field .req { color: var(--_danger); }
        .field .desc { font-size: 11px; color: var(--_muted); margin-top: 2px; }
        input[type="text"], input[type="number"], input[type="date"], input[type="datetime-local"], input[type="time"],
        input[type="email"], input[type="url"], select, textarea {
          width: 100%; font: inherit; padding: 7px 8px; border: 1px solid var(--_border);
          border-radius: var(--_radius); background: var(--_bg); color: var(--_text);
        }
        textarea { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 12px; resize: vertical; }
        fieldset { border: 1px solid var(--_border); border-radius: var(--_radius); padding: 10px; margin: 0; display: grid; gap: 10px; }
        legend { font-size: 12px; font-weight: 600; color: var(--_muted); padding: 0 4px; }
        .array-items { display: grid; gap: 6px; }
        .array-row { display: grid; grid-template-columns: 1fr auto; gap: 6px; align-items: start; }
        .array-row .rm { padding: 4px 8px; }
        .empty { color: var(--_muted); font-size: 12px; }
      </style>
      <div class="root"></div>
    `;
  }

  renderForm() {
    const root = this.$('.root');
    if (!root) return;
    root.replaceChildren();
    this._read = null;

    const descriptor = this._descriptor;
    const noFields = !descriptor || typeof descriptor !== 'object'
      || (descriptor.type === 'object' && (!descriptor.properties || Object.keys(descriptor.properties).length === 0));

    if (noFields) {
      // No typed schema → a raw-JSON object editor so the field still works.
      const hint = document.createElement('div');
      hint.className = 'empty';
      hint.textContent = 'No typed schema available — enter a JSON object.';
      const built = unknownField({ type: 'unknown' }, { name: null, required: false });
      root.append(hint, built.node);
      this._read = built.read;
      return;
    }

    const built = buildField(descriptor, { name: null, required: false });
    root.appendChild(built.node);
    this._read = built.read;
  }
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

  if (Array.isArray(d?.enum) && d.enum.length) {
    return enumField(d, ctx);
  }

  switch (type) {
    case 'object': return objectField(d, ctx);
    case 'array': return arrayField(d, ctx);
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
  if (names.length === 0) {
    const empty = document.createElement('div');
    empty.className = 'empty';
    empty.textContent = 'No fields.';
    wrap.appendChild(empty);
    return { node: wrap, read: () => undefined };
  }

  const readers = [];
  for (const name of names) {
    const childCtx = { name, required: required.has(name) };
    const field = document.createElement('div');
    field.className = 'field';
    const built = buildField(props[name], childCtx);
    // Scalars/enums get a label here; nested objects/arrays render their own legend.
    if (props[name]?.type !== 'object') {
      const label = document.createElement('label');
      label.innerHTML = labelText(props[name], childCtx);
      field.appendChild(label);
    }
    field.appendChild(built.node);
    field.insertAdjacentHTML('beforeend', descHtml(props[name]));
    wrap.appendChild(field);
    readers.push([name, built.read, childCtx.required]);
  }

  const read = () => {
    const out = {};
    for (const [name, r, req] of readers) {
      const v = r();
      if (v !== undefined) out[name] = v;
      else if (req) throw new Error(`"${name}" is required.`);
    }
    return Object.keys(out).length || ctx.name != null ? out : undefined;
  };
  return { node: wrap, read };
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
  const wrap = document.createElement('div');
  const items = document.createElement('div');
  items.className = 'array-items';
  const add = document.createElement('button');
  add.type = 'button';
  add.className = 'ghost';
  add.textContent = '+ Add item';
  const rowReaders = [];

  const addRow = () => {
    const row = document.createElement('div');
    row.className = 'array-row';
    const built = buildField(d.items || { type: 'unknown' }, { name: null, required: false });
    const rm = document.createElement('button');
    rm.type = 'button';
    rm.className = 'rm ghost danger';
    rm.textContent = '✕';
    const entry = { read: built.read, row };
    rm.addEventListener('click', () => { row.remove(); const i = rowReaders.indexOf(entry); if (i >= 0) rowReaders.splice(i, 1); });
    row.append(built.node, rm);
    items.appendChild(row);
    rowReaders.push(entry);
  };

  add.addEventListener('click', addRow);
  wrap.append(items, add);
  return {
    node: wrap,
    read: () => {
      const arr = rowReaders.map((e) => e.read()).filter((v) => v !== undefined);
      return arr.length ? arr : (ctx.required ? [] : undefined);
    },
  };
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

define('arazzo-patch-builder', ArazzoPatchBuilder);
export { ArazzoPatchBuilder };
