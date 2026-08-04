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

## The lease token never leaves the client

`TryClaimAsync` returns what a runner needs to act on — the run, its workflow, its environment, and when the lease
lapses — and keeps the lease token itself. Every later operation for that run presents it automatically.

That is not only convenience. The token and the authenticated principal are the two things that authorise an operation
on a run, so a runner that never handles the token cannot log it, persist it, or send it for the wrong run.

Releasing a run the client does not hold does nothing and is not an error, so a runner can release in a `finally`
without first working out whether it still holds the lease.

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