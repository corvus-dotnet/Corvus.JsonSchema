# Identity membership campaign — one identity, membership everywhere

**Status: RATIFIED (design decision), IN PROGRESS (slice 1).** Branch `arazzo-workflow-designer`.
Pre-ship, so no data migration.

## The decision

Authorization in the control plane resolves against **one canonical `sys:` identity**, and every facet
matches it by **membership (superset)**, not by set-equality and not against raw OIDC claims:

> A principal **administers / reaches / may-use** a target if the principal's stamped canonical identity
> **contains** the target's named identity (the founder identity, the binding's named dimension, the
> credential's usage tags). "Contains" = the named tag set is a **subset** of the principal's identity.

This **supersedes §16.5.4's set-equality rule** ("a superset or partial match is not an administrator").
Set-equality was belt-and-suspenders against a *coarse* founder over-granting (`tenant=acme` = the whole
tenant); the resolved-grantee model (§16.5.4) already prevents coarse founders by resolving to the exact
grain, so membership + resolved-grantee founders holds together and is the correct model for group/team
authorization ("you are in this group" is the right test to administer it too).

### Why (the collision that forced it)

Reach wants the identity **rich** (many dimensions, to match many grants by membership); set-equality
administration wants it **exact** (to equal a named founder). One identity cannot be both. Concretely: the
demo stamps a group-form identity `{sys:group, sys:iss}` so group administration set-equals its founder;
access-request grants key on `sub`, matched today against the raw `sub` claim. Unifying reach onto the
group-form identity regresses `sub` grants; enriching the identity with `sys:sub` regresses group
administration under set-equality. Membership over a rich identity resolves both: the admin *contains*
`{sys:group=arazzo-admins, sys:iss}` (administers) and alice *contains* `sys:sub=alice` (her grant matches).

## What changes

1. **Rich stamped identity.** The deployment resolver stamps the full identity a principal carries
   (`{sys:group…, sys:sub, sys:iss}`), not a single dimension. Demo: map `groups`→`sys:group` **and**
   `sub`→`sys:sub`, plus the issuer tag.
2. **Reach matcher** (`PersistentRowSecurityPolicy.Matches`): a binding applies if the caller's canonical
   identity (`GetInternalTags`, ambient-stamped, prefix-stripped) **contains** the binding's named dimension
   = value (membership). Wildcard `*` unchanged. Not raw claims.
3. **Self-elevation guard** (`ArazzoControlPlaneSecurityHandler.CallerMatches`): same membership over the
   caller's canonical identity.
4. **Administration** (`WorkflowAdministrators` / `EnvironmentAdministrators` `IsAdministeredBy`,
   `WorkflowIdentity.SameAdministrator`): the forward check becomes **founder ⊆ candidate** (subset), not
   set-equality.
5. **Administration reverse index** (`ListAdministeredWorkflowsAsync`, the digest reverse-index):
   "what does this identity administer" must look up **every non-empty subset-digest of the caller's
   identity** (bounded, 2^k−1 for k tags, k small), not just the caller's full digest. The stored index is
   unchanged (founder-digest keyed); only the query fans out over subset-digests. Across in-memory + 9
   backends.
