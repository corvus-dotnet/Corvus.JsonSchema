# Arazzo source credentials: storage, lifecycle, and refresh

How a source's credentials are stored, resolved, and rotated. The governing decision, that the store holds a
**reference** and the runner resolves the secret at bind time, is
[ADR 0048](../adr/0048-source-credentials-are-references.md); the bytes-native store discipline is
[ADR 0037](../adr/0037-bytes-native-seams.md); the management and usage tags build on the two-plane access
model ([ADR 0001](../adr/0001-two-plane-access-model.md), [0002](../adr/0002-grant-verbs-are-reach-not-scopes.md)).
This guide is the exhaustive credentials detail those do not carry.

A source needs credentials (a bearer token, OAuth client-credentials, an API key, basic auth, a client
certificate) to call its operations. The run requester never supplies them, a run carries only inputs;
credentials are operator-managed state bound to a version's sources and resolved per run, so rotation is
transparent (the next run or resume picks up the current secret without changing the workflow or the request).
Secret material lives in a dedicated secret store, never in the Arazzo store, a checkpoint, an index, a log, or
telemetry ([ADR 0048](../adr/0048-source-credentials-are-references.md)).

## The credential store and binding

`ISourceCredentialStore` (per-backend, like the run, catalog, and registry stores) holds a
`SourceCredentialBinding` per `(sourceName, environment)`. The binding is not sensitive, so it persists as plain
JSON like every other entity. Its shape:

- **`secretRefs[]`**, a set of role-tagged `{ name, ref }` references (so a multi-secret kind like mTLS needs no
  schema change), plus non-secret **`config[]`** (for example a basic-auth `username`). A `SecretRef` is
  `scheme://locator[#version]` over the schemes `keyvault`, `awssm`, `vault`, `env`, `file`.
- **`authKind`**, one of `apiKey`, `bearer`, `basic`, `oauth2ClientCredentials`, `mtls` (`SourceCredentialKind`).
- **`managementTags`** and **`usageTags`** (the two-plane tags below), `usageKind` / `usageLabel`, `expiresAt?`,
  `rotatedAt`, and the usual `id`, audit fields, and `etag`.

**Resolution is runner-side.** The credential-aware binder is `SourceCredentialTransports.CreateBinder`, which
builds a `WorkflowTransportBinder` whose `SourceCredentialApiTransportFactory` and
`SourceCredentialAuthenticationProvider` resolve the binding from a `SourceCredentialCache` and build the
`IHttpAuthenticationProvider` through `SourceCredentialProviderFactory.CreateAsync`. (The plain
`WorkflowTransportRegistry` is the static, credential-unaware alternative this replaces; it carries no store or
resolver.) For `oauth2ClientCredentials` the provider holds the long-lived client id and secret and fetches and
caches a short-lived access token at run time, so only the long-lived secret is what rotation tracks.

**The resolver set is bring-your-own.** `ISecretResolver` (runner-side, read-only) dereferences a `SecretRef`,
scheme-dispatched. `SecretResolverBuilder` composes the set: `env` and `file` in the core assembly, and each
external store's resolver plus its `Add…` extension in its own package, so a deployment references only the SDKs
it uses and supplies the SDK client (its least-privileged identity). Schemes are disjoint, and `Build()` rejects
a duplicate or empty scheme.

```csharp
ISecretResolver resolver = new SecretResolverBuilder()
    .AddEnvironmentAndFile()         // in-box env:// and file://
    .AddKeyVault(credential)         // Azure TokenCredential
    .AddAwsSecretsManager(smClient)  // an IAmazonSecretsManager
    .AddHashiCorpVault(vaultClient)  // an IVaultClient
    .Build();
```

Only the runner holds secret-store read access; the control plane manages bindings, references, and status with
no secret-read. An encrypted-in-DB fallback (an enveloped blob for a deployment with no secret store) is
design-intent, off by default and **not built**, as is a control-plane write-through `ISecretWriter`;
reference-rotation is the only path today.

## The two-plane tags on a binding

A binding carries two independent tag sets, matching the two-plane model:

- **`managementTags`** are reach labels: who may administer the binding, filtered like every other reach-scoped
  resource ([ADR 0002](../adr/0002-grant-verbs-are-reach-not-scopes.md)).
- **`usageTags`** are a run entitlement: which runs may **use** the credential. A run may use a binding only if
  its identity carries all the usage tags (`IsUsableBy` / `ResolveForUsageAsync`, a label-superset, fail-closed).
  The `usageGrantee` an operator names is resolved bytes-to-bytes to the unforgeable internal tags; summaries
  describe the grant back without exposing the raw internal tags.

The catalog-time gate `EvaluateSourceAccessAsync` returns `Granted`, `Denied`, or `Unconfigured` from the
binding and its tags, so a run that a binding does not admit is refused before it starts.

## mTLS

`mtls` is the one kind that uses more than one secret slot: a `certificate` (required) plus an optional
`privateKey` and `passphrase`. A client certificate is established at the TLS handshake (connection-level), not
per request, so it does not flow through the per-request resolution path:

