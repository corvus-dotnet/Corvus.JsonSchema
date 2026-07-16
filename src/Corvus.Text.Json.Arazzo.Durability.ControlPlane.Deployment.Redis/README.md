# Corvus.Text.Json.Arazzo.Durability.ControlPlane.Deployment.Redis

Redis deployment provisioning for an Arazzo durability control plane. It provisions a deployment in
one call: it creates the schema for **every** store the control plane owns, then runs the
deployment-agnostic security bootstrap (the editable §14.2 rules, the read-all shell binding, and the
§16.2-tier-3 genesis-admin grant).

```csharp
await RedisControlPlaneDeployment.ProvisionAsync(configuration, options);
```

`options` is a [`DeploymentBootstrapOptions`](../Corvus.Text.Json.Arazzo.Durability.ControlPlane.Bootstrap/README.md) —
a JSON-Schema-generated value a deployment binds from configuration (ZeroFailed, appsettings, a secret
store, an env-injected blob).

## What it is, and what it is not

- **Coupled to Redis.** Schema creation is inherently backend-specific — each backend's `PrepareAsync`
  has a different signature — so a Redis deployment owns the Redis schema. Other backends get their
  own deployment package.
- **Identity-provider agnostic.** This package never references Keycloak, OIDC, or any ASP.NET wiring. It
  provisions the store and the security policy; the host wires whatever identity provider it uses
  (Keycloak, Entra ID, Auth0, …) separately in its composition root. Redis **and** *whatever IdP*.
- **Idempotent.** Every operation is create-if-absent / upsert, so it is safe to run on every
  startup.

## Layering

| Package | Responsibility |
| --- | --- |
| `…ControlPlane.Bootstrap` | Backend-agnostic security/identity/policy seeding from `DeploymentBootstrapOptions`. |
| `…ControlPlane.Deployment.Redis` (this) | Redis schema for the full control-plane store set + the bootstrap above, in one call. |
| host composition | The runtime store graph, the HTTP surface, and the identity-provider wiring. |

`ProvisionSchemaAsync(configuration)` is available on its own for hosts whose composition interleaves
store construction between schema creation and policy seeding.
