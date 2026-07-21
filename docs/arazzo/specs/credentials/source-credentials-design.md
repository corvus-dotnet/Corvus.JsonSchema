# Arazzo source credentials: storage, lifecycle, and refresh

This spec covers how source credentials are stored, resolved, and rotated: the `secretRef` boundary that keeps
secret material out of the control plane, the resolver that fetches it at run time, and the separation of
duties between authoring a credential binding and resolving the secret. It was split out of the execution-host
design so the credentials subsystem stands on its own.

The section numbering is retained from the original execution-host design (this spec begins at §13), so
existing cross-references to these sections stay valid.

## 13. Source credentials — storage, lifecycle, refresh

Sources need credentials (bearer tokens, OAuth client-credentials, API keys, mTLS certs — §8). The run
requester must **never** supply them: a run carries only inputs. Credentials are **host/operator-managed
state**, bound to the catalog version's sources and resolved per run by the transport binding (§8), so
rotation is transparent — the next run/resume picks up the current secret without changing the workflow or
the request.

**Secret material lives in a dedicated secret store, never in the Arazzo database.** The durability layer
persists only a **secret reference** (a scheme'd pointer such as `keyvault://`, `awssm://`, `vault://`,
`env://`, `file://`) plus **non-sensitive lifecycle metadata** (`kind`, `expiresAt?`, `rotatedAt`, derived
`credentialStatus`); the runner dereferences the reference through an `ISecretResolver` **at bind time** and
the auth provider caches only short-lived derivatives (e.g. an OAuth access token) in memory. No secret —
encrypted or otherwise — is ever written to the Arazzo store, a checkpoint, an index, a log, or telemetry.
A compromise of the Arazzo store therefore yields references and expiry dates, **never usable credentials**;
the secret store's ACLs and access audit are the security boundary. Secrets are never logged or surfaced on
any control-plane response.

### 13.1 Credential store and binding

- **`ISourceCredentialStore`** in the durability layer (per-backend, like the run/catalog/registry stores),
  holding a **`SourceCredentialBinding`** per `(sourceName, environment/tenant)` —
  `{ sourceName, environment, kind (bearer | oauth-client-credentials | api-key | mtls), secretRef, expiresAt?, rotatedAt }`.
  **`secretRef` is a reference, not the secret** (e.g. `keyvault://vault/secret[/version]`, `awssm://arn`,
  `vault://mount/path#field`, `env://VAR`, `file://path`); the store never holds secret material. Bindings are
  not sensitive, so they persist as plain JSON like every other entity — no protected-store wrapper needed.
- **`ISecretResolver`** (runner-side, read-only) dereferences a `secretRef` to live secret material at bind
  time: `ResolveAsync(SecretRef, ct) → SecretMaterial`, scheme-dispatched to a provider — **Azure Key Vault**
  (its *secrets* client, distinct from the existing KeyVault checkpoint key-wrap protector), **AWS Secrets
  Manager**, **HashiCorp Vault**, and in-box **`env://`/`file://`** (covers k8s Secrets mounted as a volume or
  projected to env). A discouraged **encrypted-in-DB** fallback (`SourceCredentialBinding` carrying a
  KMS/KeyVault-enveloped blob) exists only for deployments with no secret store; it is not the default and is
  documented as such.
- **Binding.** `WorkflowTransportRegistry` (§8) resolves each source's `IApiTransportFactory` from the
  credential store **and the secret resolver**: it reads the binding, dereferences its `secretRef` via the
  `ISecretResolver`, and builds the `IHttpAuthenticationProvider` (`BearerTokenAuthenticationProvider`,
  `ApiKeyAuthenticationProvider`, … already exist) from the **resolved** secret. For
  **oauth-client-credentials** the provider holds the long-lived client id/secret and fetches + caches a
  short-lived access token at runtime, so *access-token* expiry is handled automatically by re-fetching; only
  the **long-lived** secret (client secret, refresh token, API key, cert) is what §13.2 tracks for operator
  rotation.
- **No per-run credentials, no per-requester secrets.** The seam already established (`IApiTransportFactory`
  per source) means credential resolution is entirely host-side; the trigger surface (§6) is unchanged.
