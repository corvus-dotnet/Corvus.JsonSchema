# Control-plane pagination campaign

**Branch / worktree:** `control-plane-pagination` (forked from `worktree-arazzo-workflow-engine-plan` @ `1d423ec65d`,
which is 468 commits ahead of `main` — the whole Arazzo control plane lives on the campaign line, not `main`).

## Why this campaign exists

**Standing principle (memory `page-all-multi-result-apis-and-stores`):** *every API that can return more than one
result MUST be paged — and so must the store query behind it — from the moment it's designed.* An unpaged list is a
latent failure: it works on seed/demo data and **always** gets caught out at real scale (transfer size, store read, DOM
render, memory). A client-side search/filter is **find, not scale** — it still loads and renders everything.

This campaign brings every **unpaged** control-plane list endpoint (and its backing store, across every backend) up to
the paged standard the already-paged endpoints set. It was triggered by the security-UI campaign: `listSecurityRules` /
`listSecurityBindings` shipped unpaged, the "hundreds of scopes" question exposed it, and the audit below showed it
wasn't just those two.

## The audit — control-plane list endpoints

Already paged (the standard to match): `listRuns`, `searchCatalog`, `listCatalogVersions`, `listCredentials`,
`searchGrantees`. Each takes `q?`/`limit`/`pageToken` and returns a `nextPageToken` via the §803 continuation-token
carrier; each has a `*Paged` client iterator.

| Unpaged endpoint | Backing store | Grows? | Scope |
|---|---|---|---|
| `listSecurityRules` | `ISecurityPolicyStore` | yes (scopes) | seam + ~9–10 backends |
| `listSecurityBindings` | `ISecurityPolicyStore` | yes (grants) | seam + ~9–10 backends |
| `listAdministrators` | administrator store (via `ISecuredWorkflowCatalog`) | yes (per-workflow admin set) | seam + backends (verify) |
| `listAccessRequests` | `IAccessRequestStore` | **yes — an approval queue, unbounded** | seam + backends (verify) |
| `listRunners` | `IRunnerRegistry` | yes (fleet size) | seam + backends (verify) |
| `listSecurityOrderings` | (deployment config, not a store) | bounded config | defensible exempt — page only if it ever stops being hard-bounded |

`ISecurityPolicyStore` backends found: InMemory + Postgres + SqlServer + Sqlite + Cosmos + Redis + Mongo + AzureStorage +
NatsJetStream (verify whether a MySql impl exists too — the catalog/state stores have one). **Phase 0 re-audits the exact
store + backend set for the administrator / access-request / runner stores.**

## The pattern to apply (the already-paged endpoints + memory `arazzo-continuation-token-carrier`)

- **OpenAPI:** add `q?` + `limit` + `pageToken` params and a `nextPageToken` to the list response (and a paged list
  wrapper schema if the store returns a bare array today). Regen server + client; delete stale nested-array files.
- **Store seam:** a paged `ListXAsync(query, ct)` returning a page + a **bytes-native continuation token** — `JsonString`
  token in via `From()`, pooled `ReadOnlyMemory<byte>` out via the page `Create(...)` factory (the page becomes a sealed
  disposable class, never a record struct — a value-copy double-returns the rent). **Keyset** paging (a stable sort key
  + the token encodes the last key), not offset. `RefreshAsync`-style full loads (e.g. the policy compiling ALL rules)
  keep a separate non-paged read — paging is the API list only.
- **Backends:** SQL = `WHERE <keyset predicate> ORDER BY <key> LIMIT @n`; document/KV (Cosmos/Mongo/Redis/Nats/Azure) =
  the backend's native continuation or a keyset filter+take. Server-side `q` search per backend.
- **Handler:** thread `q`/`limit`/`pageToken` → store; project the page through the carrier seam (copy a scalar token
  into the body synchronously; `From`-wrapped sub-docs are referenced → `TransferOwnershipTo`).
- **Client:** `listXPaged` async-iterator + a single-page `listX({ q, limit, pageToken })`.
- **UI (last):** the scopes/grants panels swap their client-side search + datalist typeahead to **debounced server
  search + Load-more / infinite scroll**; the grant scope-picker rides the paged scope search. (Done in the *UI*
  worktree once this lands, or as the final phase here.)

## Protocol (per §803 discipline — `docs/control-plane/allocation-protocol.md`)

One endpoint, then one backend at a time. Per row: **ground** (read the already-paged sibling + the skill) → **baseline
benchmark** (the current unpaged list projection; paste the number) → **ownership ledger** → **STOP for go-ahead** →
change → **re-run benchmark** (before→after, bytes-native token) → **container conformance** for paging+search on that
backend → update the matrix Part D → commit when asked. Warning-free `dotnet build Corvus.Text.Json.slnx` before every
commit. **Run ONE build at a time** (`MSBUILDDISABLENODEREUSE=1 … -nodeReuse:false`) and wait on its task-notification —
do not pile up backgrounded builds (a prior session accumulated 77 MSBuild nodes + a stray `pkill` killed a live build).

