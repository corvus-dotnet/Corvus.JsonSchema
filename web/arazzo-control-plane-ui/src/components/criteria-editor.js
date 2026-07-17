// <arazzo-criteria-editor> — an ordered list of Arazzo criterion rows (design §5.3): per row a
// type picker (simple | regex | jsonpath | xpath, with version for the expression types), an
// optional `context` runtime expression, and the condition itself — both edited through
// <arazzo-expression-input> (highlighting, schema completions).
//
//   const ed = document.createElement('arazzo-criteria-editor');
//   ed.completionContext = { … };                    // forwarded to the expression inputs
//   ed.value = step.successCriteria;                 // array of criterion objects
//   ed.addEventListener('criteria-changed', (e) => { step.successCriteria = e.detail.criteria; });
//
// Emits `criteria-changed {criteria}` on every edit. External `.value` sets rebuild the rows;
// internal edits mutate the model in place (no rebuild → focus is preserved while typing).

import { ArazzoElement, SHARED_CSS, escapeHtml, define } from './base.js';
import './expression-input.js';

// The types the runtime evaluates. xpath is schema-valid Arazzo and round-trips untouched, but the
// runtime does not evaluate it — so it is never OFFERED for new criteria (scope honesty), only
// preserved and flagged when a document already carries it.
const TYPES = ['simple', 'regex', 'jsonpath'];
const UNSUPPORTED = new Set(['xpath']);
const VERSIONS = {
  jsonpath: ['draft-goessner-dispatch-jsonpath-00'],
  xpath: ['xpath-30', 'xpath-20', 'xpath-10'],
};
const PLACEHOLDERS = {
  simple: '$statusCode == 200',
  regex: '^created$',
  jsonpath: '$[?@.status == "authorized"]',
  xpath: '//status[text()="ok"]',
};

/** A version picker is only worth pixels when there is a real choice (xpath); jsonpath has
 *  exactly one spec-defined version, which the plain string `type` already implies. */
const hasVersionChoice = (type) => (VERSIONS[type]?.length ?? 0) > 1;

class ArazzoCriteriaEditor extends ArazzoElement {
  constructor() {
    super();
    /** @private */ this._criteria = [];
    /** @private */ this._completionContext = {};
  }

  connectedCallback() {
    if (!this._built) this.renderShell();
    this.renderRows();
  }

  /** The criterion list (Arazzo criterion objects). Setting rebuilds the rows. */
  get value() { return structuredClone(this._criteria); }
  set value(list) {
    this._criteria = structuredClone(list || []);
    if (this.isConnected) { if (!this._built) this.renderShell(); this.renderRows(); }
  }

  /** Schema context forwarded to every expression input. */
  get completionContext() { return this._completionContext; }
  set completionContext(ctx) {
    this._completionContext = ctx || {};
    for (const x of this.$$('arazzo-expression-input')) x.completionContext = this._completionContext;
  }

