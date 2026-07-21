# Control-plane observability coverage

This is the catalog of what each governed control-plane action emits: its span, its metric, and whether it
leaves an audit record. It is the companion to the [use-case catalog](control-plane-use-cases.md), keyed the
same way by surface, and it is the reference an operator or an auditor uses to know which actions are traceable
and which reads are recorded.

## How coverage is emitted

Every governed mutation is audited through one primitive, `GovernanceAudit.Mutation`
([ADR 0038](../adr/0038-payload-safe-governance-audit.md)). One call emits three things at once: a span named for
the action on the `Corvus.Arazzo` activity source (carrying `actor`, `target_kind`, `target_id`, and `outcome`
tags), an audit-grade structured log, and an increment of `corvus.arazzo.governance.decisions` dimensioned by
`action` and `outcome`. In the tables below, **decisions** is shorthand for that counter. A refused action is
audited too, with a `refused-...` outcome, so a security control firing is recorded rather than silent.

The run lifecycle emits its own counters and the checkpoint histogram (see the
[observability guide](../guides/observability.md)). A sensitive read of a run's step journal emits a
`workflow.journal.read` span carrying the disclosure tier reached
([ADR 0013](../adr/0013-step-output-disclosure-tier.md)). Ordinary list and get reads are not audited by design.

