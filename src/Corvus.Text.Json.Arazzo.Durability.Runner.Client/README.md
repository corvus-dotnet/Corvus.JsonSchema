# Corvus.Text.Json.Arazzo.Durability.Runner.Client

The runner's client for the [Arazzo runner API](../../docs/arazzo/reference/arazzo-runner.openapi.json), generated from
its OpenAPI 3.2 description. A runner using this **binds no store SDK and holds no store credential**
([ADR 0065](../../docs/arazzo/adr/0065-control-plane-owns-store-runners-encrypt-payload.md)): it authenticates as its
own machine principal, and the control plane, which owns the store, performs every read and write on its behalf.

```csharp
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Runner.Client;
using Corvus.Text.Json.OpenApi.HttpTransport;

using HttpClient http = new() { BaseAddress = new Uri("https://runner-api.example.com/arazzo/runner/v1") };
await using HttpClientTransport transport = new(http);
await using ArazzoRunnerClient runner = new(transport);

if (await runner.TryClaimAsync(hostedVersions) is { } claimed)
{
    try
    {
        // The run loads and advances through the client's checkpoint store, exactly as it would over a
        // database-backed one. The run itself never learns the difference.
        using WorkflowRun? run = await WorkflowRun.ResumeAsync(runner.Checkpoints, claimed.RunId);
        if (run is not null)
        {
            await resume(run, cancellationToken);
        }
    }
    finally
    {
        await runner.ReleaseAsync(claimed.RunId);
    }
}
```

## The dispatch loop

`RunnerApiDispatcher` is that cycle as a loop, and is the API-backed counterpart to `WorkflowDispatcher`. Both present
the same `DispatchClaimableAsync(hostedVersions, resumer, cancellationToken)`, so a runner switching to the API keeps
its executor untouched.

```csharp
var dispatcher = new RunnerApiDispatcher(runner);
int dispatched = await dispatcher.DispatchClaimableAsync(hostedVersions, resumer, stoppingToken);
```

What it does **not** take is the point. There is no environment parameter, because the candidate set is intersected
with the principal's bindings server-side. There is no dispatch-authorization gate, because a revoked runner resolves
to no bindings and is offered nothing. There is no claimability re-check, because claiming is one operation rather
than a query followed by a lease. Each of those was a check a runner previously made about itself.

A pass is bounded (`MaximumRunsPerPass`, default 16) so a backlog cannot monopolise the runner, and it ends early if a
run comes back claimable without having advanced, rather than spinning on a run that is making no progress. Release
runs on every exit path including cancellation, because cancellation is how a runner shuts down and that is exactly
when giving the lease back matters.

## Resuming a waiting run

`RunnerApiWorker` is the counterpart to `WorkflowWorker`: a suspended run resumes when its durable timer fires or when
a message it awaits arrives, and neither now requires the runner to query the store's wait index.

```csharp
var worker = new RunnerApiWorker(runner);
int resumed = await worker.ResumeDueTimersAsync(hostedVersions, resumer, stoppingToken);
int delivered = await worker.DeliverMessageAsync(channel, correlationId, payload, hostedVersions, resumer, stoppingToken);
```

There is no due-time cutoff to pass, because the server resumes against its own clock. A runner naming one would be
asking for timers that have not fired, and a runner with a fast clock would do so without meaning to. A sweep is also
intersected with the runner's hosted versions, which the store-backed worker never did: it would hand a runner a due
run for a version it had not baked, which the runner could only fault.

**The payload never reaches the control plane.** These operations ask which runs a message can resume, not what the
message said, so the runner keeps the only copy and hands it to each resumed run itself.

**Correlation absence is a wildcard on either side.** A message carrying no correlation id reaches every run awaiting
the channel, and a run awaiting no particular correlation is reached by any message on it. Only two correlations that
are both present and different fail to match. That is the store's rule, pinned by the conformance suite across every
backend; the client passes the correlation through unchanged rather than reinterpreting it.

## Loading an executor without a catalog credential

`RunnerApiArtifactSource` serves the version's content hash and its package documents over the API, so it drops
straight into `HostedWorkflowResumer` or `LoaderHostedWorkflowResolver` in place of a catalog store.

```csharp
var artifacts = new RunnerApiArtifactSource(transport);
var resumer = new HostedWorkflowResumer(artifacts, new WorkflowExecutorLoader(verifier), binder);

IReadOnlyList<RunnerHostedVersion> hosted = await runner.ListHostedVersionsAsync();
```

Being served from the control plane is not why the runner trusts what it gets. The assembly is verified against the
content hash and the manifest's signature after the pull, and the hash comes from its own operation rather than from
inside the package, because a manifest vouching for itself would prove nothing.

A version the runner may not execute is answered as absent, indistinguishable from one that was never catalogued.
Saying "forbidden" would confirm that the version exists.

## The lease token never leaves the client

`TryClaimAsync` returns what a runner needs to act on — the run, its workflow, its environment, and when the lease
lapses — and keeps the lease token itself. Every later operation for that run presents it automatically.

That is not only convenience. The token and the authenticated principal are the two things that authorise an operation
on a run, so a runner that never handles the token cannot log it, persist it, or send it for the wrong run.

Releasing a run the client does not hold does nothing and is not an error, so a runner can release in a `finally`
without first working out whether it still holds the lease.

