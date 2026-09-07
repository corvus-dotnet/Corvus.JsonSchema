# ADR 0070. Read-side audit in three tiers: disclosures audited, refusals audited, bulk reads metered

Date: 2026-09-07. Status: **Accepted**. Implementation: **not started**. Scope: which read surfaces produce an audit record, and which produce a metric instead. Resolves GAP-7 of the 2026-08-07 security audit and the read half remediation row 11 names. Builds on [ADR 0038](0038-payload-safe-governance-audit.md) for the record, [ADR 0004](0004-fail-closed-non-disclosing-enforcement.md) for what a refusal looks like to the caller, [ADR 0067](0067-reach-enforced-by-the-store-proven-on-the-wire.md) for why the store leaves nothing to audit, and [ADR 0069](0069-audit-as-evidence-append-only-chained-signed-sink.md) for where the record lands.

## Context

`GovernanceAudit` exposes only `Mutation`. The one audited read surface is the step journal: `SensitiveReadAudit.JournalRead` records who read which run's step outputs at which disclosure tier (`Full`, `Redacted`, or a non-disclosing not-found). Every other read, list, query and search produces nothing, so the highest-value event in a multi-tenant deployment, one tenant's principal reading another's data, leaves no record. The audit named a second half at the store: on the backends that filtered reach in process, cross-tenant rows were physically read on every query. Since ADR 0067 that half is closed on all ten backends, so the API is the only place a read can be observed, and this record is about the API.

Two constraints shape the answer. ADR 0004 makes a denied row indistinguishable from an absent one to the caller, which is right, and which means probing is quiet by design unless something records the probe. And read volume is not mutation volume: a list is a page of index rows on every console refresh, and a per-record audit of it would be both useless as evidence and expensive as a chain.

## Options

**Audit every read.** Uniform and simple to state. Drowns the chain in index-row reads that disclose nothing beyond what the caller's reach already admits, and makes the one record that matters, a payload disclosure, one line among millions.

**Audit nothing on reads, meter everything.** Cheap. A cross-tenant payload read is a counter increment with no actor, which is the gap restated.

**Three tiers.** Audit the reads that disclose a payload tier. Audit every refusal on a read path. Meter, rather than audit, successful bulk reads of index rows.

## Antagonistic review

*Against auditing refusals:* a refusal discloses nothing, so why record it. *For:* the refusal is the probing signal. A principal enumerating ids outside its reach produces a stream of non-disclosing 404s that nothing today records, and ADR 0004's silence toward the caller is exactly why the record has to be internal. Refusals are rare in legitimate traffic, so the volume argument does not apply to them.

*Against recording the refused id:* it names a row the caller may not see. *For:* the record is internal to the deployment and never returned to the caller; withholding the id from the audit would blind the investigator to protect the prober.

*Against metering bulk reads instead of auditing them:* a counter cannot say who. *For:* a successful list is bounded by the caller's reach on every backend (ADR 0067), so it discloses nothing the caller was not granted; a counter dimensioned by action, owner group and outcome is what a dashboard needs, and the audit chain is not a dashboard.

## Decision

**Tier one, disclosures are audited.** Every read that returns a payload tier appends a record through the sensitive-read primitive: the run's checkpoint state (the run-scoped checkpoint surface included, with the run as subject since its caller is a dispatched function), a run's inputs and outputs, the step journal (already), a debug run's trace, a credential binding's detail, and a secret resolution on the runner. The record names the actor, the target and the disclosure tier, never the payload.

**Tier two, refusals are audited.** Every read path that answers with a non-disclosing not-found because the row is outside the caller's reach appends a refusal record naming the actor, the target kind and the requested id. Absent rows are not distinguished from denied ones in the record's outcome vocabulary, because the store does not distinguish them either; the record is the probe, and its volume is the signal.

**Tier three, bulk reads are metered.** Successful lists, searches, counts and index-row gets increment a counter dimensioned by action, owner group and outcome, and append nothing.

**Every read record carries the same subject and dimensions as a mutation record**, and lands in the same sink.

## Consequences

- A cross-tenant payload read, the event the audit called highest-value, has an actor, a target and a tier on the chain. Enumeration has a signature: a refusal stream from one subject.
- The chain's volume is bounded by disclosures and refusals, both of which are rare against legitimate traffic, so ADR 0069's synchronous append stays cheap on the read path.
- The threat model's detection table changes three rows: any read or search on the governance API, authorization denial on read paths, and secret resolution.
- Adding a payload-bearing read surface obliges its author to audit it; the review checklist gains that question.
- This record does not decide the sink (ADR 0069), the record's contents (ADR 0038), or authentication failures ([ADR 0071](0071-authentication-event-telemetry.md)), which are refusals of a different kind.
