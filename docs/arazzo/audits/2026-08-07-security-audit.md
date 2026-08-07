# Security audit, 2026-08-07

**This document is the result of an audit.** The standard it audits against is the
[threat model](../reference/threat-model.md), which is the standing artefact: assets, adversaries,
trust boundaries (`TB-1`…`TB-10`), undesired outcomes (`UO-1`…`UO-11`), the control inventory, and
the accepted-risk register. This document is the point-in-time measurement of the built system
against that model, and the prioritised work that follows from it. Re-running the audit produces a
new dated document here; the threat model is amended, not replaced.

It is written to be handed to an implementation agent.

**Baseline.** Branch `worktree-arazzo-workflow-engine-plan`. The findings were established against
the tree as of the commit that introduced the threat model.

**Audit scope.** 66 ADRs, roughly 186k lines of non-generated Arazzo source, 50 projects. Nine
parallel antagonistic reviews across separate domains, each required to trace every ADR claim to
enforcing code and to label conclusions CONFIRMED or PLAUSIBLE. Only confirmed items appear. The most
severe were independently re-verified against source. Source-level only, so nothing was built or
executed and runtime behaviour, deployed configuration and upstream dependencies are unverified.

**Premise.** The system is assumed to be building out as designed. ADR 0065 phases B and C are
sequenced work, not defects. The audit therefore measures **conformance, not completeness**.

---

## 1. How to read this

| Class | Meaning | Action |
|---|---|---|
| **DIV** | **Divergence.** Built, and an ADR or guide asserts a property it does not have. | Fix in code. The design is already correct, so no decision is needed. |
| **SEQ** | **Sequenced.** Designed, specified, correctly deferred. | Build to the existing spec. Not a defect. |
| **GAP** | **Design gap.** No ADR covers it. | Decide first with an ADR, then build. **Do not let an agent invent the policy.** |
| **PROC** | **Process and tooling.** Repository and CI hygiene. | Fix in config. Cheap, high leverage. |

**Priority tiers.** P0 voids a control the design claims exists, and most are one-line or
one-argument changes. P1 are divergences phase B would inherit, so they precede SEQ work. P2 need a
design decision. P3 is hardening.

**The load-bearing sequencing rule.** Phase B builds freshness and integrity on top of four things
this audit finds diverging right now: the lease token (P1-1), the store reach predicate (P1-2), the
run-id key (P1-7) and the audit primitive (P1-6). Wiring the anchor to a lease whose epoch is never
compared, or sealing payloads behind a marker interface that certifies pushdown by default, carries
each divergence into the layer meant to close it. **Close P0 and P1 before starting phase B.**

---

## 2. Audit result at a glance

| | Count | Note |
|---|---|---|
| Controls verified as holding | 22 technology, 3 process, 1 people | Catalogued in threat model §7. Injection absent across all nine store backends, no XSS in 86 UI components, ADRs 0045 and 0047 hold in code |
| **Divergences (DIV)** | **28** | Built, non-conformant. The audit's primary output |
| Design gaps (GAP) | 16 | No ADR covers them |
| Sequenced (SEQ) | 8 | Correctly deferred, not scored |
| Accepted risks confirmed | 17 | Mostly from ADR 0065's published residues |
| Deployment assumptions | 7 | Load-bearing, unverifiable in code |
| Outcomes with **zero** layers | 4 | `UO-1`, `UO-5`, `UO-7`, `UO-9`. See threat model §10 |

**One reviewer claim was refuted on verification** and is deliberately absent. The
`WorkflowsCancelled` and `WorkflowsSuspended` counters were reported as never incremented; both are
(`WorkflowRun.cs:438, 463, 477`; `SecuredWorkflowManagement.cs:535`). Verify before acting on any
item you cannot see directly in the code.

---

## 3. P0, void a claimed control and are cheap

### P0-1 · DIV · `TB-2` · Checkpoint endpoint is unauthenticated in every meaningful sense

