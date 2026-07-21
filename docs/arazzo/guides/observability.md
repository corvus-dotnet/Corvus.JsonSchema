# Observability and resilience

The workflow engine, the transports it drives, and the control plane are all observable through OpenTelemetry,
with no separate history or trace resource, and the transports can be wrapped in resilience policies. Every
governed action leaves a payload-safe audit ([ADR 0038](../adr/0038-payload-safe-governance-audit.md)). All of it
is opt-in and zero-cost until a listener or a policy is attached.

## Wiring the sources

Three `ActivitySource` and `Meter` pairs publish the telemetry, each independently enable-able.

| Source | Name | Emits |
|--------|------|-------|
| Workflow engine and control plane | `Corvus.Arazzo` (`ArazzoTelemetry`) | The run-lifecycle counters and spans, and the governance-audit spans, described below. |
| OpenAPI transport | `Corvus.OpenApi` (`OpenApiTelemetry`) | A `Client` span per HTTP operation (`{method} {route}`, with `http.request.method`, `url.template`, `http.response.status_code`, `error.type`) and the `http.client.request.duration` histogram. |
| AsyncAPI transport | `Corvus.AsyncApi` (`AsyncApiTelemetry`) | `Producer` and `Consumer` spans per publish, subscribe, and request, with `messaging.*` tags and W3C trace-context propagation through the message headers. |

Add whichever you collect to the pipeline, then an exporter your dashboard consumes (OTLP for Jaeger, Tempo,
Grafana, Honeycomb, Azure Monitor, and the rest).

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(b => b
        .AddSource(ArazzoTelemetry.ActivitySourceName)    // "Corvus.Arazzo"
        .AddSource(OpenApiTelemetry.ActivitySourceName)   // "Corvus.OpenApi"
        .AddSource(AsyncApiTelemetry.ActivitySourceName)  // "Corvus.AsyncApi"
        .AddOtlpExporter())
    .WithMetrics(b => b
        .AddMeter(ArazzoTelemetry.MeterName)
        .AddMeter(OpenApiTelemetry.MeterName)
        .AddMeter(AsyncApiTelemetry.MeterName)
        .AddOtlpExporter());
```

Every instrument is zero-cost when no listener is attached, so a deployment that collects nothing pays nothing.

## Instrumenting the transports

The OpenAPI transport is not instrumented by default. Wrap the transport you pass to the executor to turn its
spans on.

```csharp
IApiTransport transport = new InstrumentedApiTransport(rawTransport);
```

Each operation span nests under the ambient `Activity.Current`, which is the workflow step span, so a step
correlates end to end with the HTTP operation it invokes without any context threading. The OpenAPI
instrumentation does not inject W3C trace context into outbound requests; the underlying HTTP transport carries
its own client instrumentation, which propagates `traceparent` downstream. The AsyncAPI instrumentation does
inject trace context, because it owns the message headers.

## Metrics

The run lifecycle emits counters (`corvus.arazzo.workflows.{started, completed, faulted, resumed, cancelled,
suspended, purged, deleted}`, `corvus.arazzo.steps.{executed, retries}`, and `corvus.arazzo.gotos`) and a
histogram for checkpoint persistence (`corvus.arazzo.checkpoint.duration`, in seconds). There is deliberately no
workflow-duration or step-duration histogram. A durable run re-enters the executor at its cursor per advance, so
end-to-end timing is carried by the per-workflow and per-step trace spans, not an in-process timer. Governance
emits `corvus.arazzo.credentials.rotated` and `corvus.arazzo.governance.decisions`, the governance-decision rate
dimensioned by action and outcome, so approval, denial, revocation, and refusal rates are queryable per action.

## Spans

Run execution emits per-step spans and the checkpoint measurement above. Reading a run's step outputs (a
sensitive read) emits a `workflow.journal.read` span recording who read which run's journal and the disclosure
tier reached, `full`, `redacted`, or `refused` ([ADR 0013](../adr/0013-step-output-disclosure-tier.md)). Spans and
tags use the `corvus.arazzo.*` naming: `run_id`, `workflow_id`, `actor`, `resume_mode`, `outcome`, `status`,
`correlation_id`, `target_kind`, `target_id`, `action`, and `journal_disclosure`.

## The governance-audit primitive

Every governed action (a grant, a revoke, an approval, a denial, a publish, a delete, a promotion, an
administrator transfer) is audited through one payload-safe primitive, `GovernanceAudit.Mutation`
([ADR 0038](../adr/0038-payload-safe-governance-audit.md)). It emits a span named for the action plus an
audit-grade structured log naming the actor, the target kind and id, and the outcome, and it feeds the
`corvus.arazzo.governance.decisions` counter. Its parameters are only controlled vocabulary and identifiers, never
a workflow payload or a secret, so an action cannot leak its inputs through the audit. A refused action is audited
too (its outcome carries the refusal), because a security control firing is exactly what an audit wants to record.
The audited actor is the authenticated principal. Log the actor, target, and outcome as structured fields rather
than interpolated into the message, so a log pipeline can index them and correlate them with the trace by the
ambient `TraceId`. Never log a message payload or a credential.

## Resilience

`ResilientApiTransport` (in `Corvus.Text.Json.OpenApi.Polly`) wraps every operation of an `IApiTransport` in a
Polly `ResiliencePipeline`, so a workflow step's HTTP call gains retry, circuit-breaker, timeout, rate-limiter, or
hedging behaviour without the executor knowing about it.

```csharp
ResiliencePipeline pipeline = new ResiliencePipelineBuilder()
    .AddRetry(new RetryStrategyOptions { MaxRetryAttempts = 3, BackoffType = DelayBackoffType.Exponential })
    .AddCircuitBreaker(new CircuitBreakerStrategyOptions())
    .Build();

IApiTransport transport = new ResilientApiTransport(rawTransport, pipeline);
```

The decorators compose. Wrap for instrumentation and for resilience together, innermost first, so the spans and
metrics record each pipeline attempt.

```csharp
IApiTransport transport = new ResilientApiTransport(new InstrumentedApiTransport(rawTransport), pipeline);
```

Transport-level resilience is orthogonal to Arazzo's declarative `onFailure` and `retry` step actions. The
pipeline governs the transport (it retries or breaks a failing HTTP call); the step actions govern workflow
control flow (they route a failed step to another step, retry the step, or end the run). A transport retry is
invisible to the workflow, whereas a step retry is a recorded control-flow decision (the
`corvus.arazzo.steps.retries` and `corvus.arazzo.gotos` counters above). The AsyncAPI side has the same resilience
surface through `Corvus.Text.Json.AsyncApi.Polly.PollyResilienceMiddleware`, which wraps the message handler.

## See also

- The [observability coverage reference](../reference/control-plane-observability-coverage.md) for the per-action
  span, metric, and audit-log inventory across every surface, and the actions not yet instrumented.
- [ADR 0038](../adr/0038-payload-safe-governance-audit.md) for the payload-safe audit decision, and
  [ADR 0013](../adr/0013-step-output-disclosure-tier.md) for the disclosure-tier audit.
