# ADR 0048. Source credentials are references, resolved runner-side

Date: 2026-07-21. Status: **Accepted**. Scope: how a source's credentials are stored and resolved. This records
why the control plane stores a **reference** to a secret and never the secret itself, and why the secret is
dereferenced by the runner at bind time.

## Context

A workflow's sources need credentials (a bearer token, OAuth client-credentials, an API key, basic auth, a
client certificate) to call their operations. The run requester never supplies them, a run carries only inputs,
so credentials are operator-managed state bound to a version's sources. The question is where the secret
material lives and who can read it. If the Arazzo control-plane store held the secret (even encrypted), a
compromise of that store, a backup, an index, a log, or a checkpoint could yield usable credentials, and the
control plane would need secret-read capability it does not otherwise need.

### Grounded architectural facts

- **The store holds a reference, not the secret.** `SourceCredentialBinding` persists `secretRefs[]`
  (role-tagged `{name, ref}`), non-secret `config`, and lifecycle metadata (`managementTags`, `usageTags`,
  `expiresAt?`, `rotatedAt`, audit fields), never secret material. A `SecretRef` is `scheme://locator[#version]`
  over the schemes `KeyVault`, `AwsSecretsManager`, `HashiCorpVault`, `Environment`, `File` (`SecretScheme.cs`,
  `SecretRef.cs`).
- **The runner dereferences at bind time.** `ISecretResolver` (runner-side, read-only) resolves a `SecretRef`
  to live material when a run binds its transports, scheme-dispatched. `SecretResolverBuilder` composes the
  bring-your-own set (env and file in the core assembly; Key Vault, AWS Secrets Manager, and HashiCorp Vault
  each in their own package), rejects a duplicate or empty scheme, and fails closed on an unregistered scheme.
- **The control plane has no secret-read.** It manages bindings, references, status, and the rotation
  lifecycle. The catalog-time gate `EvaluateSourceAccessAsync` returns `Granted`, `Denied`, or `Unconfigured`
  from the reference and its tags, never from the secret.
- **Rotation re-points the reference.** The default is reference-rotation: the operator rotates in the secret
  store and Arazzo re-reads, so the control plane never handles plaintext. A control-plane write-through
  (`ISecretWriter`) is deliberately not built.

## Options

**Store the secret (encrypted) in the Arazzo store.** Simple to reason about, one store. Rejected: it makes the
Arazzo store a secret store, so its compromise yields usable credentials, and it forces the control plane to
hold decryption capability. (An enveloped-blob fallback is noted as design-intent for deployments with no secret
store, off by default, and is not built.)

**Store a reference, resolve runner-side (chosen).** The store holds a pointer plus non-secret metadata; only
the runner resolves the secret, at bind time, as its own least-privileged identity.

## Decision

Source credentials are stored as **references**. The Arazzo store persists a `SecretRef` and non-sensitive
lifecycle metadata; the live secret is dereferenced by the runner's `ISecretResolver` at transport-bind time;
the control plane manages references and status with no secret-read capability; and rotation re-points the
reference. A compromise of the Arazzo store therefore yields references and expiry dates, never usable
credentials; the secret store's own access control and audit are the security boundary.

## Consequences

- Writing a secret and reading it are separated (a separation of duties). The runner holds a read-only,
  path-scoped token to the secret store; writing the secret is a separate CI or infrastructure identity the
  control plane never uses.
- Rotation is transparent to the workflow. Re-pointing the reference (or rotating in the store) means the next
  run or resume picks up the current secret without changing the workflow or the request.
- A deployment references only the secret-store SDKs it uses, because each resolver ships in its own package and
  the deployment supplies the SDK client (its least-privileged identity).
- An unregistered or unresolvable scheme fails closed, so a missing resolver denies the run rather than falling
  back to an insecure path.
- The store stays a plain-JSON store like every other entity; no protected-store wrapper is needed, because it
  holds nothing sensitive.
