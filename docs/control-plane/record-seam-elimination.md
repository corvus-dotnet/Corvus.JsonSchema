# Record↔document seam elimination — design

A scoping document for removing the **hand-rolled POCO record↔document seams** in the Arazzo durability +
control-plane code: hand-rolled `record`/`record struct`/`class` types that force a managed-`string` (or
`List<string>`/`Dictionary<string,…>`) materialization on a path where **both ends are bytes** — a request body
and a persisted document, a directory response and an API response, a stored row and a filter decision. The
string is a *u-turn*: `bytes → POCO strings → bytes`.

This is the same class as the seam already removed from the administrators handler (where the add/transfer/remove
paths were rewritten bytes-to-bytes through `SecurityTagSet.Build` + `ResolveUsageGrantInto`). This document
captures the full inventory found by the sweep, the fix recipe, and the implementation order.

## The anti-pattern

A type is a record↔document seam when **all** of:

1. It is **hand-rolled** (not a generated Corvus.Text.Json type under a `Generated/` directory, not a
   `config`/`options` type read once at startup).
2. It sits on a **per-request / per-row / warm** path.
3. Its **source is bytes** (a JSON/UTF-8 body parsed with `Utf8JsonReader`, a DB column, a directory response)
   **and its sink is bytes** (a serialized JSON document, a generated response model, a `SecurityTagSet`, a DB
   write).
4. It **forces a managed-string materialization** between them that the bytes-native machinery (deferred
   `SecurityTagSet`/`TagSet` holders, `IdentityBuilder`, `Utf8JsonWriter`) would otherwise avoid.

**Not** the anti-pattern: generated CTJ types; config/options; genuinely string-typed leaves with no bytes
upstream **or** downstream (a `ClaimsPrincipal` claim, a string store **key** like `baseWorkflowId`, an external
string-typed library like Novell LDAP, an HTTP `Authorization` header, a human-facing audit/error message).

## The fix recipe

The cure is the pattern already used by `IdentityBuilder.Add(span)` / `Add(string)`:

- **Bytes-native default.** Add a span/`in`-`SecurityTagSet` path that walks the persisted UTF-8 directly and
  writes straight into the destination buffer/holder. This becomes *the* path the warm code takes.
- **Opt-in string extensibility.** Where the seam is a **deployment-authored extension point** (e.g.
  `IDirectoryIdentityMapper`), keep the string-POCO contract as the *opt-in* path for deployments that want the
  easy contract — explicitly the exception, not the default. Built-in adapters and in-box mappers take the
  bytes path.
- **Genuine string leaves stay strings.** An external string-typed source (LDAP) does its one unavoidable
  `string→bytes` write at its own leaf; everything downstream stays bytes.

Every change carries a **ledger comment** (what is owned vs. borrowed vs. pooled) and is proven by a
BenchmarkDotNet `[MemoryDiagnoser]` benchmark showing the allocation drop. (Benchmark allocation figures are
exact, not statistical.)

## Inventory (from the sweep)

### Tier 1 — warm, implement now

| # | Seam | Where | Frequency | Fix |
|---|------|-------|-----------|-----|
| 1 | `SecurityFilter.IsSatisfiedBy(IReadOnlyList<SecurityTag>)` → per-row `SecurityTagSet.ToList()` | `SecurityFilter.cs:49`, `SecurityRule.cs:82`, `SecurityTagSet.ToList()` at `SecurityTagSet.cs`; call sites in the **5 non-SQL** backends (Mongo/Cosmos/Redis/AzureStorage/NATS) catalog-search + run-list + observed-identity read-reach | **Per scanned row** | ✅ **DONE.** Bytes-native `IsSatisfiedBy(in SecurityTagSet)` / `SecurityRule.EvaluateAll` walks the persisted UTF-8 (operand strings pre-encoded at compile, claims at request via `Utf8ClaimSet`); the list overload delegates so the tests lock semantics; all 17 call sites pass the deferred holder. Benchmark: **25.78 KB → 1.56 KB (0.06×), 2.5× faster** per 50-row page. |
| 3 | Credentials create: `ReadTags`/`ReadGrants` string-materialize while bytes-native `ResolveUsageGrantInto` sits unused | `ArazzoControlPlaneCredentialsHandler.cs` `ReadGrants` | **Per credential create** | ✅ **DONE.** Usage `SecurityTagSet` built via `SecurityTagSet.Build` + `ResolveUsageGrantInto(...GetUtf8String().Span…)`, exactly as the administrators handler's `BuildSingleGrantIdentity`; dead `ReadGrants` removed. Alloc win is the `Build`-vs-`FromTags` primitive proven by `IdentityBuildBenchmarks` (272 B → 96 B, 0.35×). (The remaining `SourceCredentialDefinition` secretRef/config string fields are the cold store-contract seam — Tier 2.) |
| 4 | Mongo `TagSet.FromTags(AsBsonArray.Select(t => t.AsString))` | `MongoWorkflowCatalogStore.cs` `ReadTags`, `MongoWorkflowStateStore.cs` | Per Mongo row (small) | ✅ **DONE.** New `MongoTags.Read` mirrors the store's own `MongoSecurityTags.Read` (BSON → `Utf8JsonWriter` → `TagSet.CopyFromJsonArray`), dropping the LINQ `Select` + intermediate `List` + re-encode. **Small win**: the per-element `BsonValue.AsString` is the driver's already-materialised string (a genuine BSON-driver leaf), so this removes the LINQ/list overhead, not the strings — a consistency fix matching the sibling security-tags path. |

