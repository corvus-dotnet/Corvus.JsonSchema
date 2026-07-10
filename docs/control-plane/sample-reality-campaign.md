# Sample reality campaign — governance reachability + the seeding split

Making the live Arazzo control-plane sample (`samples/arazzo/`) *completely* real: every capability
the platform ships must be **reachable** and **exercised** in the running sample, and all data
seeding must be split into a **real, config-driven deployment bootstrap** (production-shaped, the
basis for a configurable ZeroFailed deployment) versus **example seeding** (demo fiction only).

This continues the "reality gaps" arc (the execution core — Postgres, Vault AppRole, NATS, three
real services, six live-executed workflows — is already real). It was scoped from a three-part
survey (capability×UI reachability, seeding inventory, not-real/unexercised) on 2026-07-10.

## Diagnosis

The gaps are concentrated in the **governance surface** and are shallow:

1. **Five governance stores silently run in-memory.** `ControlPlane.Demo/Program.cs`'s
   `MapArazzoControlPlane` omits `availabilityStore`, `availabilityRequestStore`, `sourceStore`,
   `environmentAdministratorStore`, and `observedIdentityStore`; the server falls back to a fresh
   `InMemory*` for each. A `Postgres*Store` **already exists** for all five (uniform
   `PrepareAsync(dataSource)` / `ConnectAsync(dataSource, timeProvider?, ct)`). Ephemeral, empty,
   unshared with the runner. Omitting `availabilityStore` also passes `null` to the catalog
   handler, so run-creation availability gating is skipped.
2. **No environment has an administrator** (that store is one of the five, and empty), which
   *structurally* blocks promotion and runner-authorization through the real API — the demo works
   only because `RunnerAutoAuthorizationService` writes to the runner-auth store directly.
3. **The live console mounts 3 of ~11 panels.** `wwwroot/index.html` wires Runs, Catalog, Access.
   Every other admin component (grants & rules, environments, credentials lifecycle, runner roster,
   runner-authorizations, promotion-approval inbox) **already exists and is wired** but appears only
   in the static **mock** `demo/index.html`. Only **one** component is genuinely missing: a
   sources-registry governance panel (only source-acquisition-into-a-working-copy exists).
4. **Seed inconsistencies.** Credentials seeded for `production` + `development` but only the
   `development` environment record exists (promote-to-production would 404); live runs carry no
   `environment`; no pending access/promotion request exists to exercise the approver inboxes.
5. **Seeding is scattered across five mechanisms** — the AppHost Vault provision script, the
   Keycloak `arazzo-realm.json`, the `Program.cs` inline region, `DemoData`, and per-service
   stores — with real bootstrap and demo fiction physically interleaved.

## Workstreams and sequence

**Agreed sequence: W1 → W4 → W2 → W3** (wire stores first; then stand up the bootstrap/seed split
*before* writing the new governance seed, so new seed lands in the refactored structure, not inline
then moved; console last). W5 runs alongside.

### W1 — Wire the five omitted Postgres stores (small)
Add `PrepareAsync` + `ConnectAsync` for `PostgresAvailabilityStore`,
`PostgresAvailabilityRequestStore`, `PostgresSourceStore`, `PostgresEnvironmentAdministratorStore`,
`PostgresObservedIdentityStore`, and pass all five to `MapArazzoControlPlane`. Restores durable,
shared governance stores + run-creation availability gating. One focused change; own commit.

### W4 — The seeding split (large, architectural)
Extract the **real** bootstrap into a new **product library**
`Corvus.Text.Json.Arazzo.Durability.ControlPlane.Bootstrap` (alongside `Security/SecurityBootstrap`),
leaving example seeding in the sample. Design below. This is the ZeroFailed-configurable structure.

### W2 — Seed the governance data (medium) — authored **into** the W4 structure
`production` (+ `staging`) environments; environment administrators (`arazzo-admins` per env, the
key unblock); register the four sources in the registry; ≥1 availability entry; a pending access
request + a pending promotion request; `environment` stamped on the live runs. All as **example
seed** (`IExampleSeed`), not bootstrap.

### W3 — Expand the live console (medium)
Mount the already-built, already-wired admin panels in `wwwroot/index.html` (Runners, Environments,
Credentials lifecycle, Security grants/rules/access-overview, Runner-authorizations,
Availability-requests inbox); fix the administrators-editing scope gap (console `arazzo-catalog`
lacks `administrators:read/write`); **build the one missing** sources-registry governance component.

### W5 — Parity + doc truth (small)
Debug-run CLI verb (`arazzo-runs debug-runs {start·get·resume·inject-message·cancel·delete}`) —
**built**. Refresh stale docs: `ControlPlane.Demo/docs/live-execution.md` (SQLite→Postgres,
`/svc`→real services, InMemory→NATS) and the `Program.cs:194` access-request comment.

## W4 design — the bootstrap/seed split

**Status / resume state (2026-07-10):**
- **W4a DONE** (commit `ada3327fc0`): new library `Corvus.Text.Json.Arazzo.Durability.ControlPlane.Bootstrap`
  with `IDeploymentBootstrap` + `DefaultDeploymentBootstrap`; `DeploymentBootstrapOptions` is a
  JSON-Schema-generated CTJ type (`deployment-bootstrap-options.json`). `BootstrapSecurityAsync` seeds the
  §14.2 rules + read-all shell binding + §16.2-tier-3 genesis-admin grant; `BuildLabelOrderings(options)`.
