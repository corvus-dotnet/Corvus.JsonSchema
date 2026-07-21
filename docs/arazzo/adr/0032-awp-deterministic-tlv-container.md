# ADR 0032. The `.awp` container is a deterministic TLV framing, not a ZIP

Date: 2026-07-21. Status: **Accepted**. Scope: how a package's bytes are framed. Builds on
[ADR 0030](0030-immutable-content-hashed-versioned-packages.md) and
[ADR 0031](0031-content-hash-over-rfc8785-canonical.md). This records why the package container is a small,
deterministic length-prefixed framing rather than a ZIP.

## Context

A package bundles several documents (the workflow, its sources, optional schema metadata, the compiled
executor) into one artifact ([ADR 0030](0030-immutable-content-hashed-versioned-packages.md)). The obvious
container is a ZIP. But a ZIP is non-deterministic (timestamps, compression choices, and entry order vary), it
carries compression machinery the package does not need, and reading it builds a per-entry object graph. A
package is moved as a blob and hashed by logical content
([ADR 0031](0031-content-hash-over-rfc8785-canonical.md)), so its container should be simple, deterministic,
and cheap to read and write with spans.

### Grounded architectural facts

- **The container is a length-prefixed TLV framing, explicitly not a ZIP.** `WorkflowPackage`
  (`src/Corvus.Text.Json.Arazzo.Durability/Catalog/WorkflowPackage.cs`) documents a small, deterministic
  length-prefixed (tag-length-value) framing, not a ZIP, so it reads and writes with spans and a single output
  buffer, with no per-entry object graph and no compression streams.
- **The layout is fixed.** A header of the magic `"AWP"` (3 bytes), a format version byte, and an entry count
  (`uint32`), then entries in a deterministic fixed order, each a name length (`uint16`), the UTF-8 name, an
  encoding byte (`0` for stored, other values reserved for future compression), a data length (`uint32`), and
  the opaque data (UTF-8 JSON or binary). Multi-byte integers are little-endian.
- **It is an opaque blob.** The package is moved as a file (multipart upload, streamed download) and stored
  verbatim; its identity is the logical content hash, not the container bytes.

## Decision

The package container (`.awp`) is a **small, deterministic length-prefixed TLV framing, not a ZIP**. It has a
fixed header, a fixed entry order, and stored (uncompressed) entries by default, so packing and unpacking are
span operations over a single buffer with no object graph or compression streams. The container is an opaque
blob moved as a file and stored verbatim.

## Consequences

- Packing is deterministic, so the same logical content packs to the same bytes in the same order, which is
  what lets a canonical repack be stable ([ADR 0031](0031-content-hash-over-rfc8785-canonical.md)).
- Reading and writing a package is cheap: spans over one buffer, no per-entry allocation, no decompression, so
  it suits the bytes-native persistence posture the rest of the platform follows.
- A future need for per-entry compression is reserved in the encoding byte, so it can be added without a new
  container format.
- Because the container is opaque and stored verbatim, a backend persists it as a blob and does not need to
  understand its framing; only the packer and unpacker do.
