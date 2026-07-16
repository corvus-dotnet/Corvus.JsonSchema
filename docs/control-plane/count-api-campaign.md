# Count-API campaign — bounded counts for every paged list

Goal: give every count-worthy paged/list API an efficient **bounded** count, so the console can render
work badges (Approvals) and list-footer totals without fetching rows — and never lie about scale.

## Ratified contract

- **Store seam:** `ValueTask<(int Count, bool Capped)> CountAsync(<same filter as the list>, int cap, CancellationToken)`.
  Query with `LIMIT cap + 1`; if the `cap+1`th row exists, return `(cap, Capped: true)`, else `(actual, false)`.
  Honors the caller's §14.2 **reach** exactly like the paged list (reuse the list's predicate so it can't drift).
  Allocation-free — a number, no row materialization (aligned with the #803 bytes-to-bytes bar).
- **API:** an independent `GET /…/count?<same filters as the list>` → `{ "count": N, "capped": true|false }`.
  `capped` is first-class in the response so clients never guess. Endpoints stay **independent** (no coupling);
  an aggregate/BFF summary is a possible *later* thin composition, not now.
- **UI:** render `count` normally, and `` `${count}+` `` whenever `capped` is true (badges + any list footer that opts in).
- **Cap:** default 100 (badges/to-dos need "is there work / roughly how much", not exact-beyond-99). Small registries
  fit under the cap so their count is effectively exact.

## Per-slice sequence (each list, one committed piece at a time)

1. **API-first** — author the `…/count` operation in the OpenAPI surface (filters mirror the list; response
   `{count, capped}`), regenerate `Generated/`, then the handler. (See `api-first-openapi-surface-before-backend`.)
2. **Store seam** — add `CountAsync(filter, cap)` to the interface; **fan out to all 10 backends**: native
   `COUNT(*)` over the reach+filter predicate with `LIMIT cap+1` on the relational four (Sqlite/Postgres/MySql/SqlServer);
   a bounded scan (stop at `cap+1`) on the scan-based ones (Redis/Mongo/Cosmos/NatsJetStream/AzureStorage) + InMemory.
3. **Wire** the consumer (badges now; footers as opted in) onto `/count`, rendering `x+` on `capped`.
4. **Conformance tests** per backend: count matches list length below the cap; `capped` trips at `cap+1`; reach-filtered.

## Scope (everything, systematically) — worklist

> **Verifying container backends here** (Testcontainers over rootless podman on WSL): the Aspire composition uses DCP, not
> Testcontainers, so its socket isn't exposed. Start one: `podman system service --time=0 unix:///run/user/1000/podman/podman.sock &`
> then `export DOCKER_HOST=unix:///run/user/1000/podman/podman.sock TESTCONTAINERS_RYUK_DISABLED=true TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE=/run/user/1000/podman/podman.sock`.
> Then `dotnet test --project <backend>.Tests.csproj --filter "FullyQualifiedName~Counting"`. All 10 pass this way.

**Slice 1 — approval queues (the immediate badge payoff):**
- [x] `accessRequests` (scope=queue, status) — `IAccessRequestStore`. **DONE — all 3 slices committed + all 10 backends verified.**
  - 1a `8c20d97ac5`: `/accessRequests/count` API + handler (`HandleCountAccessRequestsAsync`, mirrors the list's reach; `CountCap=100`)
    + interface `CountAsync(AccessRequestQuery, int cap, ct) -> (int Count, bool Capped)` default.
  - 1b `7878fa3cc0`: native bounded `CountAsync` on every backend. Relational: COUNT over a `LIMIT @cap`/`TOP (@cap)` subquery
    (`@cap = cap+1`), each extracting a per-backend `AppendFilterConditions`/`BuildFilter` shared by full-list + paged-list + count
    (drift-proof; cursor stays list-only). Scan (Redis/Nats/InMemory): bounded `Matches` scan `++count > cap ⇒ (cap,true)`.
    AzureStorage: `BuildFilter` + RowKey-only projection, `maxPerPage: cap+1`. Cosmos: `SELECT c.id … OFFSET 0 LIMIT @lim`.
    Uniform map `total > cap ? (cap, true) : (total, false)`.
  - 1c `8c31e750ae`: two shared conformance tests (cap boundary + filter reach) in `AccessRequestStoreConformance`. Verified
    green 2/2 on InMemory + Sqlite (in-process) and Postgres/MySql/SqlServer/Mongo/Redis/AzureStorage/NatsJetStream/Cosmos
    (real containers/emulator). Full `slnx` build was 0-warning (composition stopped to release DLL locks).
- [x] `availabilityRequests` (scope=queue/environment, status) — `IAvailabilityRequestStore`. **DONE — all backends verified,
  commit pending.** `/availabilityRequests/count` (op `countAvailabilityRequests`, filters status/environment/scope) + handler
  `HandleCountAvailabilityRequestsAsync` (mirrors the list; env-admin 403) + interface default + native `CountAsync` on all 10
  backends. Availability backends already shared `AppendFilters`/`BuildFilter`/`Matches`, so count just reuses them (no
  extraction). Conformance (cap boundary + filter reach) green 2/2 on InMemory + Sqlite + Postgres/MySql/SqlServer/Mongo/Redis/
  AzureStorage/NatsJetStream/Cosmos.
- [x] `runnerAuthorizations` (status/environment; the inbox `/runnerAuthorizations`, not the per-env `/environments/{name}/runners`)
  — `IEnvironmentRunnerAuthorizationStore`. **DONE — all backends verified, commit pending.** `/runnerAuthorizations/count`
  (op `countRunnerAuthorizations`, filters status/environment; status defaults to Pending like the inbox) + handler + interface
  default + native `CountAsync` on all 10 backends (backends already shared AppendFilters/BuildFilter/Matches/AppendConditions).
  Conformance green 2/2 on every backend (InMemory+Sqlite in-process; the other 8 incl. Cosmos via containers/emulator).
- [x] **Approvals badges rewired onto `/count` — DONE + live-verified.** Added `countAccessRequests`/`countAvailabilityRequests`/
  `countRunnerAuthorizations` to `web/…/src/arazzo-client.js` and switched the `samples/…/wwwroot/index.html` badge poll from
  fetch-`limit:100`-and-count-`.length` to the three count calls, rendering `${count}${capped?'+':''}` (the tab total is `N+`
  if any queue was capped). Live-verified on the restarted composition (Postgres): the three `/count` endpoints return
  `{count,capped}` (200), and Playwright confirmed the badges render Access 1 / Availability 1 / Runners 0-hidden / tab total 2,
  the poll hits the `/count` endpoints (not the lists), 0 JS errors. **Slice 1 (the badge payoff) COMPLETE.**

> **Cosmos has NO bounded server-side COUNT** (verified on the emulator): a bare `COUNT` ignores the outer LIMIT and scans the
> whole set, and wrapping COUNT around a cap-limited subquery is rejected — both `'OFFSET LIMIT' clause is not supported in
> subqueries` and `'TOP' is not supported in subqueries`. So the Cosmos count uses the outer `SELECT c.id … OFFSET 0 LIMIT @lim`
> (allowed) counted client-side (≤ cap+1 tiny id rows). A code comment on both Cosmos stores records this — do not "fix" to COUNT.

> **Composition is currently STOPPED** (SIGTERM'd to release DLL locks for the build). Leave it down through the availability +
> runner verticals + badge rewire, then restart ONCE at Slice 1 completion and live-verify all three badges rendering `N+`.

**Regen command** (from `…ControlPlane.Server/README.md`):
`dotnet run --project src/Corvus.Json.Cli -f net10.0 -- openapi-server docs/control-plane/arazzo-control-plane.openapi.json --rootNamespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server --outputPath src/Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server/Generated`
Diff must be count-only (a drifting regen = generator version skew — reconcile before committing).

**Slice 2+ — the rest (governance / registry / runs):**
- [x] `runs` — **DONE + committed + live-verified.** `IWorkflowWaitIndex.CountAsync(WorkflowQuery, cap)` (default over
  `QueryAsync(Limit=cap+1)`) + `SecuredWorkflowManagement.CountAsync` mirroring `ListAsync` (`Reach(Read)` → `EnsureSupported` →
  delegate) + `ProtectedWorkflowStateStore` delegation + `/runs/count` (op `countRuns`, mirrors listRuns's 8 filters, `runs:read`)
  + `HandleCountRunsAsync` + `Counting_is_bounded_by_the_cap_and_scoped_to_the_read_reach` conformance. **Native `CountAsync` on ALL 10
  backends** (each store's inline `QueryAsync` filter+security-splice extracted into a shared `BuildVisibilityFilter`/`Matches` helper
  reused by list + count so the reach can't drift): relational `COUNT` over `LIMIT/TOP @cap`; InMemory/Redis/Nats bounded scan; Mongo
  `CountDocuments(Limit)`/bounded stream; Cosmos client-counted `SELECT c.id … OFFSET 0 LIMIT`; AzureStorage bounded stream. **Also fixed
  a §18 draft-run visibility leak** discovered here: only Sqlite+InMemory excluded `$draft` from the unfiltered runs list; the other 8
  backends leaked draft/debug runs — the shared helper now excludes `$draft` everywhere (fixing list AND count). **Plus** a decorator gap
  fix (`ProtectedWorkflowStateStore` now forwards `ISupportsRowSecurityFilter` via a default capability property, so an encrypting wrapper
  over a reach-capable store no longer fails `EnsureSupported`). Verified: InMemory 444/444, Sqlite 213/213 in-process; 8 container backends
  29/29 each (incl. the draft test now green); web-ui 208/208; live on seeded Postgres (`/runs/count`=={runs list}=6, 0 draft leak, footer
  renders "6 runs" via `/runs/count`). Commits `ac9ef6ea3c` (gap fix), `4a05315a34` (core+API), `28b68d3a77` (native fan-out + draft fix),
  `dd039c8729` (UI footer).
- [x] `catalogVersions` — **DONE + committed + live-verified.** Same push-down shape as runs, with a `CatalogQuery.DistinctWorkflows`
  wrinkle (count matching versions, or distinct base workflows). `IWorkflowCatalogStore.CountAsync` (default over `QueryAsync(Limit=cap+1)`,
  honours DistinctWorkflows free) + `SecuredWorkflowCatalog.CountAsync` mirroring `SearchAsync` + conformance (`e7f367f439`); `GET
  /catalog/count` op `countCatalog` (mirrors searchCatalog's filters incl. `distinctWorkflows`) + regen + `HandleCountCatalogAsync`
  (`fe013f6e1a`); **native `CountAsync` on ALL 10 backends** (`4a94cc6af4`) — relational extract a shared `BuildFilterWhere`, non-distinct
  `COUNT(*)` over `LIMIT/TOP cap+1`, distinct `COUNT(DISTINCT BaseWorkflowId)` (SqlServer `DISTINCT TOP`); scan reuse `Matches` + HashSet;
  Mongo `CountDocuments(Limit)`/`$group…$count` vs client-stream on reach; Cosmos client-counted `SELECT c.id … LIMIT` / `SELECT DISTINCT
  VALUE c.baseWorkflowId`; AzureStorage base-id-only server filter + Status in `Matches`. Catalog-table footer (`db449b9736`). Verified:
  InMemory+Sqlite 23/23 in-process; 8 container backends 23/23 each; web-ui 208/208; live on seeded Postgres (`/catalog/count` == list == 6
  versions / 4 distinct; footer renders "4 workflows" via `/catalog/count?distinctWorkflows=true`, 0 page errors).
- [x] **Slice 2 (full native fan-out) — DONE + pushed + container/live-verified (2026-07-12).** `runners` (2A `dbe31f259f`: reach is
  per-row ABAC, not SQL — interface-default count over the native reach `ListAsync`; no per-backend override); `versionAvailability`/
  `environmentAvailability` (2B `275faa873c`: `IAvailabilityStore.CountByVersionAsync`/`CountByEnvironmentAsync`, native COUNT on all 10,
  path-scoped no reach); `environmentRunnerAuthorizations` per-env (2C `05644f43c7`: handler-only, reuses the Slice-1 store `CountAsync`);
  `securityRules`/`securityBindings` (2D `8e791d6d48`: `ISecurityPolicyStore.CountRulesAsync`/`CountBindingsAsync`, capability-scoped q-filter,
  interface-default for InMemory/AzureStorage + native on the other 8; grants+scopes console footers). Cosmos count fix `7ff1de477e`
  (`SELECT c.id AS doc` + `ORDER BY`, container-caught). All 8 container backends 37/37; live 6/6 `/count`==list on seeded Postgres.

**Excluded (no count value):** `securityOrderings` (fixed tiny config); `administrators`/`environmentAdministrators` (not paged);
observed identities (a prefix TYPEAHEAD for grantee resolution, `SearchAsync(kind, prefix, …)` — top-N autocomplete, not a browsable
paged list with a total). **CAMPAIGN COMPLETE: every count-worthy paged control-plane list now has a bounded `/count`.**

### RATIFIED (2026-07-11): path (b) — fold the metadata stores onto row-security push-down FIRST, then count

The metadata stores — **`environments` (`IEnvironmentStore`), `sources` (`ISourceStore`), `credentials`
(`ISourceCredentialStore`), `workingCopies` (`IWorkspaceWorkflowStore`)** — currently apply the §14.2 reach as an **in-memory
per-row predicate** (`context.Admits(Read, candTags)` after parsing each row's ManagementTags). Runs/catalog instead **push the
reach to SQL** (a `*_security_tags(id, tag_key, tag_value)` side table + `SecurityFilter.ToSqlPredicate`). The user chose (b): give
the metadata stores the same push-down so their reach is indexed AND their counts are native/uniform (not a materialising default).
This is a **security-critical epic** (a mistake = auth-boundary leak) of ~40 store changes across 4 families — do it **test-first**,
per-family, per-backend, per-piece commits, re-running reach conformance at each step.

**Exemplar (Postgres runs — replicate it):** side table `workflow_run_security_tags(run_id, tag_key, tag_value)` + indexes;
`SyncSecurityTagsAsync` delete+reinsert on every save; delete cascades in DeleteAsync; the query has a `{{securityPredicate}}`
placeholder replaced by `security.ToSqlPredicate(new SqlSecurityRuleEmitter("workflow_run_security_tags", ["run_id"], "tag_key",
"tag_value", "workflow_runs", paramBinder))`. Store marked `ISupportsRowSecurityFilter`. See `PostgresWorkflowStateStore` L185-205,
L429-451, schema L526-533; `RowSecurityFilter.cs`; `SqlSecurityRuleEmitter`.

**Approach (i) — chosen:** the metadata stores keep their `ListAsync(AccessContext, …)` signature (no query-object/marker/facade
churn); each store INTERNALLY extracts `SecurityFilter? reach = context.Reach(AccessVerb.Read)` and, if non-null, splices
`reach.ToSqlPredicate(emitter over the family's side table)` into its SQL — replacing the in-memory `Admits` scan. Relational
(Sqlite/Postgres/MySql/SqlServer): side table + sync + provisioning + push-down + native `COUNT(*)` over the predicate `LIMIT cap+1`.
Scan/InMemory (Redis/Mongo/Cosmos/Nats/AzureStorage/InMemory): keep the in-memory reach in their scan (correct); add a bounded-scan
`CountAsync`. Provisioning: the family's side table must be created in the store's schema AND the deployment libraries.

**Test-first safety net:** the conformance suites only used `AccessContext.System` (full reach) — a reach leak would pass unnoticed.
Add a restricted-reach test to EACH family suite BEFORE converting. **DONE for environments:**
`EnvironmentStoreConformance.Listing_is_scoped_to_the_read_reach` (committed) — construct with
`AccessContext.Uniform(new SecurityFilter([SecurityRule.Compile("tenant == 'acme'")], EmptyClaims))`; tag rows via
`Environment.Draft(name, dn, desc, SecurityTagSet.FromTags([new SecurityTag("tenant","acme")]))`; assert restricted list returns only
the admitted row. Add the analogous test to Source/SourceCredential/WorkspaceWorkflow conformance next.

**Per-family sequence:** (1) restricted-reach conformance test → verify green on current in-memory impl. (2) convert relational
backends to side-table push-down + verify the reach test still passes on each (real containers). (3) add native `CountAsync` (all
backends) + the count conformance. (4) API-first `…/count` (reverted the earlier big-batch `/environments/count` etc. — re-add
per-family) → regen → handler. (5) wire the console footer (`web/…/src/arazzo-client.js` count method + the list panel footer).

**Status (2026-07-11):** environments row-security push-down **DONE + verified on Sqlite (in-process) and Postgres (real container)** —
`f41c8f37ed` (Sqlite exemplar: `IEnvironmentStore.CountAsync` default + Sqlite side-table push-down + native COUNT + conformance),
`706c74eba4` (Postgres, 10/10 on a real container). Deployment provisioning is **auto-covered**: the deployment libs call the
store's `PrepareAsync` → `SchemaSql`, which now creates `EnvironmentSecurityTags` (`CREATE TABLE IF NOT EXISTS`). The reach safety-net
(`Listing_is_scoped_to_the_read_reach`) + count test run in the SHARED `EnvironmentStoreConformance`, so they execute on every backend.

**environments STORE-SIDE DONE + verified across ALL 10 backends (2026-07-11).** Relational four (Sqlite `f41c8f37ed`,
Postgres `706c74eba4`, MySql `d88a0bbda2`, SqlServer `7aa2357ce2`) push the reach into SQL via `EnvironmentSecurityTags`
side tables + `SqlSecurityRuleEmitter` with native `COUNT`; scan/InMemory (Redis/Mongo/Cosmos/Nats/AzureStorage/InMemory) keep
in-memory `Admits` reach + the interface DEFAULT bounded count. The shared reach safety net (`Listing_is_scoped_to_the_read_reach`)
+ count test pass on every backend — no leaks. `IEnvironmentStore.CountAsync` default landed. Deployment auto-covered (PrepareAsync).
**sources STORE-SIDE DONE — relational four verified across real containers (2026-07-11).** SourceStore is structurally
identical to EnvironmentStore ((Name,Tags) PK, canonical tags, in-memory Admits); ported verbatim (side table
`SourceSecurityTags`, main `Sources`, type `RegisteredSource`, page `SourcePage.Sources`). Relational four push the reach into
SQL via `SourceSecurityTags` side tables + `SqlSecurityRuleEmitter` with native bounded `COUNT`: Sqlite `410dfb7484` (in-process),
Postgres `0978d5624d`, MySql `b1a55894a9` (owner cols `["Name","TagsHash"]`, CHAR(64) hash), SqlServer `e289b81701` (owner cols
`["Name","TagsHash"]`, BINARY(32) HASHBYTES, TOP-subquery COUNT) — each verified 2/2 (reach safety net + bounded count) on a real
container. `ISourceStore.CountAsync` default added. Scan/InMemory (Redis/Mongo/Cosmos/Nats/AzureStorage/InMemory) keep in-memory
`Admits` + the DEFAULT count — the shared conformance (`Management_reads_are_reach_filtered_and_non_disclosing` +
`Counting_is_bounded_by_the_cap_and_scoped_to_the_read_reach`) runs on every backend. Scan backends confirmed: InMemory
(in-process) + Mongo (container) count test green.

**§14.2 METADATA-STORE ROW-SECURITY PUSH-DOWN EPIC — COMPLETE (2026-07-11).** All FOUR metadata store families now push the
§14.2 read reach into SQL on the relational four (side table + `SqlSecurityRuleEmitter` correlated EXISTS + native bounded
`COUNT`) and keep in-memory `Admits` + the interface DEFAULT bounded count on the scan five + InMemory; shared reach safety net +
count test green on every backend, no leaks. Every family also gained a bounded `CountAsync` (interface default + native override).
Deployment auto-covered (deploy libs call each store's `PrepareAsync`→`SchemaSql`, which now creates the `*SecurityTags` side table).
- **environments** — side table `EnvironmentSecurityTags`, owner `(Name, Tags)` / `(Name, TagsHash)`. Sqlite `f41c8f37ed`, Postgres
  `706c74eba4`, MySql `d88a0bbda2`, SqlServer `7aa2357ce2`.
- **sources** — `SourceSecurityTags`, same 2-col owner. Sqlite `410dfb7484`, Postgres `0978d5624d`, MySql `b1a55894a9`, SqlServer `e289b81701`.
- **credentials** — `SourceCredentialSecurityTags`, 3-col owner `(SourceName, Environment, Tags/TagsHash)`; the row `Tags` is a COMBINED
  mgmt+usage discriminator but the side table mirrors the MANAGEMENT tags (what reach filters on). Sqlite `006e976931`, Postgres
  `1d28310e39`, MySql `3607885343`, SqlServer `3766e7af6a`.
- **workingCopies** — `WorkspaceWorkflowSecurityTags`, single-col owner `(Id)` (globally-unique id, no discriminator/hash). Sqlite
  `15c890a503`, Postgres `518e186f9d`, MySql `f1f8f90fc5`, SqlServer `07b5b354cc`.
Each relational backend verified 2/2 (reach safety net + bounded count) against a real container (Sqlite/InMemory in-process).

**METADATA COUNT-API LAYER DONE + built end-to-end (2026-07-11, `43d393c777`).** Four parameterless, reach-scoped `/count`
endpoints added to the OpenAPI spec (`/environments/count`, `/sources/count`, `/credentials/count`, `/workspace/workflows/count`;
each mirrors its family's list read scope; shared `CountResult` schema). ONE `openapi-server` regen (count-only: 4 `Count*Params`/
`Result` model pairs + 4 handler interfaces + endpoint registration; the manifest `generatorVersion` is `1.0.0+<git-sha>` provenance,
NOT a generator bump — no model drift) + ONE `openapi-client` Cli regen (also picked up the 3 Slice-1 approval `/count` ops the Cli had
never regenerated). Wired the 4 `HandleCount*Async` handlers (each `store.CountAsync(this.access.Current(), CountCap=100)` — the exact
§14.2 reach as the family's list) + 4 `arazzo-client.js` count methods (`countEnvironments/countSources/countCredentials/
countWorkingCopies`). **Server + Cli + Demo all build 0/0.** Regen commands live in each project's README (Server=`openapi-server`,
Cli=`openapi-client`); Demo/Generated is empty (Demo references the Server project).

**UI FOOTERS DONE + LIVE-VERIFIED (2026-07-11, `98f5831a4f`).** The four metadata list panels (`environments-panel`, `sources-panel`,
`credentials-table`, `workspace-table` — the console renders `<arazzo-environments>`/`<arazzo-sources>`/`<arazzo-credentials>` in the
demo `index.html`) now show the reach-scoped bounded grand total (`N` / `N+`) in their pager footer, fetched via the new `countX()`
methods in parallel with the page load (`.catch` → falls back to the visible page count; the credentials footer shows the total only
when no client-side status/source filter is active, since `/count` is unfiltered). web-ui node tests 208/208 + component tests green for
all four panels (the 2 catalog-detail failures are pre-existing). **LIVE end-to-end verification (real composition, real seeded
Postgres):** the four `/count` endpoints returned EXACTLY their list length via `curl` with `X-Api-Key: demo-admin-key` — environments
3=3, sources 4=4, credentials 8=8, workingCopies 0=0 (all `capped:false`); and a Playwright run (Keycloak login arazzo-admin/admin)
confirmed the footers render "3 environments" / "4 sources" / "8 bindings" with zero page errors. Composition SIGTERM-stopped after.

**COUNT-API CAMPAIGN — METADATA FAMILIES COMPLETE end-to-end** (store push-down + bounded count · `/count` endpoints · Server/Cli/JS
clients · UI footers), all container/live-verified. **NEXT (remaining families):** runs / catalog native counts — already
reach-pushed-down, so thread `CountAsync` through `IWorkflowWaitIndex`/catalog (native push-down count like the metadata relational
stores), + their `/count` endpoints + any list footers. Then the smaller lists (runners, securityBindings/Rules, administrators, etc.)
per the Slice-2 worklist above; `securityOrderings` excluded.

**(prior fan-out notes)** MySql + SqlServer — mirror the Postgres push-down (Npgsql→MySqlConnector / SqlClient; SqlServer COUNT via
`SELECT COUNT(*) FROM (SELECT TOP (@cap) 1 … WHERE <reach>) AS bounded`). **MySql wrinkle:** its `Environments` table discriminates rows
by a fixed-size `TagsHash` column (MySql can't fully index a `TEXT` column, so it hashes Name+Tags), and Add inserts `(Name, TagsHash,
Tags, …)`. So the MySql side table is `EnvironmentSecurityTags(Name, TagsHash, TagKey, TagValue)` and the emitter owner columns are
`["Name","TagsHash"]` (NOT `Tags` — a TEXT owner column would need a prefix length). Check SqlServer's row discriminator too before
picking its owner columns. (2) The scan backends (Redis/Mongo/Cosmos/Nats/AzureStorage)
+ InMemory already apply reach in-memory in their ListAsync and get a correct bounded count from the interface DEFAULT — **run the env
conformance on each to confirm the reach test passes (no leak) + count correct**; add a native bounded-scan `CountAsync` only if worth it.
(3) Then `/environments/count` OpenAPI + handler + console footer. Then repeat the whole family cycle for sources, credentials, workingCopies.
Per-backend recipe (relational): `using System.Text` + `System.Globalization`; side table in `SchemaSql`; transaction+`SyncSecurityTagsAsync`
on add (tags immutable → none on update); cascade delete; `AppendReachPredicate` (composite owner cols `["Name","Tags"]`, tag table
`EnvironmentSecurityTags`, main `Environments`) shared by ListAsync (WHERE + `ORDER BY … LIMIT @limit`) and the native `CountAsync`.
Composition STOPPED. Podman socket for container conformance: note at top of this file.
