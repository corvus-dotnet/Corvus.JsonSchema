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
