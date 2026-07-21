# ADR 0002. Grant verbs are row-access levels, not scopes; rules conjoin, bindings union

Date: 2026-07-21. Status: **Accepted**. Scope: the grant-binding and rule model.
Builds on [0001](0001-two-plane-access-model.md). This records what a grant binding grants (row reach, per
verb, never a capability scope) and how bindings and rules compose (rules conjoin within a binding, bindings
union across matches).

## Context

[0001](0001-two-plane-access-model.md) puts reach on its own plane, authored as grant bindings and rules. Two
questions then have to be settled so the model is unambiguous, both for the person authoring a grant and for
the resolver enforcing it.

- **What does a grant's `read`/`write`/`purge` mean?** It could be read as "this binding confers the
  `runs:read` capability scope", or as "this binding widens which rows the read verb may touch". These are
  different planes, and conflating them would let a row grant silently widen a capability, breaking the
  separation [0001](0001-two-plane-access-model.md) rests on.
- **How do multiple grants combine?** A principal typically matches more than one binding, and a binding
  names more than one rule. The direction of composition (does adding a rule narrow or widen, does adding a
  binding narrow or widen) has to be one fixed, memorable answer, or authors cannot predict what a change
  does.

### Grounded architectural facts

- **A verb grant is a row-reach level.** `SecurityBindingDocument.VerbGrantInfo`
  (`Durability/Security/SecurityBindingDocument.cs`) is either `unrestricted` (a null reach: the operator
  case, every row) or a set of named rules. It never carries or implies a capability scope. A binding maps
  `claim -> {read, write, purge}` row sets.
- **Rules within a binding conjoin.** `PersistentRowSecurityPolicy.VerbClauseFor`
  (`PersistentRowSecurityPolicy.cs`) joins a verb's named rule expressions with `" && "`. Adding a rule to a
  verb narrows that verb's reach.
- **Bindings across matches union.** `PersistentRowSecurityPolicy.ResolveReach` joins the matched bindings'
  clauses with `" || "`. Matching one more binding widens reach.
- **The deployment shell wraps the union.** `SecurityShell.BuildFilter` (`SecurityShell.cs`) prepends the
  deployment's mandated wrapper rules, conjoined around the whole union. A user rule can narrow within the
  shell, never widen past it (see ADR 0006).

## Decision

The verbs on a grant binding are **row-access levels on the reach plane**, not capability scopes. A binding
is `claim -> {read, write, purge}`, where each verb is either unrestricted (every row) or a set of rules.
Granting a verb never grants a capability scope, and the two are authored, stored, and enforced separately.

Composition has one fixed direction.

- **Rules conjoin within a verb.** The verb's reach is the AND of its rules. Adding a rule narrows.
- **Bindings union across matches.** The principal's reach for a verb is the OR of every matched binding's
  clause for that verb. Matching another binding widens.
- **The shell conjoins around the union.** The deployment's mandated rules AND the whole result, so a tenant
  boundary the shell imposes cannot be widened by any user grant.

An either-or intent ("this claim OR that claim") is expressed as two bindings whose reaches union, never as
an `OR` inside a rule. A must-hold-both intent is expressed as rules conjoined within one binding.

## Consequences

- Authors have a predictable model. More rules on a verb means less; more bindings means more. This is the
  rule stated in the ubiquitous-language glossary and is not to be re-litigated as an `OR` inside a rule.
- The resolver can compile a principal's reach into one predicate per verb by ANDing the shell, ORing the
  matched bindings, and ANDing each binding's rules, and push that predicate into the store
  (`SecurityFilter.ToSqlPredicate`).
- Because a verb grant is never a scope, a deployment can run reach with capability turned off, or the
  reverse, exactly as [0001](0001-two-plane-access-model.md) and `ControlPlaneSecurityMode` allow.
- A binding whose verb is `unrestricted` yields a null reach for that verb, which the resolver treats as the
  operator case and short-circuits to allow. The one exception, a wildcard binding, is constrained by
  [0004](0004-fail-closed-non-disclosing-enforcement.md).
