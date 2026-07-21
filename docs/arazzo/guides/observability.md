# Observability

The control plane and the run lifecycle are observable through OpenTelemetry, with no separate history or trace
resource. Every governed action leaves a payload-safe audit
([ADR 0038](../adr/0038-payload-safe-governance-audit.md)).

## Wiring the sources

All instruments are on one source and one meter, both named `Corvus.Arazzo` (`ArazzoTelemetry`). Add them to an
OpenTelemetry pipeline:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(b => b.AddSource(ArazzoTelemetry.ActivitySourceName))    // "Corvus.Arazzo"
    .WithMetrics(b => b.AddMeter(ArazzoTelemetry.MeterName));             // "Corvus.Arazzo"
```

Every instrument is zero-cost when no listener is attached, so a deployment that collects nothing pays nothing.

## Metrics

The run lifecycle emits counters (`corvus.arazzo.workflows.{started, completed, faulted, resumed, cancelled,
suspended, purged, deleted}`, `corvus.arazzo.steps.{executed, retries}`, and `corvus.arazzo.gotos`) and a
histogram for checkpoint persistence (`corvus.arazzo.checkpoint.duration`, in seconds). Governance emits
`corvus.arazzo.credentials.rotated` and `corvus.arazzo.governance.decisions`, the governance-decision rate
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
The audited actor is the authenticated principal.

## See also

- [ADR 0038](../adr/0038-payload-safe-governance-audit.md) for the payload-safe audit decision, and
  [ADR 0013](../adr/0013-step-output-disclosure-tier.md) for the disclosure-tier audit.
