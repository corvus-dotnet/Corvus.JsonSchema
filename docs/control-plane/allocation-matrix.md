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
| `IObservedIdentityStore.SeenAsync` | `ObservedIdentity` | `ObservedIdentitySerialization` | `params` (CTJ kind + JsonString value/label + SecurityTagSet) — seam bytes-native; the **write document** is realized into a pooled buffer (`SerializeXPooled → PooledUtf8`) and bound memory/stream where the driver supports it, owned `byte[]` only where it requires an array | ✅ **write 376→56 B (−85%); 10 backends container-verified (Part D)** |
| `ISecurityPolicyStore.Add/UpdateRule` | `SecurityRuleDocument` | `SecurityPolicySerialization` | `draft` — write serialized once into the pooled buffer the **returned** document owns (`SerializeXDoc → ParsedJsonDocument`), bound memory/stream via `JsonMarshal.GetRawUtf8Value(doc.RootElement).Memory`; update merge takes the **parsed model** (`in SecurityRuleDocument`), parsed non-copying at each leaf | ✅ **write-realization + allocate-on-read; 10 backends conformance-green (Part D)** |
| `ISecurityPolicyStore.Add/UpdateBinding` | `SecurityBindingDocument` | `SecurityPolicySerialization` | `draft` — same as rule (owned-doc write + non-copying parsed-model update) | ✅ **Part D** |
| `IAccessRequestStore.CreateAsync` (+ `DecideAsync`) | `AccessRequest` | `AccessRequestSerialization` | `draft` — create/decide serialized into the pooled buffer the **returned** document owns (`SerializeNewDoc`/`SerializeDecisionDoc → ParsedJsonDocument`), bound via `JsonMarshal`; decide takes the **parsed model** (`in AccessRequest`), parsed non-copying at each leaf | ✅ **write-realization + allocate-on-read; 10 backends conformance-green (Part D)** |
| `IAccessRequestStore.DecideAsync` | `AccessRequest` | `AccessRequestSerialization` | `record` (`AccessRequestDecision`) | ➖ **n/a** — `AccessRequestDecision` is a `readonly record struct` (a **value type, zero heap**): the decision *input* (status/reason/binding/expiry), not a persisted shape. The persisted `AccessRequest` is already done; converting the input to a draft doc would *add* allocation |
| `ISourceCredentialStore.Add/UpdateAsync` | `SourceCredentialBinding` | `SourceCredentialSerialization` | `draft` (seam carries the generated doc; `SourceCredentialDefinition` retained only as the cold-caller convenience input via `Draft(definition)` + extension) | ✅ (Part D) |
| `IWorkflowCatalogStore.AddAsync` | `CatalogVersion` | (catalog serialization) | `CatalogMetadata` (a generated **CTJ struct**, not a hand-rolled record) + `bytes` package — the submitted + canonical **package** (potentially large) bound memory/stream (SqlServer streams, Pg/MySql/Redis memory, Cosmos base64 `.Span`, Azure `BinaryData.FromBytes(ReadOnlyMemory)`, NATS envelope `.Span`); byte[]-leaf (Sqlite/Mongo) take the array zero-copy via `MemoryMarshal.TryGetArray`; the CatalogVersion doc is columns + an owned return doc (no blob) | ✅ **package write de-arrayed; 10 backends conformance-green (Part D)** |
| `IWorkflowCatalogStore.UpdateMetadataAsync` | `CatalogVersion` | (catalog serialization) | `CatalogMetadataPatch` (a generated **CTJ struct**) | ✅ **n/a beyond done** — column-only update (no package/doc blob), the existing-read is already a pooled `ParsedJsonDocument` (catalog-version row); nothing to realize |
| `IWorkflowAdministratorStore.PutAsync` | `WorkflowAdministrators` | `WorkflowAdministratorsSerialization` | `list` (`IReadOnlyList<SecurityTagSet>`) — put serialized into the pooled buffer the **returned** document owns (`SerializeNewDoc`/`SerializeUpdatedDoc → ParsedJsonDocument`), bound via `JsonMarshal`; update takes the **parsed model** (`in WorkflowAdministrators`), parsed **once** non-copying (replacing the old `EtagOf(existing)` + `SerializeUpdated` double-parse); `EtagOf` removed (column etag from the generated value) | ✅ **write-realization + allocate-on-read; 10 backends conformance-green (Part D)** |
| `IWorkflowStateStore.SaveAsync` | opaque checkpoint | (executor-owned) | `bytes` + index — opaque `ReadOnlyMemory<byte>` seam; the memory/stream backends bind it directly (SqlServer streams, Pg/MySql `ReadOnlyMemory`, Redis memory `RedisValue`, Cosmos base64 `.Span` into the run-doc envelope) instead of the old per-save `checkpointUtf8.ToArray()`; `byte[]` only at Sqlite/Mongo/Azure leaves; NATS already encodes from `.Span`; InMemory dict canonical | ✅ **write-realization (hot path); 5 memory/stream backends de-arrayed; Part D** |
| **(read seam, all backends)** read-existing + projection reads | the persisted doc | — | the seam now carries the **parsed model** (`SerializeUpserted(in ObservedIdentity)`), parsed at each leaf the leanest way; merge parses **non-copying** over the owned bytes (no `ToPooledDocument` re-copy). Driver-minted arrays (ADO `GetFieldValue<byte[]>`, Mongo/NATS/Azure) **are the leaf** (confirms §13.4.1 — no GC win there, only the copy elided); genuine GC eliminated at Cosmos (`.ToArray`→pooled) and Redis (`(byte[])`→`Lease<byte>`) | ✅ **done — Part D** (Sqlite in-process unchanged 8864→8865 B upsert / 7872→7873 B search = relational read array IS the driver leaf; Cosmos/Redis GC win container-verified; 10 backends conformance-green) |

**Backend leaf-realisation (all seams, §13.4.1 static audit — to re-confirm per worked row):** every
backend persists the same document bytes; realisations only at the driver leaf — indexed key columns
+ etag. Backends: InMemory (core) · Sqlite · Postgres · SqlServer · MySql · Mongo · Cosmos · Redis ·
NatsJetStream · AzureStorage.

---

## Part B — full API matrix (all endpoints)

`Handler.Method → [Client] → Store.Method`. `R`/`W` = read/write. *Existing bench* = benchmark code
that exists today (a starting point to be re-baselined, **not** evidence of completion).

### Runs — `ArazzoControlPlaneHandler` → `ISecuredWorkflowManagement` → `IWorkflowStateStore`

| Op | Call tree | R/W | Current pattern / hotspots | Existing bench | Target pattern + grounding | Status |
|---|---|---|---|---|---|---|
| `GET /runs` | ListRuns → ListAsync | R | builds `WorkflowRunSummary` page loop | — | confirm `From()`-wrap projection ([[ctj-handler-response-projection]]) | ✅ genuine (`From()`-wrap; Part D audit) |
| `GET /runs` *(page token + InMemory query)* | ListRuns → `IWorkflowWaitIndex.QueryAsync` | R | `string? ContinuationToken` in (`(string)PageToken`) / `string? ContinuationToken` out (store-minted token string per page); InMemory query is a `Where/OrderBy/Take/Select/ToList` LINQ chain | `WorkflowStateStoreBenchmarks` (Query_Page) | continuation-token **carrier seam**: `JsonString` token in (`From()`), pooled `ReadOnlyMemory<byte>` out via `WorkflowRunPage.Create(...)` (the page becomes a disposable class); decode bytes-native. **Bonus:** InMemory query → capped insertion-sorted top-K buffer (no LINQ) | ✅ **19.93→1.72 KB (−91%, Part D)** |
| `GET /runs/{id}` | GetRun → GetAsync | R | conditional fault/wait/tags arrays; a `ParseValue` in detail build | — | review projection; kill `ParseValue` (`corvus-typed-model-construction`) | ✅ genuine — `ParseValue(Tags.RawJson)` is the TagSet leaf (no `IJsonElement`); Part D audit |
| `DELETE /runs/{id}` | DeleteRun → GetAsync ×checks → DeleteAsync | W | 2× GetAsync access checks | — | — | ➖ |
| `POST /runs/{id}/resume` | ResumeRun → GetAsync ×2 → ResumeAsync | W | builds detail 3×; union `Match()` | — | review repeated detail builds | ✅ genuine — union `Match()` projection; Part D audit |
| `POST /runs/{id}/cancel` | CancelRun → GetAsync ×2 → CancelAsync | W | builds detail 3×; `(string)reason` | — | as resume | ✅ genuine — `Match()` projection; `(string)reason` is a request-param read; Part D audit |
| `POST(custom) /runs` purge | PurgeRuns → PurgeAsync | W | small result model | — | — | ➖ |

### Credentials — `ArazzoControlPlaneCredentialsHandler` → `ISourceCredentialStore`

