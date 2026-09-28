# ADR 0074. Readiness is the version's usability, judged once by the server

Date: 2026-09-28. Status: **Accepted**. Implementation: **complete**. Built: one server evaluator that both promotion gates use, `listVersionReadiness` and `evaluateDraftReadiness`, the four kit surfaces and the demo mock on them, and a live test that raises a production promotion through the dialog. Scope: what "a version is ready in an environment" means, where that is decided, and how it reaches a user. It corrects a divergence found while closing GAP-1 of the 2026-08-07 security audit, and builds on [ADR 0003](0003-membership-matching-over-canonical-identity.md) and [ADR 0008](0008-resolved-grantee-resolution.md).

## Context

The glossary defines readiness as the gate on promotion: every source a version references has a *usable* credential in the target environment, and, where the environment requires evidence, the version's publish suite is green. Usability is the label-superset test a run's credential lookup applies (`IsUsableBy`): a run may use a credential when it carries every tag of the credential's usage restriction.

Three places applied a rule, and they disagreed:

- **The server's promotion gates** (making a version available, approving an availability request) counted any credential for the source in the environment, through the approver's management view. That is looser than usability. A credential restricted to another workflow, or to a group the version's publisher is not in, counted, so a promotion could be approved whose runs would then be refused their credential.
- **The web kit** (the request dialog, the availability matrix, the catalog detail, the add-workflow wizard, and the demo's mock) counted a credential only when it was unrestricted or restricted to the workflow. That is stricter. A credential restricted to the admins group never counted, so the dialog never offered production for the demo's onboarding workflow, for anyone.
- **A run** gets the credential its version's identity satisfies. A catalogued version carries its publisher's identity and `sys:workflow=<id>` (`WorkflowIdentity.VersionTags`), and its runs start with those tags (`catalogVersion.SecurityTagsValue`). Who starts the run does not enter into it.

The kit cannot compute usability. The identity tags are internal, and the API strips them from every version it returns.

## Options

**A1. The kit follows the server's gate.** Count any credential, and show who it is restricted to.

**A2. One exact rule, evaluated by the server.** The gates and the kit both use usability against the version's identity. The server exposes it per version, and for a draft of one.

**B. Judge from the requester.** Offer an environment only if the requester's identity satisfies the restriction.

**C. The server adopts the kit's rule.**

## Antagonistic review

*Against A1:* it copies the server's looseness into the UI, so both would call a version ready when its runs cannot run.

*Against B:* the requester is the wrong identity. A run uses its version's identity, not the requester's, and a promotion makes the version available to everyone in the environment. It also needs the requester's full identity in the kit, which it does not have.

*Against C:* a group-restricted credential would then never count, although the version's runs may use it. The demo seeds a production availability that the rule would forbid.

*Against A2:* it is a new API surface and a larger change. *For:* it is the only option under which the gate, the view and the run agree. The disclosure is small and bounded: whether a credential the version's runs may use exists, and the display label of who it is restricted to. The credential, its secret references and its identity tags are never returned.

*The approver's view:* A2 judges by what a run can use, not by which credentials the approver can see. An approver who cannot see a credential may approve when the version's runs can use it. Readiness is a fact about the version, the same for every caller, and the alternative, an approver seeing "not ready" for a version that would run, is a false answer.

## Decision

**A2.** One internal evaluator, `VersionReadiness`, owns the rule. For each source it asks the credential store's usage path, `ResolveForUsageAsync(source, environment, version.SecurityTagsValue)`, which returns the credential a run of the version would get. It applies the evidence rule where the environment requires it. Both promotion gates use it; their refusal names the sources with "no credential its runs may use".

Two operations expose it, under the `availability` tag:

- **`GET /catalog/{base}/versions/{v}/readiness`** (`listVersionReadiness`, `availability:read`, visible to a caller who can read the version), paged over the environments the caller can see. Each entry has `ready`, `credentialsReady`, `evidenceRequired`, `evidenceGreen`, and one entry per source: whether it is `usable` and, for a restricted credential, a `restriction` naming the grantee's kind and label.
- **`POST /catalog/{base}/readiness`** (`evaluateDraftReadiness`, `catalog:write`) for the add-workflow wizard. It takes the source names of a version the caller has not yet published and judges them against the identity publishing would give it: the caller's, and the workflow's. A draft has no evidence, so `evidenceGreen` is absent and an environment that requires evidence is not ready.

The grantee a restriction names comes from the display fields a credential records (`usageKind`, `usageLabel`). The HTTP create path records them from the resolved grantee; a credential defined in code records them through `SourceCredentialDefinition.UsageKind` and `UsageLabel`, and the demo seed names its production restriction "arazzo-admins (team)". A credential that recorded neither is reported as restricted, unnamed.

The kit presents the server's answer and never approximates it (`src/readiness.js`). The request dialog offers the environments the version is ready in and says who the chosen one's credentials are restricted to. A ready matrix cell names its restrictions. A not-ready cell says which gate refused. The wizard gates on the draft's credentials being ready, since evidence comes with publishing. The mock applies the same rule over its own stand-in identities.

## Consequences

- A promotion is approved only when the version's runs will get their credentials, and every surface that shows readiness shows the same answer.
- The demo's production credentials, restricted to the admins group, count for versions an admin published: the request dialog offers production for them, and the prod-ops live test raises its request there.
- A credential restricted to another workflow, or to a group the publisher is not in, no longer makes a version ready. A deployment that relied on the looser gate sees such promotions refused with the sources named.
- An approver may approve on the strength of a credential they cannot see. The readiness view tells them one exists, and who it is restricted to, and nothing more.
- The kit's four copies of an approximate rule are gone; a fifth surface asks `listVersionReadiness`.
