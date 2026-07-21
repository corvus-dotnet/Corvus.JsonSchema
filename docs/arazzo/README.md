# Arazzo engine and control plane documentation

This tree documents the Corvus Arazzo workflow engine and its control plane. It is organised into three kinds
of document, each with a distinct job.

- **`adr/`** holds Architecture Decision Records. An ADR captures one decision, why it was taken, the options
  weighed, and the consequences. ADRs are the *why*. They are append-only history. A superseded decision is
  marked superseded and points to the ADR that replaces it, rather than being edited away.
- **`guides/`** holds implementation guides. A guide is the *how*: both task walkthroughs (author, generate, and
  run a workflow; wire authentication; deploy a backend) and subsystem guides (how the catalog, the execution
  host, row security, or the web kit work and how to work with them). A guide shows working code and links the
  ADRs that explain the decisions behind it.
- **`reference/`** holds stable reference material: the ubiquitous-language glossary, the control-plane REST API
  reference, and the OpenAPI contract that is its source of truth.

When an ADR, a guide, and the reference would say the same thing, the ADR owns the decision, the guide owns the
walkthrough, and the reference owns the exhaustive contract. A guide carries only what an ADR or the reference
does not, and links them for the rest.

## Guides

### Authoring and running

| Guide | Covers |
|-------|--------|
| [`authoring-generating-running.md`](guides/authoring-generating-running.md) | Authoring an Arazzo document, generating an executor, running it. **(done)** |
| [`catalog.md`](guides/catalog.md) | The immutable, content-hashed versioned package catalog: the data model, the operation surface, the store. **(done)** |
| [`catalog-and-promotion.md`](guides/catalog-and-promotion.md) | Packaging, publishing, promotion across environments, readiness. **(done)** |
| [`execution-host.md`](guides/execution-host.md) | The build side, the runner (load, isolation, registration), triggers, and the execution model. **(done)** |
| [`running-a-runner.md`](guides/running-a-runner.md) | Deploying and operating an execution host. **(done)** |
| `durability-and-state-stores.md` | The checkpoint model, resume, writing an `IWorkflowStateStore` backend. *(planned)* |

### Access, identity, and credentials

| Guide | Covers |
|-------|--------|
| [`access-model.md`](guides/access-model.md) | The capability-versus-reach access model, and the authentication and machine-identity reference. **(done)** |
| [`auth-and-authorization.md`](guides/auth-and-authorization.md) | Wiring authentication, authoring reach, the entitlement lifecycle. **(done)** |
| [`identity-and-authorization.md`](guides/identity-and-authorization.md) | Authorization and row security, administration, and the identity and entitlement lifecycle, in depth. **(done)** |
| [`security-ui.md`](guides/security-ui.md) | The security and access UI: jobs, information architecture, safe binding authoring. **(done)** |
| [`source-credentials.md`](guides/source-credentials.md) | `secretRef` storage, the resolver, the rotation lifecycle, separation of duties. **(done)** |

### Web kit and designer

| Guide | Covers |
|-------|--------|
| [`web-ui-kit.md`](guides/web-ui-kit.md) | Adopting, theming, and composing the web component kit. **(done)** |
| [`web-ui-design.md`](guides/web-ui-design.md) | The control-plane web UI design: the governance-hub IA and the residual component rationale. **(done)** |
| [`ux-component-catalog.md`](guides/ux-component-catalog.md) | Every `arazzo-*` component: purpose, attributes, events, composition. **(done)** |
| [`workflow-designer.md`](guides/workflow-designer.md) | The designer: authoring on the surface, scenarios, debug runs, diff, git, the CI scenario runner. **(done)** |

### Cross-cutting

| Guide | Covers |
|-------|--------|
| [`platform-conventions.md`](guides/platform-conventions.md) | High-performance JSON with CTJ, keyset pagination, bounded counts, api-first codegen. **(done)** |
| `control-plane-rest-api.md` | Client and CLI generation from the contract. The surface itself is the [REST API reference](reference/control-plane-rest-api.md). *(planned)* |
| `deployment-bootstrap.md` | The production bootstrap library versus demo seeding. *(planned)* |
| `observability.md` | Spans, metrics, and the governance-audit primitive. *(planned)* |

## Reference

| Reference | Covers |
|-----------|--------|
| [`reference/control-plane-rest-api.md`](reference/control-plane-rest-api.md) | The REST surface: operation groups, the scope model, authentication, resume modes. |
| [`reference/arazzo-control-plane.openapi.json`](reference/arazzo-control-plane.openapi.json) | The OpenAPI 3.2 contract, the source of truth clients and the CLI are generated from. |
| [`reference/UBIQUITOUSLANGUAGE.md`](reference/UBIQUITOUSLANGUAGE.md) | The ubiquitous-language glossary. |

## Architecture Decision Records

See [`adr/README.md`](adr/README.md) for the full index (49 ADRs across the access model, the engine and
durability, the runner, the catalog, source credentials, platform conventions, and the web kit and designer).

## Status

This tree was populated by a documentation-normalization campaign: it extracted each decision into an ADR,
distilled each subsystem into a guide, moved the API contract and glossary into the reference, verified every
claim against the code, and removed the duplication that had spread a single topic across several documents.
