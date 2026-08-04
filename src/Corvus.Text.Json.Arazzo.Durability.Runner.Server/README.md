# Corvus.Text.Json.Arazzo.Durability.Runner.Server

An ASP.NET Core server for the [Arazzo runner API](../../docs/arazzo/reference/arazzo-runner.openapi.json), generated
from its OpenAPI 3.2 description. It is the only path from a runner to durable run state
([ADR 0065](../../docs/arazzo/adr/0065-control-plane-owns-store-runners-encrypt-payload.md)): the control plane owns the
store exclusively, and a runner holds no store credential at all.

It is a **separate component from the governance API**, not a section of it, so execution load never rides the
governance request path and the runner API is scaled and kept available ahead of governance.

```csharp
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Runner.Server;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpContextAccessor();   // the machine principal is read from the current request

WebApplication app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();

// The environments each machine principal may execute runs for, read from the runner-authorization records an
// administrator decides (ADR 0027) rather than declared in configuration.
var bindings = new RunnerAuthorizationBindings(runnerAuthorizations, environments);

// store: the durable run store the control plane owns. It must also implement IWorkflowDispatchIndex.
app.MapGroup("/arazzo/runner/v1").MapArazzoRunnerApi(store, bindings);

app.Run();
```

A deployment that runs its own runners and knows them by name can supply
`DeclaredRunnerEnvironmentBindings` instead, a fixed map from principal to environments. The two answer the same
question from different sources of truth, which is why the surface is an interface.

## What the surface is

| Operation | What it does |
|---|---|
| `POST /claims` | Takes a claimable run and its lease in one operation, or `204` when nothing is claimable. |
| `POST /timerClaims` | Takes the runs whose durable timer has fired, each with its lease. The cutoff is this server's clock, never a request parameter. |
| `POST /messageClaims` | Takes the runs awaiting a message on a channel, each with its lease. The payload is not sent and never reaches the store. |
| `POST /runs/{runId}/lease/renewal` | Extends a held lease, so a long advance does not have its run reclaimed underneath it. |
| `DELETE /runs/{runId}/lease` | Hands a run back so another runner may claim it without waiting for the lease to expire. |
| `GET /hostedVersions` | Lists the versions this runner may execute, resolved from its bindings and each environment's availability. |
| `GET /versions/{baseWorkflowId}/{versionNumber}` | Reads one such version and its content hash. |
| `GET /versions/{baseWorkflowId}/{versionNumber}/documents/{documentName}` | Pulls one package document (executor, manifest, signature) as opaque octets. |
| `GET /runs/{runId}/checkpoint` | Reads the run's checkpoint as opaque octets, with the sequence the store has persisted. |
| `PUT /runs/{runId}/checkpoint` | Replaces the run's row under compare-and-swap, accepting only the persisted sequence plus one. |

## Who a request is

The runner API mints no credential of its own. A runner authenticates with the machine principal it already has, and
`RunnerApiOptions.PrincipalClaimType` names the claim that carries it (`sub` by default). That value is the lease owner
and the subject of every binding lookup, and nothing in a request body or header contributes to it. A client-supplied
owner would let a compromised runner rename itself into another's leases, so there is nowhere to supply one.

`IRunnerEnvironmentBindings` resolves the environments a principal may execute runs for, per request rather than per
process, which is what gives runner revocation its effect.

`RunnerAuthorizationBindings` is the governed implementation. Only an `Authorized` record binds — Pending,
Quarantined, and Revoked are all excluded — so revoking a runner stops it being offered work without the runner
having to cooperate. Resolution is cached for **thirty seconds at most**, and that bound is the property rather than a
tuning knob: the window is exactly the fence's latency, so a deployment asking for longer gets thirty seconds.

**A principal may not hold both a platform binding and a tenant one.** Holding both is a laundering route out of a
sealed environment into the never-sealed platform one, so such a principal resolves to nothing at all rather than to
one side of the pair. The rule is re-checked on every resolution, because a binding written before it was enforced, or
written concurrently, would otherwise pass unexamined.

## How refusals work

Every operation over a run presents its lease, and the lease check is reached before anything else. A run outside the
principal's bindings, one held by another runner, and one that does not exist all fail it identically and answer `409`,
so the surface cannot be used to learn which of the three it was.

A `404` therefore means something narrower and more serious: the lease is current and the row is nonetheless absent, so
it was deleted or is being withheld. A runner holding a non-terminal anchor entry for that run faults rather than
treating it as a fresh one.

## The one failure the save operation exists to prevent

A save proposes a sequence, and the server accepts only the persisted sequence plus one. It validates rather than
assigns, so the value is predictable to the writer when it authors the checkpoint and authoritative in the store
afterwards.

A save that loses that predicate answers `409` carrying the sequence that would be accepted, and **never** `204`. A
superseded save reported as success is indistinguishable from a durable write, which would leave a runner committed to a
checkpoint the store does not have. A retry is a byte-identical resend of the same sequence, not a fresh authoring.

## Regenerating

```bash
dotnet run --project src/Corvus.Json.Cli -f net10.0 -- \
  openapi-server docs/arazzo/reference/arazzo-runner.openapi.json \
  --rootNamespace Corvus.Text.Json.Arazzo.Durability.Runner.Server \
  --outputPath src/Corvus.Text.Json.Arazzo.Durability.Runner.Server/Generated
```

A change starts in the OpenAPI document, the `Generated/` code is regenerated, and only then is the handler implemented
against the new shape ([ADR 0039](../../docs/arazzo/adr/0039-api-first-openapi-source-of-truth.md)).