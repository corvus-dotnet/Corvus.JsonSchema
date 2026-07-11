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
