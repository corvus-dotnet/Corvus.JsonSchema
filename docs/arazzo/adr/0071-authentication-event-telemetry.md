# ADR 0071. Authentication event telemetry: a library helper the host wires, and audited runner-API refusals

Date: 2026-09-07. Status: **Accepted**. Implementation: **not started**. Scope: how authentication successes and failures, and the runner API's refusals of a principal, are recorded. Resolves GAP-8 of the 2026-08-07 security audit and the runner-API half remediation row 11 names. Builds on [ADR 0042](0042-auth-agnostic-host-owns-session.md), which places authentication in the host and is why the library cannot observe it unaided, on [ADR 0038](0038-payload-safe-governance-audit.md) for the record, and on [ADR 0069](0069-audit-as-evidence-append-only-chained-signed-sink.md) for where it lands.

## Context

Neither a successful nor a failed authentication is recorded anywhere, so brute force and credential stuffing are undetectable by construction. The library does not own authentication: ADR 0042 makes the browser session the host's, and the API's bearer authentication is likewise host-configured (the demo host registers its own scheme in `Program.cs`), so no library code sees a token validated or rejected. On the runner API the library does see its own refusals: `RunnerPrincipalAccessor` resolves the machine principal from the token and answers null when there is none, and the handlers refuse on that, silently. The threat model's detection table records the whole runner API as emitting nothing.

The telemetry the library already has is the right shape for the successful path. `ArazzoTelemetry.Meter` (`Corvus.Arazzo`) carries the workflow counters, and every governance record carries the canonical subject. What is missing is a place for the host to hand authentication outcomes to the library, and a record for the refusals the library makes itself.

## Options

**The library owns authentication.** Contradicts ADR 0042 and forecloses the hosts the kit exists to serve.

**Leave it to the host.** Every host records something different or nothing, and the detection layer stays where the audit found it.

**A library helper the host attaches to its authentication events, plus audited library-side refusals.** The host keeps ownership and gains one registration; the library owns the vocabulary and the sink.

## Antagonistic review

*Against a helper the host may not wire:* the gap returns by omission. *For:* the secured postures assert the helper is registered at startup, the same shape as ADR 0016's required posture and ADR 0069's required sink, and the sample hosts wire it.

*Against logging failures per event:* a credential-stuffing run produces a flood. *For:* the per-event record is what an investigation needs, the flood is bounded by whatever throttles the endpoint, and throttling is GAP-3's decision, not this one. The counter carries the rate; the record carries the who.

*Against recording the remote address:* it is personal data in some jurisdictions. *For:* it is the one field that distinguishes one actor at a thousand accounts from a thousand actors, and the sink's retention policy governs it like every other field.

## Decision

**The library ships an authentication telemetry helper the host attaches to its bearer and cookie authentication events in one registration.** A counter on `ArazzoTelemetry.Meter` dimensioned by scheme, outcome and failure reason carries the rate. A failure appends a refusal record to the audit sink and writes a warning-level structured log carrying the scheme, the issuer, the subject if the token parsed far enough to name one, the failure reason and the remote address. A success increments the counter and appends nothing; the mutation and read records already name the subject on every action that matters.

**Token material is never recorded.** Not the token, not a hash of it, not a fragment.

**The runner API audits its own refusals through the governance primitive.** A request with no resolvable machine principal, a principal whose runner is revoked or quarantined, a claim or checkpoint refused by the bindings resolver, and a lease or epoch mismatch each append a record with the outcome vocabulary the governance audit already uses. This is the runner-API half of remediation row 11.

**The secured postures assert the helper is registered.** A control plane in `Scoped`, `RowSecurityOnly` or `ScopesOnly` posture that maps its API without the helper registered does not start, and the sample hosts register it.

**Detection only.** Lockout, back-off and throttling of the authentication endpoints are rate limiting (GAP-3) and are not decided here.

## Consequences

- Brute force and credential stuffing have a signature: a failure counter with a rate and a refusal stream naming the remote address.
- The runner API stops being the silent seam. Every refusal it makes is on the chain with the same subject and dimensions as a governance record.
- Hosts gain one obligation, the registration, and the secured postures enforce it at startup.
- The threat model's detection table changes two rows, authentication success and failure and every runner API operation, and TB-5's observability row moves from Absent.
- This record does not decide throttling (GAP-3), session hardening (GAP-2) or the sink (ADR 0069).
