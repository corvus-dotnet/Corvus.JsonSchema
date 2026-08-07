# ADR 0066. Rate and capacity limiting across the runner API and the control plane

Date: 2026-08-07. Status: **Accepted**. Scope: quota and capacity enforcement for ADR 0065 decision 3
(#876). This records how the limits ADR 0065 requires are actually enforced, what the shipped
implementation does **not** deliver, and why a runner is allowed to wait a refusal out.

## Context

ADR 0065 puts the control plane on the hot path of every checkpoint of every tenant. Its decision 3
requires quotas that are "aggregate per tenant, not only per runner", names the dimensions (a
registered-runner cap per environment, a per-environment checkpoint rate, a total payload-bytes
quota, a run-count cap, a parked-wait cap, per-runner sub-limits, and a body-size cap), and requires
a quota rejection to be a distinguishable retryable signal: `429`, naming the quota and its counter,
which the runner holds and backs off against rather than failing the advance for.

The contract had carried all of that since it was written. `QuotaProblem` declares `quota` and
`counter`, all nine runner operations declare `429` with `Retry-After`, and the generated
`TooManyRequests` factories exist on every result type. **Not one of them had ever been
constructed**, and there was no rate limiter of any kind anywhere in `src/`. The requirement was
ratified and unimplemented.

Two things about this workload shape the design. A runner's traffic is bursty by nature: a sweep
claims a batch of due timers, and an advance makes several requests as one unit of work. And a
quota refusal on the checkpoint path arrives *after* a workflow's external calls have already
landed, so treating it as a fault would discard real work whose side effects have already happened.

## Options

**A. One counter per tenant.** Meter the aggregate the ADR names and nothing else.

**B. One counter per runner.** Meter each machine principal and let the tenant total be whatever its
fleet adds up to.

**C. Both scopes, tighter refuses.** Meter each dimension per tenant *and* per runner.

For where the counters live, independently of the above:

**D. In-process token buckets.** Each API instance holds its own buckets.

**E. Shared state.** The counters live somewhere every instance reads and writes.

**F. Refuse to ship without E.** Treat anything less than the true aggregate as not meeting the ADR.

## Antagonistic review

**A** is what the ADR literally says, and it is not enough on its own. A single runaway runner — one
in a retry loop, or one whose workflow has gone wrong — consumes its tenant's entire allowance and
starves that tenant's own well-behaved runners. That is a self-inflicted outage the platform could
have contained and chose not to.

**B** is escaped by the thing the ADR's residues already worry about: autoscaling. A tenant that
wants more throughput adds runners, and under a per-runner limit alone its aggregate consumption
rises without bound while every individual counter looks healthy. It does not meter the resource the
control plane is actually protecting.

**C** costs two counters per dimension and a second lookup per request. In exchange each scope
closes the other's hole, which neither does alone. The steelman against it is that the per-runner
limit is redundant once the aggregate exists — but that is only true if you do not mind one runner
consuming the whole aggregate, which is precisely case A's failure.

**D** is a real limitation, not a detail: N instances hold N sets of buckets and never compare
notes, so the effective allowance is N times each configured rate. For a single-instance deployment
it *is* the aggregate; beyond that it is containment of a runaway caller and nothing more. Its
merit is that it is correct in the small, costs nothing operationally, and cannot itself fail.

**E** delivers the ADR's aggregate literally. It also puts a shared dependency on the hottest path in
the system, one whose own failure has to be designed for: if the counter store is unreachable, the
guard must choose between failing every request (an outage from a metering component) and admitting
every request (the quota silently absent). That is a real design problem, not a configuration.

**F** is the disciplined-sounding option and it is wrong here. The runner API today runs
single-instance in every deployment we have, where D *is* the aggregate. Refusing to ship any
metering until shared state exists would mean the surface stays completely unmetered — the state it
was already in — while the work that actually protects it waits on infrastructure nothing yet needs.
The failure mode of F is the status quo, which is the worst of the options.

## Decision

**Both scopes (C), with in-process token buckets (D) behind an interface that E implements.**

Five dimensions are metered — claim, checkpoint, checkpoint bytes, lease renewal, catalog — each at
two scopes, the tenant (the environment's owner group) and the machine principal, with the tighter
refusing. The tenant is resolved from the same read and the same bounded cache as the principal's
reach, so a counter can never be charged on a different schedule from the authorization that
bounds it.

`IRunnerQuotaGuard` is the seam. `TokenBucketRunnerQuotaGuard` is the in-process implementation and
**states its own limitation in its documentation**; a deployment that runs several instances and
means the aggregate literally supplies a guard over shared state.

Consequential rules, each of which is load-bearing:

- **The charge precedes the lease check.** The other order lets a caller that is going to be refused
  drive a store read first, at whatever rate it likes, which is the load being bounded.
- **A refusal spends nothing.** Charging a refused request would let a caller already at its limit
  hold itself there by retrying, turning a momentary overshoot into an indefinite one.
- **Every scope is tested before any is spent.** Otherwise a runner pinned by its own sub-limit
  drains its tenant's aggregate while never completing a request.
- **A configured burst is honoured, never raised.** A burst below the sustained rate is meaningful:
  tokens refill continuously, so evenly spread traffic at the rate finds a token waiting however
  small the burst. Only an unset burst is defaulted. A setting that reads as effective and is not is
  worse than no setting.
- **Releasing a lease is never metered.** Refusing it strands a lease on a runner trying to hand work
  back, which makes an overload worse rather than better.
- **Metering is on unless the deployment turns it off**, by passing `NoRunnerQuotaGuard.Instance`. A
  quota a deployment must opt into is one most deployments will not have, and the load it bounds does
  not arrive with notice.
- **Read volume is charged after the fact.** A checkpoint read has no size until it has been read, so
  it is metered in two halves: a probe for any remaining allowance before the read, and the bytes
  actually moved charged afterwards through `SpendAsync`, which cannot refuse. The counter carries the
  deficit, so an overshoot refuses the *next* request. Read volume therefore enforces one request
  late; that is the price of not knowing the size in advance, and it converges.

**The runner waits a refusal out, boundedly.** A `429` is the one non-2xx that does not fail an
advance, because the workflow's external calls have already landed and only the record of them is
being refused. The allowance is **per advance, not per request** — keyed by run and dropped with the
lease, since a lease is held for exactly the advance — and load, save and renewal all draw on the one
budget. Two independent bounds apply, a total hold time and an attempt count, because they fail
differently: a total alone lets a server spin the runner with very short `Retry-After` values, and an
attempt cap alone lets one long wait stall it. `Retry-After` is **clamped**, because ADR 0065 puts the
runner and the control plane in mutual distrust and an unclamped value parks a runner for as long as
whoever answers likes, with one integer.

## Consequences

**The aggregate ADR 0065 decision 3 requires is not delivered on a multi-instance deployment.** This
is a deviation from a ratified decision and is recorded here rather than left in a code comment: with
the in-process guard, N instances admit up to N times each configured rate. Single-instance
deployments are unaffected. Closing it means a shared-state `IRunnerQuotaGuard`, which is tracked
separately; until then a multi-instance deployment that needs the true aggregate must supply one.

The defaults are **starting points sized to sit clear of a working deployment, not measured against
one**. They exist so a deployment that enables quotas without tuning refuses only plainly abnormal
traffic, and so the refusal path is exercised rather than dormant. A deployment that cares about the
numbers sets them from its own load. The demo runs on them untouched and reaches no refusal.

An unbounded runner-side hold would have been a silent, targeted stall primitive: a fabricated `429`
is indistinguishable from a real one, the background renewer keeps the lease alive so the run never
fails over or faults, and a quota hold raises no audit event. The run would sit holding external side
effects it never checkpointed with nothing anywhere reporting a problem. The bounds are what make the
exemption safe to have, and exceeding them fails the advance loudly.

## Amendment: the standing magnitudes

Decision 3's remaining dimensions are **magnitudes, not rates**, and a token bucket is the wrong
instrument for them. A bucket bounds flow; a magnitude is a standing total that must survive a
restart, and a bucket-based cap would forget everything the store still held the moment a process
recycled. They are therefore enforced by a separate seam, `IControlPlaneCapacityGuard`, measured
against the store on each check rather than cached — a cached magnitude is wrong in the direction
that matters, admitting work a tenant no longer has room for for as long as the window lasts. Every
count is bounded at the limit, so a tenant far above its cap costs the same to refuse as one just
over it.

**A capacity refusal is not a rate refusal wearing a different name.** Waiting does not clear it: the
caller has to release capacity before the request is admitted. The contract therefore documents
`Retry-After` on these operations as **advisory rather than a promise**, which is the opposite of its
meaning on the runner API, where the bounded hold depends on it being accurate.

**The run-count cap is two limits, not one.** They bound different resources and neither substitutes
for the other. `ConcurrentRuns` bounds what is in flight (Pending, Running, or Suspended); it is what
stops one tenant occupying the dispatch capacity every tenant shares, and it releases itself as runs
finish. `StoredRuns` bounds what the store holds whatever the status; a tenant can sit at zero
concurrency and still hold millions of terminal rows. Because `WorkflowQuery` carries one status at a
time, concurrency costs up to three bounded counts, each capped by what is still unaccounted for and
short-circuiting the moment the limit is reached.

**`StoredRuns` ships disabled, and that is a decision rather than caution.** Stored runs do not
release themselves: there is no automatic retention, so a completed run keeps its row until it is
purged. Enforcing the limit before a reclamation path exists would refuse new runs to a perfectly
well-behaved tenant that had merely been running for long enough, while it sat completely idle — a
slow outage rather than a limit. The scheduled retention sweep is what makes it safe to enable, and
the default becomes non-zero in the same change that lands the sweep.

**The registered-runner cap guards row creation, not registration.** Enforcing it on every
registration would refuse heartbeat re-registration for every existing runner the moment an
environment filled, taking down the fleet the cap was protecting. It therefore fires only where a row
is actually created: an enrolment-token registration, and an administrator pre-authorizing an unknown
runner. It is also checked *after* the authorization gate, never before, because answering `429` to a
caller that has proved neither a pre-authorization nor a valid token would tell it the environment
exists and is full — the enumeration decision 3's single non-disclosing refusal closes.

**The check is not mutually exclusive with the write.** Nothing holds a lock, so a concurrent start
can land between the count and the create. A capacity limit bounds accumulation, not
concurrency-of-decision; a bounded overshoot is accepted, and the next request sees the higher count.
Guaranteeing otherwise would need a lock on the hot path that the limit does not justify.

**Two of decision 3's magnitudes are deliberately not here**: the parked-wait cap and the total
payload-bytes quota. Both are incurred when a checkpoint is written, which happens through the runner
API — and that assembly does not reference the control-plane server. Enforcing them from the
control-plane seam would invert the dependency ADR 0065 exists to establish, exactly as routing the
message listeners through it would have. They are bounded on the checkpoint write path instead,
against a guard that path can see, and are tracked separately.

The claim path is rate-limited here but **not yet audited**, which decision 3 also requires of it.
There is no audit seam in the runner API to attach one to, and inventing it here would be guessing at
machinery phase B defines.