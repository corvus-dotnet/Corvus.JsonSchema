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

### W2 — Seed the governance data (medium) — authored **into** the W4 structure — **DONE**
`production` (+ `staging`) environments; environment administrators (`arazzo-admins` per env, the
key unblock); register the four sources in the registry; ≥1 availability entry; a pending access
request + a pending promotion request; `environment` stamped on the live runs. All as **example
seed** (`IExampleSeed`), not bootstrap.

**Done** — extended `ExampleSeedContext` with the five governance stores (`IEnvironmentAdministratorStore`,
`ISourceStore`, `IAvailabilityStore`, `IAccessRequestStore`, `IAvailabilityRequestStore`) and `ArazzoExampleSeed.SeedAsync`:
seeds `development`/`staging`/`production` environments; establishes `sys:group=arazzo-admins` as administrator of each
(`SecuredEnvironmentAdministration.EstablishAsync` — the key unblock); registers onboarding/ledger/kyc (OpenAPI) +
notifications (AsyncAPI) from their real specs (`RegisteredSource.Draft`); makes `onboard-customer` v1 "Available in"
production; seeds a pending access request (alice → run scopes) and a pending promotion request (alice → onboard-customer
v2 to production). Live runs already pin `development` (S2). Demo host builds warning-free.

**Composition live-verification (relaunch on real Postgres, `dotnet run --launch-profile http`,
`ASPIRE_ENABLE_CONTAINER_TUNNEL=false`).** The whole W1–W4 + W2 stack confirmed end to end against the running
composition: control plane `/alive`+`/health` = 200 (no exit-134 crash); **26 workflowstore tables** created by
`PostgresControlPlaneDeployment.ProvisionAsync` (deployment library, live); **2 security bindings** (the idempotency
fix — read-all + genesis-admin, not 4) + 3 rules; W2 governance seed present — **3 environments**, **3 environment
administrators** (arazzo-admins per env), **4 sources**, **1 availability** entry, **1 access request**, **1
promotion request**; two-process topology healthy — runner `/health` = 200 (Vault AppRole), 1 registration + 1
auto-authorization; DemoData 6 runs / 6 catalog versions / 8 source credentials.

### W3 — Expand the live console (medium) — **DONE**
Mount the already-built, already-wired admin panels in `wwwroot/index.html` (Runners, Environments,
Credentials lifecycle, Security grants/rules/access-overview, Runner-authorizations,
Availability-requests inbox); fix the administrators-editing scope gap (console `arazzo-catalog`
lacks `administrators:read/write`); **build the one missing** sources-registry governance component.

