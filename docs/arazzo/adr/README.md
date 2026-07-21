# Architecture Decision Records

An ADR records one architectural decision. It states the context and the governing constraint, the
options that were weighed, the decision, and the consequences that follow. ADRs are append-only. A
decision that is later replaced keeps its record and is marked `Superseded by NNNN`, so the history of
why the system is shaped the way it is stays intact.

## Format

Each ADR is `NNNN-short-slug.md` and opens with a status line: a date, a status, and the scope it governs.
Status is one of `Proposed`, `Accepted`, or `Superseded by NNNN`.

The body carries these sections. An ADR that records a genuine fork (two or more real options) includes
Options and an antagonistic review that steelmans each. An ADR that records an invariant or a principle
(where the alternative was never seriously on the table) may omit them and go straight from Context to
Decision.

- **Context** states the problem and the governing constraint, grounded in facts that are cited to the
  code, not assumed.
- **Options** (when there was a fork) describe each real alternative.
- **Antagonistic review** (when there were options) steelmans the pros and cons of each.
- **Decision** states what was chosen.
- **Consequences** state what the decision commits the system to.

The template to follow for a full, fork-bearing ADR is
[`0012-approval-decision-delivery.md`](0012-approval-decision-delivery.md).

## Index

### Access model and identity (the authz slice)

| ADR | Title | Status |
|-----|-------|--------|
| [0001](0001-two-plane-access-model.md) | Two-plane access model: capability and reach | Accepted |
| [0002](0002-grant-verbs-are-reach-not-scopes.md) | Grant verbs are row-access levels, not scopes; rules conjoin, bindings union | Accepted |
| [0003](0003-membership-matching-over-canonical-identity.md) | Membership matching over one canonical `sys:` identity | Accepted |
| [0004](0004-fail-closed-non-disclosing-enforcement.md) | Fail-closed, non-disclosing enforcement | Accepted |
| 0005 | Capability entitlements union into token claims; the IdP is never mutated | *planned* |
| 0006 | The deployment access-control shell and ambient identity | *planned* |
| 0007 | Administrator is a resolved identity, digest-keyed, with a reverse index | *planned* |
| 0008 | Resolved-grantee resolution: no guessing | *planned* |
| 0009 | Eligible versus active self-elevation (PIM), and the independent-decision rule | *planned* |
| 0010 | Access requests are ceiling-bounded and subject-pinned | *planned* |
| 0011 | Approval is a strategy seam | *planned* |
| [0012](0012-approval-decision-delivery.md) | Delivering an approver's decision to a suspended run | Accepted |
| 0013 | Step-output disclosure tier | *planned* |
| 0014 | Direct-grant versus request-only, split by binding type | *planned* |
| 0015 | The access overview is server-aggregated | *planned* |
| 0016 | `ControlPlaneSecurityMode`: the two planes toggle independently | *planned* |

Later domains (engine and durability, the runner, the catalog, the web kit, platform conventions) extend
the numbering as their slices land.
