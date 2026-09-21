# ADR 0071. Authentication event telemetry: a library helper the host wires, and audited runner-API refusals

Date: 2026-09-07. Revised 2026-09-21, on starting the implementation: the helper is one scheme-agnostic middleware and not hooks on each scheme's events, failure records are capped for each remote address with the suppression recorded, and the runner API's quota refusals are recorded as its other refusals are. Status: **Accepted**. Implementation: **complete**. Built: the authentication telemetry, its record, its cap and its startup assertion, and the runner API's refusals, recorded by one filter over its operations. Scope: how authentication successes and failures, and the runner API's refusals of a principal, are recorded. Resolves GAP-8 of the 2026-08-07 security audit and the runner-API half remediation row 11 names. Builds on [ADR 0042](0042-auth-agnostic-host-owns-session.md), which places authentication in the host and is why the library cannot observe it unaided, on [ADR 0038](0038-payload-safe-governance-audit.md) for the record, and on [ADR 0069](0069-audit-as-evidence-append-only-chained-signed-sink.md) for where it lands.

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

**The library ships an authentication telemetry helper the host adds in one registration**, `services.AddArazzoAuthenticationTelemetry()`. It is one middleware that reads the request's authentication result, and not hooks on the bearer and cookie schemes' events: it covers bearer, cookie, OpenID Connect and a handler of the host's own alike, a scheme added later included, and the library takes no dependency on any of them, which it has none of today. ASP.NET computes a scheme's result once for a request and hands the same result to whoever asks again, so the middleware costs the request nothing it was not going to pay and sits at the start of the pipeline whatever the host builds after it. It reads the default authentication scheme; a scheme named only on an endpoint's authorization is not seen. A request that carries no credential is counted as `none` and is not a failure, since there is nothing to have guessed. The failure reason is controlled vocabulary taken from the kind of failure, never from its message, which can quote the token. The remote address is the connection's, so a host behind a proxy configures forwarded headers. A counter on `ArazzoTelemetry.Meter` dimensioned by scheme, outcome and failure reason carries the rate. A failure appends a refusal record to the audit sink and writes a warning-level structured log carrying the scheme, the issuer, the subject if the token parsed far enough to name one, the failure reason and the remote address. A success increments the counter and appends nothing; the mutation and read records already name the subject on every action that matters.

**Token material is never recorded.** Not the token, not a hash of it, not a fragment.

**Failure records are capped for each remote address, and the suppression is itself recorded.** A failed authentication is an append to a signed chain that an unauthenticated caller can cause at will, more cheaply than a refused read, and it has no subject to be trusted, so the bound is on the address: 60 a minute by default, and past it one record a minute stating how many were suppressed for that address, with the counter, `corvus.arazzo.authentications`, carrying the full rate. It is the limiter of [ADR 0070](0070-read-side-audit-three-tiers.md)'s refusal records, and a failure's record never changes how the request is answered, whether the record is appended, suppressed or refused by the sink.

**The runner API audits its own refusals through the governance primitive.** A request with no resolvable machine principal, a principal whose runner is revoked or quarantined, a claim or checkpoint refused by the bindings resolver, and a lease or epoch mismatch each append a record with the outcome vocabulary the governance audit already uses. This is the runner-API half of remediation row 11. It is one filter over the runner API's mapped operations and not a call at each of the thirty places a handler refuses, so an operation or a refusal added later is covered. What a filter can see is how the request was answered, so the outcome is as fine as the status: `refused-no-principal` and `refused-forbidden` for a 403, by whether the request named a machine principal at all, `refused-conflict` for a 409, `refused-not-found` for a 404 and `refused-quota` for a 429. Which forbidden, a revoked runner or an environment it is not bound to, and which conflict, a lost lease or a superseded write, is in the response the runner was given and not in the record. The record names the runner's principal, the operation and the run, and never the lease token. A runner calls this API in a loop and can cause refusals at will, so its records are capped for each principal by the same limiter, and none of it changes how a request is answered. The runner API is served by the control plane, so it records into the control plane's chain, and one that authenticates its callers does not map without an auditor that appends to a sink.

**The runner API's quota refusals are recorded as its other refusals are**, capped for each runner's principal by the same limiter. A quota trip is a refusal like the rest, and high-volume by nature, which is what the cap is for.

**The secured postures assert the helper is registered.** A control plane in `Scoped`, `RowSecurityOnly` or `ScopesOnly` posture that maps its API without the helper registered does not start, and the sample hosts register it.

**Detection only.** Lockout, back-off and throttling of the authentication endpoints are rate limiting (GAP-3) and are not decided here.

## Consequences

- Brute force and credential stuffing have a signature: a failure counter with a rate and a refusal stream naming the remote address.
- The runner API stops being the silent seam. Every refusal it makes is on the chain with the same subject and dimensions as a governance record.
- Hosts gain one obligation, the registration, and the secured postures enforce it at startup.
- The threat model's detection table changes two rows, authentication success and failure and every runner API operation, and TB-5's observability row moves from Absent.
- This record does not decide throttling (GAP-3), session hardening (GAP-2) or the sink (ADR 0069).
