# Arazzo engine and control plane documentation

This tree documents the Corvus Arazzo workflow engine and its control plane. It is organised into
four kinds of document, each with a distinct job.

- **`adr/`** holds Architecture Decision Records. An ADR captures one decision, why it was taken, the
  options weighed, and the consequences. ADRs are the *why*. They are append-only history. A superseded
  decision is marked superseded and points to the ADR that replaces it, rather than being edited away.
- **`guides/`** holds implementation guides. A guide is the *how*. It is task-oriented ("author, generate,
  and run a workflow", "wire authentication and authorization", "deploy a durability backend") and shows
  working, compiled code. Each guide links to the ADRs that explain the decisions behind it.
- **`specs/`** holds the living design specifications, grouped by domain (`access/`, `execution/`,
  `catalog/`, `credentials/`, `web/`). A spec is the exhaustive detail of a subsystem's behaviour, the
  source a guide distils and an ADR records a decision from. It carries only what an ADR or a guide does
  not, and references them for the rest.
- **`reference/`** holds stable reference material: the ubiquitous-language glossary, the control-plane REST
  API reference, and the OpenAPI contract that is its source of truth.

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
| `guides/control-plane-rest-api.md` | Client and CLI generation from the contract. The surface itself is the [REST API reference](reference/control-plane-rest-api.md). *(planned)* |
| `guides/source-credentials.md` | `secretRef` storage, `ISecretResolver`, separation of duties. *(planned)* |
| [`guides/catalog-and-promotion.md`](guides/catalog-and-promotion.md) | Packaging, publishing, promotion across environments, readiness. **(done)** |
| [`guides/web-ui-kit.md`](guides/web-ui-kit.md) | Adopting, theming, and composing the web component kit. **(done)** |
| [`guides/ux-component-catalog.md`](guides/ux-component-catalog.md) | Every `arazzo-*` component: purpose, attributes, events, composition. **(done)** |
| `guides/workflow-designer.md` | Scenarios, debug runs, the CI scenario runner. *(planned)* |
| `guides/deployment-bootstrap.md` | The production bootstrap library versus demo seeding. *(planned)* |
| `guides/observability.md` | Spans, metrics, and the governance-audit primitive. *(planned)* |
| [`guides/platform-conventions.md`](guides/platform-conventions.md) | High-performance JSON with CTJ, keyset pagination, bounded counts, api-first codegen. **(done)** |

### Specs

The exhaustive per-subsystem detail, grouped by domain. Each spec is being verified against the code and
slimmed to what the ADRs and guides do not already carry.

| Spec | Covers |
|------|--------|
| [`specs/access/access-model.md`](specs/access/access-model.md) | The capability-versus-reach access model (summary). |
| [`specs/access/identity-and-authorization-design.md`](specs/access/identity-and-authorization-design.md) | Authorization, administration, and the identity and entitlement lifecycle. |
| [`specs/execution/execution-host-design.md`](specs/execution/execution-host-design.md) | The build side, the runner, triggers, and the execution model. |
| [`specs/catalog/catalog-design.md`](specs/catalog/catalog-design.md) | The immutable, content-hashed versioned package catalog. |
| [`specs/credentials/source-credentials-design.md`](specs/credentials/source-credentials-design.md) | `secretRef` storage, the resolver, and the rotation lifecycle. |
| [`specs/web/ui-design.md`](specs/web/ui-design.md) | The control-plane web UI design. |
| [`specs/web/security-ui-design.md`](specs/web/security-ui-design.md) | The security and access UI surfaces. |
| [`specs/web/workflow-designer-design.md`](specs/web/workflow-designer-design.md) | The workflow designer: scenarios, debug runs, git integration. |

### Reference

| Reference | Covers |
|-----------|--------|
| [`reference/control-plane-rest-api.md`](reference/control-plane-rest-api.md) | The REST surface: operation groups, the scope model, authentication, resume modes. |
| [`reference/arazzo-control-plane.openapi.json`](reference/arazzo-control-plane.openapi.json) | The OpenAPI 3.2 contract, the source of truth clients and the CLI are generated from. |
| [`reference/UBIQUITOUSLANGUAGE.md`](reference/UBIQUITOUSLANGUAGE.md) | The ubiquitous-language glossary. |

### Architecture Decision Records

See [`adr/README.md`](adr/README.md) for the full index. The authz decisions are the first set to land.

## Status

This tree was populated by a documentation-normalization campaign. The campaign swept the existing design
docs, extracted each decision into an ADR, distilled each subsystem into a guide, and removed the
duplication that had spread a single topic (the access model, for one) across several documents. The 47
ADRs and the guides are complete. The design specs have been gathered under `specs/`, and are being
verified against the code and slimmed to the residual detail the ADRs and guides do not carry.
