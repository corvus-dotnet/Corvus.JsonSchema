# ADR 0025. Integrity binding, with an optional signature custody split

Date: 2026-07-21. Status: **Accepted**. Scope: how a runner trusts the executor it loads. Builds on
[ADR 0024](0024-collectible-assembly-per-version.md). This records why loading verifies a digest binding by
default, and adds an optional detached signature with a custody split rather than mandating code-signing.

## Context

A runner loads and executes a compiled assembly. It has to be sure the assembly is the one the catalogue
produced for the version, not a tampered or mismatched one. The minimum is an integrity check: the assembly is
bit-for-bit what was catalogued, and it belongs to the package it claims. Some deployments need more, a
cryptographic guarantee of who produced it, but requiring that of every deployment, including a single-node one
where the control plane and runner are the same trust domain, would be friction with no gain.

### Grounded architectural facts

- **The manifest binds the assembly to the package by digest.** `WorkflowExecutorManifest`
  (`src/Corvus.Text.Json.Arazzo.Execution/WorkflowExecutorManifest.cs`) carries `assemblyDigest` (the SHA-256
  of the assembly) and `packageHash` (the package content hash). `WorkflowExecutorLoader.Load` verifies the
  assembly's digest matches the manifest, the package hash matches the expected content hash, and the target
  framework matches, before it loads.
- **A detached signature is optional, verified against a trust store.** `IExecutorPackageVerifier` /
  `TrustStoreExecutorPackageVerifier` verify an optional detached signature; `IExecutorPackageSigner` /
  `EcdsaExecutorPackageSigner` produce one. A single-node deployment passes no verifier and relies on the
  digest binding.
- **The signature is a custody split.** When configured (design work under #879), the signing key and the
  verifying trust store are held by different parties: the control plane signs the executor it catalogues, and
  the runner verifies against its trust store, so a runner will not execute an assembly the control plane did
  not sign.

## Decision

A runner trusts an executor by **verifying its integrity binding on load** (the assembly digest matches the
manifest, and the package hash matches the expected content hash). Cryptographic signing is **optional**, not
mandatory. Where a deployment configures it, a detached signature is verified against a trust store, with the
signing key and the verifying store held by different parties (a custody split): the control plane signs, the
runner verifies. A single-node deployment relies on the digest binding alone.

## Consequences

- Every load is integrity-checked, so a tampered or mismatched assembly cannot execute, in every deployment,
  with no configuration.
- A single-node deployment has no signing ceremony to run, because the digest binding is enough within one
  trust domain.
- A deployment that separates who may produce executors from who may run them configures the signature: the
  runner then refuses any assembly not signed by the trusted signer, which the custody split enforces.
- The verifier is a seam (`IExecutorPackageVerifier`), so a deployment chooses its trust-store implementation
  and signing algorithm without changing the loader.