### Tier 2 — cold or contract-wide, tracked (not in this pass)

Deferred on **measured frequency** plus **contract blast-radius**, not as an excuse — each is documented with its
real frequency so the call is auditable:

- **`SourceCredentialDefinition` store write seam** (the `string` secretRef/config/sourceName fields beyond the
  tags/grants handled in Tier 1 #3). `ISourceCredentialStore.AddAsync/UpdateAsync` is the **public** contract of
  all nine backends; the credential **read** path (the §13.4 warm path) is already bytes-clean. Cold (admin
  create/update). Its own piece: a `WriteNewFrom(Utf8JsonWriter, Models.CredentialBindingWrite)` seam.
- **`DirectoryRecord`** (the string mapper). Already mitigated: the HTTP adapters have a bytes-native
  `DirectoryRecordView` / `IDirectoryIdentitySpanMapper` span path; `DirectoryRecord` is the *fallback* for a
  string mapper, and the genuine leaf for LDAP. Action: keep it as the opt-in extensibility surface and steer
  in-box/sample mappers to the span mapper. (Subsumed by Tier 1 #2, which removes the residual `ResolvedPrincipal`
  string cost on the span path.)
- **Access-request submit scopes** (`HandleSubmitAccessRequestAsync` `List<string> scopes`): a tiny fixed array;
  the approve path's scope strings are **load-bearing** (drive `Contains`/intersection authorization), not a pure
  u-turn. Marginal.
- **Security-binding rule-names** (`ArazzoControlPlaneSecurityHandler.ToGrant`): cold admin create/update.

### Verified clean (not the anti-pattern)

Catalog handler (`.From` + `TagSet` bytes holder); access-request stores (verbatim `doc` + `PersistedJson.ToPooledDocument`,
generated `AccessRequest`); credential **stores** (verbatim binding doc + opaque tag discriminator; tags never
re-materialized on read); `PersistentRowSecurityPolicy` (`ClaimsPrincipal` + a cold pre-compiled snapshot);
execution manifest/loader (cold, cached per version, genuine reflection/equality leaves).

## Status

| # | Seam | Status | Proof |
|---|------|--------|-------|
| 1 | SecurityFilter bytes-native | ✅ Done | 18 rule/filter + 292 durability + 129 server tests; bench **0.06× alloc, 2.5× faster** |
| 3 | Credentials create | ✅ Done | 10 credential + 131 server tests; primitive proven by `IdentityBuildBenchmarks` (0.35×) |
| 4 | Mongo tags | ✅ Done | Mirrors tested `MongoSecurityTags.Read`; identical `TagSet` bytes (small consistency win) |
| 2 | `ResolvedPrincipal` bytes-native | ✅ Done | `ResolvedPrincipal` value/label → owned UTF-8 (dual span/string ctor); 6 adapters bytes-native (constructed display names assembled into a pooled buffer, not a string); handler directory projection via the generated `Build<TContext>` context-threading form (static lambdas, ref-struct state — no per-grantee `GetString`, no closures). 11 directory + 131 server tests (2 new directory-branch runtime tests); bench **0.34× alloc** |

**On #2's tiering:** I first deferred this to Tier 2 on a marginal-frequency/high-contract-cost argument. That was the wrong call — the user chose **congruence**: a hand-rolled POCO string seam in the directory path is incongruent with the bytes-to-bytes design everywhere else, and the contract blast radius turned out small (a single `new ResolvedPrincipal(...)` site, all adapters funnelling through the projector). The string `Value`/`Label` accessors remain for non-server callers (CLI, conformance tests) as the opt-in path; the built-in adapters + the server take the span path.

Each done seam carried a ledger comment, a warning-free `dotnet build`, affected tests green, and a MemoryDiagnoser
proof (or the shared primitive's proof). Tier 2 items stay tracked with their frequency + contract cost recorded above,
so the deferral is an auditable cost/benefit call, not a silent omission.
