# ADR 0037. Bytes-native seams: no record-to-document string round-trips

Date: 2026-07-21. Status: **Accepted**. Scope: how JSON values move through the persistence and API layers.
This records why a JSON value is carried as bytes or a typed document from end to end, and a string is
materialised only at the genuine leaf, rather than transcoded to a record and back.

## Context

The control plane moves JSON constantly: a request body becomes a stored document, a stored document becomes a
response. The tempting shape is to parse each into a hand-rolled record of strings, work with the record, then
serialise it back. On a warm path that is a round-trip through strings the data did not need: bytes in, strings
out, strings in, bytes out, allocating a managed string for every field twice. This "record-to-document string
u-turn" is the anti-pattern the platform's persistence layer is built to avoid, because it dominates the
allocation profile of a busy control plane.

The library the platform builds on, Corvus.Text.Json (CTJ), is designed for exactly this: it works in UTF-8
bytes, builds typed documents into pooled arenas, and reads through generated typed accessors.

### Grounded architectural facts

- **Values are carried bytes-to-bytes.** A field is bridged with a value-bridge (`Models.X.From(...)`, a
  zero-copy element wrap) rather than transcoded to a string and back, and a whole document is wrapped with a
  congruent `T.From(...)`. The `ArazzoControlPlaneSecurityHandler` (rebuilt for the paged access overview) is
  a worked example: response fields are `Models.JsonString.From(binding.Environment)`, not
  `(string)binding.Environment`.
- **Documents are built by typed construction, not hand-rolled records.** A persisted shape is a generated CTJ
  document created through `Create`, `Build`, or `CreateBuilder<TContext>` (threading a context so no closure
  captures), not a POCO record.
- **Ownership is explicit and pooled.** A per-request `JsonWorkspace` is a pooled arena; stores return pooled
  `ParsedJsonDocument<T>` and `PooledDocumentList<T>`. Because a response body's `Source` is re-read after the
  handler returns (validation, serialisation), pooled documents that feed a response outlive the synchronous
  build through `workspace.TakeOwnership(doc)` or `page.Items.TransferOwnershipTo(workspace)`.
- **A string is materialised only at the leaf.** A managed string, or an owned `byte[]`, is produced only at
  the genuine sink: a database parameter, a searchable indexed column, or a driver whose API only accepts an
  array. This is the "realise at leaf" rule.

## Decision

JSON values move through the persistence and API layers **bytes-native**. A field is value-bridged
(`Models.X.From`) rather than transcoded to a string; a document is built by typed construction, not a
hand-rolled record; ownership of pooled documents is explicit through the workspace; and a managed string or
array is materialised only at the true leaf (a database parameter, a searchable column, or an array-only driver
API). The record-to-document string round-trip on a warm path is the anti-pattern this rules out.

## Consequences

- The warm paths do not allocate a string per field per direction, so a busy control plane's allocation
  profile is dominated by genuine work, not transcoding.
- A change to a persisted or API shape is a change to a generated type, not a hand-maintained record, so the
  shape and its serialisation stay in one place ([ADR 0039](0039-api-first-openapi-source-of-truth.md)).
- The explicit ownership discipline is a real constraint: a pooled document that feeds a deferred-serialised
  response must be handed to the workspace, or it is returned to the pool before it is read. This is a known
  trap the conventions call out.
- Reifying a string only at the leaf keeps the paging token ([ADR 0035](0035-keyset-pagination-everywhere.md))
  and the count ([ADR 0036](0036-bounded-count-contract.md)) allocation-lean, because their hot paths never
  build an intermediate string.
