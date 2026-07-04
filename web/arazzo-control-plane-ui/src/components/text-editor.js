// <arazzo-text-editor> — the designer's full-document text mode (design §7.2): CodeMirror 6 in
// the shadow root over the document model's deterministic JSON, a peer of the design surface —
// never a downgraded mirror. The host owns the WorkflowDocumentModel and wires both directions:
//
//   editor.value = model.text;                                  // model → text (skip own edits)
//   editor.addEventListener('text-changed', (e) => {            // text → model (debounced)
//     const r = model.applyText(e.detail.text);
//     editor.setProblem(r.ok ? null : r.error);
//   });
//
// Uses the same vendored CM6 bundle as <arazzo-expression-input> (loadCm — one shared instance
// set), with JSON highlighting and the kit-token chrome. Until the modules load (or if they never
// do) a plain <textarea> serves the identical value/event contract.

import { ArazzoElement, SHARED_CSS, define } from './base.js';
import { ArazzoExpressionInput } from './expression-input.js';

const CHANGE_DEBOUNCE_MS = 400;

class ArazzoTextEditor extends ArazzoElement {
  constructor() {
    super();
    /** @private */ this._view = null;
    /** @private */ this._changeTimer = null;
    /** @private */ this._suppress = false; // programmatic set → no text-changed echo
  }

  connectedCallback() {
    if (!this._built) this.renderShell();
    this._upgrade();
  }

  disconnectedCallback() {
    clearTimeout(this._changeTimer);
  }

  /** True while the plain-textarea fallback is serving. */
  get usingFallback() { return !this._view; }

  get value() {
    if (this._view) return this._view.state.doc.toString();
    if (this._built) return this.$('textarea')?.value ?? '';
    return this._pending ?? '';
  }

  /** Replace the buffer (a model-originated refresh). Preserves cursor/scroll best-effort and
   *  does NOT emit text-changed. */
  set value(text) {
    const v = String(text ?? '');
    if (v === this.value) return;
    this._suppress = true;
    try {
      if (this._view) {
        const head = Math.min(this._view.state.selection.main.head, v.length);
        this._view.dispatch({
          changes: { from: 0, to: this._view.state.doc.length, insert: v },
          selection: { anchor: head },
        });
      } else if (this._built) {
        this.$('textarea').value = v;
      } else {
        this._pending = v;
      }
    } finally {
      this._suppress = false;
    }
  }

  /** Show (or clear, with null) a parse problem under the editor. */
  setProblem(message) {
    const err = this.$('.err');
    if (!err) return;
    err.hidden = !message;
    err.textContent = message || '';
    this.$('.ted')?.classList.toggle('invalid', !!message);
  }

  /** Scroll the first occurrence of `"stepId": "<id>"` into view (canvas → text selection sync). */
  revealStep(stepId) {
    const needle = `"stepId": ${JSON.stringify(stepId)}`;
    const index = this.value.indexOf(needle);
    if (index < 0) return;
    if (this._view) {
      this._view.dispatch({
        selection: { anchor: index, head: index + needle.length },
        scrollIntoView: true,
      });
      this._view.focus();
    }
  }

  /** @private */
  renderShell() {
    this._built = true;
    const initial = this._pending ?? '';
    delete this._pending;
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        :host { display: block; height: 100%; min-height: 320px; }
        .ted { height: 100%; display: flex; flex-direction: column; min-height: 0; }
        textarea, .cm-editor {
          flex: 1; min-height: 0; width: 100%; box-sizing: border-box;
          font: 12.5px/1.5 ui-monospace, SFMono-Regular, Menlo, monospace;
          border: 1px solid var(--_border); border-radius: var(--_radius);
          background-color: var(--_bg); color: var(--_text);
        }
        textarea { padding: 10px; resize: none; }
        .cm-editor { overflow: hidden; }
        .cm-editor .cm-scroller { overflow: auto; }
        .cm-editor.cm-focused { outline: 2px solid var(--_accent); outline-offset: 1px; }
        .ted.invalid textarea, .ted.invalid .cm-editor { border-color: var(--_danger); }
        .err { font-size: 11px; color: var(--_danger); padding-top: 4px; flex: none; }
      </style>
      <div class="ted" part="editor">
        <textarea spellcheck="false"></textarea>
        <div class="err" hidden part="error"></div>
      </div>
    `;
    const ta = this.$('textarea');
    ta.value = initial;
    ta.addEventListener('input', () => this._scheduleChange());
  }

  /** @private — same shared module set as the expression input (one CM6 instance set). */
  async _upgrade() {
    if (this._view || this._upgrading) return;
    this._upgrading = true;
    let cm;
    try {
      cm = await ArazzoExpressionInput.loadCm();
    } catch {
      this._upgrading = false;
      return; // the textarea stays in service
    }
    if (!this.isConnected || this._view || !cm.langJson) { this._upgrading = false; return; }

    const { state, view, language, commands, langJson, lezerHighlight } = cm;
    const t = lezerHighlight.tags;
    const highlight = language.syntaxHighlighting(language.HighlightStyle.define([
      { tag: t.propertyName, color: 'var(--arazzo-accent, #3b6cf6)' },
      { tag: t.string, color: 'var(--arazzo-status-completed, #2a8a4a)' },
      { tag: t.number, color: 'var(--arazzo-status-suspended, #b07d18)' },
      { tag: t.bool, color: 'var(--arazzo-status-suspended, #b07d18)' },
      { tag: t.null, color: 'var(--arazzo-muted, #6b7280)' },
      { tag: t.punctuation, color: 'var(--arazzo-muted, #6b7280)' },
    ]));
    const chrome = view.EditorView.theme({
      '&': { height: '100%' },
      '.cm-content': { caretColor: 'var(--arazzo-text, #1c2024)' },
      '.cm-cursor, .cm-dropCursor': { borderLeftColor: 'var(--arazzo-text, #1c2024)' },
      '.cm-selectionBackground, &.cm-focused > .cm-scroller .cm-selectionLayer .cm-selectionBackground': {
        background: 'color-mix(in srgb, var(--arazzo-accent, #3b6cf6) 28%, transparent)',
      },
      '.cm-gutters': {
        background: 'var(--arazzo-surface, #f7f8fa)',
        color: 'var(--arazzo-muted, #6b7280)',
        border: 'none',
      },
      '.cm-activeLineGutter': { background: 'color-mix(in srgb, var(--arazzo-accent, #3b6cf6) 12%, transparent)' },
    });

    const ta = this.$('textarea');
    const container = this.$('.ted');
    this._view = new view.EditorView({
      root: this.shadowRoot,
      parent: container,
      state: state.EditorState.create({
        doc: ta.value,
        extensions: [
          langJson.json(),
          highlight,
          chrome,
          view.lineNumbers(),
          commands.history(),
          view.keymap.of([...commands.defaultKeymap, ...commands.historyKeymap, commands.indentWithTab]),
          view.EditorView.updateListener.of((u) => {
            if (u.docChanged && !this._suppress) this._scheduleChange();
          }),
        ],
      }),
    });
    container.insertBefore(this._view.dom, ta);
    ta.remove();
    this._upgrading = false;
  }

  /** @private */
  _scheduleChange() {
    clearTimeout(this._changeTimer);
    this._changeTimer = setTimeout(() => {
      this.emit('text-changed', { text: this.value });
    }, CHANGE_DEBOUNCE_MS);
  }
}

define('arazzo-text-editor', ArazzoTextEditor);
export { ArazzoTextEditor };