**Done** — the console `index.html` shell was rewritten into a grouped tab layout organised by **user goal**:
**Runs, Catalog, Environments, Sources, Credentials, Runners, Security** (Grants / Rules / Access-overview),
**Approvals** (Access / Availability / Runner-authorizations — the approver queues, work the user must action) and
**Requests** (the user's own Access / Availability requests). The two request panels are mounted twice, locked to
`view="queue"`/`view="mine"` with their internal toggle hidden (`hide-view-tabs`). The **Approvals** tab and each of
its sub-tabs carry an **auto-refreshing badge** of outstanding Pending items (polls the three approver queues every
15s; 403 → 0), so a user sees at a glance whether there is work for them. Self-contained panels wired via
`.fetch = authFetch`; the security-authoring panels via a shared `.client`. Admin scope gap fixed by adding
`administrators:read/write` to `<arazzo-catalog>`'s `scopes`. Built the missing **`sources-panel`** (`<arazzo-sources>`,
ported from `environments-panel`) + the two missing client methods (`updateSource`, `deleteSource`).
**Every panel now uses the Runs/Catalog fill model** (`:host` fills, list scrolls internally via `.tablescroll` +
sticky header + pinned pager; master-detail panels split the list and a closeable, independently-scrolling detail, with
the empty detail-pane collapsing so the list fills full width). **Live-verified** (Playwright, signed in as arazzo-admin
against the running composition): all tabs + sub-navs render the real seeded data (Runs 6, Environments 3, Sources 4,
Grants 2), the Approvals badge shows **2** (1 access + 1 availability pending) with per-sub-tab counts, master-detail
select/close works, no functional JS errors; 208 UI unit tests green.

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
- **W4c DONE** (commit `5271248485`): `ExampleSeed.cs` in the sample — `IExampleSeed` + `ExampleSeedContext`
  (stores carried as backend-agnostic interfaces) + `ArazzoExampleSeed`. `SeedAsync` writes the catalogued versions
  (via `DemoData`), the source-credential *reference* bindings, and the §18 developer sandbox environment;
  `RunLiveSampleAsync` runs the live onboarding run post-startup. `Program.cs` collapsed to: real bootstrap always;
  the example seed + `RunnerAutoAuthorizationService` + live run gated on `bootstrapOptions.SeedExampleData` (set
  `true` in the demo's `DeploymentBootstrapOptions` JSON). Demo host builds warning-free.
  - **W4c follow-up DONE** — grounding first showed the originally-framed fold ("schema-prep + OIDC/claims + Vault
    trust → `DefaultDeploymentBootstrap`") is the *wrong shape*: schema-prep is backend-specific (each backend's
    `PrepareAsync` differs), OIDC/claims is IdP-specific DI/middleware (the reusable parts are already
    `AddArazzoControlPlaneAuthorization`/`UseArazzoControlPlaneAntiForgery`), and Vault trust is the AppHost provisioner
    (W4d). So instead: a new **Postgres deployment library**
    `Corvus.Text.Json.Arazzo.Durability.ControlPlane.Deployment.Postgres` — `PostgresControlPlaneDeployment.ProvisionAsync`
    creates all 17 control-plane stores' schema + runs `DefaultDeploymentBootstrap.BootstrapSecurityAsync` in one call.
    **Postgres-coupled but IdP-agnostic** (no Keycloak/ASP.NET ref — Postgres *and whatever IdP*). The sample's
    `Program.cs` collapses the 17 `PrepareAsync` + inline bootstrap into one `ProvisionAsync(dataSource, bootstrapOptions)`.
  - **Idempotency fix (found by verifying `ProvisionAsync` on a throwaway Postgres 18.3 container):**
    `DefaultDeploymentBootstrap` appended the read-all + genesis-admin bindings on every call (bindings `2 → 4` on a
    re-run; masked in the demo by the fresh ephemeral DB, but wrong for a restart-safe deployment library). Fixed by
    deduping on the binding *subject* (claim type + value), mirroring how the rule seed adds only missing names. Verified
    `2 → 2` on the container, and guarded by a container-free regression test
    (`DefaultDeploymentBootstrapTests`, `InMemorySecurityPolicyStore`, 3 tests green).
  - **Deployment rolled to ALL backends** — every state-store backend is a first-class deployment target, so the
    provisioning capability is uniform, not Postgres-only. Audit first confirmed all 8 backends implement the full 17
    control-plane store set. Added `…ControlPlane.Deployment.<Backend>` for **Sqlite, MySql, SqlServer, Redis, Mongo,
    Cosmos, NatsJetStream, AzureStorage** (8 packages), each a port of the Postgres one: `ProvisionAsync(handle,
    options)` = that backend's schema-prep + the shared backend-agnostic `BootstrapSecurityAsync`. All take a `string`
    connection handle (Mongo/Cosmos add `databaseName="arazzo"`). Two backend-specific realities handled: **Mongo** is
    schemaless, so it preps only the 9 stores that need explicit index setup (the other 8 auto-create); **AzureStorage**'s
    security store is non-disposable. The **seeding** did NOT need per-backend work — it is already backend-agnostic (over
    `ISecurityPolicyStore`). All 8 build warning-free; runtime-verified end to end (schema + seed + idempotency) on
    **Postgres**, **Sqlite**, and **NatsJetStream**; the rest are build-verified structural ports.
- **W4d DONE** — split the two interleaved files along the same real/example seam:
  - **Vault provision script** (`AppHost.cs`): `approleTrustScript` (real — policy + AppRole role + wrapped SecretID,
    every deployment) vs `exampleSecretSeedScript` (the `vault kv put …=demo-*-key` puts) vs an infra completion tail,
    assembled with the demo secrets gated on a new AppHost-level `seedExampleData` config flag (`SeedExampleData`,
    default true; the infra counterpart to the runtime `seedExampleData`).
  - **Realm** (`realms/`): base `arazzo-realm.json` (realm + groups + OIDC clients) vs `arazzo-users-0.json` (the demo
    personas arazzo-admin/alice), using Keycloak's native directory-import `{realm}-realm.json` + `{realm}-users-0.json`
    convention. Verified against a standalone Keycloak 26.6 container (the version Aspire.Hosting.Keycloak 13.4.6
    pulls): both users import into the `arazzo` realm with correct group membership; base groups + clients intact.
  - **W4d follow-up DONE** — the AppHost `SeedExampleData` flag now drives the control-plane example seed and the
    Keycloak persona import too, so **one switch governs all example seeding end to end**. The AppHost injects
    `ControlPlane__SeedExampleData` onto the control plane (which reads it in place of the previously-hardcoded
    `seedExampleData: true` in its bootstrap-options JSON). The realm import was moved off the whole-`realms/`-directory
    import onto per-file imports, split along the real/example seam: `arazzo-realm.json` + `arazzo-users-0.json` (the
    **grantee-directory service account** — real infra the §16.5.4 resolver needs) import **always**; the demo personas
    (`arazzo-admin`, `alice`) moved to a new **`arazzo-users-1.json`** that imports **only when the flag is on** (the
    identity-lookups fix had put the real service account into the personas file, so gating the whole file would have
    broken the directory — hence the split). **Live-verified both states** (two full composes): with the flag **true**,
    Keycloak imports all users (kcadm shows `arazzo-admin` + `alice`; `directorySearch:true`), auth enforces, and the
    governance seed lands (3 environments, 6 catalog versions); with the flag **false**, Keycloak imports the realm +
    service account only (kcadm users `[]`, `directorySearch:true` still), the example seed is off (`environments` +
    `catalog` empty), yet the **real** deployment bootstrap still ran (anon → 401, `demo-admin-key` → 200 via the genesis
    grant). Multi-file `WithRealmImport` composition confirmed to work (Keycloak healthy, no import crash) on the
    pinned `Aspire.Hosting.Keycloak 13.4.6`.

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
