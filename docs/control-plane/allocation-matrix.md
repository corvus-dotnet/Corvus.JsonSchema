# Control-plane allocation matrix (API → store)

> **Status: Phase 1 — skeleton for review.** Single source of truth for the bytes-to-bytes
> allocation campaign (issue #803). It enumerates every control-plane API operation, its call
> tree to the durable store, the ownership ledger, the one end-to-end baseline benchmark, the
> target CTJ pattern (grounded in a named skill / § of the design doc), and the **measured**
> before→after.

## Clean-slate rule (overrides everything below)

**No row is "done" because a prior commit or session says so.** Prior work is treated only as the
*current code shape* — a starting state to be measured, never a completion. Every row with an
allocation seam goes through the **same** process, from scratch, and is marked ✅ **only** when a
before→after measurement is recorded in Part D of this document:

1. **Ground** in the named skill(s) + design-doc § *before* writing code. Derive the target from
   the conventions; the existing code is the corpus being replaced ([[dont-anchor-on-existing-bad-code]]).
2. **Baseline** — one end-to-end benchmark (handler → InMemory store) measuring the **current**
   allocation. This is the "before". Establish it by *running it*, not by recalling a number.
3. **Ledger** — post the ownership ledger for the path ([[alloc-ownership-ledger-discipline]],
   [[frequency-is-not-a-licence]]).
4. **Change** the seam/layers per the target pattern.
5. **After** — re-run the same benchmark (with a `[Benchmark(Baseline = true)]` old-path arm so the
   delta is measured, not asserted); record before→after.
6. **Document** the row in Part D; if a better pattern emerged, **update the skill** and link it.

### The InMemory-baseline decision

The end-to-end benchmark per row runs against `InMemory*` stores — the in-process, our-code-only
floor. Per design §13.4.1, per-backend micro-benchmarks are *not* the right tool (driver allocation
dominates and isn't ours); **Sqlite** is a single spot-check for the driver delta where a row
warrants it. Backends are enumerated per seam as a **leaf-realisation** column (static audit, the
§13.4.1 method), not exploded into N benchmarks.

### Status legend (this pass only — nothing starts as ✅)

| Mark | Meaning |
|---|---|
| ⬜ | Not started in this pass |
| 🔬 | Ledger posted **and** baseline measured (the "before" exists) |
| 🔧 | Change applied, after-measurement pending |
| ✅ | before→after recorded in Part D + committed |
| ➖ | No allocation seam — confirmed allocation-free (opaque bytes / 0-copy passthrough / direct delete) |

### Current-seam-shape legend (descriptive of today's code, **not** a completion claim)

`record` = hand-rolled struct/record input · `draft` = already passes a generated CTJ document ·
`params` = loose scalar params · `list` = collection seam · `bytes` = opaque/0-copy.
A `draft` shape still requires the full process above — it is *not* presumed minimal.

---

## Part A — write-seam inventory (campaign core)

The 7 durable store interfaces, persisted CTJ document, shared serialization helper, and the
**current write-seam shape**. Goal: every write seam carries the generated CTJ document, realising
only at the genuine leaf — [[no-handrolled-records-use-codegen-jsonschema]],
[[seams-carry-json-values-realise-at-leaf]].

| Store write method | Persisted doc | Serialization helper | Current seam shape | Status |
|---|---|---|---|---|
| `IObservedIdentityStore.SeenAsync` | `ObservedIdentity` | `ObservedIdentitySerialization` | `params` (CTJ kind + JsonString value/label + SecurityTagSet) | ⬜ |
| `ISecurityPolicyStore.Add/UpdateRule` | `SecurityRuleDocument` | `SecurityPolicySerialization` | `draft` | ⬜ |
| `ISecurityPolicyStore.Add/UpdateBinding` | `SecurityBindingDocument` | `SecurityPolicySerialization` | `draft` | ⬜ |
| `IAccessRequestStore.CreateAsync` | `AccessRequest` | `AccessRequestSerialization` | `draft` | ⬜ |
| `IAccessRequestStore.DecideAsync` | `AccessRequest` | `AccessRequestSerialization` | `record` (`AccessRequestDecision`) | ⬜ |
| `ISourceCredentialStore.Add/UpdateAsync` | `SourceCredentialBinding` | `SourceCredentialSerialization` | `draft` (seam carries the generated doc; `SourceCredentialDefinition` retained only as the cold-caller convenience input via `Draft(definition)` + extension) | ✅ (Part D) |
| `IWorkflowCatalogStore.AddAsync` | `CatalogVersion` | (catalog serialization) | `record` (`CatalogMetadata`) + `bytes` package | ⬜ |
| `IWorkflowCatalogStore.UpdateMetadataAsync` | `CatalogVersion` | (catalog serialization) | `record` (`CatalogMetadataPatch`) | ⬜ |
| `IWorkflowAdministratorStore.PutAsync` | `WorkflowAdministrators` | `WorkflowAdministratorsSerialization` | `list` (`IReadOnlyList<SecurityTagSet>`) | ⬜ |
| `IWorkflowStateStore.SaveAsync` | opaque checkpoint | (executor-owned) | `bytes` + index | ➖ (confirm) |

**Backend leaf-realisation (all seams, §13.4.1 static audit — to re-confirm per worked row):** every
backend persists the same document bytes; realisations only at the driver leaf — indexed key columns
+ etag. Backends: InMemory (core) · Sqlite · Postgres · SqlServer · MySql · Mongo · Cosmos · Redis ·
NatsJetStream · AzureStorage.

---

## Part B — full API matrix (all endpoints)

`Handler.Method → [Client] → Store.Method`. `R`/`W` = read/write. *Existing bench* = benchmark code
that exists today (a starting point to be re-baselined, **not** evidence of completion).

### Runs — `ArazzoControlPlaneHandler` → `IWorkflowManagementClient` → `IWorkflowStateStore`

| Op | Call tree | R/W | Current pattern / hotspots | Existing bench | Target pattern + grounding | Status |
|---|---|---|---|---|---|---|
| `GET /runs` | ListRuns → ListAsync | R | builds `WorkflowRunSummary` page loop | — | confirm `From()`-wrap projection ([[ctj-handler-response-projection]]) | ⬜ |
| `GET /runs/{id}` | GetRun → GetAsync | R | conditional fault/wait/tags arrays; a `ParseValue` in detail build | — | review projection; kill `ParseValue` (`corvus-typed-model-construction`) | ⬜ |
| `DELETE /runs/{id}` | DeleteRun → GetAsync ×checks → DeleteAsync | W | 2× GetAsync access checks | — | — | ➖ |
| `POST /runs/{id}/resume` | ResumeRun → GetAsync ×2 → ResumeAsync | W | builds detail 3×; union `Match()` | — | review repeated detail builds | ⬜ |
| `POST /runs/{id}/cancel` | CancelRun → GetAsync ×2 → CancelAsync | W | builds detail 3×; `(string)reason` | — | as resume | ⬜ |
| `POST(custom) /runs` purge | PurgeRuns → PurgeAsync | W | small result model | — | — | ➖ |

### Credentials — `ArazzoControlPlaneCredentialsHandler` → `ISourceCredentialStore`

| Op | Call tree | R/W | Current pattern / hotspots | Existing bench | Target pattern + grounding | Status |
|---|---|---|---|---|---|---|
| `GET /credentials` | ListCredentials → ListAsync | R | `ToSummary` per binding | `CredentialStoreReadBenchmarks` (ResolveForUsage) | confirm summary projection | ⬜ |
| `POST /credentials` | CreateCredential → **AddAsync** | W | `List<SecretReferenceDefinition>`, `List<SecurityTag>`, record `SourceCredentialDefinition`, `with { }` tag stamp | `SourceCredentialStoreBenchmarks` (record vs draft) | `SourceCredentialBinding.Draft(...)` + store stamps; `SecretRef.IsWellFormed(ReadOnlySpan<byte>)` 0-B validation; `corvus-typed-model-construction`, `corvus-builder-context-threading`, `corvus-bytes-to-bytes`; §13.4.1 | ✅ **2.35→1.64 KB (Part D)** |
| `GET /credentials/{s}/{e}` | GetCredential → GetAsync | R | `ToSummary` | — | confirm projection | ⬜ |
| `PUT /credentials/{s}/{e}` | UpdateCredential → **UpdateAsync** | W | as create (record seam) | shares the create seam (not separately benched) | as create (draft seam) | ✅ converted with POST (Part D) |
| `DELETE /credentials/{s}/{e}` | DeleteCredential → DeleteAsync | W | minimal | — | — | ➖ |

### Catalog — `ArazzoControlPlaneCatalogHandler` → `IWorkflowCatalogClient` → `IWorkflowCatalogStore`

| Op | Call tree | R/W | Current pattern / hotspots | Existing bench | Target pattern + grounding | Status |
|---|---|---|---|---|---|---|
| `GET /catalog` search | SearchCatalog → SearchAsync | R | `BuildPage` loop; `ToTags` copy | — | confirm projection | ⬜ |
| `POST /catalog` | AddCatalogVersion → **AddAsync** | W | record `CatalogMetadata` + package bytes; `ToOwner`/`ToTags`; `SecurityTagSet.FromTags` | `CatalogStoreBenchmarks` (e2e baseline 19.92 KB) | owner is a **queryable indexed decomposition** (`Owner*` columns + read-reconstruct), not a string seam — confirmed genuine (Part D) | ➖ owner genuine (indexed); ↓ projection row is the real lever |
| `POST /catalog` *(projection)* | AddAsync → `CatalogPackage.Project` | W | parse + canonicalise + hash + id-rewrite + version-doc write + ZIP pack/unpack (the bulk; NOT the record seam) | `CatalogStoreBenchmarks` | **`.awp`** container (span read/write, zero-copy `OpenPooled`) + parse-once fusion + raw-value sources + zero-copy assembled parse + **pooled-disposable store seam** (`ParsedJsonDocument<CatalogVersion>`; pooled `MetadataDb`; `workspace.TakeOwnership`/`TransferOwnershipTo`) + InMemory take-don't-copy package | ✅ **19.92→3.95 KB (−80%)**; ZipArchive floor + rewrite/double/per-source parse + standalone version-record `MetadataDb` all gone (Part D) |
| `GET /catalog/{id}` list | ListCatalogVersions → SearchAsync | R | BuildPage | — | — | ⬜ |
| `GET …/versions/{n}` | GetCatalogVersion → GetAsync | R | `CatalogVersionSummary.From()` wrap | — | confirm congruent wrap | ⬜ |
| `PATCH …/versions/{n}` | UpdateCatalogVersion → GetAsync(check) → **UpdateMetadataAsync** | W | record `CatalogMetadataPatch`; `ToOwner`/`ToTags`; 2× GetAsync | none (e2e) | carry patch draft / mutable builder ([[corvus-mutable-documents]]) | ⬜ |
| `DELETE …/versions/{n}` | Delete → GetAsync(check) → DeleteAsync | W | 2× GetAsync | — | — | ➖ |
| `POST(custom) /catalog` purge | PurgeCatalog → PurgeAsync | W | small result | — | — | ➖ |
| `GET …/package` | GetCatalogPackage → GetPackageAsync | R | returns `ReadOnlyMemory<byte>` | — | confirm 0-copy | ➖ |
| `GET …/workflow,/schemas,/executor,/executor-manifest,/sources/{n}` | Get*Document → GetDocumentAsync | R | `ParsedJsonDocument.Parse` + workspace ownership (binary ones return bytes) | — | confirm pooled-parse + ownership handoff ([[corvus-parsed-documents-and-memory]]) | ⬜ |
| `POST …/validate` | ValidateCatalogValue → GetAsync + GetPackageAsync + cached schema | W | validation errors `List<>`; schema cache | — | review error projection | ⬜ |
| `POST …/runs` start | StartCatalogWorkflowRun → GetAsync + StartAsync + IsVersionHostedAsync | W | optional validation errors `List<>` | `WorkflowExecutorBenchmarks` (executor, not this handler) | review | ⬜ |

### Runners — `ArazzoControlPlaneRunnersHandler` → `IRunnerRegistry`

| Op | Call tree | R/W | Current pattern | Existing bench | Target | Status |
|---|---|---|---|---|---|---|
| `GET /runners` | ListRunners → ListAsync | R | `Runner.From()` wrap per row | — | confirm wrap | ⬜ |

### Identity — `ArazzoControlPlaneIdentityHandler`

| Op | Call tree | R/W | Current pattern | Existing bench | Target | Status |
|---|---|---|---|---|---|---|
| `GET /identity/whoami` | Whoami → (ControlPlaneAccess) | R | builds identity array | — | confirm | ⬜ |
| `GET /identity/capabilities` | Capabilities → (ControlPlaneAccess) | R | builds kind array | — | confirm | ⬜ |
| `GET /identity/grantees` | SearchGrantees → ObservedIdentity.SearchAsync / PrincipalDirectory.SearchAsync | R | RefTuple closure-free projection; directory path builds `List<ResolvedPrincipal>` | `GranteeProjectionBenchmarks` | re-baseline; confirm directory list is genuine leaf | ⬜ |

### Administrators — `ArazzoControlPlaneAdministratorsHandler` → `IWorkflowAdministratorStore` / `IObservedIdentityStore`

| Op | Call tree | R/W | Current pattern / hotspots | Existing bench | Target | Status |
|---|---|---|---|---|---|---|
| `GET /administrators/{id}` | List → GetAdministratorsAsync | R | `DescribeUsageScope` per admin | — | confirm projection | ⬜ |
| `POST …/members` | AddAdministrator → FindIdentityConflict? → AddAdministratorAsync → SeenAsync | W | `SecurityTagSet.Build` (span-threaded); collision probe; label allocs | — | review list seam to `PutAsync` | ⬜ |
| `PUT /administrators/{id}` | TransferAdministration → FindIdentityConflict×loop → TransferAsync | W | `List<SecurityTagSet>`; collision probe loop | — | `PutAsync` list seam review | ⬜ |
| `DELETE …/members/{d}/{v}` | RemoveAdministrator → RemoveAdministratorAsync | W | `SecurityTagSet` from {dim,val} | — | — | ⬜ |

### Security rules + bindings — `ArazzoControlPlaneSecurityHandler` → `ISecurityPolicyStore`

| Op | Call tree | R/W | Current pattern | Existing bench | Target | Status |
|---|---|---|---|---|---|---|
| `GET /security/rules` | ListRules → ListRulesAsync | R | `ToRuleSource` per rule | — | confirm projection | ⬜ |
| `POST /security/rules` | CreateRule → **AddRuleAsync** | W | `SecurityRule.Compile` validation; draft | `SecurityRuleStoreBenchmarks` (has baseline arm) | re-baseline e2e; confirm draft is minimal | ⬜ |
| `GET /security/rules/{n}` | GetRule → GetRuleAsync | R | `ToRuleSource` | — | confirm | ⬜ |
| `PUT /security/rules/{n}` | UpdateRule → **UpdateRuleAsync** | W | draft | `SecurityRuleStoreBenchmarks` (has baseline arm) | re-baseline e2e | ⬜ |
| `DELETE /security/rules/{n}` | DeleteRule → DeleteRuleAsync | W | direct | — | — | ➖ |
| `GET /security/bindings` | ListBindings → ListBindingsAsync | R | `ToBindingSource` per binding | `RowSecurityResolveBenchmarks` (resolve) | confirm projection | ⬜ |
| `POST /security/bindings` | CreateBinding → **AddBindingAsync** | W | `ReadBinding` → `List<string>` rule names; `Draft()` | `SecurityBindingStoreBenchmarks` (no baseline arm) | add baseline arm; measure | ⬜ |
| `GET /security/bindings/{id}` | GetBinding → GetBindingAsync | R | `ToBindingSource` | — | confirm | ⬜ |
| `PUT /security/bindings/{id}` | UpdateBinding → **UpdateBindingAsync** | W | draft | `SecurityBindingStoreBenchmarks` (no baseline arm) | add baseline arm; measure | ⬜ |
| `DELETE /security/bindings/{id}` | DeleteBinding → DeleteBindingAsync | W | direct | — | — | ➖ |

### Access requests — `ArazzoControlPlaneAccessRequestsHandler` → `IAccessRequestApprovalService` / `IAccessRequestStore`

| Op | Call tree | R/W | Current pattern | Existing bench | Target | Status |
|---|---|---|---|---|---|---|
| `GET /accessRequests` | List → ListAsync (+admin check) | R | `ToViewSource` per request; admin loop | — | confirm projection | ⬜ |
| `POST /accessRequests` | Submit → SubmitAsync → **CreateAsync** | W | `List<string>` scopes; `AccessRequest.Draft()` | `AccessRequestStoreBenchmarks` (no baseline arm) | add baseline arm; measure | ⬜ |
| `GET /accessRequests/{id}` | Get → GetAsync (+visibility) | R | `ToView` wrap | `AccessRequestViewProjectionBenchmarks` (has baseline arm) | re-baseline; confirm wrap | ⬜ |
| `POST …/approve` | Approve → ApproveAsync → **DecideAsync** | W | record `AccessRequestDecision` | — | carry decision draft / mutable builder | ⬜ |
| `POST …/approve-as-eligible` | ApproveAsEligible → ApproveAsEligibleAsync → DecideAsync (+ binding/rule Draft) | W | record decision; `Draft()` for binding/rule | — | as approve | ⬜ |
| `POST …/deny,/withdraw,/revoke` | → ApprovalService.* → DecideAsync | W | record decision | — | as approve | ⬜ |

---

## Part C — benchmark plan

One end-to-end (handler → InMemory store) benchmark per write row, each with a
`[Benchmark(Baseline = true)]` old-path arm. Existing store benchmarks are starting points to be
**re-run and recorded** under the clean-slate rule — none is treated as an established baseline
until its number is captured in Part D.

| Seam | Existing code | Action |
|---|---|---|
| ObservedIdentity `SeenAsync` | `ObservedIdentityStoreBenchmarks` (no baseline arm) | add old-path arm; measure before→after |
| Security rule Add/Update | `SecurityRuleStoreBenchmarks` (has baseline arm) | re-run; record numbers |
| Security binding Add/Update | `SecurityBindingStoreBenchmarks` (no baseline arm) | add old-path arm; measure |
| Access request Create | `AccessRequestStoreBenchmarks` (no baseline arm) | add old-path arm; measure |
| Access request Decide | (extend `AccessRequestStoreBenchmarks`) | record vs draft |
| SourceCredential Add/Update | **create** `SourceCredentialStoreBenchmarks` | `Create_FromRecord` (baseline) vs `Create_FromDraft` |
| Catalog Add / UpdateMetadata | **create** `CatalogStoreBenchmarks` | record vs draft |
| Administrators Put | **create** `AdministratorStoreBenchmarks` | list-seam before/after |

### Infra facts (verified)

- `MemoryDiagnoser` is applied **globally** in
  `benchmarks/Corvus.Text.Json.Arazzo.Durability.Benchmarks/Program.cs`
  (`ManualConfig.CreateMinimumViable().AddJob(Job.ShortRun…).AddDiagnoser(MemoryDiagnoser.Default)`).
- `BenchmarkDotNet.Artifacts/` is **gitignored** → run output is transient. Every before→after
  number must be recorded in Part D **and** the commit message.

---

## Part D — per-row ledger + before→after (the only record of completion)

> One sub-section per worked row: the ownership ledger, the pattern applied (skill ref), and the
> measured before→after (InMemory; Sqlite spot-check where relevant). A row is ✅ only once it
> appears here with numbers.

### ✅ `POST /credentials` → `ISourceCredentialStore.AddAsync` (and `PUT` → `UpdateAsync`)

**Pattern applied — record→draft seam elimination.** `AddAsync`/`UpdateAsync` now carry a draft
`SourceCredentialBinding` (the generated CTJ document the store already persists), not a
`SourceCredentialDefinition` record. The warm HTTP handler builds the draft straight from the
already-parsed request body via `SourceCredentialBinding.Draft(JsonElement …, in SecurityTagSet …)` —
`secretRefs`/`config`/`description`/lifecycle copied **bytes-to-bytes** (no `List`, no per-field
strings), management/usage tags written from the resolved `SecurityTagSet`s. The store reads the draft
bytes-to-bytes and stamps `id`/`createdBy`/`createdAt`/`etag`; reference validation moved to a 0-B span
(`SecretRef.IsWellFormed(ReadOnlySpan<byte>)`). Cold/programmatic callers keep an ergonomic record path
via `SourceCredentialBinding.Draft(SourceCredentialDefinition)` + `SourceCredentialStoreExtensions`.
Grounded in `corvus-bytes-to-bytes`, `corvus-typed-model-construction`, `corvus-builder-context-threading`,
§13.4.1; mirrors the sibling `SecurityRuleDocument`/`SecurityBindingDocument`/`AccessRequest` draft seams.

**Ledger** (measured region = parsed body → write seam → `InMemory` store; access-tag resolution and the
`ToSummary` response projection are excluded — those are precomputed / separate rows):

| Allocation | Owner / site | Verdict | Outcome |
|---|---|---|---|
| `List<SecretReferenceDefinition>` + N×2 strings | handler `ReadSecretRefs` | **KILL** | removed — `body.secretRefs` copied bytes-to-bytes |
| `List<CredentialConfigDefinition>` + M×2 strings | handler `ReadConfig` | **KILL** | removed — `body.config` copied bytes-to-bytes |
| `(string)Description` | handler `OptionalString` | **KILL** | removed — carried as a JSON value |
| `SourceCredentialDefinition` record + `with {…}` | handler `ReadWrite` | n/a | was already free — `readonly record struct`; `with` is a stack copy (matrix "hotspot" corrected) |
| persisted `byte[]` document | store serialize leaf | **LEAF** | kept (§13.4.1 write leaf) |
| returned `ParsedJsonDocument` wrapper | store return | **LEAF** | kept (caller disposes) |
| pooled draft document wrapper | handler/extension `Draft` | new | the bytes form the seam now carries (pooled, disposed) |

**Before → after** (InMemory, ShortRun, MemoryDiagnoser, **same run**; `SourceCredentialStoreBenchmarks` —
both arms source the same parsed body + precomputed tags, fresh store per op so the unique key does not
409, returned doc disposed as the handler does, no delete in the measured region):

| Arm | Mean | Allocated | Ratio |
|---|---|---|---|
| `Create_FromRecord` (baseline — old record seam) | 7.91 µs | **2.35 KB** | 1.00 |
| `Create_FromDraft` (new draft seam) | 6.56 µs | **1.64 KB** | **0.70× (−0.71 KB, −30%)** |

**Backends (§13.4.1 static audit).** All 10 (InMemory, Sqlite, Postgres, SqlServer, MySql, Mongo, Cosmos,
Redis, NatsJetStream, AzureStorage) carry the draft to the shared `SourceCredentialSerialization` → single
`byte[]` at the driver leaf. InMemory **and Sqlite** `SourceCredentialStoreConformance` pass in-process (no
container); the other 8 backends are container-gated (`integration`, not run here).

**Tests (net10.0, green).** `SourceCredentialStoreConformance` on InMemory + Sqlite (12 tests incl. the
trust-boundary inline-secret rejection and tag immutability), lifecycle, Http cache/transport, Server
handler, CLI. Slnx build **0 Warning(s), 0 Error(s)**.

**Decisions & deferrals.**
- **`PUT /credentials` → `UpdateAsync`** was converted in the same change (shared draft seam + serialization;
  identity/tags carried forward from the stored binding). Conformance-verified; **not separately benchmarked**
  — it eliminates the identical `List`s, so the create arm is the seam proxy. (Add an `Update_*` arm if a
  discrete number is wanted.)
- **Management-tag `List<SecurityTag>`** (handler `ReadTags` + `new List(InternalTags())` + `FromTags`) is
  left as-is — it is access-tag *resolution* (the usage tags already use the bytes-to-bytes `SecurityTagSet.Build`),
  a distinct seam from the record→draft persistence seam this row targets. Candidate follow-up.
- **`samples/` (Aspire demo)** is not in the slnx and was not built; it uses only the record extension overload
  (same overload resolution as the green tests), so it compiles, but this was not independently verified here.

### 🔬 `POST /catalog` → `IWorkflowCatalogStore.AddAsync` — baselined, conversion pending

**Baseline measured** (`CatalogStoreBenchmarks.Add_FromRecord`, InMemory, ShortRun, MemoryDiagnoser; owner sourced from a
parsed body so the `(string)body.Owner.*` transcode is in the measured region): **19.92 KB/op**, Mean 43.6 µs.

**Ledger (store-level).** The bulk — **~19.7 KB** — is `CatalogPackage.Project` (parse + canonicalise + hash + id-rewrite
+ version-doc write), inherent package work, **not** the `CatalogMetadata` record seam. The record seam is **`CatalogOwner`
(4 strings, ~160 B)** built by the handler's `ToOwner(body.Owner)`; `createdBy` is a server-side value, `tags`/`securityTags`
are already holders.

**Two findings (both recorded above):**
1. **Catalog's real perf lever is the projection (~19.7 KB), not the owner record (~160 B).** Added as a new
   `POST /catalog (projection)` row to Part B — audit `CatalogPackage.Project` for string materialisations.
2. **The owner is an INDEXED decomposition, not a record↔document seam — conversion attempted and REVERTED.** Building the
   `owner`→`JsonElement` change (Create owner `CatalogOwner`→element; `CatalogMetadata`/`Patch.Owner`→element; client +
   handler + all backends) the compiler proved it incompatible with the data model: the SQL/Azure backends store the
   governance owner as **queryable columns** (`OwnerName`/`OwnerEmail`/`OwnerTeam`/`OwnerUrl`, backing the catalog `owner`
   search filter) and **reconstruct** the version via `CatalogVersion.Create` from those columns on read. Carrying `owner`
   as opaque bytes would force every relational/Mongo backend to (a) re-extract the same four strings for its columns
   (zero alloc win) and (b) rebuild an owner doc on read (a *regression*). So `CatalogOwner` is a **genuine indexed field**,
   not a string-seam anti-pattern; the conversion was reverted.

**Conclusion.** Owner seam → **➖ confirmed genuine** (indexed decomposition; leave as `CatalogOwner`). The ~160 B owner
transcode is the price of a searchable owner. Catalog's real allocation lever is the **package projection** — the separate
`POST /catalog (projection)` row (below).

### ✅ `POST /catalog (projection)` → `CatalogPackage.Project` — DONE (`.awp` container + pooled-disposable store seam)

**Before → after (`CatalogStoreBenchmarks.Add_FromRecord`, InMemory, ShortRun): 19.92 KB → 3.95 KB/op (−15.97 KB,
−80%).** Build 0/0; full suite green (durability 294, generation 17, server 131, CLI 34; content hash byte-identical;
cross-language browser fixture re-locked). Parts 1–4 (the `.awp` container + projection-transient elimination) took it to
5.03 KB; **Part 5 (the pooled-disposable store seam) → 4.34 KB and Part 6 (InMemory take-don't-copy) → 3.95 KB.**

> **Part 5 — pooled, disposable `CatalogVersion` store seam, 5.03 → 4.34 KB.** `IWorkflowCatalogStore`/`IWorkflowCatalogClient`
> returned a **standalone GC-owned** `CatalogVersion` (built via the `[Obsolete]` `ParseValue` — unpooled GC bytes + GC
> `MetadataDb`). They now return a **pooled, disposable `ParsedJsonDocument<CatalogVersion>`** (`AddAsync`/`GetAsync`/
> `UpdateAsync`; lists via `PooledDocumentList<CatalogVersion>`; `CatalogPage : IDisposable`): `CatalogVersion.Create` now
> returns the pooled doc (`PersistedJson.ToPooledDocument` — rented bytes + **pooled** `MetadataDb`), `CreateBytes` returns
> the persisted `byte[]`; byte backends `Parse(storedBytes)`, column backends `Create(...)`; InMemory `Stored` holds the
> version-doc `byte[]`, not a typed value. The version-record `MetadataDb` is now pooled — `BuildVersionDocument` 1.54 KB →
> **0.27 KB**. Grounded in the converted `IAccessRequestStore` seam, not the catalog's old standalone pattern. **Lifetime
> rule (cost a real bug):** a handler result's body Source is re-read by the post-handler `ValidateBody()`/serialization, so
> the pooled docs must outlive the handler — single Get/Add/Update `workspace.TakeOwnership(doc)`; list endpoints
> `page.Versions.TransferOwnershipTo(workspace)` (new `PooledDocumentList<T>` method — workspace disposes the docs, the
> batch returns only its rented array); inspect-and-discard `using`. Converted across the contract + client + 9 backends +
> handler + conformance + all tests (the 9 backends fanned out to parallel agents against the InMemory/Sqlite reference).
>
> **Part 6 — InMemory takes the canonical package array, 4.34 → 3.95 KB.** `AddAsync` stored `projection.CanonicalPackage.ToArray()`
> — a redundant copy of an array the projection solely owns. It now takes the underlying array directly (`MemoryMarshal.TryGetArray`,
> exact-sized so the `ReadOnlyMemory` wraps it whole), copying only the rare non-array-backed case.

This landed in two parts. **Part 1 — incremental wins within the ZIP (committed `1da51c1`), 19.92 → 17.7 KB:**

1. **Single parse of the workflow.** `Project` re-parsed the *rewritten* workflow only to read title/description/sources;
   those are id-independent, so they're now read during the id-rewrite pass (`RewriteWorkflowId` returns them) — one parse.
2. **Zero-alloc content hash digest.** `ComputeContentHash` uses `SHA256.HashData(span, stackalloc span)` + `ToHexStringLower`.
3. **No-copy / right-sized ZIP reads** + a `Project`-only pooled read + `ReadOnlyMemory`-sources overloads.
4. **Pooled canonical-content hash buffer** via `JsonCanonicalizer.TryCanonicalize` (the canonical `byte[]` removed).

That left ~70% of the publish allocation in `System.IO.Compression.ZipArchive` (read 7.25 + write 5.54 ≈ 12.8 KB) — an
**unpoolable, structural framework floor** (proven against dotnet/runtime `main`: the large buffers are *already*
framework-pooled — `DeflateStream` 8192 + central-dir 4096 via `ArrayPool`, `Inflater`/`Deflater` use native zlib + pin the
input — so what remains GC-counted is the per-entry object graph the API exposes no hook to avoid: `ZipArchive` + one
`ZipArchiveEntry` per entry + `FullName` strings + extra-field arrays + stream wrappers + `MemoryStream.ToArray()`).
`CompressionLevel.NoCompression` is the only API lever and it's native/CPU, not GC. There is **no** zero-alloc ZIP in the
BCL or ASP.NET Core (its response compression wraps streaming `BrotliStream`/`GZipStream`; the only zero-GC compressors are
the span-struct `BrotliEncoder`/`BrotliDecoder`, and on .NET 11 `Zstandard*` — no span-struct Deflate, no zero-alloc ZIP).

**Part 2 — dropped the ZIP container (the decisive win), 17.7 → 5.8 KB.** `WorkflowPackage` now reads/writes a small
deterministic **length-prefixed (TLV) container** (`.awp`): `header(magic "AWP"+version, entryCount) + entries(nameLen,
name, encoding=stored, dataLen, data)`, sorted by name — no ZIP, no `manifest.json` (the reader never read it). Write is
one exact-sized output buffer filled with spans (`BinaryPrimitives` + `Span.CopyTo`); read is a `ref struct PackageReader`
over the bytes — `OpenPooled` returns `ReadOnlyMemory` **views into the package buffer** (zero per-document copy), `Open`
materializes leaves for its public contract. The `encoding` byte is stored (0) today, reserved for a future per-entry
Brotli/Zstd. The content hash is unchanged (canonicalises only `{ workflow, sources }`, never the container). The
zero-dependency browser builder (`web/.../workflow-package.js`) was rewritten to emit the same container, and the
cross-language `BrowserBuiltPackageTests` fixture regenerated; `.zip` → `.awp` across the CLI/SPA copy.

**Part 3 — fused the rewrite intermediate + hash transients (parse-once, raw-value, zero-copy), 2.99 → 2.25 KB / e2e
5.8 → 5.05 KB.** Three eliminations, each measured:

1. **Parse the rewritten workflow once** (2.99 → 2.46 KB). The projection produced the rewritten workflow as a
   `new byte[]`, then `ComputeContentHash` re-parsed it and `PackPooled` re-read it. `RewriteWorkflowToDocument` now
   writes+parses it into a single pooled `ParsedJsonDocument` (`PersistedJson.ToPooledDocument`), whose parsed element
   feeds the hash (new `ComputeContentHashPreSorted(in JsonElement,…)` — no re-parse) and whose raw UTF-8
   (`JsonMarshal.GetRawUtf8Value(...).Memory`) feeds `PackPooled` — eliminating the separate rewritten `byte[]` and the
   hash's re-parse.
2. **Skip the redundant source re-sort** — the pre-sorted hash path iterates by index (no `OrderBy`/enumerator), since
   `OpenPooled` already returns sources ordered by name.
3. **Drop the per-source parse + the assembled-bytes copy** (`ComputeHash` 1.00 → 0.87 KB; 2.46 → 2.25 KB). Sources are
   written into the assembled doc with `Utf8JsonWriter.WriteRawValue` instead of a per-source `ParsedJsonDocument` (the
   assembled is canonicalised afterward, so a verbatim copy yields the identical hash; `WriteRawValue` still validates,
   more cheaply than a parse), and the assembled is parsed **zero-copy** over the workspace buffer
   (`ParsedJsonDocument.Parse(IByteBufferWriter.WrittenMemory)` references the bytes, owning nothing — read synchronously
   and disposed before the buffer is returned) instead of `ToPooledDocument` (which rented + copied them). The per-source
   parse was the GC win; the zero-copy assembled removed the copy + a pool round-trip (mostly CPU, as the copy targeted a
   pooled buffer).

All hash byte-identical (294 durability tests green, incl. `Identical_packages_hash_identically`).

**Measured per-stage attribution** (`CatalogProjectionBreakdownBenchmarks`, each public stage isolated; `Project`-only
14.62 → 2.99 → 2.46 → **2.25 KB**, store `Add` 17.7 → 5.8 → 5.2 → **5.05 KB**):

| Stage | ZIP | `.awp` | Note |
|---|---|---|---|
| `OpenInputPackage` (read) | 7.25 KB | **0.55 KB** | ZipArchive object graph gone; now the materialized workflow leaf + sources list |
| `PackCanonicalPackage` (write) | 5.54 KB | **0.73 KB** | now one output array + a small entry list — the genuine canonical-package leaf |
| `ComputeHash` (standalone, public bytes path) | 1.00 KB | **0.87 KB** | per-source parse → `WriteRawValue` + zero-copy assembled parse; the fused `Project` path also drops the workflow re-parse |
| `BuildVersionDocument` (`CatalogVersion.Create`) | 1.56 KB | **1.54 KB** | the genuine stored version-doc leaf; *Part 4* removed the `status.ToString()` string |

**Part 4 — residual string sweep, e2e 5.05 → 5.03 KB.** Replaced `CatalogVersion.Create`'s `status.ToString()` enum
string with a `u8` switch (`BuildVersionDocument` 1.56 → 1.54 KB) and the projection's `versionNumber.ToString(...)`
intermediate with `string.Create(InvariantCulture, $"…")` (below BDN's rounding). These are floor-level (~24 B) — recorded
to show the sweep is exhausted, not because they move the number.

**Where the remaining 5.03 KB lives — pipeline vs store** (measured anchors: `Project` = 2304 B,
`ProjectAndBuildVersion` = 3992 B, `Add_FromRecord` = ~5.03 KB; the deltas isolate each layer):

| Layer | Allocated | Store-independent? | Constituents |
|---|---|---|---|
| Projection pipeline (`Project`) | **2.25 KB** | yes — every consumer pays | the rewritten workflow parsed **once** (pooled doc) + `ComputeContentHashPreSorted` (canonicalise working set + 64-char hash string, no re-parse/re-sort/per-source-parse, zero-copy assembled) + `PackPooled` (canonical-package `byte[]` + entry list) + `OpenPooled`/`workflowId`/metadata reads (views, no doc copy) |
| Version record (`ProjectAndBuildVersion − Project`) | **~1.65 KB** | yes — store-agnostic | `CatalogVersion.Create` ~1.54 KB (the persisted governance document) + `SourceSet.FromSources` ~0.11 KB |
| InMemory store + bench harness (`Add − ProjectAndBuildVersion`) | **~1.05 KB** | no | owner-record transcode (`new CatalogOwner`, 4 strings) ~0.28 KB (handler seam) + `new InMemoryWorkflowCatalogStore()` ~0.15 KB + `.AsTask()` ~0.09 KB (harness; real async path has none) + `CanonicalPackage.ToArray()` ~0.3 KB (persistence copy — every backend pays a form) + `SortKey` + `SortedDictionary` node ~0.13 KB (InMemory-only) |

**Conclusion — measured floor at 3.95 KB (−80%).** Of the e2e `Add` (measured anchors: `Project` 2304 B,
`ProjectAndBuildVersion` 2688 B, `Add_FromRecord` 3.95 KB): ~2.3 KB is the projection pipeline (every consumer pays),
~0.4 KB the persisted version record (pooled metadata now), the rest the InMemory persistence byte[] + owner decomposition
+ benchmark harness (`new` store + `.AsTask()` — not the real async path). Every reducible transient is gone — the ZIP
container, the rewrite intermediate, the double-parse, the redundant re-sort, the per-source parse, the assembled-bytes
copy, the residual enum/number strings, the **standalone version-record `MetadataDb`** (now pooled via the disposable
seam), and the **redundant InMemory package copy**. What remains is **durable output + irreducible parse machinery**: the
canonical package (stored), the version document (persisted), the content-hash string, the source/title/description strings
(stored or returned), and the few unavoidable `ParsedJsonDocument` wrappers. The owner stays a **record** (queryable
indexed decomposition; SQL backends need the column strings). Going lower means attacking the RFC 8785 canonicaliser's
working set or the per-source name framing (~150 B, fragile) — sharply diminishing returns against correctness risk.
**Row done.**

## Cross-references

- Skills: `corvus-typed-model-construction`, `corvus-builder-context-threading`,
  `corvus-bytes-to-bytes`, `corvus-ctj-handler-implementation`, `corvus-mutable-documents`,
  `corvus-parsed-documents-and-memory`, `corvus-benchmarks`, `corvus-buffer-and-pooling`.
- Design: `docs/control-plane/execution-host-design.md` §13, §13.4.1, §14.2.
- Memory: [[no-handrolled-records-use-codegen-jsonschema]],
  [[seams-carry-json-values-realise-at-leaf]], [[alloc-free-typed-model-construction]],
  [[alloc-free-persistence-seam]], [[alloc-ownership-ledger-discipline]],
  [[frequency-is-not-a-licence]], [[dont-anchor-on-existing-bad-code]],
  [[ctj-handler-response-projection]].
