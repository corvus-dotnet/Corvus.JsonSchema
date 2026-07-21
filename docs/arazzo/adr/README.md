# Architecture Decision Records

An ADR records one architectural decision. It states the context and the governing constraint, the
options that were weighed, the decision, and the consequences that follow. ADRs are append-only. A
decision that is later replaced keeps its record and is marked `Superseded by NNNN`, so the history of
why the system is shaped the way it is stays intact.

## Format

Each ADR is `NNNN-short-slug.md` and opens with a status line: a date, a status, and the scope it governs.
Status is one of `Proposed`, `Accepted`, or `Superseded by NNNN`.

The body carries these sections. An ADR that records a genuine fork (two or more real options) includes
Options and an antagonistic review that steelmans each. An ADR that records an invariant or a principle
(where the alternative was never seriously on the table) may omit them and go straight from Context to
Decision.

- **Context** states the problem and the governing constraint, grounded in facts that are cited to the
  code, not assumed.
- **Options** (when there was a fork) describe each real alternative.
- **Antagonistic review** (when there were options) steelmans the pros and cons of each.
- **Decision** states what was chosen.
- **Consequences** state what the decision commits the system to.

The template to follow for a full, fork-bearing ADR is
[`0012-approval-decision-delivery.md`](0012-approval-decision-delivery.md).

## Index

### Access model and identity (the authz slice)

| ADR | Title | Status |
|-----|-------|--------|
| [0001](0001-two-plane-access-model.md) | Two-plane access model: capability and reach | Accepted |
| [0002](0002-grant-verbs-are-reach-not-scopes.md) | Grant verbs are row-access levels, not scopes; rules conjoin, bindings union | Accepted |
| [0003](0003-membership-matching-over-canonical-identity.md) | Membership matching over one canonical `sys:` identity | Accepted |
| [0004](0004-fail-closed-non-disclosing-enforcement.md) | Fail-closed, non-disclosing enforcement | Accepted |
| [0005](0005-entitlement-scopes-union-into-claims.md) | Capability entitlements union into token claims; the IdP is never mutated | Accepted |
| [0006](0006-deployment-access-control-shell.md) | The deployment access-control shell and ambient identity | Accepted |
| [0007](0007-administrator-resolved-identity-digest-keyed.md) | Administration is a set of resolved identities, digest-keyed, with a reverse index | Accepted |
| [0008](0008-resolved-grantee-resolution.md) | Resolved-grantee resolution: no guessing | Accepted |
| [0009](0009-eligible-versus-active-self-elevation.md) | Eligible versus active self-elevation, and the independent-decision rule | Accepted |
| [0010](0010-access-requests-ceiling-bounded.md) | Access requests are ceiling-bounded and subject-pinned | Accepted |
| [0011](0011-approval-is-a-strategy-seam.md) | Approval is a strategy seam | Accepted |
| [0012](0012-approval-decision-delivery.md) | Delivering an approver's decision to a suspended run | Accepted |
| [0013](0013-step-output-disclosure-tier.md) | Step-output disclosure tier | Accepted |
| [0014](0014-direct-grant-versus-request-only.md) | Direct grant versus request-only, split by binding type | Accepted |
| [0015](0015-access-overview-server-aggregated.md) | The access overview is server-aggregated | Accepted |
| [0016](0016-control-plane-security-mode.md) | `ControlPlaneSecurityMode`: one explicit posture, no insecure default | Accepted |

### Engine and durability

| ADR | Title | Status |
|-----|-------|--------|
| [0017](0017-code-generate-the-executor.md) | Code-generate the executor, not a runtime interpreter | Accepted |
| [0018](0018-generate-only-what-is-used.md) | Generate only the operations a workflow uses | Accepted |
| [0019](0019-products-are-the-checkpoint.md) | The products are the checkpoint | Accepted |
| [0020](0020-durability-is-opt-in-codegen.md) | Durability is opt-in code generation | Accepted |
| [0021](0021-state-store-abstraction.md) | The state-store abstraction: core store plus optional wait index | Accepted |
| [0022](0022-resume-mode-taxonomy.md) | The resume-mode taxonomy | Accepted |
| [0050](0050-durable-per-step-run-journal.md) | A durable per-step run journal | Accepted |