| Op | Call tree | R/W | Current pattern / hotspots | Existing bench | Target pattern + grounding | Status |
|---|---|---|---|---|---|---|
| `GET /credentials` | ListCredentials → ListAsync | R | `ToSummary` per binding | `CredentialStoreReadBenchmarks` (ResolveForUsage) | confirm summary projection | ✅ per-field `Models.JsonString.From()` bytes bridge + `TransferOwnershipTo` (FIX #1, Part D); `credentialStatus`/`usageGrants` genuine floors |
| `POST /credentials` | CreateCredential → **AddAsync** | W | `List<SecretReferenceDefinition>`, `List<SecurityTag>`, record `SourceCredentialDefinition`, `with { }` tag stamp | `SourceCredentialStoreBenchmarks` (record vs draft) | `SourceCredentialBinding.Draft(...)` + store stamps; `SecretRef.IsWellFormed(ReadOnlySpan<byte>)` 0-B validation; `corvus-typed-model-construction`, `corvus-builder-context-threading`, `corvus-bytes-to-bytes`; §13.4.1 | ✅ **2.35→1.64 KB (Part D)** |
| `GET /credentials/{s}/{e}` | GetCredential → GetAsync | R | `ToSummary` | — | confirm projection | ✅ shares `ToSummary` bytes bridge + `TakeOwnership` (FIX #1, Part D) |
| `PUT /credentials/{s}/{e}` | UpdateCredential → **UpdateAsync** | W | as create (record seam) | shares the create seam (not separately benched) | as create (draft seam) | ✅ converted with POST (Part D) |
| `DELETE /credentials/{s}/{e}` | DeleteCredential → DeleteAsync | W | minimal | — | — | ➖ |

### Catalog — `ArazzoControlPlaneCatalogHandler` → `ISecuredWorkflowCatalog` → `IWorkflowCatalogStore`

| Op | Call tree | R/W | Current pattern / hotspots | Existing bench | Target pattern + grounding | Status |
|---|---|---|---|---|---|---|
| `GET /catalog` search | SearchCatalog → SearchAsync | R | `BuildPage` loop; `ToTags` copy | — | confirm projection | ✅ genuine — `From()`-wrap page; Part D audit |
| `GET /catalog` / `…/versions` *(page token)* | Search/List → `IWorkflowCatalogStore.QueryAsync` | R | `string? ContinuationToken` in (`(string)PageToken`) / `string? ContinuationToken` out (store-minted token string per page) | `CatalogStoreBenchmarks` (Search_Page) | continuation-token **carrier seam**: `JsonString` token in (`From()`), pooled `ReadOnlyMemory<byte>` out via `CatalogPage.Create(...)` (the page becomes a disposable class); decode bytes-native | ✅ **2.58→2.55 KB (token string; Part D)** |
| `POST /catalog` | AddCatalogVersion → **AddAsync** | W | record `CatalogMetadata` + package bytes; `ToOwner`/`ToTags`; `SecurityTagSet.FromTags` | `CatalogStoreBenchmarks` (e2e baseline 19.92 KB) | owner is a **queryable indexed decomposition** (`Owner*` columns + read-reconstruct), not a string seam — confirmed genuine (Part D) | ➖ owner genuine (indexed); ↓ projection row is the real lever |
| `POST /catalog` *(projection)* | AddAsync → `CatalogPackage.Project` | W | parse + canonicalise + hash + id-rewrite + version-doc write + ZIP pack/unpack (the bulk; NOT the record seam) | `CatalogStoreBenchmarks` | **`.awp`** container (span read/write, zero-copy `OpenPooled`) + parse-once fusion + raw-value sources + zero-copy assembled parse + **pooled-disposable store seam** (`ParsedJsonDocument<CatalogVersion>`; pooled `MetadataDb`; `workspace.TakeOwnership`/`TransferOwnershipTo`) + InMemory take-don't-copy package + UTF-8 entry names (no per-source name string) | ✅ **19.92→3.72 KB (−81%)**; ZipArchive floor + rewrite/double/per-source parse + standalone version-record `MetadataDb` + `PackPooled` entry list/name strings all gone (Part D) |
| `GET /catalog/{id}` list | ListCatalogVersions → SearchAsync | R | BuildPage | — | — | ✅ genuine — `From()`-wrap page; Part D audit |
| `GET …/versions/{n}` | GetCatalogVersion → GetAsync | R | `CatalogVersionSummary.From()` wrap | — | confirm congruent wrap | ✅ genuine — `From()`-wrap; Part D audit |
| `PATCH …/versions/{n}` | UpdateCatalogVersion → **UpdateAsync** (discriminated outcome) | W | record `CatalogMetadataPatch`; `ToOwner`/`ToTags`; ~~2× GetAsync~~ | none (e2e) | carry patch draft / mutable builder ([[corvus-mutable-documents]]) | ✅ handler pre-fetch dropped — client returns `CatalogUpdateResult` (read-then-write split) (FIX #6, Part D; 2.56→2.22 KB, ~5×). Store rebuild → mutable-builder in-place patch done (Part D; 2.22→1.41 KB, +securityTags fix) |
| `DELETE …/versions/{n}` | Delete → DeleteAsync (Forbidden outcome) | W | ~~2× GetAsync~~ | — | — | ✅ handler pre-fetch dropped — `CatalogDeleteOutcome.Forbidden` carries the 403 (FIX #6, Part D) |
| `POST(custom) /catalog` purge | PurgeCatalog → PurgeAsync | W | small result | — | — | ➖ |
| `GET …/package` | GetCatalogPackage → GetPackageAsync | R | returns `ReadOnlyMemory<byte>` | — | confirm 0-copy | ➖ |
| `GET …/workflow,/schemas,/executor,/executor-manifest,/sources/{n}` | Get*Document → GetDocumentAsync | R | `ParsedJsonDocument.Parse` + workspace ownership (binary ones return bytes) | — | confirm pooled-parse + ownership handoff ([[corvus-parsed-documents-and-memory]]) | ✅ genuine — pooled-parse + ownership handoff; Part D audit |
| `POST …/validate` | ValidateCatalogValue → GetAsync + GetPackageAsync + cached schema | W | validation errors `List<>`; schema cache | — | review error projection | ✅ genuine — error list is the validation leaf; Part D audit |
| `POST …/runs` start | StartCatalogWorkflowRun → GetAsync + StartAsync + IsVersionHostedAsync | W | optional validation errors `List<>` | `WorkflowExecutorBenchmarks` (executor, not this handler) | review | ✅ genuine — optional error list is the validation leaf; Part D audit |

### Runners — `ArazzoControlPlaneRunnersHandler` → `IRunnerRegistry`

| Op | Call tree | R/W | Current pattern | Existing bench | Target | Status |
|---|---|---|---|---|---|---|
| `GET /runners` | ListRunners → ListAsync | R | `Runner.From()` wrap per row | — | confirm wrap | ✅ genuine — `Runner.From<RunnerRegistration>` free generic wrap; Part D audit |

### Identity — `ArazzoControlPlaneIdentityHandler`

| Op | Call tree | R/W | Current pattern | Existing bench | Target | Status |
|---|---|---|---|---|---|---|
| `GET /identity/whoami` | Whoami → (ControlPlaneAccess) | R | builds identity array | — | confirm | ✅ genuine (sub-floor caveat: capturing closures); Part D audit |
| `GET /identity/capabilities` | Capabilities → (ControlPlaneAccess) | R | builds kind array | — | confirm | ✅ genuine (sub-floor caveat: capturing closures); Part D audit |
| `GET /identity/grantees` | SearchGrantees → ObservedIdentity.SearchAsync / PrincipalDirectory.SearchAsync | R | RefTuple closure-free projection; directory path builds `List<ResolvedPrincipal>` | `GranteeProjectionBenchmarks` | re-baseline; confirm directory list is genuine leaf | ✅ genuine — closure-free RefTuple projection; directory `List<ResolvedPrincipal>` is the adapter leaf; Part D audit |
| `GET /identity/grantees` *(page token)* | SearchGrantees → `ObservedIdentityStore.SearchAsync` | R | `string? pageToken` in / `string? NextPageToken` out (the keyset continuation — a store-minted Base64URL token string per page) | `ObservedIdentityStoreBenchmarks` (Search_Page) | continuation-token **carrier seam**: `JsonString pageToken` in (`From()`), pooled `ReadOnlyMemory<byte>` `NextPageToken` out via page `Create(...)`; decode bytes-native from request UTF-8 | ✅ **2.03→1.98 KB (Part D)** |

### Administrators — `ArazzoControlPlaneAdministratorsHandler` → `IWorkflowAdministratorStore` / `IObservedIdentityStore`

| Op | Call tree | R/W | Current pattern / hotspots | Existing bench | Target | Status |
|---|---|---|---|---|---|---|
| `GET /administrators/{id}` | List → GetAdministratorsAsync | R | `DescribeUsageScope` per admin | — | confirm projection | ✅ genuine (sub-floor caveat: `DescribeUsageScope` is a policy-seam leaf, no JSON inverse); Part D audit |
| `POST …/members` | AddAdministrator → FindIdentityConflict? → AddAdministratorAsync → SeenAsync | W | `SecurityTagSet.Build` (span-threaded); collision probe; label allocs | — | review list seam to `PutAsync` | ✅ genuine — span-threaded `SecurityTagSet` into the store seam; Part D audit |
| `PUT /administrators/{id}` | TransferAdministration → FindIdentityConflict×loop → TransferAsync | W | `List<SecurityTagSet>`; collision probe loop | — | `PutAsync` list seam review | ✅ genuine — span-threaded `SecurityTagSet`s into the store seam; Part D audit |
| `DELETE …/members/{d}/{v}` | RemoveAdministrator → RemoveAdministratorAsync | W | `SecurityTagSet` from {dim,val} | — | — | ✅ genuine — `SecurityTagSet` from request params; Part D audit |

### Security rules + bindings — `ArazzoControlPlaneSecurityHandler` → `ISecurityPolicyStore`

| Op | Call tree | R/W | Current pattern | Existing bench | Target | Status |
|---|---|---|---|---|---|---|
| `GET /security/rules` | ListRules → ListRulesAsync | R | `ToRuleSource` per rule | — | confirm projection | ✅ genuine — list items reference a pooled batch freed before serialize, so `ToRuleSource` materialization is required ([[ctj-handler-response-projection]]); FIX #2 (Part D) |
| `POST /security/rules` | CreateRule → **AddRuleAsync** | W | `SecurityRule.Compile` validation; draft | `SecurityRuleStoreBenchmarks` (has baseline arm) | re-baseline e2e; confirm draft is minimal | ✅ write-seam genuine (draft); response now `SecurityRuleSummary.From()` zero-copy wrap (FIX #2, Part D) |
| `GET /security/rules/{n}` | GetRule → GetRuleAsync | R | `ToRuleSource` | — | confirm | ✅ response now `SecurityRuleSummary.From()` zero-copy wrap (FIX #2, Part D) |
| `PUT /security/rules/{n}` | UpdateRule → **UpdateRuleAsync** | W | draft | `SecurityRuleStoreBenchmarks` (has baseline arm) | re-baseline e2e | ✅ write-seam genuine (draft); response now `SecurityRuleSummary.From()` zero-copy wrap (FIX #2, Part D) |
| `DELETE /security/rules/{n}` | DeleteRule → DeleteRuleAsync | W | direct | — | — | ➖ |
| `GET /security/bindings` | ListBindings → ListBindingsAsync | R | `ToBindingSource` per binding | `RowSecurityResolveBenchmarks` (resolve) | confirm projection | ✅ per-field `JsonString.From` + `VerbGrant.From` bytes bridge + `TransferOwnershipTo`; scopes/expiresAt/eligibleOnly non-leak preserved (FIX #3, Part D) |
| `POST /security/bindings` | CreateBinding → **AddBindingAsync** | W | `ReadBinding` → `List<string>` rule names; `Draft()` | `SecurityBindingReadBenchmarks` | add baseline arm; measure | ✅ `ReadBinding` → `SecurityBindingDocument.From(body)` free zero-copy view (no list, no Rules rebuild, no pooled draft); store defaults omitted verbs→None/order→0 (FIX #4, Part D; 1184→0 B, ~86×) |
| `GET /security/bindings/{id}` | GetBinding → GetBindingAsync | R | `ToBindingSource` | — | confirm | ✅ shares `ToBindingSource` bytes bridge + `TakeOwnership` (FIX #3, Part D) |
| `PUT /security/bindings/{id}` | UpdateBinding → **UpdateBindingAsync** | W | draft | `SecurityBindingReadBenchmarks` | add baseline arm; measure | ✅ shares `ReadBinding`→`From(body)`; `ApplyUpdate` defaults omitted verbs→None/order→0 (FIX #4, Part D) |
| `DELETE /security/bindings/{id}` | DeleteBinding → DeleteBindingAsync | W | direct | — | — | ➖ |

### Access requests — `ArazzoControlPlaneAccessRequestsHandler` → `IAccessRequestApprovalService` / `IAccessRequestStore`

| Op | Call tree | R/W | Current pattern | Existing bench | Target | Status |
|---|---|---|---|---|---|---|
| `GET /accessRequests` | List → ListAsync (+admin check) | R | `ToViewSource` per request; admin loop | — | confirm projection | ✅ genuine — `ToViewSource` `From()`-wrap; Part D audit |
| `POST /accessRequests` | Submit → SubmitAsync → **CreateAsync** | W | `List<string>` scopes; `AccessRequest.Draft()` | `AccessRequestDraftBenchmarks` | add baseline arm; measure | ✅ bytes-native `Draft(JsonElement…)` overload — `requestedScopes`/`baseWorkflowId`/`reason` carried verbatim, no `List<string>` (FIX #5, Part D; 520→152 B, −71%) |
| `GET /accessRequests/{id}` | Get → GetAsync (+visibility) | R | `ToView` wrap | `AccessRequestViewProjectionBenchmarks` (has baseline arm) | re-baseline; confirm wrap | ✅ genuine — `ToView` `From()`-wrap; Part D audit |
| `POST …/approve` | Approve → ApproveAsync → **DecideAsync** | W | record `AccessRequestDecision` | — | carry decision draft / mutable builder | ✅ genuine — decision carried via draft seam (Part A); Part D audit |
| `POST …/approve-as-eligible` | ApproveAsEligible → ApproveAsEligibleAsync → DecideAsync (+ binding/rule Draft) | W | record decision; `Draft()` for binding/rule | — | as approve | ✅ genuine — decision + binding/rule via draft seams (Part A); Part D audit |
| `POST …/deny,/withdraw,/revoke` | → ApprovalService.* → DecideAsync | W | record decision | — | as approve | ✅ genuine — decision carried via draft seam (Part A); Part D audit |

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

### 🔬 `POST /catalog` → `IWorkflowCatalogStore.AddAsync` — package bind done; projection is the remaining lever (Part B)

**Driver package write-realization DONE** (see the ✅ catalog row above): the two whole-package `.ToArray()` copies on the
add path were eliminated across the driver backends (memory/stream bind / `TryGetArray` / Blob `BinaryData`). That win is
**container-only** — the InMemory baseline below already avoids the package copy (`MemoryMarshal.TryGetArray`), so the
number is unchanged in-process; the remaining in-process cost is the projection, a separate Part B item.

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

**Before → after (`CatalogStoreBenchmarks.Add_FromRecord`, InMemory, ShortRun): 19.92 KB → 3.72 KB/op (−16.20 KB,
−81%).** Build 0/0; full suite green (durability 294, generation 17, server 131, CLI 34; content hash byte-identical;
cross-language browser fixture re-locked). Parts 1–4 (the `.awp` container + projection-transient elimination) took it to
5.03 KB; **Part 5 (the pooled-disposable store seam) → 4.34 KB, Part 6 (InMemory take-don't-copy) → 3.95 KB, and Part 7
(UTF-8 entry names — no per-source name string) → 3.72 KB.**

> **Part 5 — pooled, disposable `CatalogVersion` store seam, 5.03 → 4.34 KB.** `IWorkflowCatalogStore`/`ISecuredWorkflowCatalog`
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
>
> **Part 7 — UTF-8 entry names, no per-source name string, 3.95 → 3.72 KB.** `PackPooled` built a `List<PackEntry>` and a
> per-source `"sources/" + key + ".json"` concat string for every entry, then sorted the list by full entry name. It now
> orders the sources by **key** in a single **pooled** scratch array (`ArrayPool`, returned after the write) and emits
> entries in a fixed bucket order — workflow, sorted sources, metadata — writing each entry name straight into the output
> as UTF-8 (`"sources/"u8` + the transcoded key + `".json"u8`, the `uint16` length back-patched from the bytes written).
> No `List`, no per-source name string. The reader locates entries by name (not position) and the catalog re-packs to
> canonical on add, so the new fixed order is a safe stored-layout change; the content hash is unaffected (it canonicalises
> only `{ workflow, sources }`, never the container — `Identical_packages_hash_identically` and the browser fixture still
> pass). Measured: `PackCanonicalPackage` 0.73 → **0.49 KB**, `Project` 2.25 → **2.02 KB** (2304 → 2064 B), e2e 3.95 →
> **3.72 KB**. The win scales with source count — one concat string + one list slot eliminated per source.

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

**Where the remaining 3.72 KB lives — pipeline vs store** (measured anchors: `Project` = 2064 B,
`ProjectAndBuildVersion` = 2448 B, `Add_FromRecord` = 3.72 KB; the deltas isolate each layer):

| Layer | Allocated | Store-independent? | Constituents |
|---|---|---|---|
| Projection pipeline (`Project`) | **2.02 KB** | yes — every consumer pays | the rewritten workflow parsed **once** (pooled doc) + `ComputeContentHashPreSorted` (canonicalise working set + 64-char hash string, no re-parse/re-sort/per-source-parse, zero-copy assembled) + `PackPooled` (canonical-package `byte[]`; sources ordered in a **pooled** scratch array, each entry name written as UTF-8 — no per-entry list, no per-source name string) + `OpenPooled`/`workflowId`/metadata reads (views, no doc copy) |
| Version record (`ProjectAndBuildVersion − Project`) | **~0.37 KB** | yes — store-agnostic | `CatalogVersion.Create` ~0.27 KB (the persisted governance document — **pooled** `MetadataDb`) + `SourceSet.FromSources` ~0.11 KB |
| InMemory store + bench harness (`Add − ProjectAndBuildVersion`) | **~1.33 KB** | no | the persisted **version-document** `byte[]` (`CreateBytes`, the durable governance record this path stores) + owner-record transcode (`new CatalogOwner`, 4 strings) ~0.28 KB (handler seam) + `new InMemoryWorkflowCatalogStore()` + `.AsTask()` (harness; the real async path has neither) + `SortKey`/`SortedDictionary` node (InMemory-only). The canonical-package copy is gone (Part 6 takes the projection's array directly). |

**Conclusion — measured floor at 3.72 KB (−81%).** Of the e2e `Add` (measured anchors: `Project` 2064 B,
`ProjectAndBuildVersion` 2448 B, `Add_FromRecord` 3.72 KB): ~2.0 KB is the projection pipeline (every consumer pays),
~0.4 KB the persisted version record (pooled metadata now), the rest the InMemory persistence byte[] + owner decomposition
+ benchmark harness (`new` store + `.AsTask()` — not the real async path). Every reducible transient is gone — the ZIP
container, the rewrite intermediate, the double-parse, the redundant re-sort, the per-source parse, the assembled-bytes
copy, the residual enum/number strings, the **standalone version-record `MetadataDb`** (now pooled via the disposable
seam), the **redundant InMemory package copy**, and the **`PackPooled` entry list + per-source name strings** (entry names
now written as UTF-8 directly). What remains is **durable output + irreducible parse machinery**: the canonical package
(stored), the version document (persisted), the content-hash hex string (written into the version document and bound as a
query column), the source/title/description strings (stored or returned), and the few unavoidable `ParsedJsonDocument`
wrappers. The owner stays a **record** (queryable indexed decomposition; SQL backends need the column strings). Going
lower means attacking the RFC 8785 canonicaliser's working set — sharply diminishing returns against correctness risk.
**Row done.**

### ✅ Continuation-token carrier seam — observed-identity `Search` (+ the shared token helpers)

The keyset paging tokens (`*ContinuationToken` helpers + `*Page` results + store `pageToken` params + handlers) were a
**carrier seam**: an opaque token round-trips between two UTF-8 ends — emitted into a JSON response, carried back in the
next JSON request (a CTJ `JsonString`) — yet the old code minted a managed `string` at both ends ("store-minted, not
identity data, so it stays a string" — the genuine-leaf rationalization, [[arazzo-tag-string-alloc-conventions]] /
skill `corvus-bytes-to-bytes`). Both ends are bytes. Converted bytes-native via `System.Buffers.Text.Base64Url`.

This row is the **observed-identity** feature (the credentials feature was the reference, committed `0a303fcd05`;
**workflow-run** remains). The shape (mirrors credentials exactly):

| Element | Before | After |
|---|---|---|
| `IObservedIdentityStore.SearchAsync` page token | `string? pageToken` | `JsonString pageToken` (handler bridges the request value with `JsonString.From(...)`; decode bytes-native from `pageToken.GetUtf8String().Span`) |
| `ObservedIdentityPage.NextPageToken` | `string?` (a Base64URL token **string** per token-emitting page) | pooled `ReadOnlyMemory<byte>` via factory `Create(identities[, subjectValue, subjectKind])` (rent + `EncodeToUtf8`; `Dispose` returns the buffer) |
| handler emit | `(JsonString.Source)token` from the string | `(Models.JsonString.Source) page.NextPageToken.Span` — pooled UTF-8 written straight into the response body (the **deferred-body lifetime**: the buffer outlives the synchronous `Ok` build, freed on page dispose) |
| 10 backends (InMemory + Sqlite by hand, 8 via parallel agents) | `Encode(...)` → `new ObservedIdentityPage(docs, nextToken)` | `Page.Create(docs, lastValue, lastKind)` / `Page.Create(docs)` |

**Measured (`ObservedIdentityStoreBenchmarks.Search_Page`, InMemory, prefix matches 100, limit 10 → a token-emitting
page): 2.03 → 1.98 KB.** The eliminated GC allocation is the one Base64URL token string per token-emitting page (~51 B
— small, the directive's full-convert-every-seam mandate, not a headline). The token helpers themselves are 0-B on the
warm path: `ContinuationTokenBenchmarks` shows **encode 112–136 B → 0 B** (assemble the cursor into a stack/`ArrayPool`
UTF-8 buffer + separator byte → `Base64Url.EncodeToUtf8` straight into the destination; no `EncodeToString`/concat/
per-part `ToString()`). The rest of `Search_Page`'s ~1.98 KB is the closure-free projection floor + InMemory keyset
working set (the `top` capped buffer) — a real backend pushes `ORDER BY … LIMIT` down and has no `top`.

Buffer sizing uses `Encoding.UTF8.GetMaxByteCount(len)` (a multiply, not a scan) — the helper is `GetMaxEncodedLength`
(an upper bound; the exact length is `EncodeToUtf8`'s `written` return), see [[getmaxbytecount-for-scratch-buffers]].
Verified: full slnx **0/0**; observed conformance **InMemory + Sqlite pass** (token round-tripped through the `JsonString`
seam, malformed token still rejected); identity API server tests pass. **Row done** (workflow-run is the last carrier
feature).

### ✅ Continuation-token carrier seam — workflow-run `Query` (+ the InMemory capped-buffer bonus)

The last carrier feature: the run-list keyset token (`WorkflowQuery.ContinuationToken` in ↔ `WorkflowRunPage` out),
both managed `string`s before. The structurally-different one — `WorkflowRunPage` was a `readonly record struct`, so it
**became a `sealed class : IDisposable`** to own the pooled token buffer (a record struct owning a rented `byte[]`
double-returns on value-copy; `CatalogPage` only gets away with it because it owns a class `PooledDocumentList`).

| Element | Before | After |
|---|---|---|
| `WorkflowQuery.ContinuationToken` (input) | `string?` (handler did `(string)parameters.PageToken`) | `JsonString` (handler bridges `JsonString.From(parameters.PageToken)`; every store decodes the run-id cursor from `query.ContinuationToken.GetUtf8String().Span`) |
| `WorkflowRunPage` (output) | `readonly record struct (Runs, string? ContinuationToken)` | `sealed class : IDisposable` — `Runs` + pooled `ReadOnlyMemory<byte> NextPageToken` via `Create(runs[, lastRunId])` |
| `WorkflowContinuationToken.Paginate` | `new WorkflowRunPage(rows, Encode(...))` | `WorkflowRunPage.Create(rows, rows[^1].Id.Value)` — covers all 9 SQL/NoSQL backends (they delegate to `Paginate`) |
| handler emit | `(JsonString.Source)page.ContinuationToken` | `(Models.JsonString.Source)page.NextPageToken.Span` (the body `Source` closure runs synchronously inside `Ok` → `CreateBuilder`, copying the span while the `using`-scoped page is alive — the deferred-body rule) |
| internal `SecuredWorkflowManagement.PurgeAsync` paging loop | `string? token` round-trip | re-presents `page.NextPageToken` through the `JsonString` seam via a pooled `WrapContinuationToken` (quote+parse) — net-neutral (the store no longer mints a token string; the loop wraps the bytes instead) |
| 10 state stores | `Decode(query.ContinuationToken)` (string) | bytes-native span decode (InMemory + Sqlite by hand, the other 8 mechanically) |

**Bonus — InMemory query rewrite (the headline).** `InMemoryWorkflowStateStore.QueryAsync` built its page with a
`Where().OrderBy().Take().Select().ToList()` LINQ chain over the whole entry set — the dominant allocation. Replaced with
a **capped, insertion-sorted top-K buffer** (one bounded `List<WorkflowRunListing>(Limit+1)`, the in-memory analogue of
`ORDER BY run-id LIMIT Limit+1`, mirroring the observed-identity InMemory store), eliminating the LINQ iterators +
closures + the unbounded `OrderBy` buffer.

**Measured (`WorkflowStateStoreBenchmarks.Query_Page`, InMemory, 100 rows, limit 10 → a token-emitting page): 19.93 →
1.72 KB (−91%).** The carrier conversion removes the per-page token string (the 0-B token helper proof is
`ContinuationTokenBenchmarks`); the capped buffer removes the LINQ working set (the bulk). The token elimination applies
to **all 10 backends** (production SQL/NoSQL too), not just InMemory.

Verified: full slnx **0/0**; WorkflowStateStore conformance **InMemory + Sqlite pass** (the paging round-trip through the
`JsonString` seam, ascending-id order preserved); the runs API server tests, `SecuredWorkflowManagementTests` (list +
purge), the trigger/worker tests all pass. **Row done.**

### ✅ Continuation-token carrier seam — catalog-search `Query` (the seam closed)

The fourth and final carrier feature. `CatalogQuery.ContinuationToken` (in) ↔ `CatalogPage.ContinuationToken` (out) shared
the `WorkflowContinuationToken` helper and were both managed `string`s (the catalog *projection* row was done; its *search
paging* token had never been converted). `CatalogPage` was a `readonly record struct : IDisposable` (it already owns a
`PooledDocumentList<CatalogVersion>`) → **became a `sealed class`** to also own the pooled token buffer (the same record-
struct-can't-own-a-rented-buffer reasoning as `WorkflowRunPage`).

| Element | Before | After |
|---|---|---|
| `CatalogQuery.ContinuationToken` (input) | `string?` (handler did `(string)parameters.PageToken`, two call sites) | `JsonString` (handler bridges `JsonString.From(...)`; each store decodes the `(baseWorkflowId, versionNumber)` sort-key cursor from `query.ContinuationToken.GetUtf8String().Span`) |
| `CatalogPage` (output) | `readonly record struct (Versions, string? ContinuationToken) : IDisposable` | `sealed class : IDisposable` — `Versions` + pooled `ReadOnlyMemory<byte> NextPageToken` via `Create(versions[, sortKey])` |
| 10 catalog stores | `Decode(query.ContinuationToken)` + `Encode(SortKey(...))` + `new CatalogPage(...)` | span decode + `CatalogPage.Create(versions, sortKey)` / `Create(versions)` (no shared `Paginate` — each store changes both ends; InMemory + Sqlite by hand, 8 mechanically; Mongo's inline-pattern decode) |
| handler (2 search sites) | `(string)PageToken` in; `(JsonString.Source)page.ContinuationToken` out | `JsonString.From(...)` in; `(Models.JsonString.Source)page.NextPageToken.Span` out (the token is a scalar copied into the body synchronously during `Ok`→`CreateBuilder`, while the `using` page is alive — the `From`-wrapped versions are the ones the existing `TransferOwnershipTo` handles) |

**Measured (`CatalogStoreBenchmarks.Search_Page`, new; InMemory, 25 versions, limit 10 → a token-emitting page): 2.58 →
2.55 KB.** A small no-regression token elimination (the per-page Base64URL token string; the pooled `CatalogVersion`
document working set dominates the rest) — same magnitude as observed-identity, with the 0-B token proof from
`ContinuationTokenBenchmarks`. **No capped-buffer bonus**: the InMemory catalog store already iterates a `SortedDictionary`
into a `PooledDocumentList` (no LINQ). Verified: full slnx **0/0**; catalog conformance **InMemory + Sqlite pass** (the
first→second paging round-trip through the `JsonString` seam); catalog API server tests pass.

**The continuation-token carrier seam is now closed — all four paging features (credentials, observed-identity,
workflow-run, catalog-search) carry their tokens bytes-native.** Both ends of every opaque token are UTF-8 with no managed
token string on the warm path; the `WorkflowContinuationToken` / `*ContinuationToken` helpers are 0-B
(`ContinuationTokenBenchmarks`). **Row done.**

### ✅ Persistence write-realization — `IObservedIdentityStore.SeenAsync` document (the pattern-setter)

The store serializers realized the persisted document as an **owned GC `byte[]`** (`PersistedJson.ToArray`) on *every*
write, for *every* backend — defensible only where the driver genuinely needs an array. Most drivers don't: they bind a
`ReadOnlyMemory`/`ArraySegment`, a span, or stream the BLOB. So the document is now realized into a **pooled buffer** and
the byte[] kept only at the leaves that require it.

- **Infra (shared, `Corvus.Text.Json.Arazzo.Durability`):** `PooledUtf8` (a disposable `readonly struct` — ArrayPool
  buffer + length, exposing `.Memory`/`.Span`/`.Segment`); `PersistedJson.RentDocument(in ctx, write) → PooledUtf8` (the
  pooled-result counterpart of `ToArray`); `ReadOnlyMemoryStream` (a **pooled, dual-mode** read-only seekable stream —
  `Rent(memory)` borrows / `RentOwned(buf,len)` owns — object-pooled via a `ConcurrentQueue`, so no per-write stream
  allocation; it supersedes the non-poolable `CosmosJson.PooledWriteStream`, which `WriteToStream` now uses, pooling every
  Cosmos write's stream instance). The serializer gains `SerializeXPooled(...) → PooledUtf8` beside `SerializeX → byte[]`.
- **Per-backend bind (the backend picks the form its driver needs):** SqlServer streams `VARBINARY(MAX)` from a
  `ReadOnlyMemoryStream`; Postgres binds `NpgsqlParameter<ReadOnlyMemory<byte>>` (`bytea`); MySql binds `ReadOnlyMemory`;
  Redis passes a memory `RedisValue`; Cosmos `WriteRawValue(doc.Span)` into the envelope. **byte[] kept (genuine leaf):**
  Mongo `BsonBinaryData(byte[])`, NATS `NatsKVStore<byte[]>`, Azure Table binary, Sqlite `SqliteParameter`, InMemory storage.
- **Measured:** `ObservedIdentityStoreBenchmarks.Serialize_ToArray` **376 B → `Serialize_Pooled` 56 B (0.15×)** — the ~320 B
  document array eliminated per write on the memory/stream backends (the 56 B residual is the provenance list, common to both).
- **Verified against real containers** (podman socket, [[broker-integration-tests-wsl-podman]]): all 10 `IObservedIdentityStore`
  conformance suites green — SqlServer / Postgres / Redis / MySql / Cosmos (converted) **and** Mongo / Nats / AzureStorage /
  Sqlite / InMemory (byte[]-leaf, unchanged), 7/7 each. **Row done** (the write-realization pattern-setter; the same shape
  rolls out to the other serialization helpers — SourceCredential, CatalogVersion, SecurityRule/Binding, AccessRequest,
  WorkflowAdministrators, WorkflowCheckpoint, RunnerRegistration — and the sibling **allocate-on-read** row in Part A).

### ✅ Allocate-on-read — `IObservedIdentityStore` read paths (upsert merge + projection)

The sibling of the write row: every read minted a document `byte[]` and the upsert merge then **re-parsed** it through
`PersistedJson.ToPooledDocument` (an `ArrayPool` rent + copy of bytes we already owned). The fix carries the **parsed
model** on the serializer seam (`SerializeUpserted(in ObservedIdentity)` / `SerializeUpsertedPooled(in ObservedIdentity)`)
so each backend reads+parses at its leaf the leanest way, and the merge reads it **non-copying**.

- **Per-backend read.** ADO (`GetFieldValue<byte[]>`) / Mongo (`BsonBinaryData.Bytes`) / NATS (`NatsKVEntry<byte[]>`) /
  Azure (`GetBinary`) / InMemory (dict array): the driver mints a managed array — **the read leaf** — so we parse
  **non-copying** over it (no `ToPooledDocument` re-copy). Cosmos: parse off the live pooled query response (drop the
  `.ToArray()` GC copy). Redis: `StringGetLeaseAsync` → pooled `Lease<byte>` (replacing the `(byte[])RedisValue` GC cast),
  parsed non-copying for the merge / copied into a pooled doc for a returned projection.
- **This confirms §13.4.1, it does not reverse it.** The relational/driver-minted read array genuinely *is* the driver
  leaf: the measured in-process floor is **unchanged** — `ObservedIdentityUpsertReadBenchmarks` over a real embedded SQLite
  driver: `Sqlite_Upsert` **8864 → 8865 B**, `Sqlite_Search` **7872 → 7873 B** (InMemory floor 912 B / 2032 B unchanged).
  What the row removes there is the redundant `ArrayPool` copy + parse pass (CPU / pool-churn, not GC — so MemoryDiagnoser
  is rightly flat). A `GetStream`/`Parse(Stream)` attempt to pool the read **regressed** it (SQLite buffers internally:
  `Sqlite_Search` 7872 → 16960 B) and was reverted. The genuine GC eliminations are **Cosmos** (`.ToArray`) and **Redis**
  (`(byte[])` cast) — both backends that minted an *extra* array on top of an already-pooled buffer — verified by
  conformance + byte-flow audit, not micro-bench (no in-process driver for them; §13.4.1's tool of record).
- **Verified against real containers** (podman socket): all 10 `IObservedIdentityStore` conformance suites green
  (Postgres / MySql / Redis / Mongo / NATS / AzureStorage / SqlServer / Cosmos 7/7, InMemory + Sqlite in-process 7/7).
  **Row done.** The same parse-non-copying-at-the-leaf read shape rolls out to the other stores' read paths next.

### ✅ Security store — write-realization + allocate-on-read (`ISecurityPolicyStore`, rule + binding)

The ObservedIdentity treatment applied to the security store, where the write document is **both persisted and returned** to
the caller (the store returns the persisted `ParsedJsonDocument`). One pooled buffer now does both jobs.

- **Write-realization (memory/stream backends — SqlServer/Postgres/MySql/Redis).** New `SecurityPolicySerialization.
  Serialize{New,Updated}{Rule,Binding}Doc(...) → ParsedJsonDocument<T>` serialize once into the pooled buffer the
  **returned** document owns; the store binds those exact bytes via `JsonMarshal.GetRawUtf8Value(doc.RootElement).Memory`
  (`Corvus.Runtime.InteropServices.JsonMarshal` — no `out` plumbing, no public raw accessor needed) and returns the same
  document (disposing it only on a write failure). Eliminates the standalone GC document array the old `SerializeNewRule →
  byte[]` + `ToPooledDocument(json)` minted **in addition** to the returned doc. byte[]-leaf backends (Mongo/NATS/Azure/
  Sqlite/InMemory) keep the `byte[]` write (the driver needs an exact array; InMemory's dict is canonical).
- **Allocate-on-read (all backends).** `SerializeUpdated{Rule,Binding}` now take the existing record as the **parsed model**
  (`in SecurityRuleDocument` / `in SecurityBindingDocument`); each backend parses the stored bytes **non-copying** at its
  leaf (`ParsedJsonDocument<T>.Parse(driverArray.AsMemory())`) instead of the old `ToPooledDocument` re-copy. Projection
  reads (Get/List/Snapshot) likewise parse non-copying over the driver's array. Cosmos reads off the **live** pooled query
  response (its `DocumentAsync` → `ReadResponseAsync`, dropping a `.ToArray()`); Redis reads a pooled `Lease<byte>`.
  `RuleEtagOf`/`BindingEtagOf` keep their `byte[]` signature (a `Func<byte[],WorkflowEtag>` delete delegate) but parse
  non-copying internally.
- **Measured** (`SecurityRuleStoreBenchmarks`, in-process): `Update_FromRequest` **1086 → 1088 B** (flat — the InMemory
  reference's dict array is canonical and the read re-copy was `ArrayPool`, not GC, so MemoryDiagnoser is rightly flat, as
  for the ObservedIdentity read row). The write-realization shows at the serializer: `Serialize_NewRule_ToArray` **552 B**
  (GC array) vs `Serialize_NewRule_Doc` **512 B** (the owning pooled document the store returns — its buffer pooled, no
  standalone array). The memory/stream backends' end-to-end GC win (array eliminated) is container-side, not in-process.
- **Verified against real containers** (podman socket): all 10 `ISecurityPolicyStore` conformance suites green
  (Postgres/MySql/Redis/Mongo/NATS/AzureStorage/SqlServer/Cosmos 7/7, InMemory 9/9 + Sqlite 7/7 in-process). **Row done.**

### ✅ Access-request store — write-realization + allocate-on-read (`IAccessRequestStore`)

The security-store treatment applied verbatim to `IAccessRequestStore` (one doc type — `AccessRequest` — create + decide):
`AccessRequestSerialization.Serialize{New,Decision}Doc(...) → ParsedJsonDocument<AccessRequest>` serialize once into the
pooled buffer the **returned** document owns (memory/stream backends bind `JsonMarshal.GetRawUtf8Value(doc.RootElement).
Memory`); `SerializeDecision` takes the **parsed model** (`in AccessRequest`), parsed non-copying at each leaf; projection
reads (`GetAsync`/`ListAsync`) parse non-copying over the driver's array; `EtagOf(byte[])` keeps its delegate signature
(parses non-copying internally). byte[]-leaf backends (Mongo/NATS/Azure/Sqlite/InMemory) keep the `byte[]` write.

- **Measured** (`AccessRequestStoreBenchmarks`, in-process): `Create_FromDraft` **1270 → 1264 B** (flat — InMemory
  reference, no regression). Write-realization at the serializer: `Serialize_New_ToArray` **728 B** (GC array) vs
  `Serialize_New_Doc` **512 B** (the owning pooled document the store returns; buffer pooled, GC array dropped).
- **Bonus correctness fix (Cosmos).** Converting `DecideAsync` surfaced a latent bug: it re-stamped the envelope's
  query mirrors (`bw`/`st`/`sv`/`createdAt`) by reading those *short* names from the **embedded doc** (which uses the long
  `baseWorkflowId`/… names) → empty mirrors + a `createdAt` format mismatch after a decision, so a decided request dropped
  out of list-by-workflow/subject queries. Now derived from the parsed model (matching the create path and the other
  backends). Cosmos's `DocumentAsync` (a `.ToArray()`) became `ReadResponseAsync` reading off the live pooled response.
- **Verified against real containers** (podman socket): all 10 `IAccessRequestStore` conformance suites green
  (Postgres/MySql/Redis/Mongo/NATS/AzureStorage/SqlServer/Cosmos 6/6, InMemory + Sqlite 6/6 in-process). **Row done.**

### ✅ Workflow-administrator store — write-realization + allocate-on-read (`IWorkflowAdministratorStore`)

The proven pattern applied to `IWorkflowAdministratorStore` (§15; one doc type — `WorkflowAdministrators` — Put + Get),
with an extra cleanup: the store previously parsed the existing record **twice** on update (`EtagOf(existing)` for the
concurrency check, then `SerializeUpdated(existing)` again) and the durable backends additionally re-parsed the
just-serialized bytes via `EtagOf(json)` for their indexed etag column.

- **Serializer.** `Serialize{New,Updated}Doc(...) → ParsedJsonDocument<WorkflowAdministrators>` (owned, memory/stream;
  bound via `JsonMarshal.GetRawUtf8Value(doc.RootElement).Memory`); `SerializeUpdated` now takes the **parsed model**
  (`in WorkflowAdministrators`). **`EtagOf` removed entirely** — the update's concurrency check reads
  `current.RootElement.EtagValue` from the one non-copying parse, and the indexed column takes the etag the store already
  generated (no post-serialize reparse). byte[]-leaf backends (Mongo/NATS/Azure/Sqlite/InMemory) keep the `byte[]` write.
- **Measured** (`WorkflowAdministratorStoreBenchmarks`, serializer in-process): `Serialize_New_ToArray` **360 B** (GC array)
  → `Serialize_New_Doc` **184 B** (−49%); `Serialize_Updated_ToArray` **520 B** → `Serialize_Updated_Doc` **272 B** (−48%)
  — the GC document array dropped on the memory/stream backends (residual = the owning pooled document the store returns).
  The update's double-parse → single non-copying parse is a CPU/`ArrayPool` win (not GC-visible), as for the prior rows.
- **Verified against real containers** (podman socket): all 10 `IWorkflowAdministratorStore` conformance suites green
  (Postgres/MySql/Redis/Mongo/NATS/AzureStorage/SqlServer/Cosmos) + InMemory + Sqlite in-process. **Row done.**

### ✅ Checkpoint store — write-realization on the run-state hot path (`IWorkflowStateStore.SaveAsync`)

The matrix tentatively marked this ➖, but it was **not**: the seam carries the opaque checkpoint as `ReadOnlyMemory<byte>`,
yet most backends did `checkpointUtf8.ToArray()` on **every** save — a per-save GC array on the run-state hot path (every
workflow step). The checkpoint is opaque (no serializer), so the fix is purely the per-backend bind:

- **memory/stream backends** (SqlServer/Postgres/MySql/Redis/Cosmos) bind the memory directly — SqlServer streams it as the
  `VARBINARY(MAX)` parameter (`ReadOnlyMemoryStream`), Postgres `NpgsqlParameter<ReadOnlyMemory<byte>>`, MySql `AddWithValue`,
  Redis a memory `RedisValue` (Lua `HSET` argv), Cosmos `WriteBase64String(..., checkpoint.Span)` into the pooled run-doc
  envelope (`RunDocument.WriteJson`). The per-save `.ToArray()` is gone.
- **byte[]-leaf** (Sqlite/Mongo/Azure) keep the array (the driver needs it); **NATS** already encodes from `.Span`;
  **InMemory** keeps its canonical dict array.
- **Measured** (`WorkflowStateStoreBenchmarks`, the bind in isolation): `Checkpoint_Bind_ToArray` **1048 B** (the per-save GC
  array for a 1 KB checkpoint) → `Checkpoint_Bind_StreamRent` **0 B** (pooled stream) — eliminated entirely, scaling with
  checkpoint size, on the hottest write path in the system.
- **Verified against real containers** (podman socket): the `IWorkflowStateStore` conformance suites green on the changed
  backends (SqlServer/Postgres/MySql/Redis/Cosmos) + InMemory + Sqlite in-process. **Row done.** (`LoadAsync`'s read
  `byte[]` is the driver leaf — §13.4.1 — left as-is.)

### ✅ Catalog store — package write de-arrayed (`IWorkflowCatalogStore.AddAsync`)

The last Part A write row. The catalog stores **decomposed columns** + a (potentially large) **package** blob — the
CatalogVersion document is reconstructed from columns on read and built only for the return value (no Document blob). The
read was already pooled (catalog-version row); `CatalogMetadata`/`CatalogMetadataPatch` are generated CTJ structs (not
hand-rolled records). So the only remaining allocation was the **package**, copied to a GC array **twice** per add:
`packageUtf8.ToArray()` (the whole submitted package) + `projection.CanonicalPackage.ToArray()` (the whole canonical
package).

- **Fix.** `AddCoreAsync` takes the package as `ReadOnlyMemory<byte>` (`CatalogPackage.Project` already accepts it); the
  canonical package is bound without a copy: SqlServer streams it (`ReadOnlyMemoryStream`); Postgres
  `NpgsqlParameter<ReadOnlyMemory<byte>>`, MySql `AddWithValue(memory)`, Redis a memory `RedisValue`; Cosmos
  `WriteBase64String(..., .Span)` into the item; Azure uploads the block blob via `BinaryData.FromBytes(ReadOnlyMemory)`;
  NATS embeds it into its envelope from `.Span`; Sqlite/Mongo take the array **zero-copy** via `MemoryMarshal.TryGetArray`
  (the canonical package is an exact-sized array, so no copy), falling back to `ToArray` only if not whole-array-backed.
  InMemory already did this.
- **Impact.** The two eliminated copies are the size of the **whole package** (an Arazzo workflow + its sources) — by far
  the largest per-operation arrays in the durability layer, even if the add path itself is admin-rare.
- **Verified against real containers** (podman socket): all 10 `IWorkflowCatalogStore` conformance suites green. **Row done.**
  `GetPackageAsync`'s read `byte[]` is the driver leaf (returned to the caller; §13.4.1), left as-is.

### 🔍 Part B handler-projection audit (all 35 ⬜ handler rows)

Per the campaign directive, every Part B handler row was read end-to-end (request body / store result →
response source projection) to decide whether its allocation floor is **genuine** (a `From()`-wrap, a
union `Match()`, or a real driver/policy leaf with no bytes-to-bytes inverse) or a **record/list/string
seam** still to convert. Verdict: **29 genuine, 6 fixes, 2 sub-floor caveats.** This is a read-only audit
— no code changed; the 6 fixes are queued as their own ground→baseline→fix→benchmark→commit rows below.

**Genuine (✅) — projection floor confirmed, nothing to convert:**

- **Runs** (`ArazzoControlPlaneHandler`, all 4): `GET /runs` is a `From()`-wrap page; `GET /runs/{id}`,
  `POST …/resume`, `POST …/cancel` build their detail via union `Match()` over the stored run and the only
  realise is `ParseValue(detail.Tags.RawJson)` — `TagSet` exposes no `IJsonElement`, so the re-parse is the
  genuine leaf (same shape blessed in the credentials/security tag rows). `(string)reason` is a
  **request-parameter** read, not a body realise.
- **Runners** (`ArazzoControlPlaneRunnersHandler`): `GET /runners` is `Runner.From<RunnerRegistration>(r)`
  per row — a free cross-assembly generic wrap ([[v5-no-base-jsonstring-per-root-identity]]).
- **Identity** (`ArazzoControlPlaneIdentityHandler`, all 3): `grantees` is the closure-free `RefTuple`
  projection (already a Part A win) and the directory path's `List<ResolvedPrincipal>` is the genuine
  adapter leaf. `whoami`/`capabilities` build small CTJ arrays from the resolved access — genuine, **caveat
  below**.
- **Administrators** (`ArazzoControlPlaneAdministratorsHandler`, all 4): list/add/transfer/remove all thread
  `SecurityTagSet` spans into the store seam (already span-disciplined); the only response build is
  `DescribeUsageScope` per admin — genuine, **caveat below**.
- **Catalog** (most): `search`, `GET …/versions/{n}` (`CatalogVersionSummary.From()`), `GET …/{id}` list,
  the document GETs (`ParsedJsonDocument.Parse` + workspace ownership handoff), `validate` (error list is the
  genuine validation leaf), and `runs start` are all `From()`-wrap / pooled-parse / genuine-leaf.
- **Access requests** (most): `List` (`ToViewSource` is a `From()`-wrap over the stored view), `GET …/{id}`
  (`ToView` wrap), and `approve`/`approve-as-eligible`/`deny`/`withdraw`/`revoke` (decision carried via the
  draft seam already landed in Part A) are genuine.

**Fixes (⬜ FIX) — record/list/string seams still to convert (queued rows):**

1. **Credentials `ToSummary`** (`GET /credentials`, `GET /credentials/{s}/{e}`). Per-binding summary built
   field-by-field; **whole-doc `From()` impossible** — verified non-congruent (summary requires the derived
   `credentialStatus`, and exposes inverse-mapped `usageGrants` for the hidden internal `usageTags`). **DONE — see
   ✅ FIX #1 (Part D).** Fix = per-field `Models.JsonString.From(binding.X)` zero-copy element wrap (cleaner than the
   `GetUtf8String().Span` the audit guessed) for the directly-copied scalars + `secretRefs`/`config`, with
   `credentialStatus`/`usageGrants` as kept floors. *Lifetime learning:* a per-field `From()` makes the builder
   **reference** the source doc, so the binding must be `TakeOwnership`/`TransferOwnershipTo`-handed to the workspace
   (the deferred `ValidateBody` reads it) — a `(string)`/span `Source` is copied and would not.
2. **Security `ToRuleSource`** (`GET /security/rules/{n}`; also the `POST`/`PUT` rule **response** projection). The
   summary schema is byte-identical to the stored rule, so this is a whole-doc `Models.SecurityRuleSummary.From(r)` wrap —
   eliminates the per-rule field copy. **DONE — see ✅ FIX #2 (Part D).** *Refinement found during implementation:* the
   `GET /security/rules` **list** cannot use `From()` (its items reference a pooled batch freed before the array
   serialises), so only the three single-document responses were convertible; the list row is a genuine materialisation
   floor (re-classified ✅ genuine, not a fix).
3. **Security `ToBindingSource`** (`GET /security/bindings`, `GET /security/bindings/{id}`). Keep the builder
   (the stored binding carries `scopes`/`expiresAt`/`eligibleOnly` the open summary must **not** leak), but
   carry the scalars via `Models.JsonString.From` and the verb grants via `Models.VerbGrant.From(binding.Read/.Write/.Purge)`
   instead of rebuilding them. **DONE — see ✅ FIX #3 (Part D)** (`ToGrantSource` deleted; `TakeOwnership`/`TransferOwnershipTo`
   per the FIX #1 lifetime rule; non-leak preserved by per-field selection; 392→0 B convertible).
4. **Security `ReadBinding` → `List<string>`** (`POST /security/bindings`, `PUT /security/bindings/{id}`).
   The handler reads the request body's rule names into a `List<string>` before the store `Draft()`. Fix =
   `SecurityBindingDocument.From(parameters.Body)` carrying the rule-name array bytes-to-bytes; the store's
   `BuildNew` must default the missing verb grants to `None`. **DONE — see ✅ FIX #4 (Part D)**: `From(body)` turned out
   to be a *free zero-copy view* (not a pooled draft) and the store's selective field-reading drops any extra body props,
   so no extra-prop leak; `BuildNew`+`ApplyUpdate` default omitted verbs→None/order→0; 1184→0 B (~86× faster).
5. **Access-requests Submit `List<string>` scopes** (`POST /accessRequests`). The submit path materialises
   `requestedScopes` into a `List<string>` before `AccessRequest.Draft()`. Fix = an `AccessRequest.Draft()`
   overload carrying the `requestedScopes` CTJ array bytes-to-bytes. **DONE — see ✅ FIX #5 (Part D)** (also carries
   `baseWorkflowId`/`reason` as JsonElement; 520→152 B, −71%; residual is the irreducible pooled draft).
6. **Catalog PATCH/DELETE redundant fetch** (`PATCH …/versions/{n}`, and `DELETE …/versions/{n}`). The
   write did **2× `GetAsync`** (parse the whole version) purely for the 403/404 access-check split. **DONE — see
   ✅ FIX #6 (Part D)**: the read-vs-write reach distinction is surfaced by the client (`CatalogUpdateResult` +
   `CatalogDeleteOutcome.Forbidden`, a read-then-write resolution on one fetch), so the handler pre-fetch is dropped.
   Security-neutral relocation (read-before-write preserved). Smallest GC win (−0.34 KB; the pre-fetch parse was pooled,
   its read the §13.4.1 leaf) — the payoff is the eliminated fetch+parse round-trip. *Design decision confirmed with the
   user before coding (security analysis: byte-identical relocation to the single authoritative reach layer).*

**Sub-floor caveats (genuine, but a residual noted — not a queued fix):**

- **`whoami`/`capabilities`** capture small closures while building the identity/kind arrays. Genuine (the
  arrays are the response), but the closures are a sub-floor residual; `grantees` is the closure-free
  template if a future pass wants to drop them. Not worth a row on its own.
- **`DescribeUsageScope`** (administrators) mints per-admin scope strings. It is a **shared policy-seam leaf**
  (the scope description has no bytes-to-bytes JSON inverse — it is computed prose), so there is nothing to
  carry; recorded as a known floor, not a convertible seam. [[frequency-is-not-a-licence]] respected: this is
  a *shape* verdict (no inverse exists), not a frequency excuse.

### ✅ FIX #2 — Security `ToRuleSource` → `From()` (single-document rule responses)

The first Part B fix. `ToRuleSource` rebuilt a `SecurityRuleSummary` field-by-field (name, expression, description,
createdBy, createdAt, lastUpdatedBy, lastUpdatedAt, etag), realising a managed value per scalar into the result-builder
arena. The summary is a **congruent** projection of the stored rule — schemas verified identical property names/types and
the same required set (`name`, `expression`, `createdBy`, `createdAt`, `etag`); the summary is merely more permissive on
`additionalProperties` — so a valid stored rule is automatically valid as a summary and the projection collapses to a
`Models.SecurityRuleSummary.From(doc)` pointer-reinterpret (the cross-assembly `From<T>` bridge; `SecurityRuleDocument`
*is* a Corvus.Text.Json value — [[v5-no-base-jsonstring-per-root-identity]]). The catalog handler's
`CatalogVersionSummary.From` + `workspace.TakeOwnership` is the template.

- **Scope — single-document responses only.** `GET /security/rules/{name}`, the `POST /security/rules` response, and the
  `PUT /security/rules/{name}` response now wrap the stored element with `From()` and hand the pooled
  `ParsedJsonDocument<SecurityRuleDocument>` to the workspace (`TakeOwnership`) so it lives until the response is written
  (the `using` dispose-at-method-exit is gone; for Create/Update ownership transfers **before** `RefreshAsync` so a refresh
  failure cannot leak the document).
- **`GET /security/rules` (the list) stays materialised — and that floor is genuine, not a missed fix.** Its items come
  from a `PooledDocumentList` disposed when the handler returns, so a `From()` wrap (a reference into the pooled buffers)
  would be read after free when the array serialises ([[ctj-handler-response-projection]]). `ToRuleSource` is retained for
  exactly this path.
- **Measured** (`SecurityRuleSummaryProjectionBenchmarks`, projection in isolation, ShortRun/MemoryDiagnoser, same run; a
  fully-populated rule = all 8 fields). `Materialize_fieldByField` (baseline — field-copy) **264 B / 1399.6 ns** →
  `ElementWrap_From` **0 B / 518.9 ns** (−264 B, **−100%**; ~2.7× faster). 264 B is a conservative lower bound — the real
  handler also ran the generated result builder on top of the field-copy.
- **Verified.** `ControlPlaneSecurityApiTests` (handler response-body assertions) green; Sqlite `SecurityPolicyStore`
  conformance 7/7; slnx build **0 Warning(s), 0 Error(s)**. **Row done.**

### ✅ FIX #1 — Credentials `ToSummary` per-field bytes bridge (448→0 B convertible)

The second Part B fix. Unlike the rule summary, `CredentialBindingSummary` is **not** congruent with the stored
`SourceCredentialBinding` — verified: the summary *requires* `credentialStatus` (derived from `expiresAt` vs now, never
persisted) and exposes operator-facing `usageGrants` where the stored doc has internal `usageTags` (the raw tags are
deliberately hidden). So a whole-doc `From()` is impossible and `ToSummary` must field-copy. The fix replaces the
per-field `(string)binding.X` managed-string realisations with `Models.JsonString.From(binding.X)` — the per-field analog
of FIX #2's whole-doc wrap (`Durability.JsonString` is a `struct : IJsonElement<T>`, so `From<T>` is a zero-copy element
wrap → implicit `Models.JsonString.Source`).

- **Converted** (the directly-copied leaves): scalars `id`/`sourceName`/`environment`/`createdBy`/`etag` +
  optional `description`/`lastUpdatedBy`, and the `secretRefs` (name/ref) and `config` (key/value) arrays.
- **Genuine floors kept:** `credentialStatus` (derived token), `usageGrants` (`access.DescribeUsageScope` — the
  inverse-mapped policy-seam leaf, same caveat as the administrators audit). **Follow-ups (not this row):**
  `managementTags` (a raw-array `binding.ManagementTags` bridge — needs an ordering check vs the `SecurityTagSet`) and the
  `ToSummary` lambda **closure** (a `Source<TContext>` refactor) — left so this row's before→after stays the clean
  field-bridge delta.
- **Lifetime fix (the subtle part).** A per-field `From()` makes the result builder hold a **reference** into the
  binding's pooled document (not a copy), and the response body is validated/serialised **after** the handler returns —
  so the binding document must outlive the handler. The first attempt (`From()` under the existing `using`-dispose) threw
  `ObjectDisposedException` in the deferred `ValidateBody`. Fixed exactly as FIX #2 / the catalog list: the 3 single-doc
  sites `workspace.TakeOwnership(binding)` (no `using`), and the list `page.Bindings.TransferOwnershipTo(workspace)`.
  (A `(string)`/span `Source` is *copied* by the builder and would not need this — the element `From()` is what
  references the source; recorded for the campaign.)
- **Measured** (`CredentialBindingSummaryProjectionBenchmarks`, the convertible fields only — 5 scalars + 1 secretRef +
  1 config entry; the two floors are identical in both arms and excluded). `Materialize_fieldByField` (baseline —
  `(string)`) **448 B / 3.02 µs** → `BytesBridge_utf8` **0 B / 1.32 µs** (−448 B, **−100%**, ~2.3× faster); scales with
  secretRef/config count. 448 B is a conservative lower bound (the real handler also runs the generated result builder).
- **Verified.** `ControlPlaneCredentialsApiTests` 10/10 (full create/get/list/update/delete lifecycle + management-tag/
  usage-grant assertions); Sqlite `SourceCredentialStore` conformance 13/13; slnx build **0 Warning(s), 0 Error(s)**.
  **Row done.**

### ✅ FIX #3 — Security `ToBindingSource` bytes-bridge (392→0 B convertible)

The third Part B fix. A whole-doc `From()` is impossible — the stored binding carries
`scopes`/`expiresAt`/`eligibleOnly` that the **open** summary must not leak (summary `additionalProperties` is
permissive, so a verbatim wrap would carry them through) — so `ToBindingSource` field-selects a subset. Each selected leaf
is now carried bytes-native:

- **Scalars** (`id`/`claimType`/`createdBy`/`etag` + optional `claimValue`/`description`/`lastUpdatedBy`) →
  `Models.JsonString.From(binding.X)` (the FIX #1 element wrap).
- **Verb grants** (`read`/`write`/`purge`) → `Models.VerbGrant.From(binding.Read/.Write/.Purge)`, replacing the
  `ToGrantSource` rebuild (which realised a managed string per rule name). Verified the stored `VerbGrantInfo` is congruent
  with the summary `VerbGrant` (same optional `unrestricted`/`ruleNames`), and `VerbGrantInfo.Rules`/`None`/`Full` always
  carry `unrestricted` — so `From()` reproduces exactly what `ToGrantSource` emitted (same fields/values).
  `ToGrantSource` is **deleted** (was used only here; the input inverse `ToGrant` stays).
- **Lifetime** (the FIX #1 rule): per-field `From()` makes the builder reference the binding doc → the 3 single-doc sites
  `workspace.TakeOwnership(binding)` (Create/Update transfer before `RefreshAsync`) and the list
  `bindings.TransferOwnershipTo(workspace)`. `order`/`createdAt`/`lastUpdatedAt` stay value-type accessors (no string).
- **Non-leak preserved:** per-field selection keeps `scopes`/`expiresAt`/`eligibleOnly` out — verified by
  `ControlPlaneSecurityApiTests` (the binding test asserts the open summary does not carry them).
- **Measured** (`SecurityBindingSummaryProjectionBenchmarks`, convertible fields — 6 scalars + 3 verb grants incl. a
  2-rule read; `order`/dates identical in both arms). `Materialize_fieldByField` (baseline — `(string)` + grant rebuild)
  **392 B / 2.58 µs** → `BytesNative_From` **0 B / 2.06 µs** (−392 B, **−100%**, ~1.25× faster); 392 B is a conservative
  lower bound (the real handler also runs the generated result builder).
- **Verified.** `ControlPlaneSecurityApiTests` 5/5; Sqlite `SecurityPolicyStore` conformance 7/7; slnx build
  **0 Warning(s), 0 Error(s)**. **Row done.**

### ✅ FIX #5 — Access-request Submit bytes-native `Draft` (520→152 B)

The fourth Part B fix, and the first **write-side** one (a draft build, not a response projection). The Submit handler
read the body's `requestedScopes` into a `List<string>` (a transcode per scope) and realised `baseWorkflowId`/`reason`
via `(string)` before `AccessRequest.Draft` rebuilt them — a bytes→string→bytes u-turn. Added a bytes-native
`AccessRequest.Draft(in JsonElement baseWorkflowId, in JsonElement requestedScopes, …, in JsonElement reason, …)` overload
(mirroring `SourceCredentialBinding.Draft(JsonElement…)`): the body-sourced fields are carried verbatim (the whole
`requestedScopes` array copied with `WriteTo`, no list), while the principal-derived `subject`/`subjectClaimType`/
`requesterLabel` stay the strings they already are. The string-list overload is kept for cold/programmatic callers + tests.

- **Lifetime:** none of the new concern — the draft is built into a fresh pooled document (`ToPooledDocument`); the
  `JsonElement`s are read synchronously during that build (the body is alive), then the draft is independent. The
  `created` response doc already gets `workspace.TakeOwnership` (unchanged).
- **Measured** (`AccessRequestDraftBenchmarks`, parsed body → draft → dispose, mirroring the handler).
  `Draft_FromStrings` (baseline — `List<string>` + `(string)`) **520 B / 1.19 µs** → `Draft_FromElements` **152 B /
  1.14 µs** (−368 B, **−71%**). The residual **152 B is the pooled draft document itself** (present in both arms — the
  irreducible seam the draft carries); the eliminated 368 B = the `List<string>` + scope strings + `baseWorkflowId`/`reason`.
- **Verified.** `ControlPlaneAccessRequestsApiTests` 5/5; Sqlite `AccessRequestStore` conformance green; slnx build
  **0 Warning(s), 0 Error(s)**. **Row done.**

### ✅ FIX #4 — Security binding write seam: `ReadBinding` → `From(body)` (1184→0 B)

The fifth Part B fix, and the largest per-op win. `ReadBinding` converted each verb grant `Models.VerbGrant →
VerbGrantInfo` (a `List<string>` of rule names + `VerbGrantInfo.Rules` rebuild through a pooled workspace/builder/clone),
realised the scalars via `(string)`, and built a fresh pooled draft via `SecurityBindingDocument.Draft`. Replaced the whole
thing with `SecurityBindingDocument.From(body)` — a **free, zero-copy element view** over the already-parsed request body
(no grant rebuild, no per-field strings, **no pooled draft document at all**); the store reads the view directly.

- **Store defaulting (correctness).** The write body's `read`/`write`/`purge`/`order` are optional but the stored document
  requires them, so `BuildNew`/`ApplyUpdate` now default an omitted verb grant to `None` and an omitted order to 0 — the
  exact semantics the handler's grant conversion used to apply, moved into the store.
- **No extra-property leak.** `BuildNew`/`ApplyUpdate` build the stored document field-by-field from the *known*
  accessors (`CreateBuilder(claimType:…, read:…, …)` / `SetX(draft.X)`), not by copying the draft wholesale — so any
  extra top-level property a (permissive `additionalProperties`) write body might carry is never read, never stored.
  Grant-*internal* extras are carried verbatim (inherent to any bytes-to-bytes carry, including the existing credential/
  access-request draft seams) and are harmless — grant enforcement uses only `unrestricted`/`ruleNames`.
- **Lifetime:** `From(body)` views `parameters.Body` (alive for the request); the store reads it synchronously in
  `BuildNew`/`ApplyUpdate` + serialize (inside `AddBindingAsync`/`UpdateBindingAsync`), so there is no pooled draft and
  nothing to dispose — `ReadBinding`'s out type became `SecurityBindingDocument` and the handler's `using (draft)`
  wrappers were removed. The dead `ToGrant`/`OptionalString` helpers and the `DurabilityVerbGrant` alias were deleted.
- **Measured** (`SecurityBindingReadBenchmarks`, parsed body → draft, rule-named read grant). `ReadBinding_FromGrants`
  (baseline) **1184 B / 3,781 ns** → `ReadBinding_FromBody` **0 B / 44 ns** (−1184 B, **−100%**, ~86× faster). The 1184 B
  = the `List<string>` + rule-name strings + the `VerbGrantInfo.Rules` workspace/builder/clone + claimType/claimValue/
  description strings + the pooled draft document — all gone (the body is already parsed; `From` is a pure view).
- **Verified.** `ControlPlaneSecurityApiTests` 5/5 (binding create/update incl. omitted grants); Sqlite
  `SecurityPolicyStore` conformance 7/7; slnx build **0 Warning(s), 0 Error(s)**. **Row done.**

### ✅ FIX #6 — Catalog PATCH/DELETE: 403/404 split folded into the client (2.56→2.22 KB)

The sixth and final Part B fix, and the smallest GC win (as forecast at the design gate). The handler pre-fetched the
version (`GetAsync` + parse + `Admits`) purely to split **403** (readable but not writable) from **404** (not readable),
and then the reach-aware `SecuredWorkflowCatalog` fetched it *again* for the actual reach-gated mutation — the version was
fetched+parsed **twice** per PATCH/DELETE. The fix moves the 403/404 split into the client (which already has the fetched
version), so the handler drops its pre-fetch.

- **Client change** (`SecuredWorkflowCatalog` + `ISecuredWorkflowCatalog`): `UpdateAsync` now returns a `CatalogUpdateResult`
  (`CatalogUpdateOutcome {Updated, NotFound, Forbidden}` + the updated doc on `Updated`); `CatalogDeleteOutcome` gains
  `Forbidden`. A new `ResolveWriteReachAsync` resolves **read-then-write** on a single fetch: outside read reach (or
  absent) → `NotFound`, readable but outside write reach → `Forbidden`, else writable. The handler maps the outcome →
  200/403/404 (DELETE also keeps 409 Referenced) and `TakeOwnership`s the returned doc on `Updated`.
- **Security (byte-identical relocation).** The decision logic is unchanged — only its location moves from the handler to
  the single authoritative reach-aware layer. The one correctness burden is the **read-before-write ordering** (a caller
  who cannot read must get 404, never a 403 that leaks existence); `ResolveWriteReachAsync` / `DeleteAsync` check
  `Admits(Read)` first. The `UpdateAsync` System/unrestricted (`Reach(Write) is null`) no-fetch short-circuit is preserved
  (existence falls to the mutation); `DeleteAsync` already fetched (it needs the `WorkflowId` for the reference check).
  Verified by `ControlPlaneCatalogApiTests` (the 403 + non-disclosing 404 cases).
- **Measured** (`CatalogVersionWriteReachBenchmarks`, the redundant pre-fetch, InMemory, `AccessContext.System`).
  `Update_WithPrefetch` (baseline) **2.56 KB / ~57 µs** → `Update_NoPrefetch` **2.22 KB / ~12 µs** (−0.34 KB, **−13%**;
  ~5× faster wall-clock, noisy). The GC delta is modest **by design** — the pre-fetch's parse is pooled (ArrayPool) and
  its read array is the §13.4.1 driver leaf; the real payoff is the eliminated fetch+parse round-trip. The residual
  2.22 KB is the actual `UpdateMetadataAsync` rebuild (read every field of the current version → re-serialise a fresh
  `updatedDoc` byte[]) — a *separate* seam, the PATCH row's deeper "carry patch draft / mutable builder"
  ([[corvus-mutable-documents]]) lever, out of scope here.
- **Verified.** `ControlPlaneCatalogApiTests` 11/11; Sqlite `WorkflowCatalogStore` conformance green; slnx build
  **0 Warning(s), 0 Error(s)**. Contained to client + interface + handler (+ a `DemoData` dispose for the changed return).
  **Row done.**

### ✅ Catalog metadata UPDATE — mutable-builder in-place patch (+ a latent securityTags-drop fix)

The deeper PATCH-row lever (the FIX #6 residual). The **document-blob** catalog stores implemented `UpdateMetadataAsync`
by reading the current version, realising **every** field as a managed value, and rebuilding the whole document via
`CatalogVersion.CreateBytes`/`Create` — a read-realise-rebuild. Replaced with a shared mutable-builder helper,
`CatalogVersion.CreatePatchedBytes(in CatalogVersion current, in CatalogMetadataPatch patch, DateTimeOffset now)`
([[corvus-mutable-documents]]): it opens a `JsonDocumentBuilder<CatalogVersion.Mutable>` over the current document and
`SetXxx` **only** the changed governance fields (`lastUpdatedBy`/`lastUpdatedAt` always; `status` when changed, with the
obsolete/reactivate transition on `obsoletedBy`/`obsoletedAt`; `owner`/`tags` when the patch replaces them) — every other
field (title, hash, sources, created*, **securityTags**, …) is carried bytes-to-bytes, no per-field string realisation.

- **Latent security bug fixed.** On InMemory/Redis/NATS the old `CreateBytes` call **omitted `securityTags`**, so every
  metadata PATCH silently **stripped the version's row-security tags** (they live inside the persisted document and gate
  §14.2 single-row read authorization — obsoleting/transferring a version would have widened or broken its reach). The
  mutable-builder carries them. It was untested; added `UpdateMetadata_preserves_the_versions_security_tags` to the
  catalog conformance base. (Cosmos already passed `securityTags` — no bug there, perf only.)
- **Hardening (defence-in-depth).** `securityTags` are server-stamped at creation and **immutable** through PATCH — the
  store/API patch types have no `securityTags` field. Closed the API `CatalogMetadataPatch` schema
  (`additionalProperties: false`, regenerated the server) so an *injected* `securityTags` (or any unknown field) is
  rejected with **400** at the schema boundary rather than silently ignored; added an API test
  (`Patching_a_version_cannot_set_securityTags_rejected_at_the_boundary`) asserting the 400 and that the original tags
  are unchanged.
- **Scope.** Converted the doc-blob rebuild stores: **InMemory, Redis, NATS** (perf + the securityTags fix) and **Cosmos**
  (perf; it parses the patched bytes back into its envelope). **AzureStorage** is column-style (`WriteGovernance` sets
  specific table-entity properties on the read entity, Replace-mode — preserves securityTags) and the 5 relational/Mongo
  stores do targeted in-DB `UPDATE`/`$set`; all six need **no change** (confirmed genuine by the new test).
- **Measured** (`CatalogVersionWriteReachBenchmarks.Update_NoPrefetch`, InMemory): **2.22 KB → 1.41 KB (−0.81 KB, −36%)** —
  the unchanged-field realisations are gone, and the patched document is now *correct* (carries securityTags) yet smaller.
  The residual is the serialised `updatedDoc` byte[] (the persisted form) + the pooled wrappers.
- **Verified.** InMemory **18/18** + Sqlite **18/18** in-process; **all 8 container backends 18/18** (Cosmos/Redis/NATS
  changed + Postgres/SqlServer/MySql/Mongo/AzureStorage unchanged, via the live podman socket), each incl. the new
  securityTags-on-update test; slnx build **0 Warning(s), 0 Error(s)**. **Row done.**

### ✅ Credentials `ToSummary` — closure → `Source<TContext>` context-threading (+ the `XxxOrNull`→`From()` convention)

The FIX #1 deferred follow-up (it explicitly left "the `ToSummary` lambda **closure** (a `Source<TContext>` refactor)"
so that row's before→after stayed the clean field-bridge delta). `ToSummary`/`ToList` built the result with **capturing
lambdas** — an outer list builder plus four nested per-item array builders (`secretRefs`/`config`/`managementTags`/
`usageGrants`), each closing over `binding`/`access`/the loop state, so a 10-binding list page heap-allocated the outer
closure + ~4 closures per item. Replaced with the closure-free pattern ([[corvus-builder-context-threading]]):
`RefTuple`-style ref-struct contexts (`SummaryContext`/`ListContext`, `allows ref struct`) carry the binding + access
scope, and **static** build methods (`BuildSummary`/`BuildSecretRefs`/`BuildConfig`/`BuildManagementTags`/
`BuildUsageGrants`) are handed to `Build(in ctx, action)` — no captured state, no per-item delegate allocation. Precedent:
the `ArazzoControlPlaneIdentityHandler` grantee projection.

- **Convention cleanup (the `XxxOrNull`→`From()` fix).** The optional scalars (`description`/`lastUpdatedBy`/`expiresAt`/
  `lastUpdatedAt`/`rotatedAt`) were projected with `binding.XxxOrNull is {} x ? x : default` /
  `binding.XxxValue is {} x ? x : default` ternaries. That is the wrong idiom under the "Undefined not null" convention:
  the nullable accessors **discard** Undefined-ness (collapsing to `DateTimeOffset?`/`string?`), so the ternary's
  `default` branch writes a real `default(DateTimeOffset)` (the **epoch**) instead of omitting the field — a latent bug
  (caught by `A_binding_without_an_expiry_is_valid_and_omits_expiresAt`). Replaced every one with a bare
  `Models.Json{String,DateTime}.From(binding.RawAccessor)`: `From()` **propagates Undefined**, so an absent field's
  `Source` is Undefined and the builder omits it — correct *and* no ternary. (Recorded in
  [[getmaxbytecount-for-scratch-buffers]]; the same `XxxOrNull`-ternary smell still sits in FIX #3's `ToBindingSource` —
  a follow-up to convert there too.)
- **Lifetime.** Unchanged from FIX #1 — the per-field `From()` Sources still reference the binding's pooled document, so
  the single-doc sites keep `workspace.TakeOwnership(binding)` and the list keeps `page.Bindings.TransferOwnershipTo`.
- **Measured** (`CredentialSummaryProjectionBenchmarks`, a 10-binding list page — the whole projection incl. the result
  builder, not just the convertible fields): `PerItemClosure` (baseline) **30.13 KB** → `ContextThreaded`
  **22.98 KB** (−7.15 KB, **−24%**). The residual is irreducible: the `From` element Sources, the `DescribeUsageScope`
  usage-grant lists (the policy-seam leaf), and the serialised response document.
- **Verified.** `ControlPlaneCredentialsApiTests` 10/10; Sqlite `SourceCredentialStore` conformance 13/13; slnx build
  **0 Warning(s), 0 Error(s)**. **Row done.**

### ✅ Security `ToBindingSource` — closure → `Source<TContext>` context-threading (+ the `XxxOrNull`→`From()` convention)

The credentials `ToSummary` follow-up applied to the security binding summary (the FIX #3 sibling — FIX #3 carried the
scalars bytes-native; this removes the closures + the residual ternaries). `ToBindingSource`/`ToBindingList` built the
response with capturing lambdas — an outer list closure, the nested array closure and a per-binding summary closure.
Replaced with the closure-free pattern ([[corvus-builder-context-threading]]): the binding itself is threaded as the
context (it is a `struct : IJsonElement`) through the static `BuildBindingSummary`, and the list is built **inline** in
`HandleListSecurityBindingsAsync` through the static `BuildBindingSummaries` (the `SecurityBindingList.Build` result is
ref-scoped to its `in` argument, so — like the credentials list — it cannot be returned from a helper).

- **Convention cleanup (the `XxxOrNull`→`From()` fix).** The optional `lastUpdatedAt` was projected via
  `binding.UpdatedAtValue is { } ua ? …` — the nullable accessor (`DateTimeOffset?`) that **discards** Undefined-ness, so
  an absent value writes the **epoch** instead of omitting the field (the same latent bug the credentials row fixed). The
  optional strings used the redundant `if (binding.X.IsNotUndefined()) { … = From(X); }` guard. Replaced every one with a
  bare `Models.Json{String,DateTime}.From(binding.RawAccessor)`: `From()` propagates Undefined so the builder omits absent
  fields — correct *and* no ternary/guard/local. ([[getmaxbytecount-for-scratch-buffers]].)
- **Lifetime.** Unchanged — the per-field `From()` Sources reference the pooled binding documents, so
  `HandleListSecurityBindingsAsync` keeps `bindings.TransferOwnershipTo(workspace)` and the single-document sites keep
  their `workspace` ownership.
- **Measured** (`SecurityBindingListProjectionBenchmarks`, a 10-binding list page — the whole projection incl. the result
  builder): `PerItemClosure` (baseline) **17.24 KB** → `ContextThreaded` **16.21 KB** (−1.03 KB, **−6%**). A smaller delta
  than the credentials summary (−24%) — the binding summary has no nested arrays, only the three `VerbGrant` whole-doc
  wraps + scalars, so the only closures removed are the list/array/per-item trio (N+2 for an N-page). The residual is the
  irreducible `From`/`VerbGrant.From` Sources + the serialised response document. (FIX #3's string-bridge delta is
  measured separately in `SecurityBindingSummaryProjectionBenchmarks`.)
- **Verified.** `ControlPlaneSecurityApiTests` + `ControlPlaneRowSecurityTests` 14/14; slnx build **0 Warning(s),
  0 Error(s)**. **Row done.**
- **Scan (`XxxOrNull`/`XxxValue`-into-CTJ-builder smell, Arazzo non-generated source).** Two further true instances, both
  response projections: `ArazzoControlPlaneSecurityHandler.ToRuleSource` (the rule summary — identical shape) and
  `ArazzoControlPlaneAccessRequestsHandler.ToViewSource` (the access-request list view — 8 optional ternaries incl. two
  epoch-risk dates; **also a whole-document `From()` collapse candidate** since its single-document sibling `ToView`
  already wraps the congruent `AccessRequestView.From`). All other hits are **not** the smell: the nine
  `*AccessRequestStore` query-filter predicates, the `ExpiresAtValue …<= now` expiry comparisons, the Cosmos/AzureStorage
  catalog envelope writers (a different shape — unix-millis dates, table columns) and the CLI `string?`→`Source` settings
  bridge (no CTJ Undefined).

### ✅ Whole-document `From()` list collapse — `ToRuleSource` + `ToViewSource` (the scan's two true smells)

The scan (above) surfaced two field-copy list projections whose **single-document siblings already respond with a
whole-document `From()` wrap** — the tell that the summary is *congruent* with the stored document and the list can wrap
the same way (the binding genuinely can't — its summary hides `scopes`/`expiresAt`/`eligibleOnly`, so it stays
field-copy). Both list bodies collapsed to per-item whole-document `From()` + ownership transfer, deleting the field-copy
helper, its closures, and (for the access request) the `requestedScopes` array rebuild outright.

- **`ArazzoControlPlaneSecurityHandler.ToRuleSource` → `Models.SecurityRuleSummary.From(r)`.** The create/get/update sites
  already used the whole-document `SecurityRuleSummary.From`; `ToRuleList` now does the same per item, built closure-free
  (the rule list threaded as the context through the static `BuildRuleSummaries`) and inlined in
  `HandleListSecurityRulesAsync` with `rules.TransferOwnershipTo(workspace)`.
- **`ArazzoControlPlaneAccessRequestsHandler.ToViewSource` → `Models.AccessRequestView.From(request)`.** The single-document
  `ToView` already wrapped the congruent view; the list field-copied only because of a **stale comment** asserting "a
  whole-document `From()` wrap cannot be used here" — true under the *old* "batch disposed at handler return" assumption,
  invalidated by the ownership-transfer pattern. Collapsed to the whole-document wrap, closure-free
  (`BuildAccessRequestViews`) + inlined with `list.TransferOwnershipTo(workspace)`; the 8 optional `XxxOrNull`/`XxxValue`
  ternaries (two epoch-risk dates) and the scopes rebuild are gone.
- **Measured** (the existing per-item projection benchmarks, which isolate exactly the field-copy→`From()` delta each list
  now applies per item): `AccessRequestViewProjectionBenchmarks` `Materialize_fieldByField` **664 B** → `ElementWrap_From`
  **0 B** (−100%, ~3.6× faster); `SecurityRuleSummaryProjectionBenchmarks` **264 B** → **0 B** (−100%, ~2.9× faster). The
  list saves that **per item** (× page size) **plus** the per-item/array/list closures the collapse removes — strictly
  larger than the credentials/binding closure-only refactors, which couldn't collapse (non-congruent summaries).
- **Verified.** `ControlPlaneAccessRequestsApiTests` + `AccessRequestApprovalServiceTests` + `ControlPlaneSecurityApiTests`
  + `ControlPlaneRowSecurityTests` **34/34**; slnx build **0 Warning(s), 0 Error(s)**. **Rows done.**
- **Process.** Why the scan, not the original Part B sweep, caught these — and the skill/process fixes — is recorded in
  the retrospective (skills `corvus-ctj-handler-implementation` §"Response projection decision order" + the protocol's
  up-front anti-pattern sweep). The root causes: the closures were *deferred* in FIX #1 to keep a "clean" before→after and
  never reticketed; the `XxxOrNull`-ternary was never a named/grepped pattern; and the collapse was mis-ruled-out by a
  stale "cannot use `From` here" comment trusted instead of re-derived ([[verify-before-declaring-impossible]],
  [[dont-anchor-on-existing-bad-code]]).

### ✅ Remaining server closure list-projections — Runners, Identity (`WhoAmI` + Capabilities), Administrators

The up-front closure sweep (now dogfooding the codified process) cleared the last server-side closure-based list
projections. Each was a capturing builder lambda (outer list + nested array + sometimes a per-item closure); all are now
the closure-free `Build<TContext>` form. (The sweep also exposed — and fixed — a gap in the *codified grep*: the original
`new Models\.…Source((ref` pattern missed the `=> new((ref` shorthand, so Runners/`BuildPage` weren't flagged; the grep
is now the builder-lambda signature `((ref …Builder )`.)

- **Runners `BuildPage`** — already a per-item whole-document `Models.Runner.From(runner)` (congruent re-wrap); just the
  outer+array closures removed, threaded through the static `BuildRunners`, inlined in `HandleListRunnersAsync`. (No
  ownership transfer — `RunnerRegistration` is GC-backed, not a pooled batch.)
- **Identity `WhoAmI` + Capabilities** — synthesized lists (`CredentialUsageGrant` / `GranteeKind`); outer+array closures
  removed (static `BuildWhoAmIIdentity` / `BuildGranteeKinds`, inlined). The shared `ToIdentity` was itself a per-grant
  closure — converted to a context-threaded single-item helper (`Build(in grant, …)`, which *can* be returned, unlike a
  list Build). That also fixes the **grantee-search** path (`HandleSearchGranteesAsync`), which appended `ToIdentity(grant)`
  per grant: the handler now matches the benchmarked closure-free ideal (`GranteeProjectionBenchmarks.Target_OkContextThreaded`
  **2.01 KB** — the handler previously paid an extra per-grant closure the Target arm never modelled).
- **Administrators `ToList`** — synthesized (`SecurityTagSet` → `DescribeUsageScope` → `AdministratorIdentity`); the
  `DescribeUsageScope` lists are the genuine inverse-map leaf, so only the closures are removable. Worst fix-shape of the
  four: the list `Build<TContext>` is ref-scoped, so it's **inlined at all four call sites** (list/add/transfer/remove)
  with an `AdministratorListContext` carrying the set + access; `ToIdentity` context-threaded as above.
- **Measured.** These are the identical closure-removal mechanism already benchmarked — the directly comparable deltas are
  the security-binding list (`SecurityBindingListProjectionBenchmarks` 17.24→16.21 KB, the outer+array+per-item closure
  trio) and the grantee Target ideal (2.01 KB) the `ToIdentity` fix now brings the live handler to. Not separately
  micro-benchmarked (same mechanism, smaller/cold lists; a per-row arm would re-prove the measured pattern). The removed
  cost is the closure count per request: Runners/Capabilities 2; `WhoAmI`/Administrators 2 + N per-grant; grantee N per-grant.
- **Verified.** `ControlPlaneIdentityApiTests` + `ControlPlaneAdministratorsApiTests` **20/20** (incl. the grantee-search
  assertions), Runners list **2/2**; slnx build **0 Warning(s), 0 Error(s)**. **Rows done.** Server-side projection
  closure sweep is now clean (remaining `((ref …Builder)` hits are single-object responses — `ProblemDetails`,
  `PurgeResult`, `WorkflowRunAccepted`, the empty-list cached delegate — and cold CLI request-building).

### ✅ Cumulative closeout — full re-run, no regressions, next target identified

The campaign's headline per-domain benchmarks re-run together (InMemory, the shipped arms) to confirm the cumulative
result and check for drift. **Every shipped arm matches its recorded Part D number — no regression introduced by any later
row.** (Run: the 14 domain benchmarks below, exit 0.)

| Domain / op | Benchmark (arm) | Campaign start | Current | Δ |
|---|---|---|---|---|
| Catalog `POST` (package project + store) | `CatalogStoreBenchmarks.Add_FromRecord` | 19.92 KB | **3.72 KB** | −81% |
| Catalog search (page-token) | `CatalogStoreBenchmarks.Search_Page` | 2.58 KB | **2.55 KB** | −1% |
| Catalog `PATCH` (write-reach + mutable patch) | `CatalogVersionWriteReachBenchmarks.Update_NoPrefetch` | 2.22 KB | **1.41 KB** | −36% |
| Run query (page-token + capped buffer) | `WorkflowStateStoreBenchmarks.Query_Page` | 19.93 KB | **1.72 KB** | −91% |
| Observed-identity write (`SeenAsync`) | `ObservedIdentityStoreBenchmarks.Serialize_Pooled` | 376 B | **56 B** | −85% |
| Observed-identity search (page-token) | `ObservedIdentityStoreBenchmarks.Search_Page` | 2.03 KB | **1.98 KB** | −2% |
| Grantee search (closure-free `Ok<TContext>`) | `GranteeProjectionBenchmarks.Target_OkContextThreaded` | 5.88 KB | **2.01 KB** | −66% |
| Credentials list projection (10) | `CredentialSummaryProjectionBenchmarks.ContextThreaded` | 30.13 KB | **22.98 KB** | −24% |
| Security-binding list projection (10) | `SecurityBindingListProjectionBenchmarks.ContextThreaded` | 17.24 KB | **16.21 KB** | −6% |
| Access-request view (per item) | `AccessRequestViewProjectionBenchmarks.ElementWrap_From` | 664 B | **0 B** | −100% |
| Security-rule view (per item) | `SecurityRuleSummaryProjectionBenchmarks.ElementWrap_From` | 264 B | **0 B** | −100% |

Per-domain write seams also re-confirmed at their pooled-doc floors (`AccessRequestStore.Serialize_New_Doc` 512 B,
`SecurityRuleStore.Serialize_NewRule_Doc` 512 B, `WorkflowAdministratorStore.Serialize_New_Doc` 184 B,
`ObservedIdentityStore.Serialize_Pooled` 56 B), and the continuation-token codec at 96–152 B.

**Largest remaining allocations (the next-target read-out):**
1. **Catalog `POST` 3.72 KB** — the single biggest per-request op, but at its **documented floor** (ZipArchive
   read/write + the genuine indexed-owner decomposition; the −81% writeup already proved the residual genuine). Not a
   lever without replacing `System.IO.Compression`.
2. **The list projections (credentials 22.98 KB, binding 16.21 KB for a 10-row page)** are now dominated by **genuine
   leaves**, not mechanism: per row the floor is the response document bytes + the **`ControlPlaneAccess.DescribeUsageScope`**
   inverse-map (a `List<CredentialUsageGrant>` per administrator/binding). `DescribeUsageScope` recurs across **credentials
   list, administrators, `whoami`, and grantee search** — the biggest *cross-cutting* remaining allocation, and the one
   lever that would move several rows at once.
3. The **management-tag `List<SecurityTag>` resolution seam** (noted in [[durability-alloc-campaign-followups]]).

**Recommended next lever:** ~~`DescribeUsageScope`~~ — **DONE** (see the row below; 568/728 B → 0 B per row across all
four read paths, via a `SecurityTagSet` unescaped-UTF-8 enumerator + a span-based policy override point). The remaining
open lever is the management-tag `List<SecurityTag>` resolution seam.

### ✅ `DescribeUsageScope` — the cross-cutting inverse usage-scope leaf (the closeout's recommended lever)

The closeout flagged `ControlPlaneRowSecurityPolicy.DescribeUsageScope` as the biggest *cross-cutting* remaining
allocation — the inverse usage-scope mapping (stored internal `sys:` tags → operator-facing `(dimension, value)` grants)
that runs **per row** of the credentials-list, administrators, `whoami` and grantee-search responses. The default built a
`List<CredentialUsageGrant>`, and the `SecurityTagSet` enumerator materialized a managed key **and** value string per tag,
plus a prefix-stripped substring per grant — ~3 strings/tag + the list. The credentials/observed sources paid an
*additional* `SecurityTagSet.CopyFrom` byte[] per row (re-wrapping a CTJ array that was already a model). All of it is
transient — the response document bytes are the only genuine leaf.

- **Span primitives (mirroring the forward `ResolveUsageGrantInto`).** `SecurityTagSet.EnumerateUtf8()` — a `ref struct`
  enumerator yielding each tag's key/value as **unescaped UTF-8** (the `ValueSpan`/`ValueIsEscaped`/`CopyString` idiom; a
  value carrying JSON escapes decodes into a pooled scratch the enumerator returns on `Dispose`, so it is 0-alloc in the
  common case, correct always). Policy `TryDescribeUsageGrant(ReadOnlySpan<byte> key, out ReadOnlySpan<byte> dimension)` —
  the span-based prefix strip / skip decision, the `virtual` override point (a deployment that overrides the list
  `DescribeUsageScope` overrides this too, exactly as `ResolveUsageGrants`/`ResolveUsageGrantInto`), with the prefix cached
  as UTF-8 once per instance.
- **Both source paths unified on `SecurityTagSet`, no `CopyFrom`.** CTJ-model sources (credentials `binding.UsageTags`,
  observed-identity tags) build a **non-owning** `SecurityTagSet.FromOwnedJsonArray(JsonMarshal.GetRawUtf8Value(array).Memory)`
  view over the array's raw UTF-8 (no copy) — the document already outlives the synchronous build; bare-`SecurityTagSet`
  sources (administrators store, `whoami` internal tags, grantee directory `principal.Identity`) enumerate directly. The
  four consumers thread the source + access and write each `(dimension, value)` bytes-native via `(JsonString.Source)span`
  — no list, no managed strings. The list-form `DescribeUsageScope` is kept for override-compatibility + the cold
  `CallerIdentityGrants`.
- **Measured** (`DescribeUsageScopeBenchmarks`, a 3-tag identity, the per-row cost): bare-`SecurityTagSet` path
  `Materialize_ListOfGrants` **568 B → 0 B** (`SpanEnumerated`, −100%, ~2.2× faster); credentials CTJ path (incl. the
  `CopyFrom` byte[]) `Credentials_CopyFromList` **728 B → 0 B** (`Credentials_SpanDirect`, −100%, ~2.5× faster). This is
  shed **per row** across all four read endpoints (× page size — e.g. ~7.3 KB off a 10-row credentials page). The
  existing `CredentialSummaryProjectionBenchmarks`/`GranteeProjectionBenchmarks` deliberately hold the grants constant to
  isolate closure-vs-context, so they do not move — the isolated micro is the faithful measure.
- **Correctness.** New `SecurityTagSetUtf8EnumeratorTests` (2/2) proves the span enumeration matches the managed-string
  enumerator including for escaped keys **and** values (both scratch regions).
- **Verified.** `ControlPlaneCredentialsApiTests` + `ControlPlaneIdentityApiTests` (incl. grantee search) +
  `ControlPlaneAdministratorsApiTests` + `ControlPlaneRowSecurityTests` + `ControlPlaneSecurityApiTests` **42/42**;
  enumerator **2/2**; slnx build **0 Warning(s), 0 Error(s)**.
- **Sweep follow-up — credentials `managementTags` (the same pattern).** A sweep with these learnings found the credential
  summary's `managementTags` projection paid the identical smell, and worse — `CopyFrom` **twice**: once in
  `hasManagementTags = !binding.ManagementTagsValue.IsEmpty` (a byte[] just to test emptiness) and again enumerating it as
  managed-string `SecurityTag`s. Converted to a cheap `binding.ManagementTags` length check + the non-owning
  `FromOwnedJsonArray` view + `EnumerateUtf8` + `(JsonString.Source)span` writes (verbatim `{key, value}` — no prefix
  strip, so no policy). Same already-measured `CopyFrom`+`GetString`→span mechanism (728→0 B per row, even simpler — not
  separately benchmarked); `ControlPlaneCredentialsApiTests` **10/10**, slnx **0/0**. **Row done — the projection/leaf
  allocation class is exhausted; the remaining open lever is the management-tag `List<SecurityTag>` *write/resolution*
  seam (the inbound side, distinct from this outbound projection).**

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
