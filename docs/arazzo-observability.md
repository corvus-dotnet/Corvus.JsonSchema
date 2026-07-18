# Arazzo observability and resilience

The Arazzo workflow engine emits OpenTelemetry-compliant traces and metrics, and lets you wrap the
transports it drives in resilience policies. Both are opt-in and zero-cost until a listener or a policy is
attached.

## Traces and metrics

The engine and the transports it calls publish to named `ActivitySource` and `Meter` instances.

| Source | `ActivitySource` / `Meter` name | Emits |
| --- | --- | --- |
| Workflow engine | `Corvus.Arazzo` | A span per step (named for the operation), plus governance-audit spans. |
| OpenAPI transport | `Corvus.OpenApi` | A `Client` span per HTTP operation (`{method} {route}` with `http.request.method`, `url.template`, `http.response.status_code`, `error.type`), and the `http.client.request.duration` histogram. |
| AsyncAPI transport | `Corvus.AsyncApi` | `Producer`/`Consumer` spans per publish/subscribe/request, with `messaging.*` tags and W3C trace-context propagation through message headers. |

An operation span nests under the ambient `Activity.Current`, which is the workflow step span, so a step
correlates end to end with the operation it invokes without any context threading.

### Wiring an exporter

Register the sources and meters with the OpenTelemetry pipeline, then add whichever exporter your dashboard
consumes (OTLP for Jaeger, Tempo, Grafana, Honeycomb, Azure Monitor, and so on).

```csharp
services.AddOpenTelemetry()
    .WithTracing(b => b
        .AddSource("Corvus.Arazzo")
        .AddSource("Corvus.OpenApi")
        .AddSource("Corvus.AsyncApi")
        .AddOtlpExporter())
    .WithMetrics(b => b
        .AddMeter("Corvus.OpenApi")
        .AddMeter("Corvus.AsyncApi")
        .AddOtlpExporter());
```

The OpenAPI transport is not instrumented by default. Wrap the transport you pass to the executor to turn
its spans on.

```csharp
IApiTransport transport = new InstrumentedApiTransport(rawTransport);
```

The OpenAPI instrumentation does not inject W3C trace context into outbound requests. The underlying HTTP
transport (for example an `HttpClient`-based one) carries its own client instrumentation, which propagates
`traceparent` downstream. The AsyncAPI instrumentation does inject trace context, because it owns the
message headers.

## Resilience

`ResilientApiTransport` (in `Corvus.Text.Json.OpenApi.Polly`) wraps every operation of an `IApiTransport`
in a Polly `ResiliencePipeline`, so a workflow step's HTTP call gains retry, circuit-breaker, timeout,
rate-limiter, or hedging behaviour without the executor knowing about it.

```csharp
ResiliencePipeline pipeline = new ResiliencePipelineBuilder()
    .AddRetry(new RetryStrategyOptions { MaxRetryAttempts = 3, BackoffType = DelayBackoffType.Exponential })
    .AddCircuitBreaker(new CircuitBreakerStrategyOptions())
    .Build();

IApiTransport transport = new ResilientApiTransport(rawTransport, pipeline);
```

The decorators compose. Wrap for instrumentation and for resilience together, innermost first, so the spans
and metrics record each pipeline attempt.

```csharp
IApiTransport transport = new ResilientApiTransport(new InstrumentedApiTransport(rawTransport), pipeline);
```

Transport-level resilience is orthogonal to Arazzo's declarative `onFailure`/`retry` step actions. The
pipeline governs the transport (it retries or breaks a failing HTTP call); the step actions govern workflow
control flow (they route a failed step to another step, retry the step, or end the run). A transport retry
is invisible to the workflow, whereas a step retry is a recorded control-flow decision.

The AsyncAPI side has the same resilience surface through
`Corvus.Text.Json.AsyncApi.Polly.PollyResilienceMiddleware`, which wraps the message handler.

## Structured logging

Audit-grade events go through `GovernanceAudit.Mutation`, which writes a payload-safe log entry and a span
on the `Corvus.Arazzo` source for each governance mutation (a credential rotation, a grant change, a run
transition, and so on). Log with the actor, target, and outcome as structured fields rather than
interpolated into the message, so a log pipeline can index and correlate them with the trace by the ambient
`TraceId`. Do not log message payloads or credentials.
