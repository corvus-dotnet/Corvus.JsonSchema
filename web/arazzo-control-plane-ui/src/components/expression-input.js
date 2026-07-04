// <arazzo-expression-input> — a one-line runtime-expression / JSONPath editor with syntax
// highlighting, schema-driven completions, and a pluggable validator; the shared editing control
// for criteria conditions, context expressions, parameters, outputs, and scenario expectations
// (design: workflow-designer-design.md §7.3).
//
//   const x = document.createElement('arazzo-expression-input');
//   x.completionContext = { inputs: ['orderId'], steps: { 'authorize-payment': { outputs: ['authorizationId'] } } };
//   x.value = '$steps.authorize-payment.outputs.authorizationId';
//   x.validator = async (value) => ({ valid: …, errors: [{ message }] });
//
// Editor technology (§7.1): CodeMirror 6, mounted with `root: shadowRoot` — first-class shadow-DOM
// support. The kit stays zero-build at consumption time: CM6 ships as a single vendored ESM bundle
// (src/vendor/codemirror.mjs, rebuilt via `npm run build:vendor`) imported lazily only when an
// editor mounts. A host can substitute its own modules through the static `cmLoader` hook (e.g.
// import-map-managed packages). Until the modules arrive — or if they never do — the component IS
// a themed plain <input> with the same value/events, so it always works.
//
// Why vendored rather than CDN: CM6 requires one shared instance of each core package, and
// per-package CDN bundles pin their internal deps independently (observed drift on jsDelivr:
// autocomplete pinning state@6.6.0 vs view/language/commands at 6.7.0), which breaks CM6's
// instanceof-based extension checks. See vendor/codemirror-entry.js.
//
// Properties : .value, .completionContext ({inputs?, outputs?, steps?}), .validator
//              (async value => {valid, errors?: [{message}]}), .usingFallback (readonly)
// Attributes : value (initial), placeholder, readonly
// Events     : value-changed {value} (on edit), commit {value} (Enter),
//              validated {valid, errors} (after each validator run)

import { ArazzoElement, SHARED_CSS, define } from './base.js';
import { expressionStreamParser, completionsFor } from '../expression-language.js';

const VALIDATE_DEBOUNCE_MS = 350;

class ArazzoExpressionInput extends ArazzoElement {
  /** Host override: `async () => {state, view, language, autocomplete, commands, lezerHighlight}`. */
  static cmLoader = null;
  /** @private — the shared, once-only module load. */
  static _cm = null;

  /** Load (and cache) the CodeMirror modules; rejects propagate to the caller. */
  static loadCm() {
    if (!this._cm) {
      const load = this.cmLoader
        || (() => import(new URL('../vendor/codemirror.mjs', import.meta.url).href));
      this._cm = Promise.resolve().then(load);
      this._cm.catch(() => {}); // observed later per instance; avoid an unhandled rejection
    }
    return this._cm;
  }

  static get observedAttributes() { return ['value', 'placeholder', 'readonly']; }

  constructor() {
    super();
    /** @private */ this._view = null;          // the CM6 EditorView once upgraded
    /** @private */ this._completionContext = {};
    /** @private */ this._validator = null;
    /** @private */ this._validateTimer = null;
    /** @private */ this._validateSeq = 0;
  }

  connectedCallback() {
    if (!this._built) this.renderShell();
    this._upgrade();
  }

  disconnectedCallback() {
    clearTimeout(this._validateTimer);
  }

  attributeChangedCallback(name, _old, value) {
    if (!this._built) return;
    if (name === 'value' && !this._view) this.$('input').value = value ?? '';
    if (name === 'placeholder' && !this._view) this.$('input').placeholder = value ?? '';
    if (name === 'readonly') this._applyReadonly();
  }

  /** True while the plain-input fallback is serving (CM6 not loaded / failed / loading). */
  get usingFallback() { return !this._view; }

  get value() {
    if (this._view) return this._view.state.doc.toString();
    if (this._built) return this.$('input')?.value ?? '';
    return this._pending ?? (this.getAttribute('value') || '');
  }

  set value(text) {
    const v = String(text ?? '');
    if (this._view) {
      this._view.dispatch({ changes: { from: 0, to: this._view.state.doc.length, insert: v } });
    } else if (this._built) {
      this.$('input').value = v;
    } else {
      this._pending = v; // set before connection — applied when the shell renders
    }
    this._scheduleValidate();
  }

  /** The schema context for completions: {inputs?: [], outputs?: [], steps?: {id: {outputs, summary?}}}. */
  get completionContext() { return this._completionContext; }
  set completionContext(value) { this._completionContext = value || {}; }

