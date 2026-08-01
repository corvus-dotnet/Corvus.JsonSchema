# Implementing secure checkpointing without paying for it twice

[ADR 0065](../adr/0065-control-plane-owns-store-runners-encrypt-payload.md) puts the control plane on the path of every checkpoint and adds per-checkpoint encryption, authentication, and chaining. This guide is the performance counterpart: where that cost actually lands, which optimizations remove it while preserving the security property, what is genuinely unavoidable, and which plausible-looking optimizations are security regressions in disguise.

The headline: **the symmetric cryptography is not the cost.** A checkpoint's whole crypto budget is under 5 microseconds on an accelerated host, against a network hop of at least a millisecond. The cost is in *round trips* and in *key-management service calls*. Optimize those; do not trade a security property to save an HMAC.

## The budget

| Stage | Budget |
|---|---|
| Runner-side CPU: serialize, split, encrypt, MAC, chain link (1 KB checkpoint) | ≤ 50 µs, **0 bytes allocated** |
| Of which symmetric crypto | ≤ 5 µs (AES-256-GCM ~0.3–1 µs/KB, SHA-256 ~0.5 µs, HMAC ~0.2 µs, HKDF ~2 µs) |
| Runner API server-side, excluding the store | ≤ 200 µs (cached token verify + binding snapshot hit + framing validation) |
| Store CAS | **exactly one round trip**; ≤ 2 ms p50, ≤ 10 ms p99 on a co-located Postgres |
| Checkpoint hop, runner co-located with the control plane | ≤ 5 ms p50, ≤ 20 ms p99 |
| Checkpoint hop, cloud-serverless guest across the internet | gate on **one round trip per checkpoint**, not an absolute number |
| KMS or Key Vault calls per checkpoint | **0** (alarm if wrap-calls ÷ checkpoints exceeds ~0.01) |
| Pre-advance round trips (claim + lease + load) | **1**, not 3 |
| Binding and authorization resolution | 0 store reads on the cached path, 0 bytes allocated |

## Where the milliseconds are

### Collapse the round trips

**Claim returns the row.** Dispatch currently does query-claimable, then acquire-lease, then load — three store round trips before a step runs, which the ADR turns into three authenticated HTTP hops. Make one runner-API verb return a bounded keyset page of *claimed* runs, each carrying the whole row: envelope, payload ciphertext, salt, sequence, etag, lease token and expiry, chain tip. Server-side it is one statement per backend (a Postgres CTE returning the claimed rows; an extension of the Redis Lua script; a Cosmos stored procedure). Batch several runs per call so the poll loop is not one round trip per run. Three hops become one — 2 round trips saved per advance, which is 2–4 ms co-located and 60–200 ms for a serverless runner across the internet.

**Pipeline lease renewal onto the checkpoint write.** The save response returns the new expiry, and the server extends the lease in the same statement as the CAS. A background renew fires only when no checkpoint has been written for more than half the TTL. This *strengthens* the lease check: renewal becomes a consequence of an authorized, CAS-accepted write, so a runner that cannot legitimately write cannot keep its lease.

**Warm, multiplexed transport.** Every runner-side client in the tree today is a bare `HttpClient` or default `SocketsHttpHandler`: HTTP/1.1, no connection lifetime, no keep-alive policy. Use one shared handler per process with `PooledConnectionLifetime` around five minutes, an idle timeout longer than the dispatch poll interval, `EnableMultipleHttp2Connections`, keep-alive pings on active requests, and `RequestVersionOrHigher` so HTTP/3 is taken where the ingress offers it. Issue a cheap health call at startup so the first checkpoint never pays the handshake. First-write-after-idle drops from about three round trips to one.

**Coalesce interim writes.** Interim saves are fire-and-forget but now authenticated and failure-significant. Keep at most one *undispatched* interim write per run and replace it when a newer one arrives, with a hard floor (at least every K milliseconds, and always the terminal write). The server already implements exactly these semantics for superseded sequences; this moves the drop to the client. Per-environment, default off, because it trades crash-replay granularity.