- **Least privilege — control plane vs runner.** Only the **runner** holds secret-store *read* access (it
  resolves at bind time). The **control plane** manages bindings, metadata, status, and the rotation
  lifecycle and needs **no** access to secret material. By default rotation is **reference-rotation** (the
  operator rotates in the secret store; Arazzo re-reads), so the control plane never handles plaintext. *(Design-intent,
  not built: an optional, off-by-default `ISecretWriter` for control-plane write-through to the secret store where that
  trade-off is wanted — no such interface exists in the code today; reference-rotation is the only path.)*
- **Composing the resolver set (`SecretResolverBuilder`).** The runner brings its own resolver set: a
  deployment registers exactly the secret stores it uses and hands the result to its
  `SourceCredentialProviderFactory`. The built-in `env://`/`file://` resolvers live in the core durability
  assembly; each external store ships its resolver — and a matching `Add…` extension — in its **own** package
  (`…Durability.KeyVault`, `…Durability.AwsSecretsManager`, `…Durability.Vault`), so a deployment references only
  the SDKs it actually uses and still supplies the SDK client (least-privileged identity). Schemes are disjoint
  so registration order is irrelevant, and `Build()` rejects two resolvers for the same scheme rather than
  silently shadowing one:

  ```csharp
  ISecretResolver resolver = new SecretResolverBuilder()
      .AddEnvironmentAndFile()         // in-box env:// and file://
      .AddKeyVault(credential)         // …Durability.KeyVault  (Azure TokenCredential)
      .AddAwsSecretsManager(smClient)  // …Durability.AwsSecretsManager
      .AddHashiCorpVault(vaultClient)  // …Durability.Vault
      .Build();

  var factory = new SourceCredentialProviderFactory(resolver);
  ```

  This is purely composition ergonomics over the bring-your-own-resolver model — it widens nothing: the control
  plane and the durability stores still never hold a resolver, and a scheme with no registered resolver still
  fails closed.

