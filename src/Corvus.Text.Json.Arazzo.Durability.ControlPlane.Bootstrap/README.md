# Corvus.Text.Json.Arazzo.Durability.ControlPlane.Bootstrap

The **real, deployment-agnostic bootstrap** for an Arazzo durability control plane — the security,
identity and policy seeding *any* deployment must run before serving traffic, driven entirely by a
JSON-Schema-generated `DeploymentBootstrapOptions` so a deployment configures it declaratively.

This is deliberately separate from the **example seeding** a sample layers on top (an `IExampleSeed`
of fictional demo content — sample workflows, personas, credential references). A production
deployment runs `IDeploymentBootstrap` and never the example seed. Keeping them apart is what lets
the real bootstrap become a configurable deployment step (e.g. a ZeroFailed task) that consumes the
JSON options, with demo content strictly on the side.

## Configuration is JSON

`DeploymentBootstrapOptions` is generated from [`deployment-bootstrap-options.json`](./deployment-bootstrap-options.json)
by the Corvus.Text.Json source generator — it is a JSON value, not a hand-authored C# record. A
deployment binds it from configuration (ZeroFailed, `appsettings`, an env-injected blob, a secret
store), validated against the schema. Every value that is specific to a deployment lives here:

| Option | Purpose |
|---|---|
| `genesisAdminGroup` (required) | The `identityClaimType` value granted genesis administration (§16.2 tier 3): all scopes + unrestricted reach. |
| `genesisScopes` | The capability scopes the genesis administrator holds (e.g. `catalog:read`, `runs:write`). |
| `identityClaimType` | The token claim carrying group/identity memberships (default `groups`); also the `sys:` identity source (§16.5.5). |
| `labelOrderings` | The ordered tag dimensions (§14.2), e.g. `classification → [public, internal, confidential, restricted]`. |
| `internalTagPrefix` | The reserved prefix for internal, client-invisible tags (§14.3), default `sys:`. |
| `selfElevationGroups` | Groups whose members may JIT self-elevate without an approver (§16.5.3). |
| `seedExampleData` | When true, the deployment additionally runs its `IExampleSeed`. |

## Usage

```csharp
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Bootstrap;

IDeploymentBootstrap bootstrap = new DefaultDeploymentBootstrap();

// options bound from configuration as JSON, validated against the schema.
await bootstrap.BootstrapSecurityAsync(securityPolicyStore, options);

// The ordered tag dimensions the row-security policy is constructed with.
SecurityLabelOrderings orderings = DefaultDeploymentBootstrap.BuildLabelOrderings(options);
```

`BootstrapSecurityAsync` seeds the editable bootstrap rules (§14.2), the read-all shell binding, and
the genesis-administrator grant. The rules are idempotent; the schema-prep, OIDC/claims wiring, and
credential/Vault trust bootstrap join here as the split proceeds.
