// WorkflowDocumentModel — the designer's shared document model (design §5.2, Layer 0.5, DOM-free),
// built collaboration-ready from the start.
//
// One Arazzo document behind a change-event seam: every editor (canvas, inspectors, text mode)
// mutates THROUGH the model and re-renders FROM it. Edits are never snapshots — every change is
// reduced (by an identity-aware structural diff) to a group of small OPERATIONS that address the
// document by its stable Arazzo identities (workflowId / stepId / names), not array indices:
//
//   { kind: 'set',    path: ['workflows', {id:'place-order'}, 'steps', {id:'authorize-payment'}, 'description'], value, prev }
//   { kind: 'remove', path, prev }
//   { kind: 'insert', path, index, value }        // ordered lists (steps, actions, criteria)
//   { kind: 'remove-at', path, index, prev }
//   { kind: 'move',   path, from, to }
//
// Because ops are identity-addressed and carry their inverses:
//   - a transport can broadcast them (the `ops` event) and feed remote ones in (`applyRemote`) —
//     convergence relies on SERVER-TOTAL-ORDER (the workspace relays ops in one order to all
//     participants; same-field concurrent writes resolve last-writer-wins in that order);
//   - undo/redo are LOCAL: they invert this actor's own op groups, not everyone's document;
//   - concurrent edits to different steps/fields merge with no transform at all.
//
// Editors keep the simple API: `update(mutator)` and `applyText(text)` — the differ turns whatever
// they did into minimal ops (a step edit in TEXT mode becomes the same op a canvas edit produces).
//
// The model also owns the DESIGNER STATE (node positions and similar, design §5.2): a sibling of
// the document, never written into it and never packaged, but edited through the SAME op pipeline —
// `updateDesignerState(mutator)` reduces to ops flagged `d: 'designer'`, so layout changes ride the
// same undo/redo stack (one Undo button, interleaved with document edits in gesture order), the
// same `ops` transport seam (collaborators see each other's layout), and the same LWW convergence.
//
//   const model = new WorkflowDocumentModel(doc, { actor: 'mwa', designerState: saved ?? {} });
//   model.addEventListener('document-changed', (e) => render(model.document, e.detail));
//   model.addEventListener('ops', (e) => transport.send(e.detail));      // → server
//   transport.onmessage = (group) => model.applyRemote(group);           // ← server order
//   model.update((d) => { … }, { origin: 'inspector', label: 'edit step' });
//   model.updateDesignerState((s) => { s.nodes = overrides; }, { origin: 'canvas', label: 'move node', coalesce: true });
//   model.undo();  model.redo();

const HISTORY_LIMIT = 200;
const COALESCE_MS = 900;

/** The list properties whose items carry a stable identity key. */
const IDENTITY_KEYS = { workflows: 'workflowId', steps: 'stepId', sourceDescriptions: 'name' };

/** The ONE deterministic text form (2-space JSON) for an Arazzo document — the text editor's `model.text`
 *  and the compare Text-merge view share it so a merge round-trips through the same serialization. */
export function serializeDocument(document) {
  return JSON.stringify(document, null, 2);
}

export class WorkflowDocumentModel extends EventTarget {
  /**
   * @param {object} document  The Arazzo document (cloned in).
   * @param {{actor?: string, designerState?: object}} [opts]  The local actor id stamped on op
   *   groups, plus the initial designer state (node positions etc. — the document's sibling).
   */
  constructor(document, { actor = 'local', designerState = {} } = {}) {
    super();
    /** @private */ this._document = structuredClone(document ?? {});
    /** @private */ this._designerState = structuredClone(designerState ?? {});
    /** @private */ this._actor = actor;
    /** @private */ this._seq = 0;
    /** @private */ this._undo = []; // [{ops, label, at}] — this actor's groups only
    /** @private */ this._redo = [];
    /** @private */ this._lastEdit = null;
  }

  /** The current document. Treat as read-only — mutate through update()/applyOps(). */
  get document() { return this._document; }

  /** The current designer state (node positions etc.). Read-only — mutate through updateDesignerState(). */
  get designerState() { return this._designerState; }

  get actor() { return this._actor; }
  get canUndo() { return this._undo.length > 0; }
  get canRedo() { return this._redo.length > 0; }

