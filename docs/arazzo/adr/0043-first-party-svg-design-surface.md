# ADR 0043. A first-party SVG design surface, not a graph library

Date: 2026-07-21. Status: **Accepted**. Scope: how the workflow designer renders and edits the workflow graph.
Builds on [ADR 0041](0041-standards-only-zero-build-elements.md). This records why the design surface is
hand-authored SVG with a small layout dependency, rather than a graph or diagramming library.

## Context

The designer shows a workflow as an editable, debuggable diagram. A graph or diagramming library is the
obvious route. But the surface is not an illustration; it is an instrument. It has to overlay debug state
(breakpoints, the current step, fault markers), support drawing and retargeting edges, and stay inside a shadow
root with the kit's zero-build, zero-dependency ethos ([ADR 0041](0041-standards-only-zero-build-elements.md)).
A library brings its own rendering, its own event and state model, and its own bulk, and bending it to the
debugger requirement tends to mean owning a fork.

### Grounded architectural facts

- **The decision is to build it first-party.** The designer design (`workflow-designer-design.md` §6.1) records
  building the design surface as hand-authored SVG inside the component's shadow root, with `@dagrejs/dagre`
  (MIT, pure JS, vendored and lazy-loaded ESM) for layered-DAG auto-layout, after surveying and rejecting a
  set of graph libraries.
- **The debugger requirement inverts the usual trade.** Because the surface is a debugger, overlay states
  become CSS classes on SVG elements, which is simpler on a first-party surface than bending a library's
  render model to it.
- **The surface stays a projection.** Nothing outside `<arazzo-design-surface>` knows SVG exists, and nothing
  inside it knows the domain: the surface renders a projected graph and emits interaction events, which is the
  discipline that keeps a first-party canvas from becoming an accidental framework.
- **The fallback is recorded.** If editing scope outgrows the bespoke surface (free-form diagramming), the
  design records that as the point to reconsider.

## Decision

The workflow design surface is **hand-authored SVG in the component's shadow root**, with `dagre` for
auto-layout, not a graph or diagramming library. Debug overlays are CSS classes on SVG. The surface is a pure
projection that renders a graph and emits events, knowing nothing of the domain, and nothing outside it knows
it is SVG.

## Consequences

- The surface meets the debugger requirement directly, because overlay state is styling on elements the kit
  owns, not a library's render model bent to fit.
- It stays inside the kit's zero-build, zero-dependency ethos
  ([ADR 0041](0041-standards-only-zero-build-elements.md)): one small pure-JS layout dependency, lazy-loaded,
  and no diagramming framework.
- The projection discipline keeps the surface reusable and testable: it renders a graph and emits, so the same
  surface serves the editor, the read-only compare view, and the debug tray.
- The recorded fallback means the decision is revisitable: a future need for free-form diagramming is the
  signal to reconsider, not a reason to have started with a library.
