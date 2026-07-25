# Corvus.Text.Json.Arazzo.Durability.Serverless

The serverless (AOT) function-side of Arazzo workflow durability.

`HttpWorkflowStateStore` is an `IWorkflowCheckpointStore` that proxies a run's checkpoint load and save to the
dispatching runner's HTTP checkpoint surface. A baked, Native-AOT-compiled serverless function (AWS Lambda /
Azure Functions) therefore binds no database SDK and holds no store credentials: the runner, a normal host,
terminates the checkpoints into the real store under the lease it already holds.

Interim checkpoints are fire-and-forget and carry a per-run monotonic write-sequence, so the runner drops any
out-of-order or stale arrival and the single stored checkpoint only ever moves forward; a lost interim is a safe
replay (runs are idempotent). The terminal checkpoint is confirmed via `IWorkflowCheckpointFlush` before the
run's outcome is reported. See [ADR 0028](../../docs/arazzo/adr/0028-pluggable-execution-backends.md).