  /**
   * Apply an edit expressed as a mutation (or replacement) of the document; the identity-aware
   * diff reduces it to ops. `coalesce` merges rapid same-label bursts into one undo unit.
   */
  update(mutator, { origin = 'unknown', label = 'edit', coalesce = false } = {}) {
    const before = this._document;
    const draft = structuredClone(before);
    const result = mutator(draft);
    const after = result === undefined ? draft : result;
    const ops = diff(before, after);
    if (!ops.length) return { ok: true, ops: [] };
    return this._commitLocal(ops, { origin, label, coalesce });
  }

  /** Apply already-shaped ops from a local editor (e.g. a reorder emits a move directly). */
  applyOps(ops, { origin = 'unknown', label = 'edit', coalesce = false } = {}) {
    return this._commitLocal(structuredClone(ops), { origin, label, coalesce });
  }

  /**
   * Replace the document and designer state wholesale — opening a working copy. Mutates IN PLACE
   * (existing references to `model.document` stay valid), clears undo/redo (a load is not an
   * edit), and emits `document-changed` with `origin: 'load'` and no ops. Nothing rides the `ops`
   * transport seam: a load is this participant catching up, not an edit to broadcast.
   */
  reset(document, designerState = {}) {
    replaceContents(this._document, structuredClone(document ?? {}));
    replaceContents(this._designerState, structuredClone(designerState ?? {}));
    this._undo.length = 0;
    this._redo.length = 0;
    this._lastEdit = null;
    this._emitChanged({ origin: 'load', label: 'open', actor: this._actor, ops: [] });
  }

  /**
   * Apply an edit to the DESIGNER STATE (node positions etc.) through the same op pipeline as
   * document edits: the diff's ops are flagged `d: 'designer'`, so they land on the same undo/redo
   * stack (layout moves undo in gesture order, interleaved with document edits) and the same `ops`
   * transport seam. The document itself is never touched.
   */
  updateDesignerState(mutator, { origin = 'canvas', label = 'layout', coalesce = false } = {}) {
    const before = this._designerState;
    const draft = structuredClone(before);
    const result = mutator(draft);
    const after = result === undefined ? draft : result;
    const ops = diff(before, after);
    if (!ops.length) return { ok: true, ops: [] };
    for (const op of ops) op.d = 'designer';
    return this._commitLocal(ops, { origin, label, coalesce });
  }

  /**
   * Apply a remote actor's op group, delivered in server order. Never touches this actor's
   * undo/redo stacks; unresolvable ops (their target left with the document) are skipped.
   */
  applyRemote(group) {
    const applied = [];
    for (const op of group.ops || []) {
      if (this._applyOne(structuredClone(op))) applied.push(op);
    }
    if (applied.length) {
      this._emitChanged({ origin: 'remote', label: group.label || 'remote edit', actor: group.actor, ops: applied, remote: true });
    }
    return { ok: true, applied: applied.length, skipped: (group.ops?.length || 0) - applied.length };
  }

  undo() {
    const entry = this._undo.pop();
    if (!entry) return false;
    const inverse = entry.ops.map(invertOp).reverse();
    const applied = inverse.filter((op) => this._applyOne(structuredClone(op)));
    this._redo.push(entry);
    this._lastEdit = null;
    const detail = { origin: 'history', label: `undo ${entry.label}`, actor: this._actor, ops: applied };
    this._emitChanged(detail);
    this._emitOps(detail);
    return true;
  }

  redo() {
    const entry = this._redo.pop();
    if (!entry) return false;
    const applied = entry.ops.filter((op) => this._applyOne(structuredClone(op)));
    this._undo.push(entry);
    this._lastEdit = null;
    const detail = { origin: 'history', label: `redo ${entry.label}`, actor: this._actor, ops: applied };
    this._emitChanged(detail);
    this._emitOps(detail);
    return true;
  }

  /** The deterministic text form (2-space JSON) the text editor binds to. */
  get text() {
    return serializeDocument(this._document);
  }

  /**
   * Replace the document from text-mode input: broken JSON is rejected without touching the
   * document, and a valid edit reduces to the SAME identity ops a structured editor would emit
   * (an edited description is one `set` on that step — collaborators' concurrent work merges).
   */
  applyText(text, { origin = 'text', coalesce = true } = {}) {
    let parsed;
    try {
      parsed = JSON.parse(text);
    } catch (err) {
      return { ok: false, error: String(err?.message || err) };
    }
    const ops = diff(this._document, parsed);
    if (!ops.length) return { ok: true, ops: [] };
    return this._commitLocal(ops, { origin, label: 'text edit', coalesce });
  }