## Phases (build order: foundation → backends → UI)

0. **Re-audit** the exact store + backend set for the administrator / access-request / runner stores; confirm the
   `ISecurityPolicyStore` backend list (incl. MySql?). Decide page size defaults + the keyset sort key per store.
1. **Foundation per store:** the paged seam + the InMemory impl + OpenAPI + handler + regen + client iterator + the
   carrier token, with the §803 ledger + MemoryDiagnoser benchmark on the InMemory paged read. Start with
   `ISecurityPolicyStore` (rules + bindings — the campaign's trigger).
2. **Persistent backends per store:** the paged keyset query + server search across each backend; container conformance.
3. **Remaining stores:** administrator, access-request (priority — unbounded queue), runner registry.
4. **UI:** panels → server-paged search + Load-more; grant scope-picker → paged scope search.

## Phase 0 — findings (audit complete)

**Stores + backends.** Each of the four stores has **10 backend implementations** — InMemory + the 9 persistent
(AzureStorage, Cosmos, Mongo, MySql, NatsJetStream, Postgres, Redis, SqlServer, Sqlite):

| Endpoint | Store | Backends | Keyset sort key (stable order) |
|---|---|---|---|
| `listSecurityRules` | `ISecurityPolicyStore` | 10 | `name` (unique) |
| `listSecurityBindings` | `ISecurityPolicyStore` | 10 | `(order, id)` — "ordered by Order then id" |
| `listAdministrators` | `IWorkflowAdministratorStore` | 10 | `digest` (unique), scoped to the base workflow id |
| `listAccessRequests` | `IAccessRequestStore` | 10 | `(createdAt, id)` — "oldest first"; **priority (unbounded queue)** |
| `listRunners` | `IRunnerRegistry` | 10 | `id` |

**Page size:** default 50, max 1000 (sensible; some siblings differ — runs 100, credentials 25 — pick per endpoint at
implementation). **`listSecurityOrderings`** stays unpaged (bounded deployment config, not a store).

**True scope (eyes open):** 4 stores × 10 backends = **40 backend paged-query implementations**, plus per store: the
interface seam + InMemory impl + OpenAPI params/response + regen + handler + client iterator + container conformance
(×10) + a §803 MemoryDiagnoser. This is a **multi-session campaign**, not a single pass — sequence it store-by-store,
backend-by-backend, per the protocol; `IAccessRequestStore` (the growing queue) is the highest-value store after the
two `ISecurityPolicyStore` trigger endpoints.

**Phase 0: DONE.** Next: Phase 1 foundation for `ISecurityPolicyStore` (`listSecurityRules` first) — ground the
already-paged `listCredentials` sibling, baseline-benchmark the current unpaged projection, post the ownership ledger,
then implement the paged seam + InMemory impl + OpenAPI + handler + client.

## Matrix — Part D (measured before→after)

_A row is "done" only when a measured before→after + paging conformance appears here, per the clean-slate rule._

### `listSecurityRules` — `ISecurityPolicyStore` (foundation seam: InMemory + default keyset pager)

**Status:** seam + InMemory + OpenAPI + handler + regen + client iterator + carrier token **done & verified**. The 9
persistent backends page correctly today via the default in-memory pager (no backend file changed); native keyset
queries per backend are a later row (Phase 2). UI panels + CLI list now walk every page (no silent first-page cap).

**What changed.** New carrier types `SecurityRuleContinuationToken` (base64url of `name`) + `SecurityRulePage` (sealed
disposable, pooled rows + pooled `NextPageToken`). New paged interface seam `ListRulesAsync(int limit, JsonString
pageToken, string? q, CancellationToken)` with a default impl paging the existing full read in memory
(`SecurityRulePaging.PageInMemory`: order by `name` ordinal → filter by `q` (name/expression substring,
case-insensitive) → keyset past the token → bound to `limit`). OpenAPI `listSecurityRules` gained `q`/`limit`/`pageToken`
+ `SecurityRuleList.nextPageToken`; handler projects one bounded page + token; JS client `listSecurityRules` is paged
with a `listSecurityRulesPaged` iterator; demo `mock-api.js` `/security/rules` pages to match.

**Benchmark** (`SecurityRuleListProjectionBenchmarks`, `[MemoryDiagnoser]`, the handler's `SecurityRuleList` projection).
The harness is **production-faithful**: the workspace is rented from `JsonWorkspaceCache` and **disposed per op**, exactly
as the request pipeline does, so the pooled arena returns to the pool each time and "Allocated" is steady-state
per-request GC (not a held-arena working-set figure).

| Method | RuleCount | Mean | Allocated |
|---|---|---|---|
| `Unpaged_All` | 50 | 24.3 µs | 168 B |
| `Paged_FirstPage` | 50 | 22.2 µs | 168 B |
| `Unpaged_All` | 500 | 67.1 µs | 169 B |
| `Paged_FirstPage` | 500 | **4.0 µs** | 168 B |

**Allocation is already clean and pagination does not change it.** Per `corvus-typed-model-construction` +
`corvus-ctj-handler-implementation`, the body is a lazy `Source<TContext>` materialised once by `Ok<TContext>(body, ws)`
into the workspace — a per-request pooled arena (`JsonWorkspace` doc-index array + `PooledByteBufferWriter` write buffer,
ArrayPool-backed). Because that arena is disposed/returned per request, steady-state GC is **~168 B for both arms at both
sizes** — flat, no per-page cost, no regression. So this row is **not** an allocation-reduction row (there is no managed
garbage to remove — unlike the record/string/closure rows that went e.g. `8584→136 B`); an earlier `136 KB→24 KB`
figure here was an artefact of a reuse-the-workspace-without-reset harness defeating the pool, and is withdrawn.

**What pagination actually bounds is size and CPU, O(total) → O(page).** The Mean shows it: projecting every rule is
O(total) — 67 µs at 500 and climbing — while a page is flat (~4 µs at 500; equal to unpaged at one page of 50). The same
bound applies to the produced **response bytes**, hence the network transfer and the browser render, and to the **live
working set per in-flight request** — all capped at one page instead of scaling with the deployment's total rule count
(the property that stops a large deployment OOM/LOH-thrashing or shipping a multi-MB list body). That structural bound —
not a GC delta — is the reason to page.

**Conformance.** `SecurityPolicyStoreConformance` (runs on all 10 backends) gained: keyset pages in `name` order with no
gaps/duplicates across boundaries + malformed-token → `FormatException`; and `q` substring filtering over name/expression.
Server-level `ControlPlaneSecurityApiTests` gained an end-to-end HTTP test (`?limit`/`?pageToken`/`?q`, last-page token
omission). Verified locally: warning-free `dotnet build Corvus.Text.Json.slnx` (0W/0E); InMemory conformance + bootstrap
(11), server API (8), Sqlite conformance (9), JS client + conformance (74) — all green. Container conformance for the 8
remaining persistent backends rides their existing suites (default seam) and is re-asserted natively in Phase 2.

### `listSecurityBindings` — `ISecurityPolicyStore` (foundation seam, same shape as rules)

Mirror of the rules row for `GET /security/bindings`, keyset on **`(order, id)`** (order ascending, the unique id the
tie-breaker): `SecurityBindingContinuationToken`/`SecurityBindingPage`, the default paged seam over the full read
(`SecurityBindingPaging.PageInMemory`), OpenAPI `q`/`limit`/`pageToken` + `SecurityBindingList.nextPageToken` (regen),
the handler, the JS client (`listSecurityBindings` paged + `listSecurityBindingsPaged`), the grants panel, and the demo
mock. Benchmark (`SecurityBindingListPagingBenchmarks`, production-faithful — workspace disposed per op):

| Method | BindingCount | Mean | Allocated |
|---|---|---|---|
| `Unpaged_All` | 50 | 75.0 µs | 168 B |
| `Paged_FirstPage` | 50 | 81.6 µs | 169 B |
| `Unpaged_All` | 500 | 803.4 µs | 177 B |
| `Paged_FirstPage` | 500 | **90.4 µs** | 169 B |

Same conclusion as rules: alloc-clean either way (~168 B; the pooled arena returns per request), and paging bounds CPU
and response size O(total) → O(page) (803 → 90 µs at 500). Conformance (`SecurityPolicyStoreConformance`, all 10 backends)
gained keyset `(order, id)` no-gaps/dupes + malformed-token + `q` over claim type/value/description; the server HTTP test
covers `?limit`/`?pageToken`/`?q`.

### Bytes-native pagers (both rows) — the string/null realisation rework

The first cut of both in-memory pagers (and tokens) realised the keyset / cursor / `q` to **managed strings**
(`NameValue`/`IdValue`/`ClaimValueOrNull`/`string.CompareOrdinal`/`Encoding.UTF8.GetString`) — the record↔document string
seam `corvus-bytes-to-bytes` forbids (and not confined to InMemory: it ran in the shared seam/handler/default pager the
persistent backends inherit). Reworked fully bytes-native:

- **keyset order + cursor** compare the persisted UTF-8 directly (`GetUtf8String().Span.SequenceCompareTo`); no name/id string;
- **tokens** are `base64url` straight over the row's UTF-8 (rule = name; binding = `order \0 id` via `Utf8Formatter`/`Utf8Parser`), decoded into a caller-owned pooled buffer with the cursor a **span**, never a string;
- the **seam** takes `q` as its `JsonString` (not `string?`); the handler rewraps via `JsonString.From` (no `(string)`); the case-insensitive substring transcodes the field + query into a **reused pooled UTF-16 scratch** (`ArrayPool<char>`, `SecurityPagingText`) and matches `OrdinalIgnoreCase` — no managed string, no `OrNull` null realisation.

Pre-commit `corvus-bytes-to-bytes` self-audit: **zero** string realisations across the 7 paging files; the only `(string)`
casts left in the handler are the pre-existing CRUD endpoints whose store seam is genuinely `string`-keyed
(`GetRuleAsync(string name)` etc.) — string-typed sinks, i.e. genuine leaves. Retrofitting
`SecurityBindingListProjectionBenchmarks` to the production-faithful lifecycle additionally surfaced the real closure cost
the old reuse-no-reset harness masked: `PerItemClosure 1224 B → ContextThreaded 168 B` (the shipped context-threaded
projection is genuinely alloc-clean).

### Credentials — deliberately not reworked (confined realisation, not debt)

`InMemorySourceCredentialStore` realises source/environment/discriminator strings for its in-memory sort, but those are
**retained inside the InMemory backend** (used locally, emitted only as a bytes-native token). The 9 persistent credential
backends decode the token to a **string cursor that is a DB query parameter** — a string-typed sink, i.e. a genuine leaf;
and the discriminator is a computed canonical tag string used as a string-keyed identity system-wide. So the credential
realisation is confined/leaf, not the shared-path debt the rule/binding pagers had — left as-is by design.

### `listAccessRequests` — `IAccessRequestStore` (the unbounded approval queue) + bytes-native `AccessRequestQuery`

The §16.5 access-request queue grows without bound (every elevation request, retained for audit), so it was the
highest-value paging target after the trigger pair. Keyset on **`(createdAt, id)`** (oldest-first): the new
`AccessRequestContinuationToken` (`base64url(utcTicks \0 id)` — the instant as UTC ticks via `Utf8Formatter`/`Utf8Parser`,
exact and offset-independent; id a span), `AccessRequestPage`, and a default paged seam
`ListAsync(query, limit, pageToken, ct)` that pages over the store's existing **filtered** read (the pager does only the
keyset + limit, bytes-native: `CreatedAtValue` is a `DateTimeOffset` value compare, the id compares on its UTF-8 span — no
managed string). OpenAPI `/accessRequests` gained `limit`/`pageToken` + `AccessRequestList.nextPageToken` (regen); paged
handler; JS client (`listAccessRequests` paged + `listAccessRequestsPaged`); the access-requests SPA panel accumulates;
demo mock pages; CLI list walks every page.

**Bytes-native `AccessRequestQuery` (the request value stays JSON to the store).** The filter's `baseWorkflowId` arrives as
the request's JSON value, so `AccessRequestQuery.BaseWorkflowId` was changed from `string?` to `JsonString` — carried
through the handler (`JsonString.From(parameters.BaseWorkflowId)`, no `(string)` cast for the query) to the store, and
reified only at **each backend's own leaf**: `(string)query.BaseWorkflowId` for the SQL/doc `@param` (a genuine DB-param
string sink, across Sqlite/Postgres/MySql/SqlServer/Cosmos/Mongo/AzureStorage), and a `GetUtf8String().Span` **span
compare** in the in-memory-filtering backends (InMemory/Redis/Nats). `status` stays a closed enum; `subjectClaim*` stay
genuine `ClaimsPrincipal`/config string leaves. (The status/subject row-field reads in the in-memory backends' `Matches`
are a separate axis — confined stored-field reads — left as-is.)