- **Resolution.** `SourceCredentialProviderFactory.ResolveClientCertificateAsync` dereferences the certificate
  (and optional key and passphrase) references into an `X509Certificate2`, a base64 PKCS#12/PFX or a PEM
  certificate paired with a PEM key (re-exported to PKCS#12 so it works for client auth on every platform). The
  per-request `CreateAsync(mtls)` returns a no-op provider.
- **Wiring.** `SourceCredentialTransports.CreateSourceHttpClientAsync` builds the host-owned per-source client
  with the certificate on a `SocketsHttpHandler.SslOptions.ClientCertificates`, owning the certificate's
  lifetime, and installs it in the dictionary handed to `CreateBinder`.
- **Connection-scoped, never usage-scoped.** Because the certificate authenticates the deployment to the source
  (not a run), an mTLS binding is shared and cannot be usage-scoped: `SourceCredentialBinding.ValidateDraft`
  requires the certificate reference and rejects usage tags, and the create handler refuses an explicit
  `usageGrantee` (`400`). The credential dialog offers `mtls` with a certificate slot, forces "Shared", and
  hides the restrict-to-a-grantee option (correct by construction).

## Expiry, status, and rotation

Each binding carries `expiresAt` when knowable (a certificate's `NotAfter`, an API-key or refresh-token
lifetime) as non-secret metadata, and a `credentialStatus` (`valid`, `expiringSoon`, `expired`) is **derived per
binding on read** (never persisted, so it cannot go stale) and surfaced on the `/credentials` endpoints, which
are the operator's rotation worklist.

There is deliberately no per-version catalog rollup of credential status: joining credential lifecycle onto
catalog versions would leak it to the broader catalog-read audience, crossing the independent management and
catalog scopes, so status stays on the credential-management surface only. Trigger gating is runner-side, since
the control plane has no environment (bindings are per `(source, environment)`, an environment is a runner
concern): the expiry check happens at bind time within the run's own usage entitlement. The runner raises a
resumable `credentials-expired` fault when it binds an expired credential, and reactively when a bound credential
is rejected `401`/`403`. The one credential telemetry counter is `corvus.arazzo.credentials.rotated`; operators
build their own expiry alerting from the binding metadata.

**Faulted run, refresh, resume.** A run that fails because a source rejected its credential records a typed
`Faulted` with `errorType = "credentials-expired"` and the offending source, filterable in the control plane.
The operator rotates the secret in the secret store and updates the binding's reference and metadata through a
control-plane credential endpoint (a new `secretRef` version and refreshed `expiresAt`, stamping `rotatedAt`);
the control plane touches only the reference. On resume (retry or rewind), the transport binding dereferences
the rotated secret at bind time, so the resumed run picks it up automatically with no requester involvement.

## Performance

Enabling source credentials must add no measurable per-request cost once warm, or operators turn security off.

- **The runner-side cache is the cornerstone.** Keyed by `(sourceName, environment)`, it caches the resolved
  binding **and the built `IHttpAuthenticationProvider`** (not just the raw secret) with a short TTL (bounded
  staleness, so a rotation is picked up within minutes) plus eager invalidation. The warm path does zero
  secret-store I/O and zero per-request allocation: the cached provider is reused and applies a pre-built header
  value. A cache miss costs one secret-store round-trip, amortized across every subsequent run and request.
- **OAuth client-credentials** refresh is single-flight (concurrent callers await one in-flight fetch) and
  proactive (refreshed ahead of expiry, off the request path).
- **Bounded exposure.** Cached material is memory-only, TTL-bounded, and scrubbed on eviction where the type
  allows; where possible only the derived provider or header value is retained, not the raw secret.

The store read and write are bytes-native ([ADR 0037](../adr/0037-bytes-native-seams.md)): the driver's
`byte[]` is the read leaf, parsed once through the pooled zero-copy `PersistedJson.ToPooledDocument`, the reach
and usage filter is applied in memory over the small candidate set through deferred holders
(`ManagementTagsValue`, `IsUsableBy`), and a write serialises through the shared pooled scratch, a `byte[]` only
at the driver leaf. The warm bind is benchmark-proven 0 B/op (`WarmCredentialBindBenchmarks.Warm_GetProvider`),
and every backend's read and write path is proven by the shared `SourceCredentialStoreConformance` suite
(including the trust-boundary test) on real containers.

**Hardening.** An OAuth token endpoint over plain HTTP is rejected unless a deployment opts in
(`allowInsecureOAuthTokenEndpoint`), and a `file://` resolver is confined to a configured root
(`SecretResolverBuilder.AddFile(secretRoot)`).

## Provisioning: separation of duties (write is not read)

Putting a secret into the store is a separate security concern from reading it, and the two must be distinct
least-privilege identities. This is the governing rule for the subsystem
([ADR 0048](../adr/0048-source-credentials-are-references.md)):

- **The runner is a read-only consumer**, granted read on its own scoped paths and nothing else.
- **Provisioning is a distinct, write-capable identity owned by automation** (a CI/CD pipeline or an IaC step
  such as Terraform's Vault provider), never the workload and never the control plane. It writes the secrets and
  the consumer's read-only policy as code (explicit paths, no wildcards), versioned and auditable.
- **The control plane never binds to the secret store** at all; it persists only the reference.
- **Secure introduction ("secret zero").** The runner authenticates to the store by platform-native attestation
  (a Kubernetes ServiceAccount, cloud IAM, or a Vault AppRole with a short-TTL response-wrapped `secret_id`
  delivered by the trusted orchestrator), never a long-lived write-capable token embedded in the workload.

### Local-dev composition

Like SQLite stands in for the production durability store, the demo composition (Aspire AppHost, `samples/arazzo/`)
runs HashiCorp Vault locally and mirrors the separation faithfully: a Vault dev-mode container (the secret
store); a one-shot provisioner (a Vault-CLI init container, the only write-capable identity) that writes a
read-only path-scoped policy (`path "secret/data/arazzo/*" { capabilities = ["read"] }`), mints the runner's
read-only token, seeds the demo secret values, and exits; the runner holding only that read-only token (its
startup self-check resolves the demo reference **and** asserts a write is refused with `403`, proving the
boundary); and the control plane staying Vault-free with only the `vault://…` reference. The honest dev-to-prod
deltas (the read-only token is delivered by the Aspire orchestrator rather than platform attestation, the dev
root token is fixed, and the secret values are dummies) do not weaken the write and read split.