  /** Optional `async (value) => {valid, errors?: [{message}]}`; runs debounced on edit. */
  get validator() { return this._validator; }
  set validator(fn) {
    this._validator = typeof fn === 'function' ? fn : null;
    if (this._validator) this._scheduleValidate();
    else this._setInvalid(null);
  }

  focus() { (this._view ?? this.$('input'))?.focus(); }

  /** @private */
  renderShell() {
    this._built = true;
    const initial = this._pending ?? this.getAttribute('value') ?? '';
    delete this._pending;
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        :host { display: block; }
        .xin { position: relative; min-width: 0; }
        :host { min-width: 0; }
        .cm-scroller { overflow-x: auto; }
        input, .cm-editor {
          width: 100%; box-sizing: border-box;
          font: 12.5px ui-monospace, SFMono-Regular, Menlo, monospace;
          border: 1px solid var(--_border); border-radius: var(--_radius);
          background-color: var(--_bg); color: var(--_text);
        }
        input { padding: 7px 9px; }
        input:focus-visible { outline: 2px solid var(--_accent); outline-offset: 1px; }
        .cm-editor { padding: 2px 4px; }
        .cm-editor.cm-focused { outline: 2px solid var(--_accent); outline-offset: 1px; }
        .xin.invalid input, .xin.invalid .cm-editor { border-color: var(--_danger); }
        .err { font-size: 11px; color: var(--_danger); margin-top: 3px; }
        [hidden] { display: none !important; }
      </style>
      <div class="xin" part="input">
        <input type="text" spellcheck="false" autocomplete="off"
               value="${escapeAttr(initial)}"
               placeholder="${escapeAttr(this.getAttribute('placeholder') || '')}">
        <div class="err" hidden part="error"></div>
      </div>
    `;
    const input = this.$('input');
    input.addEventListener('input', () => {
      this.emit('value-changed', { value: input.value });
      this._scheduleValidate();
    });
    input.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') this.emit('commit', { value: input.value });
    });
    this._applyReadonly();
  }

  /** @private — swap the fallback input for a CM6 editor once (and if) the modules arrive. */
  async _upgrade() {
    if (this._view || this._upgrading) return;
    this._upgrading = true;
    let cm;
    try {
      cm = await ArazzoExpressionInput.loadCm();
    } catch {
      this._upgrading = false;
      return; // the fallback input stays in service
    }
    if (!this.isConnected || this._view) { this._upgrading = false; return; }

    const { state, view, language, autocomplete, commands, lezerHighlight } = cm;
    const t = lezerHighlight.tags;
    const lang = language.StreamLanguage.define({
      ...expressionStreamParser,
      tokenTable: {
        exprRoot: t.keyword,
        exprBadRoot: t.invalid,
        exprKeyword: t.modifier,
        exprOperator: t.operator,
        exprString: t.string,
        exprNumber: t.number,
        exprAtom: t.bool,
        exprFunction: t.function(t.variableName),
        exprProperty: t.propertyName,
        exprVariable: t.variableName,
        exprPunct: t.punctuation,
        exprBracket: t.bracket,
        exprPointer: t.labelName,
      },
    });
    const highlight = language.syntaxHighlighting(language.HighlightStyle.define([
      { tag: t.keyword, color: 'var(--arazzo-accent, #3b6cf6)', fontWeight: '600' },
      { tag: t.modifier, color: 'var(--arazzo-accent, #3b6cf6)' },
      { tag: t.invalid, color: 'var(--arazzo-danger, #d4351c)', textDecoration: 'underline wavy' },
      { tag: t.string, color: 'var(--arazzo-status-completed, #2a8a4a)' },
      { tag: t.number, color: 'var(--arazzo-status-suspended, #b07d18)' },
      { tag: t.bool, color: 'var(--arazzo-status-suspended, #b07d18)' },
      { tag: t.propertyName, color: 'var(--arazzo-text, #1c2024)' },
      { tag: t.labelName, color: 'var(--arazzo-status-running, #2f74d0)' },
      { tag: t.operator, color: 'var(--arazzo-muted, #6b7280)' },
      { tag: t.punctuation, color: 'var(--arazzo-muted, #6b7280)' },
    ]));
    // Editor + tooltip chrome themed by the kit tokens — CM6's defaults assume a light page, so
    // without this the completion popup is white-on-white in dark mode and the caret vanishes.
    const chrome = view.EditorView.theme({
      '.cm-content': { caretColor: 'var(--arazzo-text, #1c2024)' },
      '.cm-cursor, .cm-dropCursor': { borderLeftColor: 'var(--arazzo-text, #1c2024)' },
      '.cm-selectionBackground, &.cm-focused > .cm-scroller .cm-selectionLayer .cm-selectionBackground': {
        background: 'color-mix(in srgb, var(--arazzo-accent, #3b6cf6) 28%, transparent)',
      },
      '.cm-placeholder': { color: 'var(--arazzo-muted, #6b7280)' },
      '.cm-tooltip': {
        background: 'var(--arazzo-bg, #fff)',
        color: 'var(--arazzo-text, #1c2024)',
        border: '1px solid var(--arazzo-border, #e3e6ea)',
        borderRadius: 'var(--arazzo-radius, 8px)',
        boxShadow: '0 6px 24px rgba(0, 0, 0, 0.18)',
        overflow: 'hidden',
      },
      '.cm-tooltip.cm-tooltip-autocomplete > ul': {
        font: '12px ui-monospace, SFMono-Regular, Menlo, monospace',
        maxHeight: '200px',
      },
      '.cm-tooltip.cm-tooltip-autocomplete > ul > li': { padding: '4px 8px 4px 4px' },
      '.cm-tooltip.cm-tooltip-autocomplete > ul > li[aria-selected]': {
        background: 'var(--arazzo-accent, #3b6cf6)',
        color: '#fff',
      },
      '.cm-tooltip.cm-tooltip-autocomplete > ul > li[aria-selected] .cm-completionDetail': {
        color: 'rgba(255, 255, 255, 0.85)',
      },
      '.cm-completionDetail': {
        color: 'var(--arazzo-muted, #6b7280)', fontStyle: 'italic', marginLeft: '0.7em',
      },
      '.cm-completionIcon': { color: 'var(--arazzo-muted, #6b7280)' },
    });
    const singleLine = state.EditorState.transactionFilter.of(
      (tr) => (tr.newDoc.lines > 1 ? [] : tr),
    );
    const completionSource = (ctx) => {
      const r = completionsFor(ctx.state.doc.toString(), ctx.pos, this._completionContext);
      if (!r) return null;
      return { from: r.from, options: r.options, validFor: /^[\w$.#/-]*$/ };
    };

    const input = this.$('input');
    const container = this.$('.xin');
    this._view = new view.EditorView({
      root: this.shadowRoot,
      parent: container,
      state: state.EditorState.create({
        doc: input.value,
        extensions: [
          lang,
          highlight,
          chrome,
          singleLine,
          autocomplete.autocompletion({ override: [completionSource], activateOnTyping: true }),
          commands.history(),
          view.keymap.of([
            // Completion first: with the popup open, Enter accepts the option; only then does
            // Enter fall through to commit.
            ...autocomplete.completionKeymap,
            { key: 'Enter', run: () => { this.emit('commit', { value: this.value }); return true; } },
            ...commands.defaultKeymap,
            ...commands.historyKeymap,
          ]),
          view.placeholder(this.getAttribute('placeholder') || ''),
          view.EditorView.updateListener.of((u) => {
            if (u.docChanged) {
              this.emit('value-changed', { value: this.value });
              this._scheduleValidate();
            }
          }),
          view.EditorView.editable.of(!this.hasAttribute('readonly')),
        ],
      }),
    });
    container.insertBefore(this._view.dom, input);
    const hadFocus = this.shadowRoot.activeElement === input;
    input.remove();
    if (hadFocus) this._view.focus();
    this._upgrading = false;
  }

  /** @private */
  _applyReadonly() {
    const ro = this.hasAttribute('readonly');
    const input = this.$('input');
    if (input) input.readOnly = ro;
    // The CM6 path fixes editability at build time; a dynamic toggle would use a Compartment —
    // not needed by the current consumers (inspectors re-create on selection change).
  }

  /** @private */
  _scheduleValidate() {
    if (!this._validator) return;
    clearTimeout(this._validateTimer);
    const seq = ++this._validateSeq;
    this._validateTimer = setTimeout(async () => {
      let result;
      try {
        result = await this._validator(this.value);
      } catch (err) {
        result = { valid: false, errors: [{ message: String(err?.message || err) }] };
      }
      if (seq !== this._validateSeq || !this.isConnected) return;
      this._setInvalid(result?.valid === false ? (result.errors || [{ message: 'Invalid expression.' }]) : null);
      this.emit('validated', { valid: result?.valid !== false, errors: result?.errors || [] });
    }, VALIDATE_DEBOUNCE_MS);
  }

  /** @private */
  _setInvalid(errors) {
    const box = this.$('.xin');
    const err = this.$('.err');
    if (!box) return;
    box.classList.toggle('invalid', !!errors);
    err.hidden = !errors;
    err.textContent = errors ? errors.map((e) => e.message).join(' · ') : '';
  }
}

function escapeAttr(value) {
  return String(value ?? '').replaceAll('&', '&amp;').replaceAll('"', '&quot;').replaceAll('<', '&lt;');
}

define('arazzo-expression-input', ArazzoExpressionInput);
export { ArazzoExpressionInput };