Benchmark (`AccessRequestListPagingBenchmarks`, production-faithful, workspace disposed per op):

| Method | RequestCount | Mean | Allocated |
|---|---|---|---|
| `Unpaged_All` | 50 | 11.5 µs | 168 B |
| `Paged_FirstPage` | 50 | 12.4 µs | 168 B |
| `Unpaged_All` | 500 | 105.9 µs | 169 B |
| `Paged_FirstPage` | 500 | **11.4 µs** | 168 B |

Same conclusion as the trigger pair: alloc-clean either way (~168 B; the pooled arena returns per request), and paging
bounds CPU and response size O(total) → O(page) (105.9 → 11.4 µs at 500). Conformance (`AccessRequestStoreConformance`,
all 10 backends) gained keyset `(createdAt, id)` oldest-first no-gaps/dupes + malformed-token, and the existing
status/workflow/subject filter test now carries `baseWorkflowId` as a `JsonString`; the server HTTP test covers
`?limit`/`?pageToken` over the queue. `corvus-bytes-to-bytes` self-audit: zero string realisations in the token/page/pager;
`baseWorkflowId` reifies only at the per-backend leaves above. Verified: warning-free slnx (0W/0E); InMemory + Sqlite
conformance (7 each), server API (6), JS (74) — green.

