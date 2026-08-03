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