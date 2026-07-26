# Corvus.Text.Json.Arazzo.Durability.Serverless.Lambda

The AWS Lambda entry shim for a baked, Native-AOT Arazzo serverless workflow function (issue #876, ADR 0028).

A per-(environment, version) function is compiled with the workflow's executor baked in. Its generated entry point calls `LambdaServerlessFunction.RunAsync(resolver, transportBinder)`, which runs the Lambda custom-runtime loop: each invocation carries `{ "runId", "environment", "checkpointUrl" }`, and the shim feeds it to the vendor-neutral `ServerlessInvocationHandler`, which restores the run, advances it, and checkpoints it back over HTTP to the dispatching runner named by `checkpointUrl`.

It is reflection-free: all JSON is handled by Corvus.Text.Json through a raw `Stream` handler, so no Amazon serialization enters the AOT graph. The function binds no database SDK and holds no store credentials — the runner terminates its checkpoints into the real store.