**Cache the catalog locally.** Packages are immutable and content-hashed, so cache them on disk keyed by the *authorized path* and revalidate with `If-None-Match`. After the first pull of a version the runner never re-downloads it. Authorization stays per request and path-scoped — a revoked runner gets `404`, not `304`.

### Get the KMS off the checkpoint path

This is the single highest-value item. The envelope protector today generates a data key and calls Key Vault or KMS to wrap it — **a network call per checkpoint write and another per open**, 10–60 ms each way against a third-party availability dependency.

Keep the fresh-key-per-checkpoint property but *derive* the key instead of wrapping it: generate a 32-byte random salt per checkpoint and compute the data key with `HKDF.Expand` from the environment payload key over `(keyId, runId, sequence, salt)`, storing the salt where the wrapped key sits today. Because the derived key is unique per checkpoint, the GCM nonce can be a counter, so the random-nonce generation goes too. Where the environment key must stay non-exportable in an HSM, unwrap one *per-lease* intermediate key at claim time (the claim response carries it) and derive per checkpoint from that: KMS calls drop from per-checkpoint to per-lease.

This preserves ADR 0065 decision 5 exactly — a fresh data key per checkpoint, the payload key never used directly as a GCM key, the nonce-reuse bound never approached. It replaces 10–60 ms with about 2 µs.

### Make the authorization path free

There is no cache on this path today: row-security policy resolution, claims transformation, and a linear claim scan run per request, and the dispatch loop re-reads runner authorization every poll. Copy the structure that already works in the control plane's persistent row-security policy: a single volatile immutable snapshot with a generation token, refreshed only when the generation moves, failing closed before the first refresh.

Hold each principal's authorized environments as a sorted UTF-8 key blob with an offset table, so the per-request check is an ordinal span compare with no string materialized and no allocation. Cache the validated principal too, keyed by a hash of the raw token and bounded by the token's own expiry, so a burst of checkpoints does not re-verify the signature each time; keep JWKS local through the bearer handler's configuration manager. A store round trip becomes a lock-free dictionary hit.

**Push invalidation, keep the TTL.** The policy cache is already invalidated explicitly from every policy write; extend those call sites to bump a process-wide revocation epoch and publish it across runner-API instances (a counter row polled through the store, or pub/sub where the deployment has it). Each snapshot records the epoch it was built at, and a request discards it with one volatile read if the epoch moved. Revocation becomes sub-second *and* the TTL can rise to 60–120 seconds, cutting background refresh traffic. The TTL stays as the fail-safe.

Token acquisition belongs in the same category: reuse the existing OAuth2 client-credentials provider (volatile cached token, single-flight refresh gate, refresh-ahead skew) rather than acquiring per attempt, and prefer a locally-minted private-key JWT assertion or mTLS where the deployment supports it, which removes the token endpoint from the hot path entirely.

## Where the microseconds are

### One pass, pooled buffers, no intermediate arrays

A checkpoint today mints three or four owned arrays: the serializer's `ToArray`, the protector's output array plus a `GetBytes` for the AAD on every protect *and* unprotect, the HTTP client's `ToArray`, and another at several store drivers. Splitting the checkpoint naively doubles all of it.

Instead: write the payload JSON through the pooled writer/buffer primitive the serializer already uses. Because AES-GCM ciphertext length equals plaintext length, the exact row size is known before encrypting — rent **one** destination buffer for header, salt, nonce, tag, ciphertext, envelope, and MAC, and return it as the repo's pooled UTF-8 type (which already exposes the memory, span, and array segment the drivers bind from). Then encrypt **directly into the row buffer**: the mandatory copy out of the thread-affine writer and the encryption become the same memory traversal, so the split costs zero extra copies versus today. Write the envelope JSON into its region through a buffer-writer slice, and assemble the AAD in a `stackalloc` — never `Encoding.UTF8.GetBytes`.