> **mTLS source credentials — DELIVERED.** `SourceCredentialKind.Mtls` (+ the `"mtls"` JSON token, `Parse`/
> `ToJsonToken`, and the OpenAPI `SourceCredentialKind` enum, regenerated into the Server+CLI `Generated/`) is built.
> It is the one kind that uses **more than one** secret slot: a `certificate` (required) plus an optional `privateKey`
> and `passphrase`. **The crucial design point** — a client certificate is established at the **TLS handshake
> (connection-level)**, not per request, so it does **not** flow through the §13.4 per-request resolution path: it is
> resolved once and configured on the source's `HttpClient` handler at construction. Concretely:
> - **Resolution.** `SourceCredentialProviderFactory.ResolveClientCertificateAsync` dereferences the `certificate`
>   (and optional `privateKey`/`passphrase`) secret **references** via the `ISecretResolver` into an
>   `X509Certificate2` — a base64 PKCS#12/PFX (optionally passphrase-protected), or a PEM `certificate` paired with a
>   PEM `privateKey` (a PEM key is re-exported to PKCS#12 so it is usable for client auth on every platform). The
>   per-request `CreateAsync(mtls)` returns a **no-op** `IHttpAuthenticationProvider` (nothing to add per request).
> - **Wiring.** `SourceCredentialTransports.CreateSourceHttpClientAsync` builds the host-owned per-source client with
>   the certificate on a `SocketsHttpHandler.SslOptions.ClientCertificates`, owning the certificate's lifetime; the
>   host installs the result in the dictionary it hands to `CreateBinder`.
> - **Connection-scoped, never usage-scoped.** Because the certificate authenticates the *deployment* to the source
>   (not an individual run), an mTLS binding is **shared** across the runs that reach the source and cannot be
>   usage-scoped: `SourceCredentialBinding.ValidateDraft` requires the `certificate` reference and rejects usage tags,
>   and the control-plane create handler refuses an explicit `usageGrantee` (`400`) and never applies the default
>   creator-identity usage scoping.
> - **Surface.** The credential UI (`credential-dialog.js`) offers `mtls` with a certificate slot plus optional
>   key/passphrase slots, forces "Shared", and hides the "Restrict to a grantee" option (correct-by-construction).
>
> The binding remains a *set* of role-tagged references, so this (and future multi-secret kinds) needed no schema
> change to the binding itself — only the `authKind` enum gained `mtls`.

### 13.2 Expiry tracking, states, and telemetry

- Each `SourceCredentialBinding` carries `expiresAt` when knowable (cert `NotAfter`, API-key/refresh-token
  lifetime) as **non-sensitive metadata** — so the monitor and UI read status without secret-store access.
- A catalog version derives a **`credentialStatus`** — `Valid` | `ExpiringSoon(at)` | `Expired` — as the worst
  status across the sources it binds (min `expiresAt`). It surfaces on the version's control-plane GET
  endpoints and the UI. The catalog list endpoint accepts a `credentialStatus` filter (indexed), so the
  **catalog UI can filter to active workflows with expiring/expired credentials** — the operator's primary
  rotation worklist.
- A control-plane **credential monitor** (a periodic sweep, like the runner-registry prune §5.4) evaluates
  credentials and:
  - **emits telemetry** so operators build their own alerting/rotation rules (we expose the signal, not a
    built-in scheduler): an `arazzo.credential.expires_at` gauge and `arazzo.credential.expired` counter,
    tagged by `sourceName` / `baseWorkflowId` / `versionNumber`. This is the "auto-reminder" — surfaced in
    OpenTelemetry and the UI's version view.
  - when a credential is **expired**, marks the version's binding **`Credentials Expired`** — a degraded,
    non-runnable state that gates *new* triggers (`409`, like the no-live-runner gate §6.2) while leaving
    catalogued/in-flight state intact.

**✅ Implemented — what shipped (and where it differs from this sketch).** The binding carries non-secret
`expiresAt?`/`rotatedAt`, and a **`credentialStatus`** (`valid` | `expiringSoon` | `expired`) is **derived per
binding on read** (never persisted, so it cannot go stale) and surfaced on the `/credentials` GET/list endpoints — the
operator's rotation worklist lives there. The **per-version catalog rollup was cut on security review**: joining
credential-binding lifecycle onto catalog versions would leak it to the broader catalog-read audience, crossing the
independent management/catalog scopes, so status stays on the credential-management surface only. Trigger gating moved
**runner-side**: the control plane has no environment (bindings are per `(source, environment)`, and environment is a
runner concern), so the expiry check happens at **bind time within the run's own usage entitlement** — the runner
raises a resumable **`credentials-expired`** fault when it binds an expired credential, and reactively when a bound
credential is rejected `401`/`403`; the warm bind path stays **0 B/op**. The operator reads and rotates through the
`/credentials` API and the `arazzo-runs credentials` CLI (`update` re-points a reference and stamps `rotatedAt`).

### 13.3 Faulted run → refresh → resume

- A run that fails because a source rejected its credential (`401`/`403`, or the binding throws a
  credential-expired error) records a **typed fault**: `Faulted` with `errorType = "credentials-expired"` and
  the offending source — distinguishable from ordinary faults and **filterable** in the control plane.
- An operator **rotates the secret in the secret store** (uploads a new secret / re-runs OAuth consent /
  rotates the cert) and, via a control-plane credential endpoint, updates the binding's **reference + metadata**
  (a new `secretRef` version and/or refreshed `expiresAt`) — the rotation lives with the catalog version's
  source binding, not the run, and the control plane touches only the reference, never plaintext (a control-plane
  write-through path — the design-intent `ISecretWriter` — is not built; reference-rotation is the only path today).
- **Resume** uses the existing machinery (retry/rewind §7.2): because the transport binding resolves the
  binding and **dereferences its secret at bind time**, the resumed run picks up the rotated secret
  automatically — the original requester is not involved. At-least-once step re-execution (§10) re-runs the
  interrupted step against the now-valid credential and continues from the last checkpoint.

### 13.4 Performance — secure-by-default must be ~free on the hot path

Security overhead has to be negligible or operators turn it off; the design goal is that enabling source
credentials adds **no measurable per-request cost** once warm, and amortizes the one-time resolve — so
secure-by-default is the cheap default, not a tax.

- **Runner-side credential cache (the cornerstone).** The runner caches, keyed by `(sourceName, environment)`,
  the resolved binding **and the built `IHttpAuthenticationProvider`** — not just the raw secret — with a
  **fairly short TTL** (bounded staleness so a rotation in the secret store is picked up within minutes without
  an explicit invalidation) plus eager rotation/version invalidation. The **warm path does zero secret-store I/O and zero per-request
  allocation**: the cached provider instance is reused and applies a **pre-built header value** (no per-request
  `"Bearer " + token` concatenation). A cache miss costs one secret-store round-trip, amortized across every
  subsequent run and request.
- **OAuth client-credentials.** The short-lived access token is fetched and cached until just before expiry;
  refresh is **single-flight** (one in-flight fetch; concurrent callers await it — no thundering herd) and
  **proactive** (refreshed ahead of expiry, off the request critical path), so the hot path never stalls on a
  token fetch.
- **Binding reads** follow the durability allocation discipline (pooled JSON, parse-non-copying, deferred
  holders) — the binding is a small reference document, read rarely and cached.
- **Bounded exposure for the perf/security trade-off.** Cached material is **memory-only** (never persisted or
  logged), TTL-bounded, scrubbed on eviction where the type allows; where possible only the *derived* artifact
  (the provider / header value) is retained rather than the raw long-lived secret.
- **Dual gate.** Every §13 hot path ships with **both** a trust-boundary assertion (no secret leaks to store /
  checkpoint / log / response) **and** a `MemoryDiagnoser` + latency benchmark proving the warm bind/auth path
  is ≈0 B and ≈0 added latency versus an unauthenticated request — the same benchmark rigour as the allocation
  campaign.

#### §13.4.1 Backend store allocation ledger (audited)

The warm bind (the per-request hot path) is benchmark-proven **0 B** (`WarmCredentialBindBenchmarks.Warm_GetProvider`)
— it is the runner cache, backend-independent. The credential **store** read is the *cold/miss* path the cache
amortizes; it is driver/IO-bound, so the right confidence tool is not a per-backend micro-benchmark (the driver's own
allocation dominates and is not ours) but a **static byte-flow audit** against a fixed ledger checklist, plus a
measured floor for the one in-process driver:

- **Read leaf.** The driver hands back the binding document as a `byte[]` (relational `GetFieldValue<byte[]>`, Mongo
  `BsonBinaryData.Bytes`, Redis `(byte[])RedisValue`, NATS KV `byte[]`, Azure Table `GetBinary`, Cosmos base64 field
  decoded straight to bytes) with **no intermediate copy, stream round-trip, or `System.Text.Json`**; it is parsed once
  via the pooled, zero-copy `PersistedJson.ToPooledDocument`.
- **Reach / usage filter.** Applied in memory over the small per-`(sourceName, environment)` candidate set through the
  **deferred holders** (`candidate.RootElement.ManagementTagsValue`, `IsUsableBy`) — never an eager tag-list
  materialization; non-matching candidate documents are disposed.
- **Write.** Serialized through the shared pooled-scratch `SourceCredentialSerialization` (`PersistedJson.ToArray`);
  `byte[]` only at the driver leaf.

This was audited across all nine backends (InMemory, SQLite, Postgres, SqlServer, MySql, Mongo, Cosmos, Redis,
NATS JetStream, AzureStorage): all conform. The lone deviation found was **Cosmos**, which stores the document as a
base64 field and originally materialised a transient base64 `string` before decoding — now decoded directly from the
response bytes (`CosmosJson.GetBytesFromBase64`), restoring the single-`byte[]`-leaf shape. Measured floor (the shared
CTJ parse + filter primitive, cache-miss): **624 B** in-process (InMemory); through a real embedded driver
(`SqliteSourceCredentialStore`) **2288 B**, the ~1.7 KB delta being `Microsoft.Data.Sqlite`'s reader/command, not our
code. Correctness of every backend's read/write path is proven by the shared 12-test `SourceCredentialStoreConformance`
(incl. the trust-boundary test) run on real containers via Testcontainers.

**Refinement (allocate-on-read work, `IObservedIdentityStore`).** The audit above is **confirmed, not reversed**: the
read `byte[]` a driver hands back (`GetFieldValue<byte[]>`, `BsonBinaryData.Bytes`, `NatsKVEntry<byte[]>`, `GetBinary`)
genuinely *is* the leaf — a measured in-process floor over a real embedded SQLite driver is **unchanged** by any read
reshuffling (`ObservedIdentityUpsertReadBenchmarks`: `Sqlite_Upsert` 8864→8865 B, `Sqlite_Search` 7872→7873 B), and a
`GetStream`/`Parse(Stream)` attempt to "pool" it **regressed** (SQLite buffers internally). Two narrow refinements still
apply and are worth doing on read paths generally: (1) parse the existing document **non-copying** (`ParsedJsonDocument
<T>.Parse(memory)`) over the driver's already-owned array when the consumer is synchronous (a merge/etag check), rather
than `ToPooledDocument` (which rents + copies bytes we already hold) — reserve the copying `ToPooledDocument` for documents
**returned to the caller**; (2) a backend that mints an *extra* array on top of an already-pooled buffer can drop it —
**Cosmos** parses off the live pooled query response (no `.ToArray()`), **Redis** reads a pooled `Lease<byte>` (no
`(byte[])RedisValue` cast). These are the only genuine GC wins on the read side; the relational/driver-minted array stays
the leaf. Confidence tool is unchanged: byte-flow audit + the one in-process driver floor + container conformance.

**Decision (§13):** secrets live in a **dedicated secret store, never the Arazzo database**; the durability
layer persists only an operator-managed **reference + non-sensitive metadata**, resolved to live secret
material by the runner's `ISecretResolver` at transport-bind time (Key Vault / AWS Secrets Manager /
HashiCorp Vault / env+file; encrypted-in-DB only as a discouraged fallback). The control plane manages
references/status with no secret read (reference-rotation by default; optional write-through). Expiry is
surfaced as version status + telemetry (operators own the rotation policy); a `credentials-expired` fault is
refreshable from the catalog and resumable with no requester involvement.