  /** @private — routes an op to its root: document ops (no flag) vs designer-state ops (`d`). */
  _applyOne(op) {
    return applyOp(op.d === 'designer' ? this._designerState : this._document, op);
  }

  /** @private */
  _commitLocal(ops, { origin, label, coalesce }) {
    const applied = ops.filter((op) => this._applyOne(structuredClone(op)));
    if (!applied.length) return { ok: true, ops: [] };

    const now = Date.now();
    const merge = coalesce
      && this._lastEdit?.label === label
      && now - this._lastEdit.at < COALESCE_MS
      && this._undo.length;
    if (merge) {
      this._undo.at(-1).ops.push(...applied);
    } else {
      this._undo.push({ ops: applied, label });
      if (this._undo.length > HISTORY_LIMIT) this._undo.shift();
    }
    this._lastEdit = { label, at: now };
    this._redo.length = 0;

    const detail = { origin, label, actor: this._actor, ops: applied };
    this._emitChanged(detail);
    this._emitOps(detail);
    return { ok: true, ops: applied };
  }

  /** @private */
  _emitChanged(detail) {
    this.dispatchEvent(new CustomEvent('document-changed', { detail }));
  }

  /** @private — the transport seam: local op groups, stamped with actor + sequence. */
  _emitOps({ label, ops }) {
    this.dispatchEvent(new CustomEvent('ops', {
      detail: { actor: this._actor, seq: ++this._seq, label, ops: structuredClone(ops) },
    }));
  }
}

// Replaces an object's contents in place (delete all keys, copy the new ones), so references held
// by editors stay valid across a wholesale load.
function replaceContents(target, source) {
  for (const key of Object.keys(target)) delete target[key];
  Object.assign(target, source);
}

// ── Path addressing ───────────────────────────────────────────────────────────────────────────
// A path is a list of segments: a string (object property) or {id} (an identity-keyed list item).

/** Resolve a path to its parent container + final segment; null when the target's parents left. */
function resolveParent(doc, path) {
  let node = doc;
  for (let i = 0; i < path.length - 1; i++) {
    node = stepSegment(node, path[i]);
    if (node == null) return null;
  }
  return { parent: node, last: path[path.length - 1] };
}

function stepSegment(node, seg) {
  if (typeof seg === 'string') return node?.[seg];
  if (Array.isArray(node)) return node.find((item) => itemId(item) === seg.id);
  return undefined;
}

/** The identity of a list item, whatever identity property it carries. */
function itemId(item) {
  return item?.workflowId ?? item?.stepId ?? item?.name;
}

// ── Op application (returns false when the target no longer resolves) ─────────────────────────

function applyOp(doc, op) {
  const loc = resolveParent(doc, op.path);
  if (!loc) return false;
  const { parent, last } = loc;

  if (op.kind === 'set' || op.kind === 'remove') {
    if (typeof last === 'string') {
      if (op.kind === 'set') parent[last] = op.value;
      else delete parent[last];
      return true;
    }
    if (!Array.isArray(parent)) return false;
    const i = parent.findIndex((item) => itemId(item) === last.id);
    if (op.kind === 'set') {
      if (i < 0) { parent.push(op.value); return true; } // a set of an absent identity inserts
      parent[i] = op.value;
    } else {
      if (i < 0) return false;
      parent.splice(i, 1);
    }
    return true;
  }

  // List ops: the path names the LIST itself.
  const list = typeof last === 'string' ? parent[last] ?? (op.kind === 'insert' ? (parent[last] = []) : undefined) : stepSegment(parent, last);
  if (!Array.isArray(list)) return false;
  if (op.kind === 'insert') {
    list.splice(Math.min(Math.max(op.index, 0), list.length), 0, op.value);
    return true;
  }
  if (op.kind === 'remove-at') {
    if (op.index < 0 || op.index >= list.length) return false;
    list.splice(op.index, 1);
    return true;
  }
  if (op.kind === 'move') {
    if (op.from < 0 || op.from >= list.length) return false;
    const [item] = list.splice(op.from, 1);
    list.splice(Math.min(Math.max(op.to, 0), list.length), 0, item);
    return true;
  }
  return false;
}

