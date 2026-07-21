# Deployment bootstrap

How a control-plane deployment provisions itself, and how that differs from a sample's demo seeding. The decision
is [ADR 0046](../adr/0046-deployment-bootstrap-example-seed-split.md): the real, config-driven setup every
deployment runs is one seam, and a sample's demo content is a separate seam the sample alone runs.

## The two seams

- **`IDeploymentBootstrap`** (`DefaultDeploymentBootstrap`) is the real setup every deployment runs before
  serving traffic: the row-security bootstrap rules, the read-all shell binding, the genesis-administrator grant,
  and (optionally) the bootstrapped access-approval system workflow. It is idempotent, so it is safe to run on
  every start, and it is driven entirely by `DeploymentBootstrapOptions`, so nothing is hard-coded to one
  deployment.
- **`IExampleSeed`** (`ArazzoExampleSeed`, in the sample) writes the demo fiction: catalogued workflow versions,
  source-credential references, environments, personas, and a live-executed run. A production deployment never
  references it.

## Provisioning a deployment

Each backend ships a deployment library, `...ControlPlane.Deployment.<Backend>` (Postgres, SQL Server, MySQL,
Sqlite, Redis, Mongo, Cosmos, NATS JetStream, Azure Storage), exposing one call that provisions everything:

```csharp
await PostgresControlPlaneDeployment.ProvisionAsync(dataSource, options, cancellationToken);
```

`ProvisionAsync` creates every control-plane store's schema and then runs the shared, backend-agnostic security
bootstrap. It is coupled to the backend (for the schema) but agnostic to the identity provider, which the host
wires separately, so the same provisioning serves any IdP.

## Configuring the bootstrap

`DeploymentBootstrapOptions` is a CTJ type generated from a checked-in JSON Schema
(`deployment-bootstrap-options.json`), so a deployment supplies its configuration **as JSON**, validated against a
published schema ([ADR 0039](../adr/0039-api-first-openapi-source-of-truth.md)), rather than as a hand-authored
record. The options lift the demo-specific values out of hard-coding: the genesis-admin group and scopes, the
label-ordering taxonomy, the identity claim type, self-elevation eligibility, the OIDC and secret-store wiring,
and the optional access-approval system workflow. A configurable deployment (a ZeroFailed task, appsettings, or an
environment-injected blob) binds the options and calls `ProvisionAsync`.

## The example seed

A sample runs the real bootstrap **and** the example seed, gated by one switch. The composition root runs
`ProvisionAsync` always, and runs the example seed and the live sample run only when `seedExampleData` is set
(read from `ControlPlane:SeedExampleData`, injected by the AppHost's own flag), so all demo fiction is on one
switch end to end. Turning it off gives a bootstrap-only deployment, real setup with no demo content, that mirrors
production.

## See also

- The [auth and authorization guide](auth-and-authorization.md) for the security the bootstrap seeds.
- [ADR 0046](../adr/0046-deployment-bootstrap-example-seed-split.md) for why the two seams are separate.
