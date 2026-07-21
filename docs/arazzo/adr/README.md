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
| [0005](0005-entitlement-scopes-union-into-claims.md) | Capability entitlements union into token claims; the IdP is never mutated | Accepted |
| [0006](0006-deployment-access-control-shell.md) | The deployment access-control shell and ambient identity | Accepted |
| [0007](0007-administrator-resolved-identity-digest-keyed.md) | Administration is a set of resolved identities, digest-keyed, with a reverse index | Accepted |
| [0008](0008-resolved-grantee-resolution.md) | Resolved-grantee resolution: no guessing | Accepted |
| [0009](0009-eligible-versus-active-self-elevation.md) | Eligible versus active self-elevation, and the independent-decision rule | Accepted |
| [0010](0010-access-requests-ceiling-bounded.md) | Access requests are ceiling-bounded and subject-pinned | Accepted |
| [0011](0011-approval-is-a-strategy-seam.md) | Approval is a strategy seam | Accepted |
| [0012](0012-approval-decision-delivery.md) | Delivering an approver's decision to a suspended run | Accepted |
| [0013](0013-step-output-disclosure-tier.md) | Step-output disclosure tier | Accepted |
| [0014](0014-direct-grant-versus-request-only.md) | Direct grant versus request-only, split by binding type | Accepted |
| [0015](0015-access-overview-server-aggregated.md) | The access overview is server-aggregated | Accepted |
| [0016](0016-control-plane-security-mode.md) | `ControlPlaneSecurityMode`: one explicit posture, no insecure default | Accepted |

Later domains (engine and durability, the runner, the catalog, the web kit, platform conventions) extend
the numbering as their slices land.
