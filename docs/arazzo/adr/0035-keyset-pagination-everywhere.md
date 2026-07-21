# ADR 0035. Keyset pagination everywhere, API and store

Date: 2026-07-21. Status: **Accepted**. Scope: how every list is paged. This records why every list endpoint,
and the store behind it, is keyset-paged from the start, across every backend, rather than offset-paged or
unpaged.

## Context

Any list that can grow is a latent failure if it is returned whole: it works in a demo and falls over in
production when the data grows. The safe default is to page every list, and to page the store behind it, not
just the API, or a large result set is still materialised inside the store. The paging method also matters.
Offset pagination (`LIMIT n OFFSET m`) degrades as the offset grows and can skip or repeat rows when the
underlying data changes between pages. Keyset pagination (seek past an opaque cursor) is stable and does not
degrade.

### Grounded architectural facts

- **Every list is keyset-paged, API and store.** The standing rule is that every API that can return more than
  one result is paged, and so is the store behind it, across every backend. The store query is
  `WHERE key > @after ORDER BY key LIMIT @n+1`, native per backend.
- **The continuation token is bytes-native.** A token carrier such as `RunnerRegistryContinuationToken`
  (`src/Corvus.Text.Json.Arazzo.Durability/RunnerRegistryContinuationToken.cs`) encodes the cursor as
  base64url straight into caller buffers (`Base64Url`, `GetMaxEncodedLength`, `EncodeToUtf8`), keeping the
  cursor as a `ReadOnlySpan<byte>` and reifying a string only at the genuine backend leaf.
- **A page is a sealed disposable class.** A page owns a pooled `ReadOnlyMemory<byte>` next-page token, so it
  is a sealed disposable class, not a record struct: a value copy would double-return the pooled rent.
- **The exemptions are named.** A bounded single-record read (the administrators of one workflow) and small
  configuration (the label orderings) are the documented exceptions, because they are bounded by construction.
- **Search is part of the same principle.** A client-side filter over a fully loaded list is find, not scale,
  because it still loads and renders every row. The list surfaces pair paging with a server-side `q` search (the
  security rules list debounces `q` into `searchSecurityRules`), so the filtered case is bounded too.
- **Paging bounds the client-facing list surface, not every internal read.** An internal full-corpus read that
  is not a client list stays unpaged by design: `PersistentRowSecurityPolicy.RefreshAsync` compiles the whole
  rule set in a single load, a bounded internal need rather than a list a caller pages.

## Decision

Every list is **keyset-paged, at the API and at the store**, from the start, across every backend. The store
seeks past an opaque cursor (`WHERE key > @after ORDER BY key LIMIT @n+1`), the continuation token carries the
cursor bytes-native, and a page is a sealed disposable that owns its pooled token. An unpaged list is treated
as a latent failure, and the only exceptions are results bounded by construction.

## Consequences

- No list can materialise an unbounded result set, at the API or in the store, so a list that grows in
  production keeps working.
- Keyset paging is stable under concurrent change and does not degrade with depth, unlike offset paging.
- The continuation token is an opaque cursor the client round-trips, so the client does not know or depend on
  the store's key, and the store can change its physical cursor without changing the token contract.
- The bytes-native token carrier keeps the paging hot path allocation-lean
  ([ADR 0037](0037-bytes-native-seams.md)), reifying a string only at the backend leaf.
- Search is server-side, so filtering a large list does not degrade into loading it whole; the filtered read is
  bounded by the same page, not by the client.