- **W4b DONE** (commit `96ce6f1a29`): the demo host (`ControlPlane.Demo/Program.cs`) constructs
  `DeploymentBootstrapOptions` AS JSON and calls the library for its security bootstrap (replacing the inline code).
- **W4c NEXT** — extract `IExampleSeed` for the demo fiction. Move into it (currently inline in the demo host):
  `DemoData.SeedAsync` (catalog versions) + `DemoData.RunLive*` (live runs); the source-credential *reference*
  bindings + their Vault demo values (`Program.cs` ~301-321); the `development` environment instance (~327-331) +
  `RunnerAutoAuthorizationService`; the dev API keys. Then collapse `Program.cs` to
  `await bootstrap.BootstrapSecurityAsync(...); if (options.SeedExampleData) await exampleSeed.SeedAsync(...)`.
  Fold the *remaining* real bootstrap in as follow-ups: schema-prep (the `PrepareAsync` block), OIDC/BFF/claims
  wiring, Vault AppRole trust. `IExampleSeed` lives in the sample (never a production dependency).
- **W4d** — split the two interleaved files: the Vault provision script (AppRole trust vs `vault kv put` demo
  values) and `arazzo-realm.json` (base realm + OIDC clients vs demo personas alice/payments/onboarding).

Two seams. A production deployment runs **only** `IDeploymentBootstrap`, driven entirely by config;
the sample additionally runs `IExampleSeed`.

**Config comes through JSON model generation (not hand-rolled).** `DeploymentBootstrapOptions` (and any
nested option shapes) is a **generated CTJ type** from a checked-in JSON Schema — the same
`no-handrolled-records-use-codegen-jsonschema` rule the rest of the platform follows — so the whole
options surface is strongly-typed, schema-validated, and bindable straight from a JSON document. A
deployment (ZeroFailed, appsettings, a secret store, an env-injected blob) supplies its config **as
JSON** validated against that schema; nothing is a hand-authored C# record. This also gives the
config surface a published schema ZeroFailed can template against.

```csharp
// REAL — every deployment runs this; idempotent; every demo-specific value is config.
public interface IDeploymentBootstrap
{
    ValueTask BootstrapAsync(NpgsqlDataSource store, DeploymentBootstrapOptions options, CancellationToken ct);
}

// DEMO — fictional content; a production deployment never references it.
public interface IExampleSeed
{
    ValueTask SeedAsync(ExampleSeedContext context, CancellationToken ct);
}
```

Sample composition root collapses to:

```csharp
await deploymentBootstrap.BootstrapAsync(dataSource, options, ct);   // REAL, from config
if (options.SeedExampleData)
    await exampleSeed.SeedAsync(context, ct);                        // DEMO only
```

**`IDeploymentBootstrap` covers (with the config each item needs in `DeploymentBootstrapOptions`):**

| Bootstrap step | Config it lifts out of hard-coding |
|---|---|
| Schema prep (the `PrepareAsync` set, now incl. the W1 five) | none (fixed DDL) |
| Security bootstrap rules + read-all shell binding | optional rule overrides |
| Genesis-admin grant | `GenesisAdminGroup` (was `arazzo-admins`), `GenesisScopes` (default `ControlPlaneScopes.All`) |
| Label-ordering taxonomy | `LabelOrderings` (was `classification:[public…restricted]`) |
| Entitlement resolver / identity dimension | `IdentityClaimType` (was `groups`), `InternalTagPrefix` |
| Self-elevation eligibility | `SelfElevationGroups` |
| OIDC / BFF / claims wiring | `OidcRealm`, `OidcAuthority`, `UiClientId`/secret ref, `SubjectClaimType`, `ClaimToScopeMap` |
| Vault AppRole trust (policy, role, secure introduction) | `SecretPathPrefix` (was `secret/arazzo/*`), `RunnerRoleId`, TTLs, handoff path |
| OIDC realm shell + app-client registrations | realm name, client ids/secret refs, redirect patterns |

**`IExampleSeed` covers:** the demo catalog versions + live runs (`DemoData`), the demo
credential-reference bindings + their `vault kv put …=demo-*-key` values, the `development`/
`production`/`staging` environment instances, environment-admin seeding, the source-registry
entries, availability + pending-request seeds, `RunnerAutoAuthorizationService`, the dev API keys,
and the realm personas (alice, payments/onboarding groups).

**Also split the two interleaved files:** (1) the Vault provision script — AppRole policy/role/
secure-introduction (real) apart from the three `vault kv put` demo values (example); (2)
`arazzo-realm.json` — a base realm (shell + `arazzo-admins` + OIDC clients + groups mapper = real)
apart from a demo overlay (alice, domain groups, seed passwords = example), imported only in the
sample.

**Cross-references that must derive from one config value (else silent drift):**
`arazzo-admins` (realm, genesis binding, self-elevation, dev-key group) → `GenesisAdminGroup`;
`vault://secret/arazzo/<source>#api-key` (Program.cs) must match the AppHost `vault kv put`
paths and the runner's read-only policy path → one `SecretPathPrefix`; the `development`
environment name repeats in Program.cs / `RunnerAutoAuthorizationService` / AppHost → one config
value.

## Per-slice gate (every workstream)

Ground → make the change → warning-free build (`0 Warning(s)`) → run affected tests → rebuild &
relaunch the Aspire composition → verify the capability live → update this doc → commit when asked.
One slice at a time; no batching.