Give `ICheckpointProtector` a synchronous buffer-writer-shaped overload, default-implemented over the async one and negotiated the way this repo already negotiates store capabilities. With derived keys there is no I/O left in protect, so the async state machine leaves both the write and read paths. Target: **0 bytes allocated** per checkpoint, against today's several kilobytes of Gen0 churn per checkpoint per run.

The one genuinely unavoidable copy: .NET's AEAD APIs are one-shot, so the payload plaintext must exist contiguously once. Fusing that with the copy-out makes it free relative to today.

### Frame the row; do not canonicalize it

ADR 0065's "canonical form of the runner-authored region" must not be read as an invitation to run the RFC 8785 content-hash path per write *and* per open — a parse, sort, and re-emit is 10–50× the cost of the hash itself. Use the deterministic TLV framing the package container already uses: length-prefixed regions for the runner envelope, the control-plane region, salt, nonce, tag, ciphertext, and MAC. The runner writes its region once with a non-indented, validation-skipping writer in a fixed property order, so **the written bytes are the canonical form** and the MAC is a single HMAC over an existing span. Canonicalization is only needed when two parties independently serialize the same logical value; here the runner writes and the runner verifies.

Framing also puts the control-plane-authored fields in their own region, so cancel and resume requests never touch the MAC'd bytes, and it removes base64 wherever a backend accepts binary — Cosmos currently base64s the checkpoint, inflating high-entropy ciphertext 33% on the wire, in request units, and at rest.

### Chain link and subkeys

Compute the chain link as a single SHA-256 over the ciphertext already sitting in the row buffer, immediately after encryption, into a `stackalloc` — it cannot be computed during serialization because it is over the ciphertext, but it is one ~0.5 µs pass. Carry the tip as an inline 32-byte field threaded the way the coordinator threads etags today, and **persist it alongside the lease** so a restart or a sibling runner re-anchors legitimately (holding it only in process memory turns a legitimate re-claim into a false tamper alarm).

Worth a crypto sign-off as a stronger variant: use the previous checkpoint's GCM tag as the link. It is already computed, is a function of the whole ciphertext, AAD, and key, and cannot be produced by a party without the key — unlike a public SHA-256 anyone holding the ciphertext can recompute. Its 128-bit width should not be relied on for collision resistance against the key holder, and that should be stated rather than assumed.

Derive the wait-index and MAC subkeys **once per key generation** into an immutable key ring over a single pinned buffer (pinned so rotation can reliably zero it), held in a frozen dictionary keyed by key id so prior generations stay openable. Use the static `HashData` overloads — no HMAC instances, no allocation.

### Store-side

**One statement per checkpoint.** The Postgres save today issues the CAS update, then unconditionally deletes and re-inserts security tags one command at a time, outside any transaction — six round trips for a run with four tags, and a real consistency hole since the child rows can diverge from the run row. Security tags are creation-time immutable: compare with the existing string-free set-equality path and skip the sync entirely on the common checkpoint; batch it inside the CAS transaction when it genuinely changes. Add `RETURNING version` (or the equivalent output clause, Lua return, or KV revision) so the persisted sequence comes back in the same round trip instead of being client-computed — which is also what makes the sequence genuinely server-validated rather than an in-memory authority.

**Index the blind key, not text.** Replace the nullable channel and correlation text columns with a single fixed-width 32-byte binary column and index `(status, wait_key)`; the lookup is pure equality. Drop the old three-column collated index. While in the DDL, add the indexes the dispatch path lacks — claimable-by-environment, and `leases(owner, expires_at)`, since the revocation fence currently scans the table it is meant to fence.

**Stop compressing and indexing ciphertext.** Postgres attempts pglz compression on every `BYTEA` write over about 2 KB, which is guaranteed-zero-gain CPU on AEAD output: set the column storage to external. Exclude the payload, salt, nonce, tag, and MAC paths from the Cosmos indexing policy, which today indexes ciphertext and pays request units for it.

