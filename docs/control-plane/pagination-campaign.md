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