### Runner and execution host

| ADR | Title | Status |
|-----|-------|--------|
| [0023](0023-two-process-store-as-queue.md) | Two processes sharing the store, never calling each other on the hot path | Accepted |
| [0024](0024-collectible-assembly-per-version.md) | One collectible assembly per version, loaded and unloaded on demand | Accepted |
| [0025](0025-integrity-binding-optional-signature.md) | Integrity binding, with an optional signature custody split | Accepted |
| [0026](0026-triggers-async-by-default.md) | Triggers are async by default | Accepted |
| [0027](0027-runner-environment-binding.md) | Runner-to-environment binding, with the revocation fence in the store | Accepted |
| [0028](0028-pluggable-execution-backends.md) | Pluggable execution backends and isolation models | Proposed |
| [0029](0029-native-heartbeat-partial-update.md) | Native server-side partial update for the hot heartbeat path | Accepted |

### Catalog

| ADR | Title | Status |
|-----|-------|--------|
| [0030](0030-immutable-content-hashed-versioned-packages.md) | Immutable, content-hashed, versioned packages | Accepted |
| [0031](0031-content-hash-over-rfc8785-canonical.md) | The content hash is over the RFC 8785 canonical form of the logical content | Accepted |
| [0032](0032-awp-deterministic-tlv-container.md) | The `.awp` container is a deterministic TLV framing, not a ZIP | Accepted |
| [0033](0033-compile-at-catalog-add.md) | Compile at catalog-add; the package is a complete code-generation input | Accepted |
| [0034](0034-standalone-hosting-not-required.md) | A standalone published-endpoint hosting service is not required | Accepted |

### Platform conventions

| ADR | Title | Status |
|-----|-------|--------|
| [0035](0035-keyset-pagination-everywhere.md) | Keyset pagination everywhere, API and store | Accepted |
| [0036](0036-bounded-count-contract.md) | The bounded count contract | Accepted |
| [0037](0037-bytes-native-seams.md) | Bytes-native seams: no record-to-document string round-trips | Accepted |
| [0038](0038-payload-safe-governance-audit.md) | A payload-safe governance-audit primitive | Accepted |
| [0039](0039-api-first-openapi-source-of-truth.md) | API-first: the OpenAPI surface is the source of truth | Accepted |

### Deployment and bootstrap

| ADR | Title | Status |
|-----|-------|--------|
| [0046](0046-deployment-bootstrap-example-seed-split.md) | The deployment bootstrap and the example seed are two seams | Accepted |

### Source credentials

| ADR | Title | Status |
|-----|-------|--------|
| [0048](0048-source-credentials-are-references.md) | Source credentials are references, resolved runner-side | Accepted |

### Web kit and designer

| ADR | Title | Status |
|-----|-------|--------|
| [0040](0040-three-layer-web-kit.md) | The web kit is three layers, and Layer 1 never fetches | Accepted |
| [0041](0041-standards-only-zero-build-elements.md) | Standards-only, zero-build custom elements | Accepted |
| [0042](0042-auth-agnostic-host-owns-session.md) | Auth-agnostic: the host owns the session | Accepted |
| [0043](0043-first-party-svg-design-surface.md) | A first-party SVG design surface, not a graph library | Accepted |
| [0044](0044-collaboration-ready-document-model.md) | A collaboration-ready document model: identity-addressed operations with inverses | Accepted |
| [0045](0045-debug-runs-never-credentials-in-browser.md) | Remote dev-environment debug runs, never credentials in the browser | Accepted |
| [0047](0047-web-kit-permission-gating-server-authoritative.md) | Web-kit permission gating is server-authoritative | Accepted |
| [0049](0049-codemirror-vendored-single-bundle.md) | CodeMirror 6, vendored as a single bundle | Accepted |
