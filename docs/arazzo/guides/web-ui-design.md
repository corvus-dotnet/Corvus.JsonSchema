# Control-plane web UI design

This guide records the design decisions the control-plane web UI makes that are not owned by the web-kit ADRs
([0040](../adr/0040-three-layer-web-kit.md) to [0047](../adr/0047-web-kit-permission-gating-server-authoritative.md)),
the [web-ui-kit guide](web-ui-kit.md) (how to adopt, theme, and compose the kit), or the
[UX component catalog](ux-component-catalog.md) (every component's attributes, events, and composition). It is
the residual UI rationale: the information architecture, the typed-authoring seams, and the correct-by-construction
invariants.

## The per-workflow detail page is the governance hub

The central IA decision: **administration and source credentials live on the workflow, not in deployment-wide
tabs.** A workflow's administrator set (§15) and its self-service access request are embedded in the catalog
version detail (`<arazzo-catalog-detail>`, the per-workflow governance hub), keyed by the version's
`baseWorkflowId`, rather than a separate global "administrators" screen. This keeps governance where a reader is
already looking at the thing being governed.

The administrators panel is **subject-agnostic**: the same `<arazzo-administrators-panel>` serves a workflow's
administrators (set `base-workflow-id`, gated on `administrators:write`) or an environment's (set `environment`,
gated on `environments:write`), so environment governance reuses the workflow pattern rather than inventing a
second one. Creating an environment grants the creator its administration, the same establish-by-creation shape
the catalog uses for a workflow.

## Typed authoring, not guarded JSON

Two seams turn schema-driven forms into first-class editors instead of a JSON textarea:

- **`<arazzo-value-editor>`** builds a strongly-typed form from a step's precomputed schema metadata (unions,
  tuples, maps, `const`, inline booleans, per-field validation). It **normalizes each schema at the field
  boundary** (`normalizeDescriptor`), so a raw `oneOf`/`anyOf` renders as a union chooser and a simple `allOf` as
  a merged form even off the baked path (the run dialog included), rather than degrading to a raw-JSON fallback.
  It deliberately does **not** resolve `$ref`s; reference resolution stays the baked path's job, so authored
  library references reach it pre-resolved.
- **`<arazzo-schema-editor>`** authors a workflow's `inputs` and the components library's input schemas visually,
  editing a subset of JSON Schema and staying lossless over the rest (constructs it cannot render become advanced
  rows that preserve the raw subschema and open the JSON tier). The design invariant is **normalization parity**:
  the client renderer (`schema-descriptor.js`) and the server's baked-schema generator
  (`WorkflowSchemaMetadataGenerator`) apply the same rules (`oneOf`/`anyOf` to a union picker, simple `allOf` to a
  merged object), so both consumers agree, and `$ref`s are not resolved at this authoring seam. The type menu
  leads with the shared library, so referencing an existing `components.inputs` type is the default for nested
  schemas.

## Correct by construction

Two invariants are enforced in the UI's shape, not left to the operator to remember:

- **No secret ever reaches the UI.** The credential surface moves references and identity metadata only; a
  credential is a `secretRef` (`scheme://locator[#version]`) plus non-secret config, and rotation is re-pointing
  the reference, never entering a secret. The dialog rejects a value that is not a well-formed `secretRef` before
  submit, the same boundary the server enforces, so a secret cannot be smuggled in. (The store side is
  [ADR 0048](../adr/0048-source-credentials-are-references.md) and the
  [source-credentials guide](source-credentials.md).) For `mtls` the dialog forces "Shared" and hides the
  usage-restrict option, because a client certificate is connection-level and cannot be usage-scoped.
- **A request never targets a third party.** View and Operate are self-service (a caller requests them for
  themselves through the request-then-approve path, [ADR 0014](../adr/0014-direct-grant-versus-request-only.md));
  administration is the only grant that names someone else. So there is deliberately **no unified picker** by
  which an administrator grants View or Operate to a named third party in one step: that would be a new grant
  primitive relaxing the invariant. The `catalog:read` "view" grant is offered as the default, least-privilege
  option in the access-request dialog, self-service, not a third-party grant.

## Theming

Every component reads a small token set, and a host overrides any of them (the defaults are a clean neutral look;
light and dark follow `prefers-color-scheme` unless a `theme` is set). The tokens:

```
--arazzo-font, --arazzo-radius, --arazzo-bg, --arazzo-surface, --arazzo-border,
--arazzo-text, --arazzo-muted, --arazzo-accent,
--arazzo-status-running, --arazzo-status-suspended, --arazzo-status-faulted,
--arazzo-status-completed, --arazzo-status-cancelled, --arazzo-status-pending
```

Plus a `::part()` on every structural node for deeper restyling without forking. Setting the tokens once on a
themed ancestor themes the whole kit; the [web-ui-kit guide](web-ui-kit.md) covers the mechanism.

## See also

- [Web UI kit](web-ui-kit.md): adopting, theming, and composing the kit (the three layers, `authProvider`, the
  shared conventions).
- [UX component catalog](ux-component-catalog.md): every `arazzo-*` component, and the kit's shared and design
  conventions.
- The web-kit ADRs [0040](../adr/0040-three-layer-web-kit.md) to
  [0047](../adr/0047-web-kit-permission-gating-server-authoritative.md) for the decisions behind the kit's shape.
