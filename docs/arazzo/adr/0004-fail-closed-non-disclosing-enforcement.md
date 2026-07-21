# ADR 0004. Fail-closed, non-disclosing enforcement

Date: 2026-07-21. Status: **Accepted**. Scope: the default behaviour of reach enforcement at every gate.
Builds on [0001](0001-two-plane-access-model.md) and [0002](0002-grant-verbs-are-reach-not-scopes.md). This
records that reach denies by default and denies silently, so a missing, empty, or not-yet-loaded policy can
never admit, and an out-of-reach row is absent rather than forbidden.

## Context

A security model fails one of two ways. It fails open, where a gap in the policy admits access, or it fails
closed, where a gap denies. The direction has to be chosen once and enforced at every gate, because the gaps
are exactly the cases nobody wrote a test for: the policy that has not loaded yet, the row nobody tagged, the
comparison against a value that was never ranked.

A second choice sits alongside it. When reach denies, does the caller learn that the row exists but is
out of their reach (a disclosing `403`), or does the row simply not appear (a non-disclosing `404`)? For
row-level policy, disclosure is itself a leak: knowing that run `r-9f2` exists, even without reading it, tells
a caller something about another tenant.

### Grounded architectural facts

- **Empty admits nothing.** `SecurityFilter.IsSatisfiedBy` (`SecurityFilter.cs`) denies by default; an empty
  rule set admits no row, and an untagged row is invisible to any scoped principal.
- **Unranked comparisons deny.** `SecurityRule.OrderedComparisonNode` (`SecurityRule.cs`) denies an ordered
  comparison (`<`, `<=`, `>`, `>=`) against a label that has no configured ordering, rather than guessing.
- **Pre-refresh denies everything.** `PersistentRowSecurityPolicy` starts at `Compiled(-1, [])`
  (`PersistentRowSecurityPolicy.cs`), so before the first policy refresh the resolver admits nothing.
- **Wildcard cannot grant unrestricted reach.** The secure default `allowWildcardUnrestrictedReach = false`
  (execution-host §17.5 / finding F7) demotes a wildcard binding's `unrestricted` verb to no-reach, so a
  `claimType = "*"` binding cannot silently hand every principal every row and dissolve tenant isolation.
- **Reach denial is non-disclosing.** The design (`docs/control-plane/access-model.md`) fixes an out-of-reach
  row as a `404`, indistinguishable from a row that does not exist, while a capability failure stays a
  disclosing `403`.

## Decision

Reach enforcement is fail-closed and non-disclosing.

- **Fail-closed.** The absence of a grant is a denial, at every gate. An empty rule set, an untagged row, an
  unranked ordered comparison, and a policy that has not yet loaded all deny. A wildcard binding cannot confer
  unrestricted reach under the secure default. Access is only ever granted by a rule that matches, never by
  the failure of one that does not.
- **Non-disclosing.** A row outside the caller's reach is returned as absent (`404` for a single row, omitted
  from a list), so its existence is not disclosed. A capability failure remains a disclosing `403`, because
  the two-plane model ([0001](0001-two-plane-access-model.md)) deliberately treats "you may not invoke this
  operation" as safe to state and "this row is not yours" as not.

## Consequences

- A deployment that misconfigures reach fails safe. The worst outcome of a missing grant is that a legitimate
  caller is denied and asks for access, never that an illegitimate caller is admitted.
- Callers cannot probe for the existence of rows outside their reach by watching for `403` versus `404`. Both
  out-of-reach and nonexistent read as `404`.
- The list and single-row paths must agree. A list omits out-of-reach rows via the pushed-down predicate
  (`SecurityFilter.ToSqlPredicate`); a single-row read gates via `AccessContext.Admits` and returns `404` on
  a miss. Neither path may leak the row.
- The system and operator cases are the explicit, named exceptions. A null reach (operator, or
  `AccessContext.System`) short-circuits to allow, and an unscoped deployment runs as `System`. These are the
  only ways enforcement admits without a matching rule, and they are named, not accidental.