### `listRunners` — `IRunnerRegistry` (the runner fleet) — single-key keyset on `runnerId`

The runner registry grows with the fleet (every live runner self-registers and heartbeats), so `GET /runners` is paged.
Keyset on the unique **`runnerId`** — the simplest token of the campaign: `RunnerRegistryContinuationToken` is just
`base64url(runnerIdUtf8)` (the last page row's id carried verbatim as bytes, no instant/compound key), with
`RunnerRegistryPage`, `RunnerRegistryPaging.PageInMemory`, and a **default seam** `ListAsync(limit, pageToken, ct)` on
`IRunnerRegistry` that pages over the existing full `ListAsync(ct)` read in memory (ordinal `runnerId` UTF-8 sort + cursor
skip + limit). All 9 persistent backends inherit that default unchanged; Phase 2 swaps in native keyset queries so the read
itself is bounded.

**Detached-rows ownership (the row's distinguishing feature).** Unlike the rules/bindings/access-request rows (whose pages
carry pooled `PooledDocumentList<T>` documents), the registry contract already states each listed `RunnerRegistration` is
**detached** from any store-side buffer. So `RunnerRegistryPage` holds the subset `IReadOnlyList<RunnerRegistration>` plus
the pooled token only — no `PooledDocumentList`, no `TransferOwnershipTo`, no re-parse. The handler's `using page` returns
**just the token buffer**; the body's congruent `Models.Runner.From(registration)` wraps keep the detached registrations
GC-reachable through the synchronous `Ok`. There is no `q` filter (runners have no search dimension), and no
CLI/JS/SPA/mock consumer — `/runners` is a server-only observability read.

OpenAPI `/runners` gained `limit`/`pageToken` + `RunnerPage.nextPageToken` (regen 515 server / 522 client; the rest are
line-ending no-ops). Benchmark (`RunnerListPagingBenchmarks`, production-faithful, workspace disposed per op):

| Method | RunnerCount | Mean | Allocated |
|---|---|---|---|
| `Unpaged_All` | 50 | 23.9 µs | 168 B |
| `Paged_FirstPage` | 50 | 14.6 µs | 168 B |
| `Unpaged_All` | 500 | 104.2 µs | 169 B |
| `Paged_FirstPage` | 500 | **11.0 µs** | 168 B |

Same conclusion as every list row: alloc-clean either way (~168 B; the pooled arena returns per request — the detached
registrations are GC, not pooled), and paging bounds CPU and response size O(total) → O(page) (104.2 → 11.0 µs at 500; the
50-row means are noisy under ShortRun but directionally identical). `corvus-bytes-to-bytes` self-audit: zero string
realisations — `pageToken` flows in as its JSON value (`JsonString.From`) and is base64url-decoded into a *pooled* cursor
buffer; the `runnerId` sort/compare reads each row's persisted UTF-8 (`GetUtf8String().Span`, ordinal); the next token is
base64url-encoded from the last row's `runnerId` UTF-8 into a pooled buffer and written verbatim into the response; the only
leaf is the malformed-token `FormatException` message. Verified: warning-free slnx (0W/0E); InMemory + Sqlite
`RunnerRegistryConformance` (12 each, incl. keyset no-gaps/dupes + malformed-token); server HTTP
`ListRunners_keyset_pages_over_http` (27 server tests) — green.

### `listAdministrators` — exempt (bounded single-record get, not a multi-row store list)

Grounding showed `GET /administrators/{baseWorkflowId}` returns **one** `WorkflowAdministrators` record (the admin store's
`GetAsync(baseWorkflowId)`) whose `administrators[]` array the handler projects — a single-record get, not a multi-result
store query. There is no store query to "get caught out by scale": it is bounded by one workflow's admin count
(privileged identities — inherently small), structurally like `listSecurityOrderings`. The Phase-0 audit penciled in
"keyset on digest" assuming a multi-row store; the implementation is a single record. **Exempted** (decision recorded
with the user) — paging it would bound only the response projection over an already-whole-loaded record, no store-read
benefit.

## Matrix — Part D, Phase 2 (native per-backend keyset reads)

_Phase 1 made every endpoint paged via the default in-memory seam (read all, page in memory). Phase 2 replaces that seam,
backend by backend, with a native keyset query (`WHERE key > @after ORDER BY key LIMIT @n+1`) so the **store read** itself
is bounded to one page — the corpus is the already-paged runs store (`WorkflowStateStore.QueryAsync`). The metric here is
the store read, not the projection: a backend store-read benchmark whose cost should go from O(total) to O(page) (flat as
the table grows). Sequenced by feasibility: `listRunners` first (key is a real PK column / doc-id / KV key in all 10
backends), then `listSecurityRules`/`listSecurityBindings` and `listAccessRequests` (keys live inside the JSON blob — need
key-extraction columns or JSON-path queries, a per-backend design decision)._

### `listRunners` — native keyset on `runner_id` (per backend)

The store-read benchmark (`RunnerRegistryReadPagingBenchmarks`, the first page of 50 read through a real embedded SQLite
driver over an in-memory database, `MemoryDiagnoser`) before (default in-memory pager: a full `SELECT doc` + parse of every
registration, then sort + slice) → after (native `SELECT doc FROM runner_registrations WHERE (@after IS NULL OR runner_id >
@after) ORDER BY runner_id LIMIT @limit`, `@limit = pageSize + 1`, the `runner_id` PK B-tree driving the seek + order):

| Backend | RunnerCount | Mean before → after | Allocated before → after |
|---|---|---|---|
| **Sqlite** | 50 | 2.50 ms → 54 µs | 43.74 KB → 41.31 KB |
| **Sqlite** | 500 | 7.51 ms → 51 µs | **416.36 KB → 42.09 KB (−90%)** |

The headline is the allocation at 500: the first page no longer reads the whole registry, so the store read is now **flat**
across table size (41→42 KB) instead of growing O(total) (43→416 KB). The cursor reifies to a managed string only at the
ADO `@after` TEXT-parameter (a genuine DB-param leaf — one transient cursor string per request, never per row); decoded
base64url over the request UTF-8 into a pooled buffer; `runner_id` BINARY collation == ordinal UTF-8 byte order == the
in-memory pager's `SequenceCompareTo`, so the native query pages identically — the Sqlite `RunnerRegistryConformance` (12,
incl. keyset no-gaps/dupes + malformed-token) validates that against the in-memory reference. `corvus-bytes-to-bytes`
self-audit: token in via `GetUtf8String().Span` → pooled decode; rows via driver `byte[]` → `FromJson`; token out via the
last row's `RunnerId` UTF-8 → pooled encode; only leaves = the `@after` DB-param string + the malformed-token
`FormatException`. (Default-page-size constant promoted to public `RunnerRegistryPage.DefaultPageSize` so backends share
one source — no InternalsVisibleTo.) Warning-free slnx (0W/0E).

