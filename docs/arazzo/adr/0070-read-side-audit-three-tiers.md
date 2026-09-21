# ADR 0070. Read-side audit in three tiers: disclosures audited, refusals audited, bulk reads metered

Date: 2026-09-07. Revised 2026-09-21, on starting the implementation: a read that discloses a payload fails closed on the sink and no other read does, a runner keeps an audit chain of its own, and refusal records are capped for each subject with the suppression itself recorded. Status: **Accepted**. Implementation: **in progress**. Built: the read record, a record kind of the audit chain carrying the same subject and dimensions as a mutation plus the disclosure tier, and the tier-one disclosures the control plane makes: the step journal, a debug run's view with its trace, a credential binding's detail, and the run-scoped checkpoint read; and the refusal records with their cap, recorded for every operation at once. Not yet built: the bulk-read counter, and the runner's chain. Scope: which read surfaces produce an audit record, and which produce a metric instead. Resolves GAP-7 of the 2026-08-07 security audit and the read half remediation row 11 names. Builds on [ADR 0038](0038-payload-safe-governance-audit.md) for the record, [ADR 0004](0004-fail-closed-non-disclosing-enforcement.md) for what a refusal looks like to the caller, [ADR 0067](0067-reach-enforced-by-the-store-proven-on-the-wire.md) for why the store leaves nothing to audit, and [ADR 0069](0069-audit-as-evidence-append-only-chained-signed-sink.md) for where the record lands.

## Context

`GovernanceAuditor` exposes only `MutationAsync`. The one audited read surface is the step journal: `SensitiveReadAudit.JournalRead` records who read which run's step outputs at which disclosure tier (`Full`, `Redacted`, or a non-disclosing not-found). Every other read, list, query and search produces nothing, so the highest-value event in a multi-tenant deployment, one tenant's principal reading another's data, leaves no record. The audit named a second half at the store: on the backends that filtered reach in process, cross-tenant rows were physically read on every query. Since ADR 0067 that half is closed on all ten backends, so the API is the only place a read can be observed, and this record is about the API.

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

**Tier one, disclosures are audited.** Every read that returns a payload tier appends a record through the sensitive-read primitive: the run's checkpoint state (the run-scoped checkpoint surface included, with the run as subject since its caller is a dispatched function), a run's inputs and outputs, the step journal (already), a debug run's trace, a credential binding's detail, and a secret resolution on the runner. The record names the actor, the target and the disclosure tier, never the payload. On the control plane that is four reads. A run's inputs are returned by no operation, and its outputs only through the step journal, since a run's detail carries neither, so the journal read is the record of both. A debug run's view carries its trace. A credential binding's detail names where the secret lives and how the source is called, and never the secret. The checkpoint read is mapped by hand outside the generated surface, so it writes its own refusal when its record cannot be appended.

**Tier two, refusals are audited.** Every read path that answers with a non-disclosing not-found because the row is outside the caller's reach appends a refusal record naming the actor, the target kind and the requested id. Absent rows are not distinguished from denied ones in the record's outcome vocabulary, because the store does not distinguish them either; the record is the probe, and its volume is the signal. It is recorded in one place for every operation: a filter over the mapped operations records a refusal for any `GET` that names a resource in its path and answers with a not-found, so an operation added later is covered without its author remembering to be. The record's action is the operation's id, its target kind the path's first segment, and its target id what the caller put in the path, cut to 128 characters, since it is whatever the caller chose to send. The checkpoint surface, mapped by hand, records its own.

**Tier three, bulk reads are metered.** Successful lists, searches, counts and index-row gets increment a counter dimensioned by action, owner group and outcome, and append nothing.

**Every read record carries the same subject and dimensions as a mutation record**, and lands in the same sink. It is a record kind of the audit chain ([ADR 0069](0069-audit-as-evidence-append-only-chained-signed-sink.md)), `read`, naming the actor, the target and the disclosure tier, and never what was read.

**A disclosure fails closed on the sink, and nothing else on the read side does.** A tier-one read is recorded before it is answered. When the sink refuses the record the read is refused with a 500 problem of its own type, `audit-read-record-failed`, saying that nothing was disclosed and that it is safe to ask again: a payload read that left no record is the event this audit exists for, and unlike a mutation a read can simply be repeated. A refusal record, a list, a search and a count never fail the request. A failed append there counts, logs at error and degrades the audit's health, and the caller is answered as it would have been. This narrows ADR 0069's "reads are never gated on the sink" to every read but a disclosure.

**A runner keeps an audit chain of its own.** A secret is resolved on the runner, a separate process that ADR 0065 does not trust and that has no path to the control plane's sink. It is given its own auditor, sink, head key and writer id, and its records are its own evidence, checked by the same verify command. The control plane's chain never carries a runner's account of itself, and no runner-to-control-plane reporting surface is added.

**Refusal records are capped for each subject, and the suppression is itself recorded.** A refusal is an append to a signed chain that any caller can cause by asking for ids it cannot see, on the path every governance mutation queues behind. Each subject's refusals are appended up to a bound a minute, 60 by default. Past it, one record a minute states how many were suppressed for that subject, and a counter (`corvus.arazzo.governance.read.refusals`) carries the full rate. A sweep each minute records the count of a subject that flooded and then went quiet, and forgets subjects that have gone. The table of subjects is bounded as well, at 4,096, because in a deployment that authenticates nobody the subject is whatever the caller says it is: when it is full, further subjects share one bound and one count, recorded against no one subject, until the sweep empties it. The probe is still evidenced, with its volume, and an enumeration cannot make the chain or the signing key the bottleneck.

## Consequences

- A cross-tenant payload read, the event the audit called highest-value, has an actor, a target and a tier on the chain. Enumeration has a signature: a refusal stream from one subject.
- The chain's volume is bounded by disclosures and refusals, both of which are rare against legitimate traffic, so ADR 0069's synchronous append stays cheap on the read path.
- The threat model's detection table changes three rows: any read or search on the governance API, authorization denial on read paths, and secret resolution.
- Adding a payload-bearing read surface obliges its author to audit it; the review checklist gains that question.
- This record does not decide the sink (ADR 0069), the record's contents (ADR 0038), or authentication failures ([ADR 0071](0071-authentication-event-telemetry.md)), which are refusals of a different kind.
