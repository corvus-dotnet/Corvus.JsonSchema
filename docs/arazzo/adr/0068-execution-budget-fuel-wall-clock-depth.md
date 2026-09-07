# ADR 0068. Execution budget: fuel, wall clock and depth, set per deployment and per environment, enforced by the runner and verified by the coordinator

Date: 2026-09-07. Revised the same day, on implementation: fuel is bounded by the per-step journal cap, which is the counter the coordinator verifies against. Status: **Accepted**. Implementation: **in progress**, the budget type, the deployment ceiling, the environment override and its API, and the resolution recorded into every run at start have landed; the coordinator's verification, the runner's enforcement, the transport bounds and the surfacing follow. Scope: what bounds a production run's resource consumption, where the bound is configured, where it is enforced, and how a run that exceeds it is classified. Resolves GAP-5 of the 2026-08-07 security audit and decides hazard H14. Builds on the mutual-distrust seam of [ADR 0065](0065-control-plane-owns-store-runners-encrypt-payload.md), which is why the bound is verified on the control plane and not only honoured on the runner, and on the capacity seam of [ADR 0066](0066-runner-api-rate-and-capacity-limiting.md), which bounds how many runs exist and says nothing about how much any one of them may do.

## Context

Nothing bounds what a single production run consumes. A per-run step budget, a run deadline, a production cap on sub-workflow depth, a per-step response size cap, a per-step HTTP timeout and a ceiling on a source's `Retry-After` are all absent. The one depth cap that exists, `IWorkflowRun.MaxSubWorkflowDepth = 8`, is enforced by the draft-run recorder (`DraftRunStepRecorder.cs`) and the test tracer (`TracingWorkflowRun.cs`) and by nothing on the production path. The simulator is bounded: `WorkspaceSimulationJson.ReadBudget` defaults a simulation to 256 steps and clamps a request to 1,024 steps and 30 seconds, through the `SimulationBudget` type (`MaxSteps`, `WallClock`) in the testing library.

The consequence has a third party in it. A pair of steps that `goto` each other issues a real request to a configured source on every iteration, forever, and the operator's runner fleet becomes a sustained flood against someone else's API. Abuse and legal exposure land on the operator. This is the gap the audit ranked as the one that should not wait.

Two properties of the existing design constrain the answer. First, the runner is not trusted (ADR 0065): a bound the runner alone honours is a cooperative control, and a compromised or misconfigured runner is precisely the host that would not honour it. Second, the coordinator already re-reads every checkpoint body on every save (`WorkflowCheckpointCoordinator.SaveAsync` projects the index through `WorkflowCheckpointSerializer.TryProjectIndex`, the P1-8 remediation), and that body carries the run's step journal (`stepJournal`) and its creation time is on the index (`CreatedAt`). The control plane therefore already holds, on every save, the two numbers a fuel and wall-clock bound need, without a new read.

## Options

**Fuel only.** A maximum number of steps per run. Bounds the flood directly, since every step that reaches a source is one request. Does not bound a run that spends its steps slowly (a step that waits on a slow source, or a durable wait that resumes late), so a run can outlive any operational horizon.

**Wall clock only.** A deadline from creation. Bounds how long a run exists but not how many requests it makes inside that window, so a tight loop still floods until the deadline.

**Both, as one budget.** Fuel bounds requests, the wall clock bounds lifetime, and the depth cap bounds the one structural way to multiply both. The simulator already expresses exactly this shape.

**Where enforced: runner only, coordinator only, or both.** Runner-only is cooperative. Coordinator-only faults a run only when it next checkpoints, so a step that never checkpoints (a loop inside one advance) is bounded only by the runner's own advance. Both closes each gap with the other.

**Where configured: deployment, environment, or version.** A version-level budget is tenant-authored, and the budget exists to bound what a tenant's author can do. An environment-level budget matches how every other governed execution property is already set (`requiredIsolation`, `requireEvidence` on the environment record). A deployment-level budget is the operator's ceiling.

## Antagonistic review