**SQL backends (Postgres, MySql, SqlServer) — done.** Same native keyset (`WHERE runner_id > @after ORDER BY runner_id` +
`LIMIT @n+1` / SqlServer `TOP (@n+1)`), the cursor decoded once via the shared, bytes-native
`RunnerRegistryContinuationToken.DecodeCursorToString` (base64url over the request UTF-8 into a pooled buffer, reified to a
string only for the keyset `@after` param — the DB-param leaf). The cross-backend correctness point is the **collation**:
the in-memory pager + the conformance assert *ordinal UTF-8 byte order*, but only SQLite's `TEXT` defaults to that
(`BINARY`). So `runner_id` is declared byte-ordinal per backend, matching the `ObservedIdentityStore` precedent — Postgres
`COLLATE "C"`, MySql `COLLATE utf8mb4_bin`, SqlServer `COLLATE Latin1_General_BIN2` (both the PK and the FK column, so the
index serves the seek+order and the FK collations match) — and SqlServer reads the UTF-8 `doc` back as `VARBINARY` to stay
bytes-native. Gotcha (caught by conformance, fixed): Npgsql can't infer the type of an untyped `DBNull` `@after` in
`@after IS NULL` (error `42P08`) — bind it as explicitly-typed `NpgsqlDbType.Text` (as the runs store does). Container
conformance (`RunnerRegistryConformance`, real containers via podman): **Postgres 12/12, MySql 12/12, SqlServer 12/12**
(each incl. the keyset no-gaps/dupes walk + malformed-token). No per-backend benchmark — the win is the same structural
full-read→`LIMIT` bounding the Sqlite figure measures; local-container latency under-measures the round-trip/payload win.