> **Resolved.** The finding held on verification. See the threat model's
> [findings ledger](../reference/threat-model.md#12-findings-ledger) (H1) and the 2026-08-07 amendment
> to [ADR 0062](../adr/0062-authenticated-serverless-checkpoint-callbacks.md). Acceptance criteria 1
> and 2 were **not** applied as written, and deliberately: see *Deviation* below. The finding text is
> left as measured.

- **Where:** `ControlPlaneEndpointExtensions.cs:351`; `WorkflowCheckpointEndpoints.cs:40, 45, 57-75, 78-134, 141-145, 152-157`
- **Divergence:** ADR 0062 specifies run-scoped token authentication. The token primitive (`CheckpointToken.cs`) is implemented and sound, with HMAC, run binding, constant-time compare and canonical expiry. `MapWorkflowCheckpointEndpoints` is called **without** `authenticateCheckpointToken`, so it defaults to `null` and `Authenticated()` returns `true` unconditionally. Neither handler constructs an `AccessContext`, so ADR 0004 reach never runs. The only gate is a bare `RequireAuthorization()` with no policy or scope.
- **Impact:** `UO-1`, `UO-2`, `UO-5`. Any authenticated principal reads and overwrites any run's plaintext by id, in any environment. Unauthenticated in `Open`. Voids ADRs 0001, 0004, 0013 and 0065 on one surface.
- **Acceptance criteria:**
  1. Pass a token authenticator, and gate on reach and lease as the runner API twin does (`ArazzoRunnerCheckpointsHandler.cs:77, 113` calls `HoldsLeaseAsync`).
  2. Declare a dedicated scope in the OpenAPI contract.
  3. Use the **same** `WorkflowCheckpointCoordinator` instance as the runner API. ADR 0065 §6 requires the single-flight interlock to be per run, not per component, and two currently exist (`RunnerEndpointExtensions.cs:78`, `WorkflowCheckpointEndpoints.cs:45`).
  4. Test: a principal with an unrelated scope gets 404 or 403 for a run outside reach, and a valid lease holder succeeds.
- **Deviation, and why.** Criteria 1 and 2 do not fit this surface. Its caller is a dispatched
  function that holds no OIDC principal and no lease, since the dispatching runner holds the lease.
  That is the case ADR 0062 exists to answer, and requiring `HoldsLeaseAsync` here would refuse every
  legitimate callback. The run-scoped token **is** this surface's reach gate. Criterion 2 does not
  apply either, because the surface is deliberately outside the generated contract, being raw
  octet-stream and hand-mapped. Criteria 3 and 4 were applied as written.
- **Also found, which the audit did not name.** The sweep the audit recommends paid off here.
  `MapWorkflowCheckpointEndpoints` had five call sites; **four** passed no authenticator and one did.
  The audit named only the control plane, but the surface that actually receives function callbacks
  in the demo composition is the serverless runner's own
  (`ServerlessRunner.Demo/Program.cs:221`), which was fully open and whose backend minted no token.
  The fix therefore closes the class rather than the instance: the authenticator is now a **required**
  parameter, so the surface cannot be mapped unauthenticated anywhere.

### P0-2 · DIV · `TB-5` · Revocation fence expires zero leases

> **Resolved.** The finding held on verification, and understated the impact. See *Worse than reported*
> below, and the threat model's [findings ledger](../reference/threat-model.md#12-findings-ledger) (H5).
> All four acceptance criteria were applied as written.

- **Where:** `ArazzoControlPlaneRunnerAuthorizationsHandler.cs:566-571`; `RunnerRunCoordinator.cs:97, 225-243, 290-303`
- **Divergence:** ADR 0027 and ADR 0065 §2 specify that revocation expires in-flight leases server-side, immediately, whether or not the runner cooperates. `FenceRevokedRunnerAsync` calls `ExpireLeasesForOwnerAsync(runnerId)`, the **client-supplied registration id**, while leases are acquired with the machine principal as owner. The comment above it still asserts the pre-0065 model.
- **Impact:** `UO-11`. Revocation is a no-op for in-flight work. Renewal and both checkpoint operations never re-resolve bindings, so a revoked runner renews indefinitely.
- **Acceptance criteria:** pass the resolved machine principal; re-resolve bindings on renewal and both checkpoint operations, bounded by the existing 30 second cache; delete the stale comment; test that a revoked runner's next renewal and next save both fail.
- **Worse than reported.** The fence did not merely expire nothing. It expired leases owned by whatever
  string the runner id happened to be, and a runner id is client-supplied and administrator-chosen. So
  registering a runner under an id equal to another principal's name turned that runner's revocation
  into an expiry of the **victim's** in-flight leases. A fix that expired by principal *and* id would
  have kept that primitive, which is why the fence now expires by the bound principal only, and why an
  unbound row fences nothing rather than falling back to the id. Both behaviours are now pinned by test.
- **A passing test agreed with the bug.** `Revoking_a_runner_fences_the_in_flight_run_it_leases`
  already existed and exercised the fence, but seeded the lease with owner `"runner-1"`, the runner
  *id*, which is not what the runner API writes. The fixture encoded the same wrong assumption as the
  code, so the test passed throughout. It now binds a principal and leases as that principal. Worth
  generalising: "prefer a test that exercises the control" is not sufficient on its own when the
  fixture is free to restate the defect.
- **Deliberately out of scope.** `ReleaseAsync` is not binding-checked. Refusing a revoked runner's
  release would strand the lease on a runner trying to hand the work back, which is the same reasoning
  that exempts release from quota metering. A test pins this so it is not "fixed" later.

### P0-3 · DIV · `TB-1` · `$ref` loader reaches `file://` and `http://` inside the control plane

> **Resolved.** The finding held on verification, and the preferred option was confirmed lossless rather
> than assumed. See the threat model's [findings ledger](../reference/threat-model.md#12-findings-ledger)
> (H2). The two options turned out not to be alternatives — see *One option per caller* below.

- **Where:** `ArazzoGenerationDriver.cs:353-376`, via `OpenApiSourceGenerator.cs:86` and `ExternalReferenceResolver.cs:411-465`
- **Divergence:** the in-memory package registry is the intended source. It is implemented as a *shortcut* with fall-through to `File.ReadAllBytes(uri.LocalPath)` and a bare `new HttpClient().GetByteArrayAsync(uri)`, with no scheme allowlist, host or private-range fencing, size limit or redirect policy, and a 100 second sync-over-async block. It runs in the control-plane process at catalog-add on an attacker-supplied package. ADR 0052's deferral of SSRF fencing covers the *source fetch* surface, not this loader.
- **Impact:** `UO-7`, `UO-5`. A `$self` of `http://169.254.169.254/…` retrieves the control plane's instance credentials, and `file:///etc/` reads local files and mounted secrets. Content surfaces through generated models and `buildError`.
- **Acceptance criteria:** restrict to the registry, which is preferred because packages are self-contained per `WorkflowExecutorProvider.cs:222-249`, or add a scheme allowlist, private-range and rebinding fencing, a size cap and a redirect policy. Test that a `$ref` naming a non-registry URI fails closed.
- **One option per caller, not two alternatives.** The loader has exactly two entry points and they want
  opposite things. `WorkflowExecutorProvider` registers the Arazzo document, every declared
  `sourceDescription` and every extra schema document in memory, and *already refuses* packages that are
  not self-contained (an `arazzo`-type cross-document source is skipped as "not self-contained", and a
  declared source missing from the package is skipped). So on that path everything legitimately
  resolvable is registered, and any fall-through is by construction reaching outside the package:
  registry-only is lossless there, verified rather than assumed. `ArazzoGenerateCommand` (the CLI) is the
  other entry point, and local **and remote** resolution is load-bearing for it — `ArazzoLockFile` records
  each resolved source's digest and re-resolves them to decide whether a regeneration can be skipped
  (#871). Applying "restrict to the registry" globally would have broken that.
- **The class defect was that policy was implicit.** Passing `registeredDocuments` only ever *added*
  sources; it never restricted them, so any embedder calling `GenerateAsync` with in-memory documents
  silently inherited file and `http(s)` reach. The fix is an explicit `ArazzoDocumentResolution` stated
  per caller, with `RegisteredOnly = 0` so `default` is the closed value and forgetting fails closed with
  a build error naming the unresolvable reference. That is deliberately the inverse of the P0-5 trap,
  where `default(struct)` zeroes the limits and switches the control off; a test asserts the zero value is
  named `RegisteredOnly` so a later reordering of the members cannot invert it. The control-plane call
  site names the policy explicitly as well as relying on the default, because a safe default protects
  callers that do not exist yet while an explicit statement protects this one from the default changing.
- **A test that asserted the outcome instead of the behaviour.** The first version of the network test
  asserted only that the loader returned `null` for a metadata address. It **passed with the fix
  reverted**, because a loader that does attempt the fetch also returns `null` once the request fails —
  betrayed only by the 100 seconds it spent blocked in the sync-over-async call. It now asserts that no
  connection is opened, observed by a loopback listener, and fails in both directions as intended. Worth
  generalising alongside the P0-2 note: an assertion is only evidence if the defect and the fix produce
  *different observables*.
- **Raised separately, not folded in.** After this change the retrieval path is reachable only from the
  CLI, where it still uses a bare `new HttpClient()` with auto-redirect on, no scheme allowlist, no size
  cap and a 100 second sync-over-async block. That is out of this finding's scope (which is the
  control-plane process) and cannot simply be deleted because of #871, but it is the same shape one host
  removed, against a CI runner that often carries cloud identity. `SourceDocumentFetcher` already
  implements redirect-safe, origin-checked fetching and is the port-don't-rebuild candidate.

### P0-4 · DIV · `TB-1` · Three codegen sites bypass the escaping convention

> **Resolved on the escaping; the identifier pattern deliberately not done.** The finding held, and
> undercounted the sites — there are six, not three. See *Six sites, not three* and *Why no pattern*
> below, and the threat model's [findings ledger](../reference/threat-model.md#12-findings-ledger) (H3).
- **Where:** `WorkflowExecutorEmitter.cs:1326, 1328, 1333`; correct usage at `:1433`; helper at `EmitText.cs:21-53`
- **Divergence:** every other emission site routes attacker-controlled strings through `EmitText.Quote`. These three append `workflowId` raw into an open C# string literal. `Arazzo11.json:116-120` declares it as a bare `type: string` with no pattern, the analyzer checks uniqueness and length only, and catalog-add performs no metaschema validation.
- **Impact, stated precisely:** `UO-4`, `UO-6`. On the **non-durable** emission this is clean remote code execution. On the **durable** emission, which every construction site here uses, it degrades to a compile failure, because `workflowId` is emitted twice with an intervening newline inside a ternary, forcing an even quote count. What prevents live execution on the shipped wiring is an incidental structural property of the emitted text, not a control. A refactor to single-line emission makes it live and nothing would catch it.
- **Acceptance criteria:** wrap all three in `EmitText.Quote`; add a charset pattern to the metaschema and an analyzer check; test that a `workflowId` containing a quote and a newline produces a compilable, semantically inert executor.
- **Six sites, not three.** The three named sites append `workflowId` into an open C# string literal.
  Three more (`:1228`, `:1271`, `:1435`) append it into generated `///` XML documentation comments,
  where a newline ends the comment and the remainder is compiled as code. Those are a clean breakout
  in **both** emission modes: the ternary-and-newline accident that made the durable literal sites
  merely a compile failure does not apply to them. They admit a second, quieter failure too, since a
  bare `<` makes the comment badly formed XML, a diagnostic in every consuming project that documents
  its public API. Fixed with a new `EmitText.XmlDocText`.
- **How the extra sites were found, which is the transferable part.** Not by inspection. A sweep tuned
  to the audit's shape (an emitted quote followed by a raw append) found exactly three and agreed with
  the audit, which is precisely why it looked like confirmation. That sweep pattern had *already* been
  wrong once, an earlier version finding only one of the three known sites, and matching the expected
  count is what stopped the search. What found the other three was an assertion that the payload must
  not appear as code anywhere in the output, which does not care which syntactic context the injection
  lives in.
- **Why no pattern.** The Arazzo 1.1 reference schema is not ours to constrain, so the metaschema is
  out. The semantic analyzer is ours, but it shares a single production call site with the metaschema
  pass (`ArazzoControlPlaneWorkspaceHandler.CollectDiagnosticsAsync`), reached only from the designer's
  validate endpoint and its publish gate. `POST /catalog` runs neither, so a pattern in either place
  would constrain designer-authored documents and not uploaded packages, which is the surface this
  finding is about. A control that reads as defence in depth while covering the wrong path is worse
  than a recorded gap, so it is recorded in the threat model instead. The substantive fix is to
  validate the document at catalog-add at all, raised separately as a compatibility decision.
- **Tested by parsing, after three heuristics failed.** Substring assertions flagged safe placements
  (the payload inside a comment, a `<` inside a literal). A balanced-quote heuristic missed a payload
  that closes and reopens a literal to restore an even count. The suite now parses the emitted source
  with Roslyn and requires zero syntax errors, which is the property the criteria name, plus a
  structural check for the doc-comment breakout, since that one produces *valid* C# containing an
  attacker's type and parsing alone cannot see it. Eight of twelve rows fail with the fix reverted.

### P0-5 · DIV · `TB-1` · YAML alias-expansion limits are declared, documented, and never read

> **Resolved.** The finding held, and understated it: the billion-laughs document does not merely expand,
> it raises `OutOfMemoryException` in under seven seconds from roughly 400 bytes of YAML. Two acceptance
> details were corrected against the code, see *Corrections* below. Threat model
> [findings ledger](../reference/threat-model.md#12-findings-ledger) (H6).
- **Where:** `YamlReaderOptions.cs:30-31, 57, 63`; expansion at `YamlToJsonConverter.cs:3993-4001, 4080-4104, 4258-4272`; `docs/Yaml.md:178, 594`
- **Divergence:** `MaxAliasExpansionDepth` and `MaxAliasExpansionSize` appear in exactly four places, all inside their own options file. No parser reads them, the matching error resources (`Strings.resx:92, 95`) are never thrown, and `docs/Yaml.md` presents the protection as an advantage over YamlDotNet.
- **Impact:** `UO-8`. Roughly 300 bytes expands to gigabytes on any fetched or uploaded source. The 16 MiB fetch cap bounds input, not amplified output.
- **Trap:** every call site passes `YamlReaderOptions options = default`, and `default(struct)` skips the parameterless constructor, so both limits read as `0`. Reading the option naively either no-ops or throws on the first alias. **Fix the default-initialisation path in the same change.**
- **Acceptance criteria:** enforce both during expansion; treat `0` as "use the documented default"; test the classic billion-laughs document for a clean `InvalidDataException`.
- **Measured, not inferred.** Before the fix: billion-laughs with defaults, `OutOfMemoryException` after
  6.9s; with `MaxAliasExpansionSize = 64` explicitly set, `OutOfMemoryException` after 3.8s, which is
  what proves the option was unread rather than merely mis-tuned; with `default(YamlReaderOptions)`,
  `OutOfMemoryException` after 3.6s. After: all refused promptly. Four of six rows fail with the guard
  reverted, each for its own reason.
- **Corrections against the code, which wins.** (1) The criterion names `InvalidDataException`;
  `YamlException` is this converter's convention and is what `docs/Yaml.md` already promises, so that is
  what is thrown and asserted. (2) The size limit was documented as a count of *nodes* in three places.
  The observed failure is memory exhaustion, and a node count does not bound memory — a million short
  scalars and a million long ones differ by orders of magnitude. It is enforced on expanded **bytes**,
  and the XML doc, the resource string and `docs/Yaml.md` were corrected to say so. Enforcing bytes
  while documenting nodes would have been a fresh instance of this very finding.
- **The trap was subtler than "someone forgot".** Three of the five options on `YamlReaderOptions` are
  enums whose documented default is the first member, so value `0`, so `default(struct)` lands on them
  correctly. The zero-value discipline *was* applied. It breaks only on the two `int` limits, where `0`
  cannot be made to mean `64` — so the struct looks safe by construction and for most of its surface is.
  Both limits now resolve an unset value to the documented default at the point of use, which is the only
  place that can be right when every call site passes `options = default`.
- **The depth limit is a distinct control, not a spare.** There is already a parse-time nesting guard,
  but an alias is written as pre-serialized bytes, so the depth an expansion adds to the OUTPUT never
  passes through it: a shallow document can emit arbitrarily deep JSON. Depth is measured once per anchor
  at capture and carried in the anchor entry, so a heavily referenced alias costs one comparison rather
  than a rescan.
- **Charged at the resolution point, not the call sites.** Both limits are applied inside
  `TryGetAnchorData`, which every expansion passes through whether the alias appeared as a value or as a
  mapping key. Charging at the two call sites instead would have been the mitigation-on-one-of-two-
  sibling-paths pattern this audit keeps finding.

### P0-6 · DIV · `TB-2` · `ControlPlaneSecurityMode` has a default in two places

> **Resolved, and it was three places rather than two.** The sweep this item calls for also surfaced a
> separate and more serious permissive default outside the security-mode type: see *What the sweep
> found* below. Threat model [§7.2](../reference/threat-model.md#7-control-inventory).
- **Where:** `ArazzoControlPlaneEnvironmentsHandler.cs:85`, whose parameter defaults to `Open`; `samples/.../Program.cs:412`, whose config binding defaults to `false`
- **Divergence:** ADR 0016 exists specifically to eliminate insecure-by-omission and states there is no default. Two places reintroduce one. The handler default is latent because the single production call site passes it explicitly, but a second caller omitting it makes ADR 0065's tenancy gate return `Admitted` unconditionally.
- **Acceptance criteria:** remove the parameter default; make the host config binding required with no fallback, failing startup when absent.
- **A third site in the same file.** `ArazzoControlPlaneEnvironmentsHandler` also has a *public*
  constructor documented as building an unscoped handler, which chained into the internal one and
  inherited the default silently. Its behaviour is correct — System reach is what it exists for — but the
  posture was implicit at a public API boundary, so it now names `Open` explicitly.
- **`= default` would have been an inert fix.** The first attempt at "remove the parameter default"
  replaced `= ControlPlaneSecurityMode.Open` with `= default`. `Open` is the enum's **zero value**, so
  that changes nothing, builds green, and reads as a fix in the diff. For a security posture there is no
  safe default value, only a required choice: the parameter is now leading and required. Compare P0-3,
  where the zero value was *chosen* to be the closed one, and P0-5, where zero meant "off" and had to be
  reinterpreted at the point of use. Same language feature, three different correct answers.
- **The demo binds explicitly rather than defaulting.** `GetValue("...", false)` became a required read
  that throws when unset, with an `appsettings.json` stating `false` for the standalone demo. The
  behaviour is unchanged for both topologies; what changed is that a lost environment variable or a
  mistyped key now fails startup instead of silently serving the control plane unauthenticated.
- **What the sweep found, which is not a security-mode default at all.**
  `Corvus.Text.Json.Validator.JsonSchema.Options` defaults `allowFileSystemAndHttpResolution` to
  **true**, and nothing in `src/` or `samples/` sets it. `ArazzoControlPlaneCatalogHandler` compiles a
  schema built from the workflow's own inputs schema — copied **verbatim** out of the tenant's package by
  `WriteValidationWrapper`'s `subSchema.WriteTo` — through that default, which registers a
  `FileSystemDocumentResolver` and an `HttpClientDocumentResolver`. So an authored `$ref` of
  `file:///…` or `http://169.254.169.254/…` is fetched by the control plane at run-start validation.
  That is H2's impact on a path P0-3 did not touch: P0-3 fixed the Arazzo *document* loader in one
  assembly, and this is the JSON Schema *document resolver* in another, reached from the same upload.
  All three control-plane compilation sites are now confined. Fixed in its own commit, with tests that
  pin the mechanism in both directions.
- **One of those sites was also an availability dependency.** `InputsMetaSchema` compiles a literal
  `$ref` to `https://json-schema.org/…`. The built-in metaschema resolver is registered first and so
  answers it today, but nothing guaranteed that ordering — leaving the HTTP resolver registered put a
  live fetch of a third-party CDN one resolver-ordering change away from every host's startup path.

### P0-7 · DIV · `TB-5` · Checkpoint save is a blind write of the reach-critical index
- **Where:** `ArazzoRunnerCheckpointsHandler.cs:138, 143`; `WorkflowCheckpointCoordinator.cs:142`; `PostgresWorkflowStateStore.cs:166-172, 184-208`; `InMemoryWorkflowStateStore.cs:59`
- **Divergence:** ADR 0065's model is *mutual* distrust, so the control plane distrusts the runner for integrity. In practice the index is projected **from the runner's own submitted bytes** and stored verbatim. Every backend then overwrites `environment`, `workflow_id`, `status` and the security tags from that projection, deleting and re-inserting the tag rows. Nothing compares any of it to what the row already held.
- **Impact:** `UO-2`, `UO-1`. A runner rewrites its own run into another owner group's environment and reach, releases the lease, and the victim's runner claims and executes attacker-authored state with the victim's credentials against the victim's sources. The same primitive re-tags a run into the platform group, or hides it from its owner. ADR 0065's answer is the runner MAC over the region, which is phase B, so an interim server-side check is required.
- **Acceptance criteria:** on save, validate the submitted index against the stored row. Environment, workflow id and security tags are not runner-mutable for an existing run; only status, cursor and wait fields are. Test that a save carrying a different `environment` is refused.
- **Uniform absence, which decides the placement.** Checked across AzureStorage, Cosmos, Mongo, MySql,
  NatsJetStream, Postgres, Redis, SqlServer, Sqlite and the in-memory store: zero comparisons of the
  submitted index against the stored row. Had it been partial this would be a per-backend conformance
  problem like H12's pushdown, needing the same check written and then policed nine times. Because the
  absence is uniform the check belongs **above** the store, in `WorkflowCheckpointCoordinator.SaveAsync`,
  where it applies once, cannot drift between backends, and covers both checkpoint-authoring surfaces.
- **The warm path does not re-read the row, which is the trap in this one.** `SaveAsync` loads the store
  only when the slot is cold; on the ordinary load-then-save path the slot was seeded by `LoadAsync`, so
  the stored identity is not in hand at save time. The identity is therefore captured when the slot is
  **seeded**, in both places that seed it. Re-reading per save would have been the obvious fix and would
  have put a store round trip on the hottest surface in the system. A test covers the warm path
  specifically, because a fix that only worked on cold slots would pass the obvious test while covering
  the path an attacker is least likely to be on.
- **`Rejected` is a new outcome rather than a reuse of `Conflict` or `Superseded`.** Those two are
  ordinary races a healthy writer retries. This is a write no honest writer produces, so folding it into
  `Conflict` would tell a runner to retry an attack and would make the one event worth alerting on
  indistinguishable from routine lease churn.
- **A fourth test asserts an ordinary advance still applies.** Status, cursor and timings are exactly
  what a runner is trusted to report; without that test, "refuse everything" passes the other three.

---

## 4. P1, divergences phase B would inherit

### P1-1 · DIV · `TB-5` · Lease epoch is fielded and contract-published but never compared
- **Where:** `RunnerLeaseToken.cs:23, 37-44`; `RunnerRunCoordinator.cs:229, 242, 264, 294`; `MonotonicRunnerLeaseEpochSource.cs:32, 36`; `WorkflowLease.cs:22`; `arazzo-runner.openapi.json` `LeaseGrant.epoch`
- **Divergence:** ADR 0065 §6 specifies two asymmetric epoch rules. The token is `"{epoch}.{storeToken}"` plaintext with no MAC, renewal parses the client's epoch and echoes it back, the lease check and release discard it, and no backend persists it. The OpenAPI contract publishes both refusal rules with no implementation.
- **Two defects, fix both:** nothing compares the epoch; and the **mint** is a process-global `Interlocked.Increment` that ignores `runId`, seeded from wall-clock milliseconds, so a restart after a burst re-issues spent epochs and two instances order only by NTP skew. Even a correct comparison cannot carry the property on this token shape.
- **Acceptance criteria:** per-run monotonic epoch persisted server-side; an authenticated token so a client cannot assert one; both §6 rules enforced, with tests for above-grant and below-high-water.
- **Blocks:** SEQ-2. The tenant anchor is meaningless without this.

### P1-2 · DIV · `TB-4` · Reach pushdown is self-attested, and four of nine backends filter in process
- **Where:** `RowSecurityFilter.cs:28` (default interface implementation) and `:41-47` (the guard); `MongoWorkflowStateStore.cs:431-435`; `RedisWorkflowStateStore.cs:418-419, 466-489`; `NatsJetStreamWorkflowStateStore.cs:381-397`; Azure Storage `:461, 555`
- **Divergence:** `SupportsRowSecurityFilter => true` is a **default interface implementation**, so carrying the marker *is* the proof, and `RowSecurityPushdown.EnsureSupported` validates a self-attestation. Postgres, SQL Server, MySQL, SQLite and Cosmos genuinely push down. Mongo, Redis, Azure Storage and NATS fetch across every tenant and discard in process while declaring the marker.
- **Worst case:** Mongo drops the server-side `Limit` *only when a reach filter is present*, so an unrestricted caller gets a bounded query and a tenant-scoped caller gets an unbounded, unprojected cursor over the whole collection. The comment shows this is deliberate, since it preserves keyset paging correctness under an in-process filter, and it does not acknowledge the consequence.
- **Amplifier:** the capacity guard calls `CountAsync` on every admission (`StoreControlPlaneCapacityGuard.cs:70, 84, 96`), turning a cross-tenant scan from per-list into per-run-start.
- **Acceptance criteria:** remove the default implementation; add a conformance test that **proves** pushdown, asserting bounded work or an observable query shape under a reach filter rather than asserting the marker; non-compliant backends return `false` and fail closed.
- **Do not spend effort on injection here.** It was checked hard and is absent across all nine.

### P1-3 · DIV · `TB-1`, `TB-8` · Content hash and compiled bytes are different bytes
- **Where:** `CatalogPackage.cs:117-137, 236-244`; `JsonCanonicalizer.cs:270-297`
- **Divergence:** ADR 0031 specifies the hash over the RFC 8785 canonical form. The code hashes canonical but passes the **raw** bytes to `BuildExecutor` and `PackPooled`. Numbers funnel through `TryGetDouble`, which silently rounds.
- **Impact:** `UO-6`. Two documents sharing a canonical form share one hash and version identity while being different compiler inputs. `EnsureContentHash` compares *canonical* hashes, so a divergent-raw package passes the only immutability guard and replaces the stored definition in place.
- **Acceptance criteria:** hash and compile the same bytes; additionally recompute the content hash on the in-process IL read path, since `LoaderHostedWorkflowResolver.cs:79` trusts the stored column while the AOT path at `WorkflowAotBuildService.cs:150` does it correctly.

### P1-4 · DIV · `TB-7`, `TB-10` · Credential `baseUrl` is a constraint on one path and a destination on another
- **Where:** `HttpClientTransport.cs:346`; `SourceCredentialTransports.cs:157-162`; `SourceDocumentFetcher.cs:169-172, 177-181, 226-231`; `ArazzoControlPlaneCredentialsHandler.cs:372-373`
- **Divergence:** ADR 0048's stated property is that control-plane compromise yields references, never usable credentials. The fetch path honours it by treating `baseUrl` as a host constraint. The run path treats it as the destination, since `resolvedBaseUrlOverride ?? httpClient.BaseAddress` lets the binding win. Neither `baseUrl` nor `secretRefs` is validated on write.
- **Impact:** `UO-5`. A `credentials:write` holder who cannot read the secret redirects it and sets `secretRefs` to `env://` or `file:///`, and the runner resolves its own host's secrets.
- **Second, independent defect:** run-path clients are bare `new HttpClient()` with `AllowAutoRedirect` at the .NET default of on. .NET strips only the typed `Authorization` header cross-host, so custom API-key headers survive and a TLS client certificate is presented to the redirect target. The mechanism is documented in-repo at `SourceDocumentFetcher.cs:177-181` and was applied to one of the two paths.
- **Acceptance criteria:** validate `baseUrl` scheme and host, and the `secretRefs` scheme, against an allowlist on write; set `AllowAutoRedirect = false` on every run-path client and follow redirects manually with the same per-hop origin and scheme checks the fetcher uses.

### P1-5 · DIV · `TB-2` · Self-elevation guard inspects the wrong verbs, and `security:*` has no reach plane
- **Where:** `ArazzoControlPlaneSecurityHandler.cs:729-753` (decision at `:737`) and `:95-355`; `PersistentRowSecurityPolicy.cs:178-213, 398-402`; `AccessRequestApprovalService.cs:527-555`; `ArazzoControlPlaneAccessRequestsHandler.cs:303-332`
- **Divergence:** ADR 0014 claims the guard makes direct authoring safe. It fires only on **write or purge** and never inspects `draft.Read` or `draft.Scopes`. None of the `security:*` handlers construct an `AccessContext`, so `security:read` enumerates every tenant's rules and bindings. ADR 0010's reach ceiling is pinned by **rule name** without checking the expression. `grant` and `settle` carry no own-request check, unlike `approve`.
- **Impact:** `UO-3`. One call yields unrestricted cross-tenant read plus capability scopes the IdP never issued, audited as ordinary authoring.
- **Acceptance criteria:** extend the guard to read reach and to `scopes`; construct an `AccessContext` on the security endpoints; validate the rule expression; add the independent-decision check to `grant` and `settle`.

### P1-6 · DIV · all boundaries · Audit actor is a display name, and there is no tenant dimension
- **Where:** `GovernanceAudit.cs:41-65`; `PrincipalDisplayName.cs:14, 18-21`; `AccessRequestApprovalService.cs` (zero audit sites); `DefaultDeploymentBootstrap.cs:58, 74`
- **Divergence:** ADR 0038 states the audited actor is the authenticated principal. Nine of thirteen handlers record the OIDC name claim, falling back to the literal `system` or `control-plane`, so every service principal collapses into one identity. Three incompatible derivations coexist. The primitive has **no tenant or environment parameter**, and the decisions counter is dimensioned by action and outcome only.
- **Also unaudited:** run start takes no actor; the bootstrap genesis grant emits nothing; the approval *service* writes bindings with no audit; self-elevation records as an ordinary `access-request.submit`.
- **Acceptance criteria:** canonical subject rather than display name on every record; tenant and environment as first-class parameters; audit run start with an actor; audit the bootstrap grant; distinguish self-elevation in the outcome vocabulary.
- **Depends on:** GAP-6 for the durability half. This is the attribution half and lands first.

### P1-7 · DIV · `TB-2`, `TB-4` · Run id key and grammar do not match ADR 0065 §9
- **Where:** `SecuredWorkflowManagement.cs:74-86, 96-122`; `PostgresWorkflowStateStore.cs:149, 172` and every backend; `arazzo-runner.openapi.json` `RunId`
- **Divergence:** the spec is an `(environmentId, runId)` primary key and exactly 32 lowercase hex characters validated at every ingress. Every store keys by `run_id` alone, and the contract declares `minLength 1, maxLength 256`. The idempotent id is `SHA256(workflowId ‖ 0x00 ‖ idempotencyKey)`, unkeyed and omitting owner group and environment, and `StartIdempotentAsync` swallows `WorkflowConflictException` and returns success.
- **Impact:** `UO-2`. Offline id computation, the same business key colliding across environments, and a pre-created id making a victim's legitimate start return 200 and never execute.
- **Acceptance criteria:** composite key in every backend; the 32-hex grammar at every ingress; derive the idempotent id over `(ownerGroup, environment, workflowId, idempotencyKey)` under a keyed MAC; return a distinguishable result on collision.
- **Blocks:** ADR 0065 §9's anti-replay property and the tombstone design.

### P1-8 · DIV · `TB-5` · Sequence validation validates a client-authored number
- **Where:** `WorkflowCheckpointCoordinator.cs:87-89, 126-128, 134-138`; `WorkflowCheckpointSerializer.cs:102, 466`; `ArazzoRunnerCheckpointsHandler.cs:118`
- **Divergence:** ADR 0065 §6 says the server *validates* the proposed sequence as `persisted + 1`. It re-seeds `LastAppliedSequence` from **the body of the previously stored checkpoint**, and the header `X-Arazzo-Checkpoint-Seq` and the body's `sequence` are never compared. `TryProjectIndex` does not require the property.
- **Impact:** `UO-2`. Omit `sequence` from every body and the accepted value is always 1, giving unlimited in-place rewrite. Write `long.MaxValue` and the next re-seed makes `accepted` negative against a `minimum: 1` header, bricking the run permanently for every writer including recovery.
- **Acceptance criteria:** require `sequence` in the projection; compare header and body; reject a mismatch; test both the omission and the overflow case.

### P1-9 · DIV · `TB-2`, `TB-5` · Quota and capacity counters collapse cross-tenant
- **Where:** `RunnerQuotaGate.cs:54-55`; `RunnerAuthorizationBindings.cs:145-155`; `TokenBucketRunnerQuotaGuard.cs:14-20, 31, 132-135`; `StoreControlPlaneCapacityGuard.cs:84, 88-104`; `ControlPlaneRowSecurity.cs:485-489`
- **Divergence:** ADR 0066 specifies per-tenant quotas. Buckets key on `resolved.Tenant`, read from the environment record's owner-group tag, which is `null` for every runner when environments carry none, putting **every tenant on one counter**. `buckets.Clear()` at 4096 counters forgives every tenant's deficit at once. Capacity counts pass the caller's `AccessContext`, so in `ScopesOnly` and `Open` they count the whole deployment.
- **Impact:** `UO-8`. One tenant exhausts the shared rate, or trips a deployment-wide cap that refuses starts for everyone. Each admission also costs up to three bounded counts walking up to `limit` rows.
- **Acceptance criteria:** fail closed, or refuse to start, when no owner-group claim is configured in a multi-tenant posture; evict per counter rather than wholesale; scope capacity counts by owner group independently of reach.

### P1-10 · DIV · `TB-6` · Micro-guest sidecar surfaces are unauthenticated
- **Where:** `arazzo-microguest-sidecar/src/main.rs:18-19, 28`; `src/lib.rs:226, 309-326, 396-416`; `MicroGuestDeployer.cs:111`
- **Divergence:** the design's boundary is the micro-guest and artifacts are signed and verified, but the sidecar, which actually boots the image, authenticates neither surface and verifies nothing about the initrd, since verification is entirely caller-side. The guest surface binds `0.0.0.0`, `GET /guest/{id}` returns the invocation including the checkpoint token with deterministic sandbox ids, and `POST` accepts an arbitrary outcome from any peer.
- **Acceptance criteria:** authenticate both surfaces; scope the guest read to the invoking sandbox; verify the artifact signature sidecar-side.

### P1-11 · DIV · `TB-6` · Azure Functions invoke is anonymous
- **Where:** `AotHostAppAssembler.cs:204`; `ServerlessInvocationHandler.cs:132-136`
- **Divergence:** ADR 0059 §4 promised identity-based invoke when the Azure target landed. It landed with `AuthorizationLevel.Anonymous`. Lambda does this correctly via `AWS_IAM`.
- **Acceptance criteria:** identity-based invoke; validate `checkpointUrl` against the environment's expected control-plane host rather than accepting any absolute URI.

### P1-12 · DIV · `TB-5` · Heartbeat reaper has no caller
- **Where:** `IRunnerRegistry.PruneAsync` (`IRunnerRegistry.cs:226`), implemented across twelve backends and called nowhere
- **Divergence:** ADR 0029's liveness path implies stale runners are reaped. Nothing invokes it, so dead runners persist and keep satisfying the fail-closed `IsVersionHostedAsync`, `IsDraftRunsHostedAsync` and `IsSchedulingHostedAsync` gates.
- **Acceptance criteria:** call it from a hosted service on a configured interval; emit a counter and an audit event on prune; act on `HeartbeatAsync` returning `false`, the runner-unknown desync signal that is currently ignored.

### P1-13 · DIV · `TB-2` · Empty administrator identity administers everything
- **Where:** `SecurityTagSet.cs:484-489`; `SecuredWorkflowCatalog.cs:530-553, 578-586`; `WorkflowIdentity.cs:50-64`
- **Divergence:** ADR 0007 says an administrator identity cannot be squatted once established. `IsSubsetOf` returns `true` for an empty left set and the implicit version-1 path has no guard, though the explicit API paths do (`ArazzoControlPlaneAdministratorsHandler.cs:142, 336`; `ArazzoControlPlaneEnvironmentsHandler.cs:228`). The first mutation then *persists* the empty identity as sole administrator.
- **Related:** the administrator identity retains author-supplied user tags, though `WorkflowAdministrators.cs:15-17` asserts they are never author-supplied.
- **Acceptance criteria:** refuse an empty administrator identity on the derived path; exclude author-supplied tags from the identity.

### P1-14 · DIV · `TB-2` · Policy refresh has no scheduler, so early revocation does not propagate
- **Where:** `PersistentRowSecurityPolicy.cs:37, 86-139, 144`
- **Divergence:** the class doc recommends polling for multi-process freshness and nothing implements it. Only the in-process security handler and approval service call `RefreshAsync`. On a multi-replica control plane a revocation removes the row on one replica while others honour the deleted binding indefinitely. Time-boxed grants still expire, so this hits precisely the incident-response action.
- **Acceptance criteria:** a hosted service polling on a bounded interval, or store-side change notification; assert revocation latency in a two-instance test.

### P1-15 · DIV · `TB-9` · Directory search fails open on the default source
- **Where:** `ArazzoControlPlaneIdentityHandler.cs:100-104, 157-164, 203-226`
- **Divergence:** `merged` is the default whenever a directory is configured, and it catches `PrincipalDirectoryException` and substitutes an empty list, while the all-kinds sweep swallows per-kind failures. The explicit `source=directory` path fails closed, and the asymmetry is the finding.
- **Impact:** an operator authors a grant against a stale or observed-only identity believing the directory answered.
- **Acceptance criteria:** fail closed on the default path, or return a partial-result indicator the UI surfaces; log the swallowed exception.

---

## 5. SEQ, designed and not yet built

Not defects. Listed so the backlog is complete and the P1 dependencies are visible.

| ID | Item | Spec | Depends on |
|---|---|---|---|
| SEQ-1 | Envelope and payload split, unified MAC over runner region and payload ciphertext hash | ADR 0065 §4 | P1-3, P1-7 |
| SEQ-2 | Tenant anchor store, acceptance predicate, open decision table | ADR 0065 §6 | **P1-1**, P1-7 |
| SEQ-3 | Envelope encryption, per-operation derived data key, four-label derivation | ADR 0065 §5 | pure functions already conformance-tested |
| SEQ-4 | Blind wait and correlation indexes | ADR 0065 §4 | backends with atomic row-plus-index CAS |
| SEQ-5 | Initiator sealing and signature, runner-side input-schema validation | ADR 0065 §9 | P1-7 |
| SEQ-6 | Runner allowlist of environment, seal-key fingerprint and sealed flag, plus minimum generation | ADR 0065 §10 | |
| SEQ-7 | Re-key sweep and generation retirement | ADR 0065 §12 | SEQ-3 |
| SEQ-8 | Phase C, tenant countersignature of the executor | ADR 0065 residues | |

**Backend prerequisite.** ADR 0065 makes two capabilities conformance requirements for a sealed
environment: expiring leases by principal, and a single atomic row-plus-index CAS. Only the
in-memory, Postgres and SQLite stores implement the first. Combined with P1-2, the set of
sealed-environment-capable backends is materially smaller than the nine-backend surface suggests, and
should be stated in the catalog rather than discovered during phase B.

---

## 6. GAP, missing features that no ADR covers

**Decide before implementing.** An agent should not invent policy for any of these.

### GAP-1 · `TB-3` · Browser security headers and a CSP strategy
No ADR covers HTTP security headers, and no header middleware exists, so there is no CSP,
`frame-ancestors`, `X-Frame-Options`, `nosniff`, `Referrer-Policy` or HSTS. The console and designer
are framable, so a framed click on Revoke or Approve is a governance mutation audited with the victim
as actor. **The decision interacts with ADR 0041**, standards-only and zero-build: served pages carry
large inline module scripts and all 86 components inject a `<style>` block into their shadow root at
runtime, so a CSP added today needs `'unsafe-inline'`. **Decide:** a nonce or hash pass at serve time,
a UI build step contradicting ADR 0041, or an explicit accepted risk. The cost grows with every
component added.

### GAP-2 · `TB-3` · Session hardening and revocable logout
ADR 0042 assigns session ownership to the host but specifies no properties. Missing: a `Secure`
cookie policy, since it defaults to `SameAsRequest` with no `UseForwardedHeaders` and so travels in
plaintext behind a TLS proxy; HSTS; and a server-side ticket store, without which `SignOutAsync`
deletes the cookie in the responding browser only, so a captured ticket outlives sign-out and "sign
out everywhere" is unachievable. Also missing: an open-redirect guard on the login return.

### GAP-3 · `TB-2` · Rate limiting on the governance and browser-facing API
ADR 0066 scopes rate and capacity limiting to the **runner** API. Nothing limits governance or
browser-facing endpoints, so grantee directory search is an unthrottled enumeration oracle against
the real IdP, `POST /sources/fetch` is an unthrottled SSRF scanner, and the simulate endpoints are
unthrottled compute. **Decide:** per-principal and per-tenant limits, and whether ADR 0066 extends or
a sibling ADR covers the governance plane.

### GAP-4 · `TB-7` · Egress policy as a product feature
ADR 0052 delegates SSRF fencing to deployment egress controls, a legitimate decision that leaves the
platform unable to *express* or *verify* the control, and three of four execution backends have none.
The micro-guest is the only one with containment, and its allowlist is host-granular with ports
stripped, so it permits every TCP port on each allowed host including the runner host. **Decide:** a
per-environment egress allowlist as first-class configuration, enforced by backends that can and
declared unsupported by those that cannot; and whether the micro-guest policy becomes host and port.
This is accepted risk `AR-15` and assumption `ASU-3` in the threat model.

### GAP-5 · `TB-7` · Execution resource governance
No ADR bounds a run's resource consumption. Confirmed absent: a per-run step budget, a wall clock,
sub-workflow recursion depth in production (`MaxSubWorkflowDepth = 8` is enforced only in the draft
recorder and the test tracer, and production returns `null`), a response size cap, a step-call
`HttpClient.Timeout`, and a `retryAfter` ceiling. **This is the gap with third-party consequences:** a
mutual-`goto` pair issues a real request to a configured source every iteration forever, turning the
operator's runner fleet into a sustained flood against someone else's API. The simulator is
fuel-bounded at 256 steps and production is not. **Decide:** the budget model, whether fuel, wall
clock or both, where it is configured, and the fault classification when exceeded.

### GAP-6 · all boundaries · Audit as evidence
ADR 0038 deliberately scopes the audit primitive to payload-safety and says nothing about durability.
There is no audit store type in the repository. The audit is an `ILogger` call plus an activity,
self-documented as best-effort observability rather than a durable store. Three ways it evaporates:
the span rides a sampled activity source; the log is at information level, so raising the level to
warning loses everything; and the logger is null-conditional. **Decide:** a durable append-only sink
with retention and tamper-evidence, separated from the operational store, plus a startup assertion
that a sink is attached. The codebase already ships an ECDSA signing stack.

### GAP-7 · `TB-2`, `TB-4` · Read-side audit
`GovernanceAudit` exposes only `Mutation`. No read, list, query or search audit exists, so the
highest-value event, one tenant's principal reading another's data, produces no record. On the four
in-process-filtering backends from P1-2 the cross-tenant rows are physically read on every query and
leave no trace at the store either. **Decide:** which read surfaces are audited and at what
granularity. `SensitiveReadAudit` on the step journal is a good model.

### GAP-8 · `TB-2` · Authentication event telemetry
Neither successful nor failed authentication is recorded anywhere, so brute force and credential
stuffing are undetectable by construction. Authorization denials on read paths are likewise
unrecorded, which matters more than usual in a deliberately non-disclosing system, since ADR 0004
makes probing quiet by design and nothing records the probe.

### GAP-9 · `TB-1` · Input constraints on the control-plane contract
Across **1,237** generated control-plane model files there is **not one** pattern validator, and path
parameters are bare strings. This is the root cause of several findings rather than a finding itself:
it is why `workflowId` reaches the emitter unconstrained (P0-4), why `runtimeIdentifier` reaches
MSBuild unconstrained (P3-2), and why user-controlled values reach log output unbounded. **Decide:** a
house rule that identifier-shaped contract fields carry `pattern` and `maxLength`, applied at the
contract rather than in handlers.

### GAP-10 · `TB-3` · Disclosure tier for draft and debug runs
ADR 0013 anchors sensitivity to a **catalog version**. A draft has none, so
`IsOutputsSensitiveVersionAsync` returns false and no redaction runs, yet ADR 0045 requires debug runs
to execute against real sources with real credentials, making them the highest-value output surface.
The trace endpoint returns full request and response bodies under `workspace:read`, below the
`runs:outputs:read` tier. The draft runner also writes raw exception text into a readable fault field.
**Decide:** how sensitivity is expressed for an artefact with no version, whether inherited from the
working copy, from the environment, or defaulted to sensitive.

### GAP-11 · `TB-4` · Interim protector key rotation
Distinct from SEQ-3. Whatever protects checkpoints *before* phase B needs a key id in the wire format.
Today's is nonce, tag and ciphertext with none, so generations cannot coexist and rotation is
destructive, meaning the key is never rotated and one compromise is permanent. The AAD is also the run
id alone rather than the specified four-tuple, and the decorator is opt-in and silent when unset.
**Decide:** add a key id and make encryption non-optional in a multi-tenant posture, or accept the
phase-A window explicitly.

### GAP-12 · `TB-7` · Broker subject grammar validation
A channel address parameter resolved from a runtime expression is interpolated into the broker subject
with only C#-literal escaping, so there is no grammar validation and no wildcard rejection. A wildcard
subscribes across every tenant on a shared broker and persists into the durable wait. No ADR covers
channel address construction. **Decide:** the permitted grammar per broker family.

### GAP-13 · `TB-9` · Directory identity derivation
Membership expansion folds group **names** into the identity under subset matching, so creating a
group in the IdP strictly widens what a principal matches, closed only by IdP policy (assumption
`ASU-2`). Attribute resolution is first-match on one path and last-match on another, so a
user-writable attribute colliding on leaf name can supply the tenant. The span projection path does
not enforce the issuer tag, and the grant path writes subject-only bindings. **Decide:** whether
identity derives from stable group ids rather than names, and pin one attribute-resolution rule.

### GAP-14 · `TB-9` · Directory transport assertions
LDAP transport security of `None` with simple bind is constructible and sends the bind DN and password
in clear, and no HTTP adapter asserts an https base URL. Defaults are safe and the guard is absent.

### GAP-15 · `TB-8` · Deploy resource ownership
Sanitised deploy names are non-injective and the deployer updates function code with no ownership, tag
or provenance check, so one tenant's deploy can replace another's. **Decide:** an ownership tag checked
before update, and an injective naming scheme.

### GAP-16 · `TB-6` · Guest entropy after snapshot restore
Snapshot restore replays the guest CSPRNG, so every advance draws the same sequence, giving identical
GUIDs and nonces. ADR 0064 names entropy replay a ship-blocker for the init/run split and notes the
current model shares the property in weaker form. **Decide:** a reseed hook on restore.

---

## 7. PROC, repository and CI

| ID | Item | State |
|---|---|---|
| PROC-1 | **Dependabot is inert.** Configured for `directory: /Solutions`, which does not exist, since solution files are at the root. No npm or GitHub Actions ecosystem entry | Fix the path, add ecosystems |
| PROC-2 | No SAST, CodeQL or equivalent, in any workflow | Add |
| PROC-3 | No dependency vulnerability scanning. No `NuGetAudit`, no `dotnet list package --vulnerable`, no `npm audit` | Add |
| PROC-4 | No lock files for the current tree, and no `RestoreLockedMode` | Add |
| PROC-5 | No `SECURITY.md` or disclosure policy | Add |
| PROC-6 | **ADRs do not record implementation status**, so a reader credits designed-but-unbuilt barriers. The root cause of the DIV class | Add a status line per ADR |
| PROC-7 | The threat model is committed at `docs/arazzo/reference/threat-model.md`. Wire its update triggers into the review checklist | Wire the triggers |
| PROC-8 | `control-plane-observability-coverage.md` claims verification against the handlers, points at an anchor that no longer exists, and omits five emitted actions | Regenerate or gate in CI |
| PROC-9 | Vendored CodeMirror has no version pin, no integrity hash and no rebuild-diff, and three sample UIs are in no CI workflow | Pin and verify |
| PROC-10 | Dependency auto-merge is scoped to `Endjin.*` and `Corvus.*`, a first-party self-trust path, narrow but real | Review policy |

---

## 8. P3, hardening

- **P3-1 · Build container confinement.** `podman run` with no `--network`, `--user`, `--read-only`, `--cap-drop`, `--pids-limit`, `--memory` or `--cpus`, against a read-write host bind-mount, and both Dockerfiles root. No package source mapping or lock file, with a private feed mixed with the public one. No build timeout, and the lease heartbeat masks a hung build stalling the queue.
- **P3-2 · `runtimeIdentifier` interpolated raw into generated MSBuild XML** with no pattern or enum. See GAP-9.
- **P3-3 · `javascript:` URI XSS** in the catalog owner link and two siblings, since `escapeHtml` does not validate URL schemes.
- **P3-4 · `allowReserved` path parameters** skip percent-encoding, so `../` escapes a gateway prefix with the credential attached.
- **P3-5 · Bootstrap idempotency.** A re-run re-creates deliberately deleted bindings and, if the genesis group changes, appends a second unrestricted grant. Neither path is audited.
- **P3-6 · Lambda redeploy never refreshes function environment.** Azure does this correctly.
- **P3-7 · Secrets in unscrubbable strings** throughout the provider layer, despite `SecretMaterial` warning about exactly this.
- **P3-8 · TLV integer overflow** defeats the length guard, and `OverflowException` escapes the validate catch filter.
- **P3-9 · Unbounded assembly-load-context growth.** ADR 0024 promises unload-on-obsolete and no wiring exists.
- **P3-10 · `/ui` serves the whole kit directory**, including `package-lock.json` and test suites naming every privileged selector.
- **P3-11 · Sample source services have no authentication**, the real containment ceiling for a compromised session or runner.
- **P3-12 · Static JSONPath cache is unbounded and process-wide**, poisonable from a response body and shared across tenants.
- **P3-13 · `InMemoryWorkflowStateStore` mints predictable lease tokens**, a public type in the shipping assembly and the store-conformance reference.

---

## 9. Order of work

1. **P0-1 to P0-7.** All small. Each restores a control the design already claims.
2. **P1-1, P1-2, P1-3, P1-7, P1-8.** The divergences phase B builds on, and the precondition for SEQ.
3. **P1-4, P1-5, P1-6, P1-9.** Credential steering, the authorization guard, audit attribution, quota isolation.
4. **GAP-5, GAP-6, GAP-7, GAP-8.** Decide, then build. GAP-5 has third-party abuse consequences and should not wait.
5. **PROC-1 to PROC-7.** About a day, and PROC-1 means no dependency updates reach the repository at all today.
6. **P1-10 to P1-15**, then **SEQ-1 to SEQ-8** in the ADR's own sequencing.
7. **GAP-1, GAP-2, GAP-3.** Browser and API hardening, gated on the ADR 0041 decision.
8. **P3** as capacity allows.

---

## 10. Notes for the implementing agent

- **Do not treat SEQ items as defects** and do not improvise a partial implementation. They have normative specs, so build to those.
- **Do not invent policy for GAP items.** Each needs an ADR first. Several interact: GAP-1 with ADR 0041, and GAP-6 with ADR 0038's payload-safety guarantee, which any change must preserve rather than bypass.
- **Prefer controls a test can exercise over controls a marker can declare.** Four divergences here, the pushdown marker (P1-2), the YAML limits (P0-5), the epoch (P1-1) and `PruneAsync` (P1-12), would each have been caught by one test asserting the control *does something* rather than that it exists. When closing any item, add that assertion, not just the code.
- **Sweep for siblings.** The most productive pattern in this audit was a mitigation applied to one of two sibling paths: redirects fixed on the fetch path but not the run path; reach pushdown real on five backends and in process on four; the lease check on the runner API but not its control-plane twin; the empty-identity guard on the explicit path but not the derived one; the disclosure tier on one of three routes to the same data; the fail-closed directory path but not the default one. **When you fix any item, grep for its siblings before closing it.**
- **Update the threat model, not just the code.** Closing an item changes a control's state in threat model §7, and may change a residual in §6 or an accepted risk in §11. The model is the standing artefact and this document is a snapshot.
- **Verify before acting** on anything you cannot see directly in the code. One reported finding in this audit was refuted on verification and dropped.