*Against both:* two limits are two things to configure and explain. *For:* each alone leaves a loop the other closes, and the simulator's users already reason in both terms.

*Against coordinator verification:* it adds a comparison to the checkpoint hot path. *For:* the comparison is two integers against numbers the coordinator has already decoded for the P1-8 sequence check, and a bound only the runner honours is the H14 divergence restated with a different owner.

*Against a per-environment override:* a tenant environment administrator could raise it. *For:* the override may only tighten the deployment ceiling, never exceed it, and the same person already governs isolation and evidence on that record.

*Against faulting rather than pausing:* an operator may want to raise the budget and continue. *For:* a run that hit its budget has done something its author did not intend; resuming it past the bound is the same run continuing, and the honest shape is a new run under a considered budget.

## Decision

**A run carries one execution budget with three limits: fuel (the maximum number of steps), a wall clock (the maximum age from creation), and the sub-workflow depth cap.** The vocabulary is the simulator's, so a workflow bounded in the designer is bounded the same way in production.

**The budget is configured at two levels and resolved at start.** The deployment sets the ceiling, an `ExecutionBudget` given to the management seam and to the control plane's mapping. An environment may carry an `executionBudget` override on its record, beside `requiredIsolation` and `requireEvidence`, which may only tighten: the API refuses a limit wider than the ceiling. The effective budget is resolved when the run starts, on every start path through the management seam, and recorded in the run's checkpoint, so a later change to the environment does not move a run's bound under it.

**Fuel cannot exceed the per-step journal cap.** The journal (ADR 0050) is the counter the coordinator verifies against, and it is exact only up to its cap of 500 entries; past the cap the run marks it truncated. A budget with more fuel than the cap would be unverifiable, so the type refuses to construct one, the deployment ceiling is at most the cap, and a truncated journal is over any admissible budget by definition. Raising fuel above the cap is a later decision that needs a control-plane-verifiable counter the runner-authored body does not offer today.

**The runner enforces the budget cooperatively, and the coordinator verifies it authoritatively on every save.** The runner counts steps and checks the wall clock as it advances, so a runaway run faults fast without a round trip. The coordinator, on every checkpoint save, compares the journal length against the fuel and the run's age against the wall clock, from the same body it already projects, and refuses a save past either. A runner that does not honour the budget therefore cannot persist the run past it.

**The depth cap applies in production at the value the recorder and tracer already enforce.** Eight, unless the budget says otherwise.

**A run that exceeds its budget faults terminally, with a distinct error type per limit.** `budget-fuel`, `budget-deadline` and `budget-depth` on the run's `ErrorType`. The whole-step resilience layer (`ResilientApiTransport`) treats them as non-retryable, dispatch never reclaims them, and nothing resumes a run past its budget. An operator who wants more starts a new run under a raised budget.

**The same record bounds the per-step transport.** A per-step HTTP timeout on the run-path clients built by `SourceCredentialTransports.CreateSourceHttpClientAsync`, a response size cap on the same transport, and a ceiling on a source's `Retry-After`, clamped the way `RunnerQuotaHoldOptions` already clamps the runner API's. These bound one step; fuel bounds how many.

## Consequences

- The platform cannot be aimed at a third party for longer than one budget. A mutual-`goto` pair makes at most fuel requests and lives at most one wall clock, whatever the runner does.
- The budget is a property of the run, not of the environment at the moment of checking. Raising an environment's budget affects new runs; it does not rescue a faulted one.
- The coordinator's every-save check gains two comparisons over numbers it already has. Threat model TB-5's integrity row records that budget enforcement is now a control plane property, not a runner one.
- The simulator and production share a vocabulary. A budget the designer shows is the budget production applies, and the simulator's ceilings (1,024 steps, 30 seconds) are the designer's, not the deployment's.
- The CLI and the UI surface the effective budget on the environment and on the run, and a faulted run's error type says which limit it hit.
- This record does not decide rate limiting on the governance plane (GAP-3), egress policy (GAP-4), or how many runs a tenant may hold (ADR 0066). It bounds one run.
