# Arazzo engine and control plane documentation

This tree documents the Corvus Arazzo workflow engine and its control plane. It is organised into
three kinds of document, each with a distinct job.

- **`adr/`** holds Architecture Decision Records. An ADR captures one decision, why it was taken, the
  options weighed, and the consequences. ADRs are the *why*. They are append-only history. A superseded
  decision is marked superseded and points to the ADR that replaces it, rather than being edited away.
- **`guides/`** holds implementation guides. A guide is the *how*. It is task-oriented ("author, generate,
  and run a workflow", "wire authentication and authorization", "deploy a durability backend") and shows
  working, compiled code. Each guide links to the ADRs that explain the decisions behind it.
- **`reference/`** holds stable reference material. The ubiquitous-language glossary lives here.

Design specifications (the detailed, living descriptions of a subsystem) remain the source for a
subsystem's full behaviour. A guide distils a spec for a reader with a task. An ADR records a decision the
spec embodies. The three do not duplicate each other. When they would, the ADR owns the decision, the
guide owns the walkthrough, and the spec owns the exhaustive detail.

## Map

### Guides

| Guide | Covers |
|-------|--------|
| [`guides/auth-and-authorization.md`](guides/auth-and-authorization.md) | The two-plane access model, wiring authentication, authoring reach, the entitlement lifecycle. **(done)** |
| [`guides/authoring-generating-running.md`](guides/authoring-generating-running.md) | Authoring an Arazzo document, generating an executor, running it. **(done, absorbs the former docs/Arazzo.md scope)** |
| `guides/durability-and-state-stores.md` | The checkpoint model, resume, writing an `IWorkflowStateStore` backend. *(planned)* |
| [`guides/running-a-runner.md`](guides/running-a-runner.md) | Deploying and operating an execution host. **(done)** |
| `guides/control-plane-rest-api.md` | The REST surface, client and CLI generation, scopes, resume modes. *(planned)* |
| `guides/source-credentials.md` | `secretRef` storage, `ISecretResolver`, separation of duties. *(planned)* |
| [`guides/catalog-and-promotion.md`](guides/catalog-and-promotion.md) | Packaging, publishing, promotion across environments, readiness. **(done)** |
| [`guides/web-ui-kit.md`](guides/web-ui-kit.md) | Adopting, theming, and composing the web component kit. **(done)** |
| [`guides/ux-component-catalog.md`](guides/ux-component-catalog.md) | Every `arazzo-*` component: purpose, attributes, events, composition. **(done)** |
| `guides/workflow-designer.md` | Scenarios, debug runs, the CI scenario runner. *(planned)* |
| `guides/deployment-bootstrap.md` | The production bootstrap library versus demo seeding. *(planned)* |
| `guides/observability.md` | Spans, metrics, and the governance-audit primitive. *(planned)* |
| [`guides/platform-conventions.md`](guides/platform-conventions.md) | High-performance JSON with CTJ, keyset pagination, bounded counts, api-first codegen. **(done)** |

### Architecture Decision Records

See [`adr/README.md`](adr/README.md) for the full index. The authz decisions are the first set to land.

## Status

This tree is being populated by a documentation-normalization campaign. The campaign sweeps the existing
design docs, extracts each decision into an ADR, distils each subsystem into a guide, verifies every claim
against the code, and removes the duplication that had spread a single topic (the access model, for one)
across eight documents. The authz domain is the first vertical slice, proving the structure end to end
before the remaining domains follow.
