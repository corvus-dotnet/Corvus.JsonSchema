// <arazzo-json-view> — a READ-ONLY, syntax-highlighted JSON document view (backlog #843).
//
//   const v = document.createElement('arazzo-json-view');
//   v.value = jsonText;                      // the document text (pretty-printed by the caller)
//
// Properties : .value (string)
// Attributes : max-height (CSS length; default 320px)
//
// Upgrades lazily to a read-only CodeMirror 6 editor (the kit's vendored bundle — one shared
// instance, see vendor/codemirror-entry.js) with the JSON language and kit-token highlighting, so
// every read-only JSON surface gets the same treatment the designer's editors have. Until the
// bundle lands — or if it fails to load — the value renders as the same themed <pre> these views
// used before, so the component always shows the document.

import { ArazzoElement, SHARED_CSS, define } from './base.js';

class ArazzoJsonView extends ArazzoElement {
  static get observedAttributes() { return ['max-height']; }

  /** Host override: `async () => {state, view, language, lezerHighlight, langJson}`. */
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

  constructor() {
    super();
    /** @private */ this._value = '';
    /** @private */ this._view = null;
    /** @private */ this._built = false;
  }

  connectedCallback() {
    if (!this._built) this.renderShell();
    void this._upgrade();
  }

  disconnectedCallback() {
    this._view?.destroy();
    this._view = null;
  }

  attributeChangedCallback(name) {
    if (name === 'max-height' && this._built) this._applyMaxHeight();
  }

  get value() { return this._value; }

  set value(text) {
    this._value = String(text ?? '');
    if (!this._built) return;
    if (this._view) {
      this._view.dispatch({ changes: { from: 0, to: this._view.state.doc.length, insert: this._value } });
    } else {
      const pre = this.$('pre');
      if (pre) pre.textContent = this._value;
    }
  }

  renderShell() {
    this._built = true;
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        :host { display: block; }
        .scroll { max-height: var(--_maxh, 320px); overflow: auto; border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_surface); }
        pre { margin: 0; padding: 10px 12px; font: 12px ui-monospace, SFMono-Regular, Menlo, monospace; }
        .cm-editor { font-size: 12px; }
        .cm-editor .cm-content { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; }
        .cm-editor .cm-gutters { background: transparent; border-right: 1px solid var(--_border); color: var(--_muted); }
      </style>
      <div class="scroll"><pre></pre></div>
    `;
    this.$('pre').textContent = this._value;
    this._applyMaxHeight();
  }

  /** @private */
  _applyMaxHeight() {
    const h = this.getAttribute('max-height');
    this.$('.scroll')?.style.setProperty('--_maxh', h || '320px');
  }

  /** @private — swap the <pre> for a read-only CM6 view once the bundle lands; keep the <pre> on failure. */
  async _upgrade() {
    let cm;
    try {
      cm = await ArazzoJsonView.loadCm();
    } catch {
      return; // the themed <pre> stays — the document is always visible
    }

    if (!this.isConnected || this._view) return;
    const { state, view, language, lezerHighlight, langJson } = cm;
    const t = lezerHighlight.tags;
    const highlight = language.syntaxHighlighting(language.HighlightStyle.define([
      { tag: t.string, color: 'var(--arazzo-status-completed, #2a8a4a)' },
      { tag: t.number, color: 'var(--arazzo-status-suspended, #b07d18)' },
      { tag: t.bool, color: 'var(--arazzo-status-suspended, #b07d18)' },
      { tag: t.null, color: 'var(--arazzo-muted, #6b7280)' },
      { tag: t.propertyName, color: 'var(--arazzo-accent, #3b6cf6)' },
      { tag: t.punctuation, color: 'var(--arazzo-muted, #6b7280)' },
      { tag: t.brace, color: 'var(--arazzo-muted, #6b7280)' },
      { tag: t.squareBracket, color: 'var(--arazzo-muted, #6b7280)' },
    ]));
    // CodeMirror's base theme sets a light-mode (near-black) content colour; without overriding it the document is
    // invisible on the dark theme. Follow the app's text colour so unhighlighted text (and the whole document if the
    // highlighter does not apply) is always legible; the syntax colours below override it per token.
    const chrome = view.EditorView.theme({
      '&': { backgroundColor: 'transparent', color: 'var(--arazzo-text, inherit)' },
      '.cm-content': { caretColor: 'transparent', color: 'var(--arazzo-text, inherit)' },
    });

    const editor = new view.EditorView({
      state: state.EditorState.create({
        doc: this._value,
        extensions: [
          langJson.json(),
          highlight,
          chrome,
          view.lineNumbers(),
          view.EditorView.editable.of(false),
          state.EditorState.readOnly.of(true),
          view.EditorView.lineWrapping,
        ],
      }),
    });
    this.$('.scroll').replaceChildren(editor.dom);
    this._view = editor;
  }
}

define('arazzo-json-view', ArazzoJsonView);