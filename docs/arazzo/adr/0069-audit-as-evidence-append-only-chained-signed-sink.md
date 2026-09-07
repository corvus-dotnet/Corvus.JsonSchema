# ADR 0069. Audit as evidence: an append-only, hash-chained, signed sink outside the operational store

Date: 2026-09-07. Status: **Accepted**. Implementation: **not started**. Scope: how a governance audit record survives and how its integrity is shown. Resolves GAP-6 of the 2026-08-07 security audit and is the durability half remediation row 11 names. Builds on [ADR 0038](0038-payload-safe-governance-audit.md), which decides what a record contains and is not changed here, and on the signing stack of [ADR 0025](0025-integrity-binding-optional-signature.md), which this record reuses.

## Context

ADR 0038 scopes the audit primitive to payload-safety and says nothing about durability, and the code matches it. `GovernanceAudit.Mutation` starts an activity on `ArazzoTelemetry.ActivitySource` and writes one information-level log line through a null-conditional logger. There is no audit store type, interface, record type or table anywhere in the repository. The record therefore evaporates three ways: the span rides a sampled activity source, so head sampling discards most of it; the log is at information level, so raising the level for noise loses everything; and a host that never wires the logger category is a silent no-op. Nothing asserts at startup that a sink is attached.

The record itself is right. Since P1-6 every record carries the canonical subject, the actor's owner group and the environment, a controlled action and outcome vocabulary and identifiers, never a payload or a secret. What is missing is somewhere for it to land that an attacker holding the operational database cannot rewrite, and a way to show it has not been rewritten. The repository already ships an ECDSA signing stack for executor packages (`EcdsaExecutorPackageSigner`, `TrustStoreExecutorPackageVerifier`) and applies it to nothing else.

## Options

**A table in each durability backend.** Ten implementations under the store-conformance discipline. The record sits in the same database as the rows it describes, which is the database an attacker who has reached the rows already holds, so it is evidence of nothing against that attacker. It is also ten backends of work for a property the audit explicitly rejects.

**The log pipeline only.** Declare the external collector (assumption ASU-1) the sink and raise the log level. Durability and retention become the collector's. Tamper evidence is whatever the collector offers, which the platform cannot express or verify, and a deployment without a collector has nothing.

**An append-only sink seam with a chained, signed reference implementation over immutable storage.** One interface in the durability library. The reference implementation appends to storage that refuses rewrite (an append blob in an immutable container, an object with a lock, a file the process only appends to for development), links each record to the previous by hash, and signs the chain head on a cadence with the package-signing stack. The log and span stay as the diagnostic twin.

## Antagonistic review

*Against a seam and a reference implementation:* a deployment can still register nothing. *For:* the secured postures assert a sink at startup, exactly as ADR 0016 makes the posture a required parameter, so registering nothing is a startup failure rather than a silent gap.

*Against a hash chain:* an attacker who holds the sink rewrites the chain from the tampered record onward. *For:* the signed chain heads are also emitted through the span and log path, so an external collector holds anchors the sink's owner cannot rewrite, and a rewritten chain fails against the last anchor it cannot reproduce. Signing every record would be simpler to state and far more expensive; a head signed per batch or per interval bounds the unsigned window to that cadence.

*Against refusing governance mutations when the sink is down:* an audit outage becomes a governance outage. *For:* proceeding unaudited is the property the audit calls a gap. Reads and run execution are not gated, so the blast radius of a sink outage is authoring, which is the surface an unaudited window would matter on.

*Against writing after the mutation commits:* a crash between commit and append loses the record. *For:* writing before commit records intents that may not happen, and doubles the record count to reconcile them. The post-commit append is synchronous and its failure is surfaced as the request's failure, so the window is a process crash inside one request, which the operational store's own durability already accepts for the mutation itself.

## Decision

**One audit sink seam in the durability library, appended to synchronously by the governance and sensitive-read primitives after the mutation commits, alongside the span and log they already emit.** The record is ADR 0038's record with a timestamp, a sequence and a chain hash. Nothing new is recorded, so payload-safety is preserved by construction.

**The reference implementation is append-only storage outside the operational database, with a hash chain and signed heads.** Each record carries the hash of the previous record. The chain head is signed with the executor package signing stack on a configured cadence, per batch or per interval, and every signed head is also emitted through the span and log path so an external collector holds anchors independent of the sink. The first implementations are an append blob in an immutable container on the Azure Storage backend the repository already carries, and a process-append file for development.

**The secured postures assert a sink at startup and fail closed on it at request time.** A control plane in `Scoped`, `RowSecurityOnly` or `ScopesOnly` posture with no sink registered does not start. A governance mutation whose append fails is refused with a server error and a health signal, and the mutation it followed stands; reads and run execution are never gated on the sink.

**Verification is a command.** `arazzo-runs audit verify` walks the chain from a given anchor, checks every link and every signed head against the trust store, and reports the first break.

**Retention is the storage's policy, documented, not a platform feature.** An immutable container's retention period is the audit's.

## Consequences

- An audit record is evidence against an attacker who holds the operational store, because it is not in it, and against an attacker who holds the sink, up to the last anchor the collector holds.
- Governance authoring depends on the sink's availability in the secured postures. That is the intended coupling: the deployment that wants unaudited authoring is the `Open` development posture.
- Read-side audit ([ADR 0070](0070-read-side-audit-three-tiers.md)) and authentication telemetry ([ADR 0071](0071-authentication-event-telemetry.md)) append to the same sink, so one chain carries every security-relevant record.
- The threat model's detection section changes state: the audit trail stops being diagnostic telemetry and becomes a record, and the §8 "no audit store" finding closes when the sink lands.
- This record does not decide what is audited (ADR 0038, ADR 0070), and does not make the record change-aware; payload-safety is the property GAP-6 was required to preserve.
