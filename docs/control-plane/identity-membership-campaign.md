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

- **S1 — core (in-memory reference).** Subset predicate; `IsAdministeredBy` → subset; reach `Matches` +
  self-elevation → membership over canonical identity; rich identity; seed re-key; in-memory
  `ListAdministeredWorkflowsAsync` subset-digest fan-out; server + durability (in-memory) tests green.
- **S2 — reverse-index fan-out across the 9 backends** + conformance (the subset-digest query per backend).
- **S3 — collision-probe semantics** under membership + tests.
- **S4 — demo resolver + seed + relaunch**, live-verify admin reach + administers + `sub` grants.
- **S5 — test migration sweep** (raw-claim binding tests) + design-doc §16.5.4 decision update + catalog gate.

## Gates (every slice)

Warning-free `Corvus.Text.Json.slnx`; affected tests (`TestCategory!=failing&!=outerloop&!=integration`);
container conformance for backend slices; catalog gate for any `docs/` change. Commit per-piece when green.

## Guard rails

This is the most security-sensitive code in the control plane. Membership makes the **grain of a named
identity load-bearing** (a founder named `{sys:iss}` = everyone from that issuer). The resolved-grantee
model must keep resolving founders to the exact grain; never store a coarse single-tag founder for a person.
Re-verify the #96(ii) administration fix and the self-elevation guard explicitly under the new semantics.