## Sealing what the client saves

A runner that serves a sealed environment passes its key ring to the client, and from then on every checkpoint row it
saves for an environment on the ring has its payload encrypted under a data key derived for that one save and carries
a MAC under that environment's key, and every row it loads is verified and opened before the run sees it (ADR 0065
decisions 4, 5 and 10). The run itself is unchanged: it saves and loads clear rows through `runner.Checkpoints`
exactly as before, and nothing above the client ever holds a key.

```csharp
RunnerKeyRing keyRing = await RunnerKeyRing.BuildAsync(
    [new RunnerKeyRingEntry("production", "k2", SecretRef.Parse("vault://secret/arazzo/payload-keys/production#key"), Sealed: true)],
    secretResolver,
    cancellationToken);

var runner = new ArazzoRunnerClient(transport, keyRing: keyRing);
```

The payload key is the runner's own, read through its own secret resolver as the base64 of its 32 bytes; nothing about
keys comes from the control plane. A load that does not verify, that carries a generation the ring does not hold, or
whose payload does not decrypt throws `CryptographicException` before any of the row is trusted, and so does a clear
row for an environment marked sealed: that is a row the control plane, a backup or a peer wrote without the key. The
one clear row a sealed environment's runner opens is a run's genesis row, which the control plane writes before any
runner has claimed and which therefore carries no lease epoch. An environment on the ring that is not marked sealed
still encrypts and seals what it writes and opens what carries a MAC, but tolerates a clear row, which is the posture
for an environment whose rows predate its key.

The control plane holds no key and cannot verify a MAC or open a payload. It requires both instead: a save for an
environment whose record holds an active key generation is refused with `400` unless the submission is encrypted and
MAC'd under one of those generations, so a runner that lost its key ring cannot write plaintext into a sealed
environment. What the control plane can read of such a run is its envelope: the run detail and the step journal are
served as usual, the journal says the payload is sealed and carries no outputs, and a re-run is refused because the
inputs cannot be read.

## Anchoring what the client loads and saves

A runner that serves a sealed environment also passes its tenant anchor store (ADR 0065 decision 6), and the client
refuses to start with a ring that marks an environment sealed and no store to anchor it in: the client is the lease
holder, and the lease holder is a run's sole anchor writer.

```csharp
PostgresTenantAnchorStore anchors = await PostgresTenantAnchorStore.ConnectAsync(tenantConnectionString);
var runner = new ArazzoRunnerClient(transport, keyRing: keyRing, anchors: anchors);
```

The store is the tenant's own, never the control plane's. It holds one record per run of what the tenant last
committed to (the epoch high-water mark, the committed and pending marks, each a checkpoint digest at an ordering key)
and the environment's attested store incarnation, and it enforces exactly the acceptance predicate under a
whole-record compare-and-swap. From then on every load of a run in an environment on the ring evaluates the anchor
decision table over the row the control plane holds before a byte of it is trusted, and every save is staged with the
tenant before it is dispatched, under the grant's epoch and the attested incarnation the run writes into its region.
A rollback, a substituted row at a committed sequence, a replay of a finished run, a row that does not verify, a lost
anchor or an environment the tenant has not attested is a `CheckpointAnchorException` (or, for a row that does not
verify, the same `CryptographicException` as before). The worker answers either the same way: the run is not advanced,
its lease goes back, the refusal is counted on `corvus.arazzo.workflows.refused`, and the sweep carries on with its
other claims. The run is left as it is for the operator to cancel envelope-only through the control plane; an
operator-signed re-anchor is admitted by the store and applied by no runner until decision 8's operator key is pinned.

The environment's first attestation is the tenant's to make, before any run in it is claimed; `AttestedIncarnationAsync`
is what the worker reads for each claim and is null for an environment the runner does not anchor.

## Refusals a runner must act on

| Situation | What you get |
|---|---|
| Nothing claimable | `TryClaimAsync` returns `null` — the common case for an idle runner, and not an error. |
| The lease is no longer current | `RunnerLeaseLostException`. The run may already be held by another runner, so stop advancing it. |
| A save lost the sequence predicate | `CheckpointSupersededException`, carrying the sequence the store will accept next. |
| Anything else non-success | `RunnerApiException` with the status. |

**A superseded save is raised, never swallowed.** Reporting it as durable would leave a runner committed to a
checkpoint the store does not have, which is the one failure the save operation exists to make impossible. The
exception carries the accepted sequence, so a runner can tell its own duplicate resend (one past what it sent) from a
genuine divergence without another round trip.

Once a lease is lost the client stops presenting it, so the next operation for that run fails immediately rather than
making a round trip that cannot succeed.

## Regenerating

```bash
dotnet run --project src/Corvus.Json.Cli -f net10.0 -- \
  openapi-client docs/arazzo/reference/arazzo-runner.openapi.json \
  --rootNamespace Corvus.Text.Json.Arazzo.Durability.Runner.Client \
  --outputPath src/Corvus.Text.Json.Arazzo.Durability.Runner.Client/Generated
```

A change starts in the OpenAPI document, the `Generated/` code is regenerated, and only then is the client's façade
adjusted to the new shape ([ADR 0039](../../docs/arazzo/adr/0039-api-first-openapi-source-of-truth.md)).