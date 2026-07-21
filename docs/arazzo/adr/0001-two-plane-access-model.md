# ADR 0001. Two-plane access model: capability and reach

Date: 2026-07-21. Status: **Accepted**. Scope: the whole control-plane authorization surface. This records
why access is decided on two independent planes, capability and reach, both of which must pass, rather than
on a single permission model.

## Context

The control plane has to answer one question about every request: is this principal allowed to do this?
That question decomposes into two that are genuinely independent.

- **What may the principal do?** Invoke this operation, on this kind of resource, at all. List runs. Write a
  security binding. Purge a catalog version. This follows a person's job function and changes slowly.
- **Which rows may the principal touch?** Of the runs that exist, which are theirs to see, resume, or
  purge? Of the catalog versions, which fall inside their tenant or team? This follows data ownership and
  shifts as membership and time-boxed grants change.

Folding both into one model forces a bad trade. A pure capability model (token scopes only) cannot express
"you may read runs, but only runs tagged `tenant=acme`" without minting a scope per tenant, which does not
scale and puts row-level policy in the identity provider. A pure row model cannot cheaply express "this
surface does not exist for you at all", and it leaks the existence of operations a caller may not invoke.

The design (`docs/arazzo/specs/access/access-model.md`) frames the split as "who can do WHAT, WHERE", and the code
implements the two planes as separate mechanisms.

### Grounded architectural facts

- **Capability is token scopes.** `ControlPlaneScopes` names the coarse permissions
  (`catalog:read`, `runs:read`, `runs:write`, `security:write`, `administrators:write`, and so on) and
  `ControlPlaneAuthorization` maps each to one ASP.NET authorization policy
  (`ControlPlane.Server/ControlPlaneAuthorization.cs`). An operation declares the scope it needs; the check
  reads the space-delimited `scope` claim.
- **Reach is grant bindings and rules over row tags.** `PersistentRowSecurityPolicy.Resolve(principal)`
  produces an `AccessContext` carrying an independent `SecurityFilter?` per verb
  (`AccessContext.cs`, `PersistentRowSecurityPolicy.cs`). A list pushes the filter into the store as an
  indexed predicate (`SecurityFilter.ToSqlPredicate`); a single-row action gates through
  `AccessContext.Admits(verb, tags)`.
- **The two planes are separately switchable.** `ControlPlaneSecurityMode`
  (`ControlPlane.Server/ControlPlaneSecurityMode.cs`) has `Scoped` (both on), `ScopesOnly`,
  `RowSecurityOnly`, and `Open`. A deployment can run one plane, both, or neither, which only makes sense
  because they are independent axes.

## Options

**Single plane, scopes only.** Express everything as a capability scope. Rejected: row-level policy would
have to be encoded as scopes (one per tenant or team), which pushes fine-grained, fast-changing data policy
into the IdP and does not scale.

**Single plane, row security only.** Express everything as a row filter. Rejected: it cannot cheaply say "you
may not invoke this operation at all", and answering an operation with an empty list discloses that the
operation exists, which the two-plane model avoids for reach but keeps for capability by design.

**Two independent planes, both must pass (chosen).** Capability decides the operation, reach decides the
rows, and a request must satisfy both.

## Decision

Access is decided on two independent planes.

- **Capability** is coarse and per-operation. It is carried as token `scope` claims, checked first, and a
  failure is a disclosing `403 Forbidden`: the operation exists, you may not invoke it.
- **Reach** is fine and per-row. It is authored as grant bindings and rules over row security tags, resolved
  into a per-verb `AccessContext`, and a failure is a non-disclosing absence: an out-of-reach row is `404`,
  indistinguishable from a row that does not exist (see [0004](0004-fail-closed-non-disclosing-enforcement.md)).

A request passes only when both planes admit it. The planes are independently toggleable through
`ControlPlaneSecurityMode` so a deployment can adopt them separately.

## Consequences

- The per-verb grants on a binding (`read`/`write`/`purge`) are reach levels, never capability scopes. This
  is [0002](0002-grant-verbs-are-reach-not-scopes.md).
- Capability scopes ride the token and are resolved from claims unioned with stored entitlements, without
  mutating the IdP (ADR 0005).
- Reach is resolved against the principal's canonical, membership-expanded identity
  ([0003](0003-membership-matching-over-canonical-identity.md)) and fails closed and non-disclosing
  ([0004](0004-fail-closed-non-disclosing-enforcement.md)).
- The access overview must present both planes together for one grantee, computed server-side so the view
  cannot disagree with enforcement (ADR 0015).