6. **Identity-collision probe** (`FindIdentityConflictAsync`): re-examine "conflict" under membership (a
   grant whose named identity is a subset/superset of another's is no longer necessarily a distinct grantee).
7. **Seed**: genesis binding keyed on the `group` **dimension**, not the `groups` OIDC claim (demo config
   `identityClaimType: groups` → `group`).
8. **Overview** (`BindingAppliesToGrantee`): already membership-over-dimension; verify no change needed.
9. **Tests**: migrate the ~75 raw-claim-contract binding tests to provide a canonical identity (resolver),
   and re-verify #96(ii) admin + the collision probe under membership.

## Slices (per-piece, test-first, gated)

- **S1 — core + reach — DONE** (commits `4d97510`, `bb3962e`). `SecurityTagSet.IsSubsetOf`; both
  `IsAdministeredBy` forward checks → subset; `PersistentRowSecurityPolicy.Matches` + the self-elevation
  guard → membership over the canonical identity (not raw claims); genesis seed re-keyed `groups`→`group`
  dimension; ~75 raw-claim-contract tests migrated to a resolver; server 242/242, durability 450/450.
- **S2 — workflow reverse-index membership — DONE** (commit `ff0cccb`). `SecurityIdentityDigest.SubsetDigests`;
  `IWorkflowAdministratorStore.ListAdministeredAsync(IReadOnlyList<string> adminDigests, …)`; one native
  DISTINCT-union query per backend across all 10 stores; membership conformance test. In-memory + Sqlite
  runtime-verified; 8 container backends build-verified, container conformance pending.
- **S3 — environment reverse-index membership — DONE** (this slice). The byte-for-byte twin of S2 on the
  environment stack: `SecuredEnvironmentAdministration` reads `SecurityIdentityDigest.SubsetDigests`;
  `IEnvironmentAdministratorStore.ListAdministeredAsync(IReadOnlyList<string> adminDigests, …)`; one native
  DISTINCT-union query per backend across all 10 environment stores; a `subset-of-a-richer-caller` membership
  conformance test; benchmark migrated to the subset-digest query. In-memory + Sqlite runtime-verified
  (16/16 each); 8 container backends build-verified, container conformance pending. This closes H1.
- **S4 — mutation-gate membership — DONE** (this slice). The add/remove/transfer authorization for both workflows
  (`SecuredWorkflowCatalog`) and environments (`SecuredEnvironmentAdministration`) now routes through a membership
  predicate `IsAdministeredByMember` (caller's identity CONTAINS a current administrator's identity) instead of
  `WorkflowIdentity.SameAdministrator` set-equality, matching the forward publish check, the reverse index, and the
  overview `administers`. The identity operations stay exact set-equality (`IsMember` for add-idempotency + `Dedupe`;
  `IndexOfDigest` for digest removal; the collision probe) — those are correctly exact. Also caught + fixed while
  here: the publish gate's version-1 fallback (`CheckAdministrationAsync`, the no-explicit-store / legacy path) was
  still `SameAdministrator` while its explicit-record sibling was already membership — now both membership, closing
  an S1 completeness gap. This closes H2 and part of H8 (the `SameAdministrator` doc-comment). Repro tests:
  `A_caller_whose_identity_contains_an_administrator_may_change_administration` (workflow + environment) + a
  transfer variant, plus `Adding_a_more_specific_identity_…_is_a_genuine_addition` guarding idempotency-stays-exact;
  15/15 in-memory administration tests green.
- **S5a — overview prefix fix (H6) + rule-eval hardening (H4) — DONE** (this slice). H6: the access-grants overview
  strips the deployment-configured internal prefix (new public `ControlPlaneRowSecurityPolicy.StripInternalPrefix` +
  `ControlPlaneAccess.StripInternalPrefix`, threaded into `BindingAppliesToGrantee`) instead of a hardcoded `sys:`;
  it is a latent divergence today (no shipping policy overrides the prefix, so it is always `sys:`), so the fix
  future-proofs a non-`sys:` deployment. H4: documented (design §16.5.5) that reach-rule evaluation reads the claim
  map, so every reach-relevant dimension must be ambient-provided or resolver-stamped, never a raw token claim — the
  review's stated remediation is documentation, and this is it.
- **S5b — collision-probe subset-overlap warning (H5) — DEFERRED, needs a decision.** Unlike its S5a siblings this is
  a full API-first + 10-backend slice: `FindIdentityConflictAsync` is an O(1) digest-indexed exact-collision probe
  (no scan, by design). A subset-overlap warning is bidirectional and asymmetric — "new grant CONTAINS an existing
  grantee" is tractable via a `SubsetDigests` lookup against the existing digest index, but "new grant is CONTAINED BY
  an existing grantee" (the review's narrow-directory-mapper example) needs superset enumeration, which is unbounded
  and requires a scan the store design forbids. Which direction(s) to detect, and how to surface the warning (a new
  response field), is a design decision to take before implementing.
- **S6 — demo resolver enrichment + relaunch**, live-verify; then container conformance for the 8 backends.

## Current state — every identity-matching surface

Verified by code audit after S1+S2. "Membership" = the caller's canonical identity **contains** the named
identity (subset). "Set-equality" = exact, order-independent. "Per-tag" = the identity contains one named
`dimension=value`. Role is **authorization** ("may this caller do X") or an **identity operation** ("act on
the entry whose identity is exactly X").

| # | Surface | Predicate | Role | Consistent? |
|---|---------|-----------|------|-------------|
| 1 | Reach binding selection (`PersistentRowSecurityPolicy.Matches`) | per-tag over the stamped `sys:` identity | authz | membership ✓ |
| 2 | Reach rule evaluation (`ResolveReach`/`CollectClaims`) | rule over **raw claims** + ambient (ambient authoritative) | authz | see review |
| 3 | Workflow admin — forward (`WorkflowAdministrators.IsAdministeredBy`) | subset | authz | membership ✓ |
| 4 | Workflow admin — reverse index (`ListAdministeredAsync`) | subset-digest union | authz-support | membership ✓ |
| 5 | Workflow admin — mutation gate (`SecuredWorkflowCatalog.IsMember`, L602) | **set-equality** | authz | **asymmetric** |
| 6 | Environment admin — forward (`EnvironmentAdministrators.IsAdministeredBy`) | subset | authz | membership ✓ |
| 7 | Environment admin — reverse index (`SecuredEnvironmentAdministration`, L66) | **single exact digest** | authz-support | **asymmetric** |
| 8 | Environment admin — mutation gate (`SecuredEnvironmentAdministration.IsMember`, L318) | **set-equality** | authz | **asymmetric** |
| 9 | Credential usage (`SourceCredentialBinding.IsUsableBy`) | per-tag (`usageTags ⊆ runTags`) | authz | membership ✓ |
| 10 | Self-elevation guard (`CallerMatches`) | per-tag over the stamped identity | authz | membership ✓ |
| 11 | Identity collision probe (`FindIdentityConflictAsync`) | exact digest | uniqueness | by design; see review |
| 12 | Access-grants overview (`BindingAppliesToGrantee`/administers/usage) | per-tag / subset-digest / per-tag | reporting | agrees with 1/4/9; see review |
| 13 | Reach binding grain (`SecurityBindingDocument`) | single `claimType` + optional `claimValue` | shape | **cannot pin a multi-tag identity** |

## Antagonistic review — holes, asymmetries, open issues

Ranked by severity. Each is a verified code observation, not a hypothesis.

**H1 — Environment reverse-index was still exact-digest (asymmetry, medium) — RESOLVED by S3.** Previously
`SecuredEnvironmentAdministration` computed a **single** whole-identity digest (`SecurityIdentityDigest.Compute`)
and queried `IEnvironmentAdministratorStore.ListAdministeredAsync(string)`, while the environment *forward*
check (`IsAdministeredBy`, #6) and the *workflow* reverse index (#4) were both membership. Effect: an approver
whose identity strictly contained the founder identity could approve promotions (forward authz passed) but the
environment never appeared in their inbox. S3 made the read side membership — `SubsetDigests` +
`ListAdministeredAsync(IReadOnlyList<string>)` across all 10 environment stores — so the reverse index now
matches the forward check. The paging-helper XML doc claim that "the index can never miss a match" is true again.

**H2 — Administration mutation gates were set-equality (asymmetry, medium) — RESOLVED by S4.** Previously the
add/remove/transfer authorization for both workflows (`SecuredWorkflowCatalog`) and environments
(`SecuredEnvironmentAdministration`) went through `WorkflowIdentity.SameAdministrator` = set-equality, while the
forward publish check, the reverse index, and the access-grants overview `administers` were all membership. Effect:
a caller whose identity was a strict superset of a stored admin identity was shown and indexed as administering the
workflow (and could publish versions), yet was denied managing its administrator set — the console over-promised
relative to the gate. S4 routes both mutation gates through a `IsAdministeredByMember` membership predicate (caller
CONTAINS a current administrator's identity), matching the read side, and additionally fixed the publish gate's
version-1 fallback (`CheckAdministrationAsync`), which was still set-equality where its explicit-record sibling was
already membership. The exact-digest paths (digest removal, `Dedupe`, add-idempotency, the collision probe) stay
set-equality — those are identity operations, correctly exact.

**H3 — Reach bindings cannot pin a multi-tag identity (structural, medium).** A `SecurityBindingDocument` is
one `claimType` + optional `claimValue`, so a per-person reach grant can only key on a single dimension
(`sub=alice`), matching **any** identity carrying `sys:sub=alice` regardless of `sys:iss`/tenant. Reach is
structurally coarser than administration (which stores the whole resolved `sys:` tag set and matches by
subset), so a `sub`-keyed reach grant is a cross-issuer / cross-tenant collision unless the deployment
guarantees `sub` is globally unique. Options: extend the binding shape to a tag-set selector; or require reach
grants to name the issuer dimension alongside the subject; or document the constraint and keep reach
group-grained. Decision needed before per-person reach grants ship.

**H4 — Reach rule evaluation trusts raw token claims (pre-existing, low-medium) — DOCUMENTED (S5a).** Binding
*selection* (#1) is on the forgery-resistant stamped identity, but a matched binding's *rule*
(`sys:tenant == $claim.tenant`) is evaluated against the raw claim map except where an ambient dimension overrides
the same name. For any dimension the deployment does not stamp as ambient, a forged token claim flows straight into
the reach predicate. Ambient closes the tenant case; the general case is a deployment responsibility, now documented
in design §16.5.5 (every reach-relevant dimension must be ambient-provided or resolver-stamped, never a raw token
claim) — the review's own stated remediation. Not a code change.

**H5 — Collision probe only catches exact collisions, not subset overlaps (membership-specific, low-medium).**
`FindIdentityConflictAsync` refuses a grant whose resolved identity **set-equals** an existing grantee's. Under
membership, a directory mapper that mints a *narrow* identity (e.g. `{sys:group=admins}`) which is a **subset**
of an existing person's identity silently confers that person's-and-everyone-in-the-group's administration, and
no conflict is raised (the digests differ). Set-equality is correct for detecting duplicate grantees; a
membership model additionally wants a **subset-overlap warning** at grant time (surface it, do not hard-block —
overlap can be intentional).

**H6 — Overview hardcoded the `sys:` prefix (divergence, low) — RESOLVED (S5a).** `BindingAppliesToGrantee`'s
`StripSysPrefix` stripped the literal `"sys:"`, whereas runtime `Matches`/`CallerMatches` strip the
deployment-configured `InternalTagPrefix`. Fixed: a public `ControlPlaneRowSecurityPolicy.StripInternalPrefix` (over
the cached UTF-8 prefix) with a `ControlPlaneAccess.StripInternalPrefix` delegator, threaded into the overview match;
a no-access host falls back to the `"sys:"` default. This is latent today — no shipping policy overrides
`InternalTagPrefix`, so the configured prefix is always `sys:` — so it future-proofs a non-`sys:` deployment rather
than fixing a live bug. Covered by a `StripInternalPrefix_uses_the_deployment_configured_prefix` unit test over a
custom-prefix policy; the existing overview suite is the no-regression lock on the `sys:` path.

**H7 — The demo does not exercise the membership generalization (coverage, low).** The demo resolver stamps
only `{sys:group, sys:iss}` (not `sys:sub`), and `DemoData.GroupIdentity` is built so a seeded grant
*set-equals* a single-group caller. So the demo's happy path keeps every identity set-equal, which means none of
the membership generalizations (the reverse index made membership by S2/S3, the mutation gates by S4) is exercised,
and Person/`sub` grants are unsatisfiable by live callers. The generalization is real but untested by the demo's
own data; S6 should enrich the identity and add a live scenario where a caller strictly contains a founder.

**H8 — mislabelled docs (docs, trivial) — RESOLVED.** `SameAdministrator`'s doc-comment called set-equality "the
membership comparison"; S4 relabelled it as the exact identity-operation comparison and pointed the authorization
gate at `IsAdministeredByMember`, and fixed the `AccessRequestApprovalService` §15-gate comment (it cited
`SameAdministrator`, but the code uses `IsAdministeredBy` membership). The `*AdministeredPaging.DistinctDigests`
docs' claim that the write-side digests "are exactly the digests the forward check compares" is now accurate in
context: the *read* side compensates with `SubsetDigests` at query time for both workflows (S2) and environments (S3).

## Gates (every slice)

Warning-free `Corvus.Text.Json.slnx`; affected tests (`TestCategory!=failing&!=outerloop&!=integration`);
container conformance for backend slices; catalog gate for any `docs/` change. Commit per-piece when green.

## Guard rails

This is the most security-sensitive code in the control plane. Membership makes the **grain of a named
identity load-bearing** (a founder named `{sys:iss}` = everyone from that issuer). The resolved-grantee
model must keep resolving founders to the exact grain; never store a coarse single-tag founder for a person.
Re-verify the #96(ii) administration fix and the self-elevation guard explicitly under the new semantics.
When adding a new identity-matching surface, classify it (authorization → membership; identity operation →
exact) and add it to the current-state table above, so no future asymmetry goes unrecorded.
