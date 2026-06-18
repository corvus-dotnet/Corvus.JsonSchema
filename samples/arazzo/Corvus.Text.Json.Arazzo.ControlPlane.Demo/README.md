# Arazzo Control Plane — live demo host

A self-contained ASP.NET Core host that runs the **real** Arazzo control-plane server over a fresh-on-startup
SQLite store, seeds it with demo workflows + runs, and serves the build-free web UI from the same origin.

This host is one project in the self-contained [`samples/arazzo/`](../) composition. The
[**Aspire AppHost**](../Corvus.Text.Json.Arazzo.ControlPlane.Demo.AppHost) is the composition root that launches
it alongside the [**runner**](../Corvus.Text.Json.Arazzo.Runner.Demo) (the execution-host process, which shares
this host's durability store) and a locally-runnable **HashiCorp Vault** for source credentials (§13) — with
Keycloak (OIDC) and optionally Postgres next, and the Aspire dashboard as the OpenTelemetry viewer.

> **Credentials are reference-only here.** This host stores the `vault://…` credential *reference* (the §13
> invariant — it never touches secret material); a separate provisioner seeds Vault and grants the runner a
> read-only token; the runner is the only secret consumer. See design §13.5.

## Run it

**Under Aspire (recommended)** — gives you the dashboard (traces, logs, metrics) and one launch point for the
whole composition:

```bash
cd samples/arazzo
aspire run            # or: dotnet run --project Corvus.Text.Json.Arazzo.ControlPlane.Demo.AppHost
```

The dashboard prints a URL; the `controlplane` resource links straight to the demo (UI at `/ui`).

**Standalone** — just this host, no dashboard (the OpenTelemetry exporter is a no-op when no OTLP endpoint is
configured), useful for a quick look:

```bash
dotnet run --project samples/arazzo/Corvus.Text.Json.Arazzo.ControlPlane.Demo
```

Either way, open the printed URL. The SQLite file is deleted and re-seeded every time the host starts, so you
always begin from the same demo state.

## What it shows

Unlike the UI kit's static demo (which uses an in-browser mock), this talks to the genuine API under
`/arazzo/v1`, so the schema metadata and the `POST …/validate` round-trip are produced by the real metadata
generator and `Corvus.Text.Json.Validator`. In particular:

- **Catalog** — `onboard-customer` (active) and `nightly-reconcile` (an Obsolete v1 plus an active v2).
- **Runs** across every status (Faulted, Suspended, Running, Completed), with faults sitting on the steps whose
  outputs carry composite shapes.
- **Typed output editor + validation** — open a faulted run → *Resume…* → *Skip* → tick *Record outputs*. The
  editor renders the step's typed outputs, including a discriminated `oneOf` **union** (`verifyIdentity.evidence`),
  a `prefixItems` **tuple** (`flagDiscrepancies.range`), and an `additionalProperties` **map**
  (`provisionResources.tags`) — and validates them against the true JSON Schema as you type.

## Access requests &amp; approval (§16.5) — a worked example

By default the demo is **open** (no authentication), so the build-free UI just works. Turn on the real
authorization model — Keycloak OIDC + the §16.5 access-request flow — with `ControlPlane__RequireAuthorization=true`
(the Aspire AppHost sets this for you once Keycloak is wired). With it on, the demo demonstrates the design's
**least-privilege** stance end to end:

- **Read is universal; acting is earned.** A bootstrap binding grants every authenticated principal *read* over the
  whole control plane. Write / run reach is **deny-by-default** — a principal can browse the catalog and runs but
  cannot trigger, resume, or administer anything until it holds a **stored grant**.
- **Capability = `claims ∪ stored entitlements`.** The [`KeycloakClaimsTransformer`](./KeycloakClaimsTransformer.cs)
  maps a principal's Keycloak `groups` to a standing **read** baseline, then *unions in the capability scopes of its
  active, time-boxed per-principal grants* (the §16.5.2 Decision-A resolution, read from the one
  `PersistentRowSecurityPolicy` the durable server also uses for row reach). Elevated capability is **never ambient**:
  no group — not even `arazzo-admins` — confers standing write. It arrives solely through an approved request (or, for
  an eligible principal, JIT self-elevation).
- **Administrators are the workflow's own.** Each workflow's §15 administrator set is established by the submitter of
  its first version — here the `arazzo-admins` group (see [`DemoData`](./DemoData.cs)). Only an administrator of the
  *target* workflow may approve a request for it, and `arazzo-admins` members are additionally **eligible to
  self-elevate** (JIT activation, no human approver).

**The story.** *alice* (Keycloak group `payments`) wants to trigger `onboard-customer`. She signs in through the BFF
and can read the catalog — but `Trigger` is denied. She submits an access request for run access to that workflow. A
member of `arazzo-admins` (an administrator of `onboard-customer`) approves it. The approval writes a stored grant
keyed on alice's subject, carrying the `runs:write` scope and a **per-workflow reach rule** (`sys:workflow ==
'onboard-customer'`) that matches because the catalog stamps each version with that internal tag. On alice's next
request the transformer unions `runs:write` into her scopes and the row policy scopes her reach to exactly that one
workflow — *alice may trigger `onboard-customer`, and only it*. An `arazzo-admins` member skips the queue: they
self-elevate the same grant on demand.

That `claims ∪ stored entitlements` mapping — and the no-ambient-elevation invariant — is pinned by
[`KeycloakClaimsTransformerTests`](../Corvus.Text.Json.Arazzo.ControlPlane.Demo.Tests/KeycloakClaimsTransformerTests.cs),
which exercises it against the real resolver.

## How it's wired

`Program.cs` deletes the temp SQLite file, connects the SQLite state + catalog stores (the catalog store bakes
schema metadata via `WorkflowSchemaMetadataProvider`), seeds from the spec files in [`specs/`](./specs), maps the
control-plane API under `/arazzo/v1`, and serves the UI source (`web/arazzo-control-plane-ui`) at `/ui` plus the
demo page from `wwwroot`.

The demo workflows and their OpenAPI sources are real, editable files under [`specs/`](./specs) — the same
documents are the catalog's source descriptions (and will drive the generated backend services).

> **Note.** The `/validate` endpoint compiles a JSON Schema at runtime, which is why this project sets
> `<PreserveCompilationContext>true</PreserveCompilationContext>` — a hosting service needs the same.
