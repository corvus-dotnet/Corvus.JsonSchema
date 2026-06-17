# Arazzo Control Plane — live demo host

A self-contained ASP.NET Core host that runs the **real** Arazzo control-plane server over a fresh-on-startup
SQLite store, seeds it with demo workflows + runs, and serves the build-free web UI from the same origin.

This host is one project in the self-contained [`samples/arazzo/`](../) composition. The
[**Aspire AppHost**](../Corvus.Text.Json.Arazzo.ControlPlane.Demo.AppHost) is the composition root that launches
it alongside the [**runner**](../Corvus.Text.Json.Arazzo.Runner.Demo) (the execution-host process, which shares
this host's durability store) — with the locally-runnable Vault / Keycloak / Postgres dependency containers next,
and the Aspire dashboard as the OpenTelemetry viewer.

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

## How it's wired

`Program.cs` deletes the temp SQLite file, connects the SQLite state + catalog stores (the catalog store bakes
schema metadata via `WorkflowSchemaMetadataProvider`), seeds from the spec files in [`specs/`](./specs), maps the
control-plane API under `/arazzo/v1`, and serves the UI source (`web/arazzo-control-plane-ui`) at `/ui` plus the
demo page from `wwwroot`.

The demo workflows and their OpenAPI sources are real, editable files under [`specs/`](./specs) — the same
documents are the catalog's source descriptions (and will drive the generated backend services).

> **Note.** The `/validate` endpoint compiles a JSON Schema at runtime, which is why this project sets
> `<PreserveCompilationContext>true</PreserveCompilationContext>` — a hosting service needs the same.