### 13.5 Secret provisioning — separation of duties (write ≠ read)

Resolution (§13.1) is only half the story. *Putting* the secret into the store is a **separate security
concern from reading it**, and the two MUST be distinct identities with distinct least-privilege policies. This
is the governing rule for the whole subsystem:

- **The runner is a read-only consumer.** Its secret-store identity is granted **read** on its own scoped paths
  and nothing else — no write, no access outside those paths. A runner that could write secrets is a privilege
  it never needs and an exfiltration/poisoning path it should not have.
- **Provisioning is a distinct, write-capable identity, owned by automation** — a CI/CD pipeline or a
  declarative IaC step (e.g. Terraform's Vault provider), never the consuming workload and never the control
  plane. It writes secrets + the consumer's read-only policy as code (explicit paths, no wildcards), versioned
  and auditable. "A CI pipeline for staging should not inherit access to production credentials."
- **The control plane never touches the secret store at all** — it persists only the *reference* (§13.1),
  upholding the no-secret-material invariant. Only the provisioner (write) and the runner (read) bind to it.
- **Secure introduction ("secret zero").** The runner still needs *some* identity to authenticate to the
  store. In production that is platform-native attestation — a Kubernetes ServiceAccount, cloud IAM (AWS/Azure/
  GCP), or Vault **AppRole** with a short-TTL, response-wrapped `secret_id` delivered by the trusted
  orchestrator — never a long-lived, write-capable token embedded in the workload. The orchestrator (the
  platform) is the trusted intermediary that hands the consumer its scoped identity.

#### 13.5.1 Local-dev composition (the locally-runnable stand-in)

Like SQLite stands in for the production durability store, the demo composition (Aspire AppHost,
`samples/arazzo/`) runs HashiCorp Vault locally and **mirrors the separation faithfully** rather than
collapsing it:

- a **Vault dev-mode container** (the secret store);
- a **one-shot provisioner** (a Vault-CLI init container — the "CI/IaC provisioning step" stand-in, the only
  write-capable identity): it writes a **read-only, path-scoped policy** (`path "secret/data/arazzo/*"
  { capabilities = ["read"] }`), mints the runner's **read-only token** bound to that policy, and seeds the
  demo secret values, then exits;
- the **runner** holds *only* that read-only token — its startup self-check resolves the demo reference **and**
  asserts a write is refused (403), proving the boundary is real;
- the **control plane** stays Vault-free, storing only the `vault://…` reference.

The security-critical properties — **separation of duties** and a **least-privilege, read-only consumer** — are
preserved exactly. The honest dev↔prod deltas (the locally-runnable concessions): the runner's read-only token
is delivered by the Aspire orchestrator rather than platform attestation/AppRole-wrapping; the dev root token
is a fixed value; secret *values* are non-sensitive dummies (in prod the provisioning step sources real secrets
from the CI's secure store). None of those weaken the write/read split.

**Decision (§13.5):** provisioning (write) and consumption (read) are **always separate least-privilege
identities**; the runner is read-only and path-scoped; the control plane never binds to the secret store. The
consumer's identity arrives by secure introduction (platform attestation / AppRole in prod; orchestrator-
delivered scoped token in local dev) — never a long-lived write-capable token in the workload.

