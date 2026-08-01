# ADR 0065. The control plane owns the store and fronts all checkpointing; runners encrypt the checkpoint payload

Date: 2026-08-01. Status: **Accepted**. Scope: the trust model between the control plane and runners in a multi-tenant deployment — who owns the durable store, how runners reach it, what each party can read and forge, and how tenants are separated on shared control-plane infrastructure. Revises the two-process shared-store topology ([ADR 0023](0023-two-process-store-as-queue.md)) and the checkpoint-listener deployment shape ([ADR 0062](0062-authenticated-serverless-checkpoint-callbacks.md)); builds on fail-closed non-disclosing enforcement ([ADR 0004](0004-fail-closed-non-disclosing-enforcement.md)), the security-posture enum ([ADR 0016](0016-control-plane-security-mode.md)), the resume-mode taxonomy ([ADR 0022](0022-resume-mode-taxonomy.md)), runner-to-environment binding ([ADR 0027](0027-runner-environment-binding.md)), canonical JSON ([ADR 0031](0031-content-hash-over-rfc8785-canonical.md)), and the runner-as-secure-boundary model ([ADR 0059](0059-serverless-deploy-runs-on-the-runner-as-the-secure-boundary.md)).

*Revision history: the first version sealed whole checkpoints away from the control plane, which broke governance and was reverted before release. The second inverted the topology as decided here. Two rounds of adversarial security review followed; both rounds' findings are folded into the decisions below, and what remains unfixed is listed as an accepted residue rather than left implicit.*

## Context

A security review of the store seam found the implemented model assumed one trust domain around the shared durability store, which is wrong for the intended deployment: **runners are owned by tenants**, checkpoints carry tenant data, and yet the control plane and every runner held full store credentials under one shared at-rest key, with the environment fences enforced by store-layer code running inside each runner's own process — real against bugs, voluntary against a hostile runner.

Two principles must hold at once:

1. **Tenant run data is isolated** — not readable by platform operators, backups, or other tenants, through key custody rather than policy code.
2. **The control plane governs every run.** Detail, cancel, resume, faulted-resume, purge, and audit are control-plane verbs over *all* runs. A design that blinds the control plane to run structure breaks the product; the first cut of this ADR did exactly that and was reverted.

The reconciliation splits on what each party needs. The control plane needs the run's *management structure* — where the run is, what it awaits, how it failed, when it moved. It does not need the tenant's *data* — the values flowing through the workflow. The runner needs both and already holds the tenant's keys for everything else.

**Nothing is shipped.** There is no deployed installation, no tenant data at rest, and no external consumer of the current wire or storage formats. This decision therefore carries no migration obligation and no compatibility hedge: the target design is built directly, the phases below are our own delivery sequencing rather than an upgrade path, and where an existing component contradicts this ADR (the checkpoint listener's deployment shape, the runner's store connection, the in-process draft runner) it is corrected rather than deprecated.

## Decision

