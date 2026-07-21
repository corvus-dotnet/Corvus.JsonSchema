# ADR 0046. The deployment bootstrap and the example seed are two seams

Date: 2026-07-21. Status: **Accepted**. Scope: how a deployment's real, required setup is separated from a
sample's demo content. This records why the platform splits deployment bootstrapping (the real, config-driven
setup every deployment runs) from example seeding (demo fiction the sample alone runs), rather than one seeding
step that mixes the two.

## Context

A control-plane deployment needs real setup before it can serve traffic: the row-security policy's bootstrap
rules, a read-all shell binding, a genesis-administrator grant, and each store's schema. A running sample
additionally wants demo content: catalogued workflow versions, source-credential references, environments,
personas, and a live-executed run. Left together, the two interleave. The sample's original seeding spread real
setup and demo fiction across five mechanisms (a Vault provision script, the Keycloak realm import, an inline
composition-root region, a demo-data class, and the per-service stores), so the real, reusable setup could not
be lifted out of the demo without untangling it from fiction first. That is the thing that stops a demo's setup
becoming a product's deployment step.

### Grounded architectural facts

- **The real bootstrap is one deployment-agnostic, idempotent seam.** `IDeploymentBootstrap`
  (`ControlPlane.Bootstrap/IDeploymentBootstrap.cs`) seeds the security, identity, and policy the platform
  needs, driven entirely by `DeploymentBootstrapOptions` so nothing is hard-coded to one deployment, and it is
  safe to run on every start. `DefaultDeploymentBootstrap` implements it.
- **Its configuration is a generated type, not a hand-rolled record.** `DeploymentBootstrapOptions` is a CTJ
  type generated from a checked-in JSON Schema (`deployment-bootstrap-options.json`), so a deployment supplies
  its config as JSON validated against a published schema
  ([ADR 0039](0039-api-first-openapi-source-of-truth.md)).
- **The example seed is a separate seam the sample owns.** `IExampleSeed` and `ArazzoExampleSeed`
  (`samples/arazzo/.../ExampleSeed.cs`) write the demo fiction into an `ExampleSeedContext` that carries the
  stores as their backend-agnostic interfaces. A production deployment never references it.
- **Provisioning is first-class on every backend.** A per-backend deployment library
  `...ControlPlane.Deployment.<Backend>` (nine: Postgres, SQL Server, MySQL, Sqlite, Redis, Mongo, Cosmos, NATS
  JetStream, Azure Storage) exposes one `ProvisionAsync(handle, options, ct)` that creates every control-plane
  store's schema and then runs the shared, backend-agnostic `BootstrapSecurityAsync`. It is coupled to the
  backend but agnostic to the identity provider.
- **One switch governs all example seeding.** The sample's composition root runs `ProvisionAsync` always, and
  runs the example seed and the live sample run only when `seedExampleData` is set (`Program.cs`, read from
  `ControlPlane:SeedExampleData`, injected by the AppHost's own flag), so demo fiction is on one switch end to
  end.

## Options

**One seeding step (the prior state).** A single seed sets everything up, with the real setup and the demo
content interleaved. Rejected: the real setup cannot be reused by a production deployment without first
disentangling it from demo fiction, and cross-references (the admin group name, a secret path, an environment
name) drift between the interleaved mechanisms.

**Two seams (chosen).** A real, config-driven `IDeploymentBootstrap` every deployment runs, and a separate
`IExampleSeed` the sample alone runs, gated by one flag.

## Decision

Deployment bootstrapping and example seeding are **two seams**. The real setup is `IDeploymentBootstrap`,
idempotent and driven entirely by a JSON-Schema-generated `DeploymentBootstrapOptions`, with a per-backend
`ProvisionAsync` that preps the schema and runs the shared security bootstrap. The demo content is a separate
`IExampleSeed` the sample owns, gated by one `seedExampleData` switch. A production deployment runs only the
bootstrap, from config; the sample additionally runs the seed.

## Consequences

- The real bootstrap is a reusable deployment capability. A configurable deployment (for example a ZeroFailed
  task) binds `DeploymentBootstrapOptions` from config and calls `ProvisionAsync`, with no demo fiction to
  strip out.
- Every backend is a first-class deployment target, because provisioning is a per-backend library, not a
  Postgres-only path.
- The bootstrap is idempotent, so a restart or re-deploy re-runs it safely, since the rules and bindings are
  added only when missing.
- Demo content is on one switch, so the sample can run in a bootstrap-only mode (real setup, no fiction) that
  mirrors a production deployment.
- The bootstrap is coupled to a backend for schema but agnostic to the identity provider, which the host wires
  separately, so the same provisioning serves any IdP.
