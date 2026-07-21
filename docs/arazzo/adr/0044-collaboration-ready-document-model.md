# ADR 0044. A collaboration-ready document model: identity-addressed operations with inverses

Date: 2026-07-21. Status: **Accepted**. Scope: how the designer represents an edit to a workflow document.
This records why every edit is a group of identity-addressed operations that carry their inverses, from day
one, rather than a snapshot, even though real-time collaboration ships later.

## Context

The designer edits a workflow document. The simple representation is a snapshot: on each edit, replace the
document. Snapshots make undo a stack of past documents and make collaboration hard to add later, because
merging two people's snapshots is a rewrite. Building the model as operations instead, where each edit is a
described change with an inverse, makes undo a local inversion and makes real-time collaboration an addition
rather than a rework. The cost is doing that up front, before collaboration ships.

### Grounded architectural facts

- **Every edit is identity-addressed operations, never a snapshot.** The designer design
  (`workflow-designer-design.md` §5.2) and `WorkflowDocumentModel`
  (`web/arazzo-control-plane-ui/src/workflow-document-model.js`) make every edit a group of identity-addressed
  operations, not a snapshot. The decision (2026-07-04) reverses an earlier non-goal and makes the model
  collaboration-ready from day one, so the real-time transport is an addition, not a rework.
- **Operations carry their inverses.** Undo and redo are local and inverse-based: they invert this actor's op
  groups (`workflow-document-model.js` inverts each op and reverses the group), rather than restoring a past
  snapshot.
- **The model is DOM-free.** `WorkflowDocumentModel` is a Layer 0.5 model with no DOM, so it is testable and
  reusable independent of the surface that renders it.

## Decision

Every edit to a workflow document is a **group of identity-addressed operations that carry their inverses**,
not a snapshot. Undo and redo invert this actor's operation groups locally. The model is DOM-free and
collaboration-ready from day one, so adding real-time collaboration is adding a transport over the operations,
not rewriting the model.

## Consequences

- Undo and redo are local inversions of operations, so they are precise (they invert exactly the edit) and do
  not depend on retaining past document snapshots.
- Real-time collaboration is an addition, not a rework: the operations are the unit a transport would carry, so
  the model does not have to change to gain it.
- Because operations are identity-addressed, an edit names the thing it changes, which is what lets two actors'
  edits be reasoned about for merging.
- The DOM-free model is testable on its own, separate from the SVG surface that renders it
  ([ADR 0043](0043-first-party-svg-design-surface.md)).
