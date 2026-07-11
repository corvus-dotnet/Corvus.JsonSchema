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

**Slice 1 — approval queues (the immediate badge payoff):**
- [~] `accessRequests` (scope=queue, status) — `IAccessRequestStore`. **Piece 1 DONE + BUILDS + COMMITTED**
  (`/accessRequests/count`, op `countAccessRequests`, filter params only): OpenAPI surface + reusable `CountResult`
  schema; regenerated clean; `HandleCountAccessRequestsAsync` mirrors `HandleListAccessRequestsAsync`'s reach/query build
  (`ParseCountStatus`/`IsCountQueueScope`/`CountResult(count,capped)` helpers, `CountCap = 100`); `IAccessRequestStore.CountAsync(AccessRequestQuery,
  int cap, ct) -> (int Count, bool Capped)` with a **default impl** (`total > cap ? (cap,true) : (total,false)` over
  `ListAsync(query)`) so all 10 backends work immediately & correctly. **NEXT (Piece 2):** native bounded `CountAsync`
  overrides — relational four (Sqlite/Postgres/MySql/SqlServer) `SELECT COUNT(*) FROM (SELECT 1 … WHERE <same conds> LIMIT @cap+1)`,
  reusing the list's status/bw/subject/administered predicate (extract a shared `Append*Conditions` so it can't drift; the
  cursor stays list-only); scan-based five (Redis/Mongo/Cosmos/NatsJetStream/AzureStorage) + InMemory bounded scan stopping at
  cap+1. Then verify vs the live Postgres container; commit. **Then (Piece 3):** conformance tests. Then availability + runner.
- [ ] `availabilityRequests` (scope=queue, status) — `IAvailabilityRequestStore`
- [ ] `runnerAuthorizations` (status) — `IEnvironmentRunnerAuthorizationStore`

**Regen command** (from `…ControlPlane.Server/README.md`):
`dotnet run --project src/Corvus.Json.Cli -f net10.0 -- openapi-server docs/control-plane/arazzo-control-plane.openapi.json --rootNamespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server --outputPath src/Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server/Generated`
Diff must be count-only (a drifting regen = generator version skew — reconcile before committing).

**Slice 2+ — the rest (governance / registry / runs):**
- [ ] `runs` — `IWorkflowStateStore` (list/index)
- [ ] `catalogVersions` — catalog store
- [ ] `environments` — `IEnvironmentStore`
- [ ] `sources` — `ISourceStore`
- [ ] `credentials` — `ISourceCredentialStore`
- [ ] `runners` — `PostgresRunnerRegistry` etc.
- [ ] `securityBindings` (grants) + `securityRules` — `ISecurityPolicyStore`
- [ ] `administrators` (workflow §15) — `IWorkflowAdministratorStore`
- [ ] `environmentAdministrators` — `IEnvironmentAdministratorStore`
- [ ] `versionAvailability` / `environmentAvailability` — `IAvailabilityStore`
- [ ] `environmentRunnerAuthorizations` (per-env) — `IEnvironmentRunnerAuthorizationStore`
- [ ] `workingCopies` — `IWorkspaceWorkflowStore`
- [ ] observed identities (typeahead) — `IObservedIdentityStore` (count optional)

**Excluded:** `securityOrderings` (fixed, tiny config — no count value).

Sequence: finish Slice 1 end-to-end first (badges show `x+`), then work the rest one list at a time, each following
the four-step per-slice sequence above, per-piece commits, all-backends-first-class.