**1. The control plane owns the durable store exclusively; runners hold no store credentials.** ADR 0023's "two processes sharing the store" is revised: the store-as-queue survives, but inside the control plane's boundary. Everything a runner did against the store — claim, lease renew and release, checkpoint save and load, timer and message queries, registry heartbeat, catalog artifact pull, deployment queue — becomes an operation on a **runner API** the control plane serves, deployed as a separately scalable component sharing the store with the governance API (preserving ADR 0023's real point: execution load never rides the governance request path). Interim checkpoint writes stay fire-and-forget with the terminal write awaited.

**2. Runners authenticate as their own machine principals; separation and lease ownership are enforced server-side.** No new bearer credential is minted: a runner authenticates with the machine principal it already has (client-credentials, private-key JWT, or mTLS through the deployment's IdP), so there is no long-lived platform-issued secret to steal from a decommissioned host. Environment bindings are resolved per request with a bounded cache (≤30 seconds), so the ADR 0027 revocation fence takes effect within that window. **Lease ownership derives from the authenticated principal** — a client-supplied owner string is rejected, every mutating operation presents its lease token, and revocation expires leases by principal, not by a self-asserted name a compromised runner could change. Registration hardens accordingly: `runners:register` is reach-scoped per environment (never the system context), an authorization row must exist before any registry row is written (so a foreign principal cannot squat a victim's runner id), and pre-authorization binds an expected principal or a short-TTL enrolment token rather than a bare, guessable id. **No principal may hold a `system` binding and a tenant binding simultaneously** — enforced when a binding is written and re-checked per request, because a principal holding both is a laundering route out of a sealed environment into the never-sealed platform one.

**3. Refusals are non-disclosing, and quotas are per tenant (ADR 0004).** An operation naming a run, artifact, or row outside the runner's bindings returns `404`, indistinguishable from a nonexistent one; only a capability failure returns `403`. Catalog artifacts are authorized **by path** (bound environment → deployment → version → package), never by bare content hash. Quotas are aggregate per tenant, not only per runner: a registered-runner cap per environment, a per-environment checkpoint rate, a total payload-bytes quota, a run-count cap, a parked-wait cap (blind index rows are high-entropy and undeduplicable), per-runner sub-limits, and a body-size cap. A quota rejection is a distinguishable retryable signal (`429`, runner-side hold and backoff) explicitly **exempt** from the fail-the-advance rule below, so a chatty legitimate workflow cannot be faulted mid-advance after its external calls have landed.

**4. The checkpoint is one row: a clear envelope and an encrypted payload, verified as a whole.** The **envelope** is run-management structure — cursor, status, wait, fault classification, retry counters, sequencing, timing, journal skeleton, and security tags. The **payload** is tenant data — run inputs, step outputs, extracted values, journal data content. The envelope's runner-authored region and the payload are **cryptographically inseparable**: the runner computes one HMAC (under an HKDF-derived subkey) over the RFC 8785 canonical form (ADR 0031) of the runner-authored region *concatenated with the hash of the payload ciphertext*, and the payload's AEAD binds that tag. A checkpoint verifies only as a whole; an envelope region from checkpoint *i* spliced onto the payload of checkpoint *j* fails, which the two independent authentications of the previous draft allowed.

The envelope is a closed schema that rejects unknown members, and its contents are constrained so it cannot become a data channel:

- **Fault**: a closed-vocabulary classification code plus `stepId` and `attempt`. Free-text failure descriptions and provider error bodies are payload.
- **Correlation**: *both* the wait match key and the run-level correlation id are blinded — `HMAC-SHA256(HKDF(payloadKey, "wait-index" ‖ keyId), len(channel) ‖ channel ‖ len(correlationId) ‖ correlationId)`, length-framed so `("orders","123")` and `("order","s123")` cannot collide — computed and queried by runners. Plaintext correlation ids live in the payload. A channel-only (wildcard) wait uses an explicit sentinel and is documented as leaking a per-channel constant; a sealed environment may forbid them. The store keys the index by `(keyId, index)` so both generations match during a re-key roll, and the runner verifies that a run returned for a query actually carries the queried index in its MAC'd region before delivering a message to it.
- **Security tags** are stamped from the catalog version at run start and live in the runner-MAC'd region: the control plane reads them (they drive the ADR 0004 reach gate) but cannot rewrite a run into another operator group's reach without failing verification.
- **Identifiers** are length- and charset-bounded, and for a sealed environment the journal records an index into an encrypted step map rather than the tenant-authored `stepId` — mandatory, not opt-in, because an identifier is otherwise an unbounded free-text channel.
- **The AEAD algorithm id and key id** are inside the MAC'd region and the AEAD's own AAD. An unauthenticated algorithm selector is a downgrade primitive the moment a second algorithm exists, and answering "unsupported algorithm" differently from "authentication failed" is an oracle on an otherwise non-disclosing surface: both fault identically.

The **stored region is opaque octets**. No backend may parse, patch, or re-emit it — server-side JSON rewriting of the kind used elsewhere in this codebase (jsonb_set, JSON_MODIFY, a document store's own re-serialization) would silently destroy a byte-exact MAC. The runner submits only its own region and the crypto regions; the control-plane region is held and written by the server, joined at read time, and excluded from the MAC by construction rather than by convention, so neither party can rewrite the other's bytes.

**5. Payload encryption is envelope encryption under a per-encryption data key.** The environment's payload key is a **wrapping and derivation** key, never a direct AES-GCM key. Every wrap and unwrap carries an encryption context of `(environmentId, runId, keyId)`, enforced by the protector conformance suite, so key material is bound to the environment and run it was written for.

Two rules govern derivation, and both are conformance-tested because getting either wrong is a full break:

- **Every derivation input is length-framed and domain-separated.** Info is `len‖label ‖ len‖environmentId ‖ len‖keyId ‖ len‖runId ‖ uint64(sequence) ‖ salt`. Without framing, `(keyId "k1", runId "0abc")` and `(keyId "k10", runId "abc")` derive the *same* key — and with a counter nonce that is a repeated `(key, nonce)` pair over different plaintexts, which yields the GCM authentication subkey and hence payload forgery. Labels are a closed set — `wrap`, `data-key`, `envelope-mac`, `wait-index`, `checkpoint-token` — so a tenant-registered `keyId` cannot collide a data key with a subkey.
- **A fresh 32-byte salt for every *encryption operation*, never per logical checkpoint and never per retry.** Re-encrypting the same checkpoint after a `409` with a cached salt restarts the counter nonce under the same derived key: same trap, same break. A counter nonce is permitted only because this rule holds.

**6. Freshness is a validated monotonic sequence, a payload chain, and a server-minted lease epoch.** AAD binding alone is not freshness: an old checkpoint served with its own matching envelope verifies perfectly. The mechanism has three parts, and the previous draft's version of it was unimplementable — a runner must fix its AAD at encryption time, so the AAD cannot contain a value the server assigns afterwards:

- **Sequence**: the runner proposes `n`, the server accepts only `persisted + 1`, and the CAS predicate is `(etag, persistedSequence)`. The server *validates* rather than assigns, so the value is predictable to the writer and authoritative in the store. The runner-authored region carries `n` (inside the MAC), so a server that lies about acceptance is caught. A save that is superseded answers `409` with the accepted sequence and chain tip — never a `204` indistinguishable from a durable write. **A retry is a byte-identical resend**: the runner retains the exact transmitted bytes for the in-flight sequence and compares an acknowledged tip against *those*, never against a re-encryption — otherwise a single dropped HTTP response faults the run, because a fresh salt necessarily produces a different ciphertext and therefore a different tip.
- **Chain**: the payload AAD is `(runId, environmentId, keyId, previousPayloadCiphertextHash)`; the genesis link is `H(runId ‖ environmentId ‖ keyId ‖ sealed-inputs-ciphertext)`, unique per run, so rollback-to-genesis is not a free re-run.
- **Epoch**: the control plane's lease grant carries a **monotonic epoch it can only increment**, minted server-side per grant and written by the runner into the MAC'd region. Two rules use it, and they are deliberately not symmetric: a runner refuses a checkpoint whose epoch is **above** the one its current lease grants (a stale holder writing under a superseded grant), and refuses one whose epoch is **below** the run's high-water epoch (a rollback). The high-water mark comes from the tenant-side anchor below, not from what a process happens to have observed — a rule predicated on process memory would refuse a runner re-opening its own checkpoint under its own lease, which is the steady-state path, while accepting the rollback it exists to catch.
- **Anchor**: the epoch and the chain both need a high-water mark the control plane cannot rewrite, so the tenant keeps one — a small runner-host-local or tenant-KV record of `runId → (epoch, sequence, chain tip)`, written before each save. A tip carried in a control-plane response is a *hint*; a mismatch against the anchor is a hard fault. Without this the control plane supplies both the rolled-back row and the value used to judge it, and detection is worth nothing.
- **Incarnation**: every freshness counter above lives in the control plane's store, so a restore from backup or a migration between backends rewinds all of them at once — re-accepting a spent sequence, re-issuing epochs, and (with derived keys and counter nonces) reproducing a `(key, nonce)` pair over different plaintext. A store **incarnation id**, advanced out of band on every restore or migration and never itself restored, is part of the epoch and of the data-key derivation info. A restore invalidates every in-flight lease.

**7. Control-plane envelope writes are requests, and they do not collide with runner saves.** Cancel, resume request, and purge marker are control-plane-authored fields the runner validates against its own MAC'd state. They carry their own CAS predicate, distinct from the runner's `payloadSequence`, so a control-plane envelope write cannot invalidate an in-flight runner save — otherwise merely touching the envelope becomes a liveness weapon that faults an advance whose external effects have already landed, and the retry repeats them.

**8. Payload-mutating resume is a custody control, not an integrity control (ADR 0022).** Only faulted-step retry *at the current cursor* is envelope-only. State-patch, skip-with-outputs, **and rewind** all mutate payload: a rewind moves the cursor back and re-runs forward, overwriting the re-executed steps' outputs and repeating their external side effects, so classifying it as envelope-only would have left the control plane an unauthorized forced-re-execution verb. For a sealed environment the control plane records any of them as a request and the environment's runner applies it inside its own boundary. **This must not be mistaken for protection**: the patch content is still authored by the control plane, and a runner cannot judge whether rewriting a payment amount was legitimate. For a sealed environment such a mutation therefore requires a tenant-side authorization — a signature from a tenant-held operator key, or an explicit confirmation at the runner — and the applied patch is recorded in the encrypted journal so the tenant can audit it.

**9. Run-start inputs are sealed by an initiator the tenant controls, and the initiator names the run.** The initiator fetches the environment's public seal key, pins its fingerprint, seals the inputs, and submits ciphertext, so the control plane never holds plaintext inputs. **The initiator chooses a high-entropy run id** and the seal's AAD binds `(environmentId, baseWorkflowId, versionNumber, sealKeyId, runId)`; the control plane must use that id. A replay then collides on the primary key and is refused by the store — which is what makes the anti-replay property durable, where an unbounded, forever-lived nonce set spanning every runner host of the tenant would not be.

Three consequences of this must be stated rather than assumed:

- **Browser-served initiators cannot provide this property.** The designer and console are served by the control plane, which could serve code that skips the pin or posts plaintext. Sealed-environment starts are therefore restricted to initiators whose code the tenant controls (the CLI, a tenant-hosted trigger host); a browser-initiated start is badged as control-plane-trusted on the run record and in the console rather than silently claimed as sealed.
- **Every ingress that carries inputs or business keys is in scope**: HTTP start, schedule create, run-schedule-now, message triggers, and the dispatcher workflow. A dispatcher *workflow* cannot start a sealed run (a workflow step is an ordinary HTTP call with no sealing machinery), so message-triggered starts for a sealed environment go through a runner-side trigger host that holds the seal key. Idempotency keys are blinded with the wait-index construction; a schedule's target inputs are sealed and never re-read by the control plane.
- **Input-schema validation moves to the runner** at first claim, since the control plane sees only ciphertext: the documented `422` becomes a fault classification, and decrypt or schema failures are rate-limited and counted against the environment's start quota so they cannot be used as an amplifier.

**10. Sealing is decided runner-side against a default-deny allowlist, fails closed on key unavailability, and is required in multi-tenant mode.** A runner's configuration is an **allowlist of the environment ids it will serve at all**: a binding the control plane writes for an environment not on that list is refused at claim. Otherwise the rule "if a payload key is configured, enforce" is fail-*open* for any environment the runner has no entry for — a hostile control plane creates one, binds the tenant's runner to it, and harvests plaintext from runs executed with the tenant's own credentials. For an allowlisted environment, a missing or unresolvable payload key is a fault, never a cleartext write. The runner API independently refuses a cleartext payload for any environment whose record is sealed. The same rule governs the residue mitigations: a runner configured for a sealed environment **always** emits the reduced journal and pads the payload **plaintext** to length buckets before encryption (with the pad length inside the authenticated plaintext — padding applied to ciphertext is either strippable framing or breaks the AEAD), and the API refuses an unpadded or full-journal envelope — otherwise those mitigations are flags on a record the platform owns and can clear. Per ADR 0016, creating an environment without a key registration fails in the multi-tenant production posture; other postures badge unsealed environments on every view. The platform's own `system` environment is never sealed and its runs are platform data.

**11. Tenant-side execution infrastructure, and no key material in guests.** The serverless checkpoint listener terminates the guest's plaintext checkpoint, so it holds the payload key, performs the split-encrypt-MAC, and speaks the runner API; it never holds a store credential (the Container Apps recipe built for ADR 0062's live proofs gave it one, and that shape is corrected in phase A). Its **load path is a decryption oracle** — it serves plaintext for any run in the environment — so it authenticates with the platform's native workload identity in addition to the ADR 0062 token, the token secret is derived per `(environment, keyId)` from the payload key rather than a standalone shared secret, token lifetime is capped validator-side, and a listener compromise is recorded below as yielding the environment's plaintext.

**No payload key material, data-key generation, or nonce generation ever happens inside a micro-guest or serverless guest.** ADR 0064's snapshot-after-warm-up restores the guest CSPRNG to its snapshotted state, so a guest generating keys or nonces would repeat them on every advance of every run — forfeiting the authentication key the anti-forgery argument rests on. Encryption happens in the runner or listener process only.

**12. Key lifecycle, and an accurate account of purge.** Rotation registers a new key id (signed by the outgoing private key, so a pinned initiator accepts a successor automatically and an unsigned change is a hard fault; registration and rotation carry a proof of possession over `(environmentId, newKeyId, predecessorKeyId, server challenge)`). The first registration in a multi-tenant production environment blocks run starts until a tenant operator records an explicit fingerprint confirmation. A runner-driven **re-key sweep** re-seals payloads *and* re-derives wait indexes under the new id, retiring a compromised generation; a maximum generation lifetime is configured per environment. The sweep **takes each run's lease** (or a sweep-exclusive marker) before re-sealing and bumps the sequence, returning the new tip: re-sealing rewrites the ciphertext the next checkpoint's AAD chains to, so a sweep racing a live advance would otherwise produce a row nothing can ever decrypt. A runner whose cached tip is stale on `409` re-anchors from its tenant-side anchor rather than faulting.

**Deleting an environment** requires that no live runs or leases remain, cascades to its bindings and index rows, records the key generation as crypto-shredded, and is CAS-conditional. An environment id is never reusable — the id is the derivation and blind-index scope, so delete-then-recreate would otherwise reset a sealed environment to unsealed and strand ciphertext whose key registration is gone.

Purge is described by enumeration, because a compliance reader needs precision: it removes the run row (envelope and payload together) and its wait and timer index rows. It does **not** reach governance-audit and telemetry records keyed by run and workflow id, nor store backups — which hold envelopes in the clear. Because payloads are envelope-encrypted, **crypto-shredding** (destroying a key generation, or wrapping per-run data keys under a per-run key) is the mechanism that makes payload erasure verifiable where row deletion cannot reach.

**13. Sequencing, and what each phase does and does not deliver.** Phase A: the runner API, machine-principal authentication and principal-derived leases, non-disclosing refusals and quotas, the validated sequence and single-row CAS with `409` on supersession, the listener re-platforming, and the retirement of runner store credentials. Phase B: the envelope/payload split, envelope-encrypted payloads with the chain and epoch, the unified MAC, blind indexes, client-side input sealing, and runner-mediated payload resume. Phase C: executor provenance under mutual distrust.

**Phase A carries no cryptography, so it leaves the control plane the sole custodian of every tenant's plaintext** — with runners stripped of even the distributed custody they had. The gate against deploying on phase A alone cannot be a startup flag, because multi-tenancy is emergent from the data rather than declared in configuration: a deployment that legitimately runs single-tenant and later onboards a second tenant does so long after construction. It is therefore a **write-time data invariant** — creating an environment or a binding belonging to a second distinct owner group is refused while any environment lacks a registered payload key — enforced alongside the ADR 0016 mode check for the modes that admit row security.

## Ownership

```mermaid
flowchart LR
  subgraph CP["CONTROL PLANE — platform-owned, multi-tenant shared"]
    GAPI["Governance REST API (operators, designers)"]
    RAPI["Runner API (claim, lease, checkpoint, load, queues)"]
    ST[("Durable store — the ONLY store credential lives here")]
    CAT["Catalog + build + signing"]
    DISP["Dispatch + wait/timer indexes (envelope + blind keys)"]
    GAPI --> ST
    RAPI --> ST
    CAT --> ST
    DISP --> ST
  end
  subgraph TA["TENANT A — runner host (tenant-owned)"]
    RA["Runner A: payload key + seal keypair + cloud credentials + source secrets"]
    LA["Checkpoint listener A (split, encrypt, MAC)"]
    TH["Trigger host A (seals inputs for message-started runs)"]
    BA["Backends: ALC | serverless invoker | micro-guest sidecar — NO key material"]
    RA --- LA
    RA --- TH
    RA --- BA
  end
  subgraph TB["TENANT B — runner host (tenant-owned)"]
    RB["Runner B: its own keys, listener, trigger host"]
  end
  RA -- "machine principal, scoped to A's environments" --> RAPI
  RB -- "machine principal, scoped to B's environments" --> RAPI
```

The control plane owns the store, the APIs, the catalog and build pipeline, dispatch, and every index — and holds no key that opens tenant data. Each tenant owns its runner host, listener, and trigger host, and every secret on them. The only path from a runner to durable state is the runner API.

## Tenant separation on shared infrastructure

```mermaid
flowchart TB
  subgraph RAPI["Runner API — server-side enforcement"]
    AUTH["1: authenticate the machine principal (IdP, no platform-minted secret)"]
    BIND["2: resolve environment bindings (30s cache, ADR 0027 fence, system-binding exclusion)"]
    SCOPE["3: refuse anything outside them as 404 (ADR 0004), by path not content hash"]
    LEASE["4: derive lease ownership from the principal, gate load on a held lease"]
    CAS["5: single-row CAS on (etag, validated sequence), 409 on supersession"]
    AUTH --> BIND --> SCOPE --> LEASE --> CAS
  end
  subgraph ROW["A sealed environment's run row, at rest"]
    ENV["Envelope (closed schema): cursor, status, blind wait key, fault CODE, security tags, epoch, sequence"]
    MAC["One HMAC over canonical runner region + H(payload ciphertext) — the two are inseparable"]
    PAY["Payload: per-checkpoint data key wrapped to tenant A's key. AAD = run + env + keyId + previous-ciphertext hash"]
    ENV --- MAC --- PAY
  end
  RAPI --> ROW
```

Separation is layered, and each layer holds if the one above fails: a tenant's runner has no store credential to abuse; the API refuses cross-environment operations server-side and non-disclosingly; the payload is AEAD ciphertext under a key that never left its owner's custody; and the runner-authored region is MAC'd together with the payload, so platform tampering is detected rather than silently obeyed.

## The anatomy of an advance

```mermaid
sequenceDiagram
  participant I as Initiator (CLI / tenant trigger host)
  participant CP as Control plane (runner API + store)
  participant R as Tenant runner + listener
  participant G as Guest (backend, no key material)
  participant S as Tenant source API
  I->>I: pin the environment seal-key fingerprint, seal inputs (AAD binds env + workflow + version + nonce)
  I->>CP: start run (ciphertext inputs)
  R->>CP: claim (machine principal, lease grant carries a monotonic epoch)
  R->>R: open sealed inputs, validate against the version's input schema
  R->>G: invoke (ALC call, function URL, or micro-VM restore)
  G->>S: source call(s)
  G->>R: checkpoint back (Model B, ADR 0062 — to the TENANT's listener)
  R->>R: split, encrypt payload (fresh data key, AAD chains to previous ciphertext), one MAC over region + ciphertext hash
  R->>CP: save (proposed sequence validated as persisted+1, single-row CAS, 409 if superseded)
  Note over CP: governance reads and acts on the ENVELOPE of every run, payload-touching verbs are requests the runner applies
```

## Tenant-side data minimization (outside this platform's scope)

A tenant with strict governance obligations — GDPR and comparable regimes for personal data, or sector rules that forbid regulated values leaving a system of record — has an option stronger than any platform control: **design the workflow so the sensitive values never enter it.** Pass handles rather than values and let the tenant's own service resolve them; proxy third-party calls through a tenant-owned facade that injects regulated fields inside the tenant's boundary; return decisions rather than the records they were computed from; keep a regulated sub-process entirely tenant-side and report only its outcome.

This composes with the encryption design rather than replacing it, and it is the technique that most reduces the residues below — but it does **not** eliminate them, and the previous draft overclaimed here. A handle used as a correlation or idempotency key is still subject to blind-index equality and frequency analysis; run existence and rate, workflow and version identity, environment, source identity, step timing, and ciphertext length remain visible; and minimization does nothing about binding issuance or executor provenance, so it is no defence against a malicious control plane. Its real force is narrower and still valuable: it keeps the platform from being a processor of the regulated data at all.

The platform's obligations are to make this practical and to be accurate about what it holds: step data stays opaque, the documentation states exactly which fields are platform-visible so tenants can design around them, and no guidance suggests putting regulated values in tags, correlation ids, or workflow identifiers.

## Accepted residues

Two rounds of adversarial review shaped the decisions above. What remains, stated so nobody has to rediscover it:

- **Until phase C, this is confidentiality against passive platform operators, backups, and other tenants — not against a malicious control plane.** The platform code-generates, compiles, signs, and delivers the executor that holds the payload key and decrypts the data; a malicious control plane could bake an exfiltrating executor whose signature chain validates. Phase C (tenant countersignature) closes it; the cheap partial available sooner is a tenant-held allowlist of promoted content hashes that the runner refuses to load outside of.
- **The environment is the blast radius.** Bindings, the payload key, and API authorization are per environment, so a compromised runner host reads and rewrites every run in its environments, including runs it never executed. Load is gated on a held lease — but because claim *returns* the row (the optimization that collapses three round trips into one), reading is how a lease is acquired, so **claim-with-row is itself a bulk read path**: it is batch-capped, per-tenant rate-limited, and audited exactly like the re-key sweep, and it burns an epoch on every run it touches, which is also a lease-denial primitive against the tenant's own other runners. In phase A, before any encryption exists, that path exports plaintext.
- **The index projection is not authenticated.** The reach gate filters on projected columns (security tags, environment, workflow id), which sit outside the MAC'd region, so tampering with a column changes reach immediately regardless of the blob. The runner re-derives the projection from the MAC'd envelope and compares on every open, but the control plane — the only party that reads the projection for governance — structurally cannot verify a MAC under a key it does not hold.
- **A run that reaches a terminal state is never re-opened**, so envelope tampering on completed runs is never detected unless the tenant runs a periodic verification sweep or keeps a terminal-state digest.
- **A listener compromise yields the environment's plaintext**, because the listener's load path decrypts.
- **Envelope metadata is visible to the platform, and for data-dependent workflows that includes the decision, not merely the shape.** The mandatory reduced journal and length-bucket padding blunt this; retry counts, status, and timing still disclose control flow.
- **Blind indexes leak equality and frequency**, and a wildcard wait leaks a per-channel constant.
- **Rollback is detected, not prevented**, and only because the tenant-side anchor of decision 6 exists — the row's own epoch and chain are self-consistent after a rollback, so nothing in the control plane's own state can catch it.
- **Forced duplicate execution remains possible without forgery**: the control plane can expire a lease mid-advance and grant it elsewhere; the epoch makes the second holder detectable, but the external side effects of both advances have already landed.
- **Payload-mutating resume is custody, not integrity** (decision 8), and the tenant-side authorization is what makes it a control at all.
- **For sealed environments some read surfaces degrade to envelope-only**: the step-journal read, the outputs disclosure tier, and any schedule surface that reads a spec from run payload. "No verb is lost" applies to the mutating verbs, not to payload reads — and any future convenience that has the runner push a decrypted journal to the console reverses this entire decision.
- **Restore and migration are a reset of every freshness mechanism**, mitigated by the incarnation id but requiring the operational discipline that the id is advanced out of band and never restored from the backup it invalidates.
- **Availability inverts relative to ADR 0023.** The control plane is on the hot path of every checkpoint of every tenant: an outage stalls execution and expires leases. The runner API is scaled and available ahead of governance, leases survive a blip without a mass re-claim storm, and any non-2xx on an interim save (other than a `429` quota hold) fails the advance rather than being dropped.

## Consequences

- The control plane governs every run with full envelope access — every mutating verb survives, two of them runner-applied — while tenant data is confidential against the platform's operators, other tenants, and backups by key custody.
- Runners lose their store credentials entirely, which dissolves the per-runner-database-role and row-level-security problem of the first design: there is no credential left to scope.
- ADR 0023 is revised, not discarded: the store stays the queue and the two-process split stays, but the runner's half speaks a versioned API — the new explicit seam, with a conformance isolation class exercising foreign, revoked, and cross-environment access, and protector conformance covering the derivation framing and encryption context.
- **Not every current backend can host a sealed environment.** Two capabilities become conformance requirements rather than nice-to-haves: expiring leases by principal (the revocation fence — implemented today only by the in-memory, Postgres, and SQLite stores), and a single atomic row-plus-index compare-and-swap (a backend that writes the row and the wait index non-atomically can acknowledge a durable checkpoint whose blind wait row is missing, and once the plaintext channel column is gone there is nothing left to recover the wait from). A backend lacking either is not a supported sealed-environment backend, and the conformance suite says so.
- The in-process draft runner, the in-process simulator, and the draft-run trace store execute tenant code and persist plaintext step outputs inside the control-plane process. A runner bound to a sealed environment refuses draft runs, the runner API refuses draft-run and trace writes for one, and supplying those components to a control plane in the multi-tenant production posture fails construction.
- Every advance pays a runner-to-control-plane hop per checkpoint; task #222's instrumentation measures it, and the execution-backend trade-offs guide's checkpoint-locality story is rewritten for all backends uniformly. A performance review of this design produced [implementing secure checkpointing without paying for it twice](../guides/secure-checkpointing-performance.md), which is binding on the implementation: it sets the per-checkpoint budget (crypto under 5 µs, zero allocation, one round trip, zero KMS calls), specifies the optimizations that preserve each decision's property, names the costs that are unavoidable because they *are* the security design, and names the plausible-looking optimizations that would be security regressions.
- The checkpoint serialization is the envelope/payload split with the chain, built directly — nothing is deployed, so there is no legacy shape. An unsealed environment writes the same structure with its payload clear, and a multi-tenant-mode deployment cannot create one.
- The demo topology changes visibly: runners stop opening the shared database, and the AppHost wires runner-API addresses and machine-principal credentials instead of a connection string.