**Document-store backends (Cosmos, Mongo, AzureStorage) — done.** Each seeks strictly past the cursor on its native
key/order and is bounded to one page + 1 (lookahead), never enumerating every registration: Cosmos `WHERE c.id > @after
ORDER BY c.id` with the lazy stream iterator drained only until one row beyond the page; Mongo `Find(_id > after)` +
`Sort(_id asc)` + `Limit(n+1)`; AzureStorage OData `RowKey gt @after` with `maxPerPage = n+1`. All three already order by the
key the in-memory pager uses — Cosmos orders strings ordinally, BSON compares strings by bytes, and Azure Table keys are
ordinal — so no collation declaration is needed (unlike the SQL trio). Cosmos gotcha (per-root type identity): its generated
`RunnerDocument` model emits a `…Cosmos.JsonString`, which shadows the core seam type and even beats a file-level
`using`-alias (a current-namespace member wins), so the override's `pageToken` parameter is written as the fully-qualified
`global::Corvus.Text.Json.Arazzo.Durability.JsonString`. Container conformance: **Cosmos 12/12, Mongo 12/12, AzureStorage
12/12** (each incl. keyset no-gaps/dupes + malformed-token).

**KV backends (Redis, NatsJetStream) — done.** A KV store has no server-side range query, so — as in the runs corpus — the
keyset order is materialised client-side over the *id index* (cheap), and only the page's **documents** are fetched (one
beyond, to look ahead), never every registration's JSON. Redis: `SMEMBERS` the id set, sort ordinal, skip past the cursor,
`GET` only the page's per-runner keys. Nats: each KV key is `Base64Url(runnerId)`, so the id (hence the order) is recovered
from the key without fetching the value — list keys, decode, sort ordinal, skip past the cursor, then `GET` only the page's
entries. Both sort ordinal (== the in-memory pager's order). Container conformance: **Redis 12/12, NatsJetStream 12/12**
(each incl. keyset no-gaps/dupes + malformed-token).

**`listRunners` Phase 2 — COMPLETE across all 10 backends.** InMemory (the reference in-memory pager) + Sqlite/Postgres/
MySql/SqlServer (SQL keyset, byte-ordinal collation) + Cosmos/Mongo/AzureStorage (native range) + Redis/Nats (client-sorted
id index, page-only document fetch). Every backend pages by ordinal `runnerId`, validated by the shared
`RunnerRegistryConformance` (12 tests each).

**Reframing the remaining stores (grounding correction).** The keyset key fields for the other three endpoints turn out to
be **already queryable columns/properties** on the SQL and document backends (the runner row had to rely on a natural PK,
but these stores already mirror their keys): access-requests store `Id`/`CreatedAt`/`BaseWorkflowId`/`Status` as columns and
already `ORDER BY CreatedAt, Id`; security rules key on a `Name` PK; bindings on `SortOrder, Id` columns. So Phase 2 for
these is mostly *adding the cursor predicate + `LIMIT` to the existing ordered query* (+ a byte-ordinal `Id` collation on the
non-SQLite SQL backends, as for runners), not new columns. Only Redis/Nats page client-side (KV, inherent), and only the
security stores' free-text **`q`** fields (rule expression; binding claimType/claimValue/description) genuinely live in the
blob — so server-side `q` is the one place key-extraction is still needed (resolved when those stores are tackled).

### `listAccessRequests` — native keyset on `(createdAt, id)` (per backend)

The unbounded approval queue. Keyset `(createdAt, id)` oldest-first; the existing filter columns
(`status`/`baseWorkflowId`/`subjectClaim*`) are untouched. **Sqlite — done.** The native override adds the keyset seek
`(CreatedAt > @ca OR (CreatedAt = @ca AND Id > @id)) ORDER BY CreatedAt, Id LIMIT @n+1` to the existing filtered query. The
cursor reifies (only at the ADO param leaf) to the ISO-8601 `"o"` form the `CreatedAt` column stores — reconstructed from
the token's UTC ticks (`new DateTime(ticks, Utc).ToString("o")`), so it byte-matches the boundary row — plus the `Id` text;
`CreatedAt` is fixed-width ISO (ordinal == chronological) and SQLite `Id TEXT PK` is BINARY (ordinal == the in-memory
pager's id span compare). The page carries pooled `PooledDocumentList<AccessRequest>` documents (parsed only for the page,
not the whole queue) + the pooled token, disposed together. `corvus-bytes-to-bytes` self-audit: token in via pooled decode →
strings only at the `@ca`/`@id` DB-param leaf; page docs parsed bytes-native; token out via the last row's
`(CreatedAtValue.UtcTicks, Id UTF-8)` → pooled encode. Default page size promoted to public `AccessRequestPage.DefaultPageSize`
(no IVT). Warning-free slnx (0W/0E); Sqlite + InMemory `AccessRequestStoreConformance` (7 each) green.

**SQL backends (Postgres, MySql, SqlServer) — done.** Same keyset predicate + `LIMIT @n+1` / SqlServer `TOP (@n+1)` added to
the existing filtered, ordered query. Two schema additions per backend: `Id` declared byte-ordinal (Postgres `COLLATE "C"`,
MySql `utf8mb4_bin`, SqlServer `Latin1_General_BIN2`) so the id tie-breaker matches the in-memory pager, and a composite
`IX_AccessRequests_Created (CreatedAt, Id)` index so the oldest-first queue read is an index seek (not a full sort) — the
unbounded-queue case where the bound matters most. `CreatedAt` needs no collation (fixed-width ISO is ordinal-stable). The
Npgsql `42P08` trap does not recur here — `@ca`/`@id` are bound only when a cursor is present, so they are never untyped
`DBNull`. Container conformance: **Postgres 7/7, MySql 7/7, SqlServer 7/7**.

**Document-store backends (Cosmos, Mongo) — done.** The keyset fields are already mirrored top-level, so each adds the
compound cursor predicate `(createdAt > @ca OR (createdAt = @ca AND id > @id))` on the existing ordered query, bounded to
one page + 1: Cosmos `… ORDER BY c.createdAt, c.id` with the stream iterator drained only one beyond the page; Mongo the
equivalent `Or(Gt(createdAt), And(Eq(createdAt), Gt(_id)))` filter + the existing `(createdAt, _id)` sort + `Limit(n+1)`.
Both order strings ordinally (ISO createdAt == chronological; id byte-ordinal), matching the in-memory pager — no collation
declaration needed. Cosmos per-root `JsonString` shadowing recurs (its generated model) → the seam param is the
fully-qualified `global::…Durability.JsonString`. Container conformance: **Cosmos 7/7, Mongo 7/7**.

**Client-sorted backends (AzureStorage, Redis, Nats) — done.** These key on the id, not `(createdAt, id)`, so a secondary
`createdAt`-ordered structure is used (per the user's "extracted index" decision):
- **AzureStorage** — no write-path change: `CreatedAt` is already an entity column, so the list projects `[RowKey, CreatedAt]`
  (no `Doc`, filter server-side on the columns), recovers the id via `Dec(RowKey)`, sorts `(createdAt, id)` client-side,
  keyset-skips, and **point-reads only the page's documents**.
- **Redis** — a zero-scored sorted set `arazzo:accessreqs:bycreated` whose members are `{createdAtIso}\0{id}`, maintained on
  create (`ZADD`); the list `ZRANGEBYLEX`-reads them in `(createdAt, id)` order (no docs) and fetches + filters only the
  page's documents (reads ≈ page / selectivity).
- **Nats** — KV listing is unordered, so a marker key `idx.{Base64Url(createdAtIso)}.{Base64Url(id)}` per request (written
  on create) is enumerated cheaply (no docs), decoded to `(createdAt, id)`, sorted client-side, keyset-skipped, then only
  the page's documents are fetched + filtered.

A decision never changes `createdAt`/`id` and there is no delete, so the index needs maintenance only on create; the unpaged
`ListAsync(query)` keeps using the existing flat id set/keys. Container conformance: **AzureStorage 7/7, Redis 7/7,
NatsJetStream 7/7**.

**`listAccessRequests` Phase 2 — COMPLETE across all 10 backends.** InMemory + Sqlite/Postgres/MySql/SqlServer (keyset
predicate + composite `(CreatedAt, Id)` index, byte-ordinal `Id`) + Cosmos/Mongo (native compound range) + AzureStorage/
Redis/Nats (secondary `createdAt` index / projection, page-only document fetch). Every backend pages oldest-first by
`(createdAt, id)`, validated by the shared `AccessRequestStoreConformance` (7 tests each).

### `listSecurityRules` + `listSecurityBindings` — native keyset on `ISecurityPolicyStore` (per backend)

The last Phase 2 store. Keyset: rules on `name` (unique), bindings on `(order, id)`. The keyset fields were already columns
(`Name` PK; `SortOrder`/`Id`); the **`q` decision (server-side via extracted fields)** means the q-searched blob fields are
extracted to columns so `q` runs server-side: rules add `Expression` (q matches `Name` OR `Expression`); bindings add
`ClaimType`/`ClaimValue`/`Description` (q matches any). The write path populates them on add/update. **Sqlite — done.** Native
`ListRulesAsync`/`ListBindingsAsync` add the keyset seek (`Name > @after`; `SortOrder > @order OR (SortOrder = @order AND Id >
@id)`) + `WHERE (@q IS NULL OR col LIKE @q ESCAPE '\' OR …)` + `ORDER BY` the keyset + `LIMIT @n+1`. The cursor (name; order +
id) and the q LIKE pattern reify to strings only at the ADO parameter leaf; SQLite `LIKE` is case-insensitive (matching the
in-memory `OrdinalIgnoreCase`), `Name`/`Id` TEXT PK are BINARY (ordinal == the in-memory pager), and the existing
`IX_SecurityBindings_Order (SortOrder, Id)` index serves the binding keyset. Default page sizes promoted to public
`SecurityRulePage`/`SecurityBindingPage.DefaultPageSize` (no IVT). Warning-free slnx (0W/0E); Sqlite + InMemory
`SecurityPolicyStoreConformance` (11 each, incl. rule/binding keyset no-gaps/dupes + `q` + malformed-token) green. **Remaining
backends:** Postgres/MySql/SqlServer (q-columns + byte-ordinal `Name`/`Id` collation + the predicates), Cosmos/Mongo (mirror
the q-fields + native range), Redis/Nats (keyset index + client-side `q` — no server-side `LIKE`).
