# ADR 0031. The content hash is over the RFC 8785 canonical form of the logical content

Date: 2026-07-21. Status: **Accepted**. Scope: what a package's content hash is computed over. Builds on
[ADR 0030](0030-immutable-content-hashed-versioned-packages.md). This records why a version's hash is the
SHA-256 of the RFC 8785 canonical form of the logical `{ workflow, sources }` content, not of the container
bytes.

## Context

[ADR 0030](0030-immutable-content-hashed-versioned-packages.md) content-addresses a version by a hash. The
question is what the hash is computed over. Hashing the raw container bytes would make the identity depend on
incidental framing: the order entries are packed in, JSON key order, whitespace. Two packages with identical
logical content but a different byte layout would then have different identities, so a repack (which the store
does when it rewrites the workflow id) would change the version's hash, and a client could not reproduce the
hash without reproducing the exact bytes.

The identity should be of the meaning, not the framing.

### Grounded architectural facts

- **The hash is over the RFC 8785 canonical form.** `WorkflowPackage.ComputeContentHash`
  (`Durability/Catalog/WorkflowPackage.cs`) computes the SHA-256, lower-hex, of the RFC 8785 (JSON
  Canonicalization Scheme) canonical form of the logical `{ workflow, sources }` content, documented as
  independent of the container framing.
- **Canonicalisation is in place.** The canonical form is produced by `JsonCanonicalizer.TryCanonicalize` into
  a pooled buffer and hashed in place, never materialised to its own array, so computing the hash is
  allocation-lean.
- **The hash survives a canonical repack.** `CatalogPackage.Process` rewrites the workflow id, repacks the
  archive canonically, and content-hashes it. Because the hash is over the canonical logical content, the
  repack does not change it.

## Decision

A version's content hash is the **SHA-256 of the RFC 8785 canonical form of the logical `{ workflow, sources }`
content**, not of the container bytes. Key order, whitespace, and container framing do not affect it. Two
packages with the same logical content have the same hash regardless of how their bytes are laid out.

## Consequences

- The identity is framing-independent. The store can repack a package (to rewrite the workflow id, or to bake
  in a compiled executor) without changing the version's hash, because the hash is of the logical content.
- A client can reproduce a version's hash from the logical content alone, using the same canonicalisation, so
  the hash is a verifiable content address rather than a checksum of one particular byte layout.
- The hash binds the executor package to the version ([ADR 0025](0025-integrity-binding-optional-signature.md)):
  the manifest's package hash is this content hash, and the loader checks the loaded assembly's package against
  it.
- Because canonicalisation is JCS (RFC 8785), the same rules the rest of the platform uses for canonical JSON
  apply, so there is one canonical form, not a bespoke one for packages.