function invertOp(op) {
  const inverse = invertOpCore(op);
  if (op.d !== undefined) inverse.d = op.d; // the inverse stays in the op's domain
  return inverse;
}

function invertOpCore(op) {
  switch (op.kind) {
    case 'set':
      return op.prev === undefined
        ? { kind: 'remove', path: op.path, prev: op.value }
        : { kind: 'set', path: op.path, value: op.prev, prev: op.value };
    case 'remove':
      return { kind: 'set', path: op.path, value: op.prev };
    case 'insert':
      return { kind: 'remove-at', path: op.path, index: op.index, prev: op.value };
    case 'remove-at':
      return { kind: 'insert', path: op.path, index: op.index, value: op.prev };
    case 'move':
      return { kind: 'move', path: op.path, from: op.to, to: op.from };
    default:
      return op;
  }
}

// ── Identity-aware structural diff ────────────────────────────────────────────────────────────
// Reduces (before, after) to minimal identity-addressed ops. Identity-keyed lists (workflows,
// steps, sourceDescriptions) diff per item — including reorders as moves; every other value
// (scalars, objects, positional lists like actions/criteria/parameters) replaces at its own path
// when changed, which keeps ops small and merges anything edited in DISJOINT subtrees.

export function diff(before, after, path = [], out = []) {
  if (deepEqual(before, after)) return out;

  if (isPlainObject(before) && isPlainObject(after)) {
    for (const key of new Set([...Object.keys(before), ...Object.keys(after)])) {
      const b = before[key];
      const a = after[key];
      if (deepEqual(b, a)) continue;
      if (a === undefined) {
        out.push({ kind: 'remove', path: [...path, key], prev: structuredClone(b) });
      } else if (b === undefined) {
        out.push({ kind: 'set', path: [...path, key], value: structuredClone(a) });
      } else if (IDENTITY_KEYS[key] && Array.isArray(b) && Array.isArray(a) && hasUniqueIds(b) && hasUniqueIds(a)) {
        diffIdentityList(b, a, [...path, key], out);
      } else {
        diff(b, a, [...path, key], out);
      }
    }
    return out;
  }

  out.push({ kind: 'set', path, value: structuredClone(after), prev: structuredClone(before) });
  return out;
}

function diffIdentityList(before, after, path, out) {
  const beforeIds = before.map(itemId);
  const afterIds = after.map(itemId);
  const afterSet = new Set(afterIds);

  // Removals first (by identity), against a working copy that tracks positions.
  const work = [...before];
  for (let i = work.length - 1; i >= 0; i--) {
    if (!afterSet.has(itemId(work[i]))) {
      out.push({ kind: 'remove', path: [...path, { id: itemId(work[i]) }], prev: structuredClone(work[i]) });
      work.splice(i, 1);
    }
  }
  // Insertions + moves to match the after order.
  for (let i = 0; i < after.length; i++) {
    const id = afterIds[i];
    const at = work.findIndex((item) => itemId(item) === id);
    if (at < 0) {
      out.push({ kind: 'insert', path, index: i, value: structuredClone(after[i]) });
      work.splice(i, 0, after[i]);
    } else if (at !== i) {
      out.push({ kind: 'move', path, from: at, to: i });
      const [item] = work.splice(at, 1);
      work.splice(i, 0, item);
    }
  }
  // Per-item content diffs, addressed by identity.
  for (const item of after) {
    const prev = before.find((b) => itemId(b) === itemId(item));
    if (prev && !deepEqual(prev, item)) diff(prev, item, [...path, { id: itemId(item) }], out);
  }
  return out;
}

function hasUniqueIds(list) {
  const ids = list.map(itemId);
  return ids.every((id) => id != null) && new Set(ids).size === ids.length;
}

function isPlainObject(v) {
  return v !== null && typeof v === 'object' && !Array.isArray(v);
}

function deepEqual(a, b) {
  if (a === b) return true;
  if (a == null || b == null || typeof a !== typeof b) return false;
  if (typeof a !== 'object') return false;
  return JSON.stringify(a) === JSON.stringify(b);
}