**Verify hardware acceleration at startup.** The whole "crypto is cheap" argument depends on AES-NI or the ARM crypto extensions; without them AES-GCM falls 10–40×. Check the intrinsics at startup, log a posture warning through the controlled-vocabulary audit path, and surface it on the operator posture strip. Where acceleration is genuinely unavailable, ChaCha20-Poly1305 is a software-fast AEAD alternative — but as an explicit per-environment algorithm id recorded in the row header, never an implicit runtime substitution.

## Unavoidable costs

Do not optimize these away; they are the security design, not overhead.

- **One authenticated round trip per durable checkpoint.** There is no local-store fallback compatible with runners holding no store credential. The only legitimate reductions are fewer checkpoints and cheaper round trips.
- **A fresh data key per checkpoint.** Derivation makes it cheap; reuse forfeits the authentication key the anti-forgery argument rests on.
- **One contiguous plaintext buffer** before the AEAD call.
- **MAC verification on every open.** The attack is a rewrite *between* opens.
- **The terminal write is awaited and must succeed**, and an interim non-2xx (other than a quota hold) fails the advance.
- **Per-request authentication and authorization** — not per connection, not per lease.
- **Catalog authorization by path**, never by content hash.
- **The runner API is scaled and available ahead of governance.** Its capacity plan is a security-adjacent requirement.

## Traps

Each of these looks like an optimization and is a security regression.

- Authenticating once per connection and trusting it thereafter (widens the revocation fence to the connection lifetime).
- A post-expiry token grace window, or longer-lived tokens to cut IdP traffic.
- Removing the binding-cache TTL because a push channel exists (a dropped invalidation becomes permanent).
- A single long-lived AES-GCM key with a counter nonce — multi-writer nonce reuse, exactly what decision 5 forbids.
- Deriving the nonce from any guest-influenced value.
- Treating the runner-supplied sequence as authoritative rather than advisory.
- Truncating the blind index below 32 bytes: collisions are a correctness bug (waking the wrong run) as well as a privacy one.
- Enabling pre-encryption compression by default. It shrinks payloads 3–5× but makes ciphertext length a function of plaintext *content* (the CRIME/BREACH class), and checkpoints are guest-driven. Per-environment opt-in, off by default, mutually exclusive with length-bucket padding, documented beside the other visibility residues.
- Caching "this row verified recently" to skip the MAC.
- Resolving a binding-cache miss to a distinguishable error or a `403` instead of failing closed to `404` — an existence oracle reintroduced through a caching decision.
- Holding the chain tip only in process memory with no persisted anchor, and then "fixing" the resulting false alarms by trusting the row on a cold open — which silently removes the rollback detection the ADR retains while prevention is deferred.
- Emitting the blind wait key, a correlation id, or per-run payload sizes as telemetry tags.

## What to measure

Telemetry today has exactly one checkpoint histogram, wrapped around the store save, with no backend dimension — so after this lands there would be no way to attribute a regression. Add, in the existing zero-cost-when-unlistened style: serialize duration (split, encrypt, MAC, chain — no I/O), transport duration (the runner-to-API hop, tagged by outcome), store duration (the server-side CAS), checkpoint bytes as a histogram tagged by region, binding-resolve duration and a cache result counter, KMS wrap and unwrap durations (so a regression back to per-checkpoint KMS calls is immediately visible), and advance duration tagged by backend — the number the [execution-backend trade-offs guide](execution-backend-tradeoffs.md) currently quotes as platform-typical. Tag only from the controlled vocabulary.

Benchmarks, in the existing projects and before/after style: a split-protect benchmark with a memory diagnoser (today's serialize-then-protect versus the single-pass pooled path), the open-verify counterpart, blind-index HMAC with and without cached subkeys, the string-free binding lookup, and the chain-link variants. In the throughput harness that already runs container-backed latency scenarios, add the one-statement CAS before/after and a full authenticated runner-API checkpoint hop against a real control plane and a real backend container — that is what answers "what did ADR 0065 actually cost". Benchmarks must exercise the real protector; a null protector certifies a path that never ships.