This catalog was verified against the control-plane handlers and `ArazzoTelemetry`. The
[not-yet-instrumented](#not-yet-instrumented) section at the end records the instruments not yet emitted, so the
catalog states reality rather than intent.

## Runs (O-RUN)

| Action | Span | Metric | Audit log |
|---|---|---|---|
| Resume (all modes) | `run.resume` | `workflows.resumed`, decisions | yes |
| Cancel (with reason) | `run.cancel` | `workflows.cancelled`, decisions | yes |
| Purge by rule | `run.purge` | `workflows.purged`, decisions | yes |
| Delete one | `run.delete` | `workflows.deleted`, decisions | yes |
| Suspend (timer or message) | `workflow.checkpoint` | `workflows.suspended`, `checkpoint.duration` | n/a (runtime, not an operator action) |

## Catalog (O-CAT)

| Action | Span | Metric | Audit log |
|---|---|---|---|
| Add version (publish) | `catalog.publish` | decisions | yes |
| Update version (retag, classify, describe) | `catalog.update` | decisions | yes |
| Delete or obsolete version | `catalog.delete` | decisions | yes |
| Purge catalog | `catalog.purge` | decisions | yes |

## Environments and promotion (O-ENV)

| Action | Span | Metric | Audit log |
|---|---|---|---|
| Create, update, or delete environment | `environment.create` / `.update` / `.delete` | decisions | yes |
| Add, transfer, or remove environment administrator | `environment.add-administrator` / `.transfer-administration` / `.remove-administrator` | decisions | yes |
| Promote (make a version available) | `environment.promote` | decisions | yes |
| Demote (withdraw availability) | `environment.demote` | decisions | yes |
| Submit a promotion request | `availability-request.submit` | decisions | yes |
| Approve promotion request | `availability-request.approve` | decisions | yes |
| Deny promotion request | `availability-request.deny` | decisions | yes |
| Withdraw a promotion request | `availability-request.withdraw` | decisions | yes |

## Sources and credentials (O-CRED)

| Action | Span | Metric | Audit log |
|---|---|---|---|
| Create credential | `credential.create` | decisions | yes |
| Update or rotate credential | `credential.update` | `credentials.rotated`, decisions | yes |
| Delete credential | `credential.delete` | decisions | yes |
| Author a usage grant | folded into `credential.create` (`created-scoped` outcome) | decisions | yes |
| Create, update, or delete source | `source.create` / `.update` / `.delete` | decisions | yes |

## Grants and rules (O-SEC)

| Action | Span | Metric | Audit log |
|---|---|---|---|
| Create, update, or delete a security rule | `security-rule.create` / `.update` / `.delete` | decisions | yes |
| Create, update, or delete a security binding (grant) | `security-binding.create` / `.update` / `.delete` | decisions | yes |

Rule ordering is deployment configuration, not a runtime mutation, so there is no reorder action to audit.

## Access-request lifecycle (O-ACC)

| Action | Span | Metric | Audit log |
|---|---|---|---|
| Submit a request | `access-request.submit` | decisions | yes |
| Approve (grant) | `access-request.approve` | decisions | yes |
| Grant directly | `access-request.grant` | decisions | yes |
| Grant as eligible | `access-request.grant-eligible` | decisions | yes |
| Approve as eligible (PIM-style) | `access-request.approve-eligible` | decisions | yes |
| Deny | `access-request.deny` | decisions | yes |
| Withdraw (by requester) | `access-request.withdraw` | decisions | yes |
| Revoke a granted access | `access-request.revoke` | decisions | yes |

A self-approval attempt is refused and audited on every decision verb, with the `refused-own-request` outcome.

## Runner authorization (O-RNR)

| Action | Span | Metric | Audit log |
|---|---|---|---|
| Authorize, reinstate, or re-authorize a runner | `runner.authorize` | decisions | yes |
| Quarantine (drain) a runner | `runner.quarantine` | decisions | yes |
| Revoke a runner (with lease fence) | `runner.revoke` | decisions | yes |
| Runner self-registration (machine principal) | `runner.register` | decisions | yes |

## Administrators (O-ADM)

| Action | Span | Metric | Audit log |
|---|---|---|---|
| Add or remove a workflow administrator | `workflow.add-administrator` / `.remove-administrator` | decisions | yes |
| Transfer workflow administration | `workflow.transfer-administration` | decisions | yes |
| Add, remove, or transfer an environment administrator | `environment.add-administrator` / `.remove-administrator` / `.transfer-administration` | decisions | yes |

## Debug and draft runs (O-DBG)

| Action | Span | Metric | Audit log |
|---|---|---|---|
| Start a debug run | `debug-run.start` | decisions | yes |
| Cancel a debug run | `debug-run.cancel` | decisions | yes |
| Delete a debug run | `debug-run.delete` | decisions | yes |
| Resume a debug run | `debug-run.resume` | decisions | yes |
| Inject a message into a debug run | `debug-run.inject-message` | decisions | yes |

## Schedules (O-SCH)

| Action | Span | Metric | Audit log |
|---|---|---|---|
| Create a schedule | `schedule.create` | decisions | yes |
| Delete (cancel) a schedule | `schedule.delete` | decisions | yes |
| Run a schedule now | `schedule.run-now` | decisions | yes |

## Reads (O-RD)

| Action | Span | Metric | Audit log |
|---|---|---|---|
| Read a run's step journal (sensitive) | `workflow.journal.read` | none (reads are not counted) | yes, with the disclosure tier (`full`, `redacted`, or `refused`) |
| Other list or get reads | none by design | none | none by design |

## Run-lifecycle metrics

Every governed action above is audited, and the run-lifecycle counters, including `corvus.arazzo.steps.retries`
and `corvus.arazzo.gotos`, are emitted by the generated executor. There is deliberately no `workflow.duration` or
`step.duration` histogram. A durable run re-enters the executor at its cursor per advance
([ADR 0019](../adr/0019-products-are-the-checkpoint.md)), so an in-process timer would measure a single advance
rather than end-to-end duration. The per-workflow and per-step trace spans (the durable executor re-establishes
the original trace via the run's correlation id) and `corvus.arazzo.checkpoint.duration` carry timing instead.

## See also

- The [observability guide](../guides/observability.md) for wiring the sources and the run-lifecycle instruments.
- [ADR 0038](../adr/0038-payload-safe-governance-audit.md) for the payload-safe audit primitive, and
  [ADR 0013](../adr/0013-step-output-disclosure-tier.md) for the disclosure-tier read audit.
- The [use-case catalog](control-plane-use-cases.md) for the operator jobs these actions serve.
