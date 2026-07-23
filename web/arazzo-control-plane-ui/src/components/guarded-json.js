// guarded-json.js — the one shared "guarded JSON textarea" wiring, extracted from the three identical copies
// (workflow-inspector inputs, document-inspector component schema, payload-editor). Parse-gated,
// last-valid-wins: a parseable buffer commits and clears `.invalid`; an unparseable one marks `.invalid` and
// shows a `not JSON yet: …` hint while the last valid value stands (never emitted broken). A plain `<textarea>`,
// exactly as the copies were — CM6-backing it is a separate optional enhancement.
//
//   wireGuardedJson(textarea, { hint, baseHint, emptyDeletes: true, onCommit: (value) => { … } });
//
// `emptyDeletes` matches each host's blank semantics: true ⇒ blank text commits `undefined` (delete/clear) and
// stays valid (workflow-inspector, payload-editor); false ⇒ blank text is just invalid JSON, holding last-valid
// (document-inspector). `onCommit` receives the parsed value (or `undefined` when blank+emptyDeletes); the host
// does the assignment + its own emit.

export function wireGuardedJson(textarea, { hint = null, baseHint = '', emptyDeletes = false, onCommit } = {}) {
  const setHint = (text) => { if (hint) hint.textContent = text; };
  textarea.addEventListener('input', () => {
    if (emptyDeletes && !textarea.value.trim()) {
      textarea.classList.remove('invalid');
      setHint(baseHint);
      onCommit(undefined);
      return;
    }
    try {
      const parsed = JSON.parse(textarea.value);
      textarea.classList.remove('invalid');
      setHint(baseHint);
      onCommit(parsed);
    } catch (err) {
      textarea.classList.add('invalid');
      setHint(`not JSON yet: ${String(err.message).slice(0, 80)}`);
      // The last valid value stands until this parses.
    }
  });
}

/**
 * The same parse-gated, last-valid-wins guard as {@link wireGuardedJson}, but over an
 * `<arazzo-text-editor>` (the syntax-highlighted CM6 JSON editor) rather than a plain textarea: it
 * listens to the editor's debounced `text-changed`, surfaces a parse problem under the editor via
 * `setProblem`, and commits only a parseable buffer. Use this everywhere a JSON value is authored so
 * the author gets highlighting; the textarea variant remains for prose fields.
 *
 *   const ed = document.createElement('arazzo-text-editor'); ed.standalone = true;
 *   wireGuardedJsonEditor(ed, { emptyDeletes: true, onCommit: (value) => { … } });
 */
export function wireGuardedJsonEditor(editor, { hint = null, baseHint = '', emptyDeletes = false, onCommit } = {}) {
  const setHint = (text) => { if (hint) hint.textContent = text; };
  editor.addEventListener('text-changed', (e) => {
    const text = e.detail.text ?? '';
    if (emptyDeletes && !text.trim()) {
      editor.setProblem?.(null);
      setHint(baseHint);
      onCommit(undefined);
      return;
    }
    try {
      const parsed = JSON.parse(text);
      editor.setProblem?.(null);
      setHint(baseHint);
      onCommit(parsed);
    } catch (err) {
      editor.setProblem?.(`not JSON yet: ${String(err.message).slice(0, 80)}`);
      // The last valid value stands until this parses.
    }
  });
}