  /** @private */
  renderShell() {
    this._built = true;
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        :host { display: block; }
        .rows { display: grid; gap: 10px; min-width: 0; }
        .row, .row > div { min-width: 0; }
        .row { border: 1px solid var(--_border); border-radius: var(--_radius); padding: 8px; display: grid; gap: 6px; background: var(--_surface); }
        .head { display: flex; gap: 6px; align-items: center; }
        .head select { font-size: 12px; padding: 4px 26px 4px 8px; }
        .head .grow { flex: 1; }
        .vwrap { display: inline-flex; gap: 5px; align-items: center; font-size: 11px; color: var(--_muted); min-width: 0; }
        .vwrap select { max-width: 130px; }
        .unsupported { font-size: 11px; color: var(--arazzo-status-suspended, #b07d18); }
        label { font-size: 11px; color: var(--_muted); display: block; margin-bottom: 2px; }
        .add { margin-top: 8px; font-size: 12px; }
        .empty { padding: 10px; font-size: 12px; text-align: left; }
      </style>
      <div class="rows" part="rows"></div>
      <button class="add ghost" type="button">+ Add criterion</button>
    `;
    this.$('.add').addEventListener('click', () => {
      this._criteria.push({ condition: '' });
      this.renderRows();
      this._emit();
      this.$$('.row arazzo-expression-input.cond').at(-1)?.focus();
    });
  }

  /** @private */
  renderRows() {
    const rows = this.$('.rows');
    rows.replaceChildren();
    if (!this._criteria.length) {
      rows.innerHTML = '<div class="empty muted">No criteria — fires unconditionally (<i>always</i>).</div>';
      return;
    }
    this._criteria.forEach((criterion, i) => rows.append(this._buildRow(criterion, i)));
  }

  /** @private */
  _buildRow(criterion, i) {
    const { type, version } = readType(criterion);
    const row = document.createElement('div');
    row.className = 'row';
    row.innerHTML = `
      <div class="head">
        <select class="type" title="Criterion type">
          ${[...TYPES, ...(UNSUPPORTED.has(type) ? [type] : [])]
            .map((t) => `<option value="${t}" ${t === type ? 'selected' : ''}>${t}${UNSUPPORTED.has(t) ? ' (unsupported)' : ''}</option>`).join('')}
        </select>
        <span class="vwrap" ${hasVersionChoice(type) ? '' : 'hidden'}>version
          <select class="version" aria-label="Expression-language version">
            ${(VERSIONS[type] || []).map((v) => `<option ${v === version ? 'selected' : ''}>${v}</option>`).join('')}
          </select>
        </span>
        <span class="grow"></span>
        <button class="close" type="button" title="Remove criterion">✕</button>
      </div>
      ${UNSUPPORTED.has(type) ? '<div class="unsupported">⚠ the runtime does not evaluate xpath criteria yet — this criterion is preserved but will not run</div>' : ''}
      <div class="ctx" ${type === 'simple' ? 'hidden' : ''}>
        <label>context (runtime expression)</label>
      </div>
      <div>
        <label>condition</label>
      </div>
    `;

    const ctxInput = document.createElement('arazzo-expression-input');
    ctxInput.className = 'ctx-input';
    ctxInput.setAttribute('placeholder', '$response.body');
    ctxInput.value = criterion.context || '';
    ctxInput.completionContext = this._completionContext;
    row.querySelector('.ctx').append(ctxInput);

    const condInput = document.createElement('arazzo-expression-input');
    condInput.className = 'cond';
    condInput.setAttribute('placeholder', PLACEHOLDERS[type]);
    condInput.value = criterion.condition || '';
    condInput.completionContext = this._completionContext;
    row.children[2].append(condInput);

    row.querySelector('.type').addEventListener('change', (e) => {
      this._setType(i, e.target.value, row);
    });
    row.querySelector('.version').addEventListener('change', (e) => {
      const t = readType(this._criteria[i]).type;
      this._criteria[i].type = { type: t, version: e.target.value };
      this._emit();
    });
    row.querySelector('.close').addEventListener('click', () => {
      this._criteria.splice(i, 1);
      this.renderRows();
      this._emit();
    });
    ctxInput.addEventListener('value-changed', (e) => {
      e.stopPropagation();
      if (e.detail.value) this._criteria[i].context = e.detail.value;
      else delete this._criteria[i].context;
      this._emit();
    });
    condInput.addEventListener('value-changed', (e) => {
      e.stopPropagation();
      this._criteria[i].condition = e.detail.value;
      this._emit();
    });
    return row;
  }

  /** @private — switch a row's type: show/hide version + context, normalize the stored shape. */
  _setType(i, type, row) {
    const criterion = this._criteria[i];
    const previousType = readType(criterion).type;
    if (type === 'simple') {
      delete criterion.type;
      delete criterion.context;
      row.querySelector('.ctx-input').value = '';
    } else if (VERSIONS[type]) {
      criterion.type = type; // plain string; the version select upgrades it to {type, version}
    } else {
      criterion.type = type;
    }
    // A typed criterion requires a context (schema: `type` requires `context`).
    row.querySelector('.vwrap').hidden = !hasVersionChoice(type);
    row.querySelector('.version').innerHTML = (VERSIONS[type] || []).map((v) => `<option>${v}</option>`).join('');
    row.querySelector('.ctx').hidden = type === 'simple';
    const cond = row.querySelector('.cond');
    // A condition still equal to the OLD type's canonical example is a seeded default, not authored content —
    // clear it so the box shows the NEW type's valid example instead of teaching the old grammar. Anything the
    // author actually wrote is theirs and survives the switch untouched.
    if (criterion.condition === PLACEHOLDERS[previousType]) {
      criterion.condition = '';
      cond.value = '';
    }
    cond.setAttribute('placeholder', PLACEHOLDERS[type]);
    this._emit();
  }

  /** @private */
  _emit() {
    this.emit('criteria-changed', { criteria: this.value });
  }
}

/** Normalize the criterion `type` field (absent | string | {type, version}). */
function readType(criterion) {
  const t = criterion?.type;
  if (!t) return { type: 'simple' };
  if (typeof t === 'string') return { type: t, version: VERSIONS[t]?.[0] };
  return { type: t.type, version: t.version };
}

define('arazzo-criteria-editor', ArazzoCriteriaEditor);
export { ArazzoCriteriaEditor };
