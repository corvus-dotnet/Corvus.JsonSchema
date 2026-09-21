# Review checklist

This is the checklist a change to the Arazzo platform is reviewed against, by its author before it is committed
and by its reviewer after. It exists because the 2026-08-07 security audit and the verification of every ADR
against the code ([audit §11](../audits/2026-08-07-security-audit.md#11-proc-6-verification-findings)) kept finding
the same few kinds of defect, and each kind has one question that would have caught it. Every question names the
finding that put it here, so a reader can see what the miss looks like in practice.

A question that does not apply to a change is skipped. A question that applies and is answered "no" blocks the
change until the answer is "yes" or the gap is written into the [threat model's findings ledger](threat-model.md#12-findings-ledger).

## 1. Does the threat model need an update?

The triggers are those of the [threat model §1.3](threat-model.md#13-what-obliges-an-update). The update lands in
the same change as the thing that triggered it.

- [ ] **An ADR is accepted or superseded.** Its claims are reconciled against the [control inventory](threat-model.md#7-control-inventory).
- [ ] **A new component, store backend, execution backend or API endpoint.** It is assigned to a
      [trust boundary](threat-model.md#2-the-system-and-its-trust-boundaries), or a boundary is added.
- [ ] **A control moves from designed to built.** Its inventory row and the residuals it affects are re-scored.
- [ ] **A phase transition.** The whole of [accepted risks and assumptions](threat-model.md#11-accepted-risks-and-assumptions) is re-scored.
- [ ] **A divergence is found.** It is added to the [findings ledger](threat-model.md#12-findings-ledger), and its
      boundary is re-checked for depth.
- [ ] **A new adversary becomes relevant.** It is added to [adversaries](threat-model.md#4-adversaries).

## 2. Does the ADR still say what the code does?

- [ ] **The implementation status line is true after this change.** Every ADR carries
      `Implementation: **...**. Verified against the code <date>.` on its status line. A change that builds,
      removes or alters what an ADR decides rewrites that line and the body in place, with a one-line revision
      note. There are no amendment sections. (PROC-6. V-16, V-39 and ADR 0064 were accepted decisions with nothing
      built, and nothing on the page said so.)
- [ ] **A barrier is credited only when it is built.** A gate that depends on another control checks that the
      control exists, not that its configuration does. (V-38: the tenancy gate admits a second owner group on a
      registered key that nothing encrypts under.)

## 3. Audit

- [ ] **Every operation that changes state records a mutation**, with `auditor.MutationAsync`, and every refusal
      of one records a refusal with its own outcome. (V-29: publishing a working copy added a catalog version and
      recorded nothing. `EveryMutationIsAuditedTests` now fails for a mutating operation that makes no audit call.)
- [ ] **The read-side question of [ADR 0070](../adr/0070-read-side-audit-three-tiers.md).** Does a new read return
      a payload, meaning step inputs or outputs, checkpoint state, a trace, or the detail of a credential? Then it
      records itself before it answers with `auditor.ReadAsync(..., failClosed: true)`, and its operation id is in
      `ReadSideAudit.Disclosures`. The full rule is in the
      [platform conventions](../guides/platform-conventions.md#adding-a-read-the-question-to-ask).
- [ ] **Every read is a `GET`**, searches and counts included. One filter tells a read from a mutation by the verb.
- [ ] **Audit parameters are vocabulary and identifiers.** No composed text, no payload, no secret. (V-33.)

## 4. The contract and the surface

- [ ] **A new endpoint is in the OpenAPI document first**, and its handler implements the generated interface. A
      surface mapped by hand is seen by no endpoint filter, so it carries its own authentication, audit and
      refusal records, and the change says why it could not be generated. (V-27.)
- [ ] **Every operation declares its scope**, and a verb marked unrestricted is justified in the change. (V-1.)
- [ ] **Every list endpoint and every store method behind it pages, and every count is bounded.** (V-23.)
- [ ] **A response that returns tags strips the reserved `sys:` tags.** (V-5.)

## 5. Identity, reach and tenancy

- [ ] **Identity comes from the authenticated principal**, never from the request body, a header the caller
      controls, or a claim the token may carry unchecked. (V-2.)
- [ ] **A check that takes a scope or an environment is given one.** A context-less overload of a reach-filtered
      call is a bypass. (V-10, V-30: the run-start isolation gate takes no environment.)
- [ ] **A new store or store method answers `SupportsRowSecurityFilter`** and ships a wire proof that the reach
      predicate reaches the backend ([ADR 0067](../adr/0067-reach-enforced-by-the-store-proven-on-the-wire.md)). (V-34.)
- [ ] **A revocation takes effect within a stated bound**, and the bound is written down. (V-22.)

## 6. Secrets and execution

- [ ] **The control plane resolves no source secret.** A new call to `ISecretResolver` outside a runner is
      justified against [ADR 0048](../adr/0048-source-credentials-are-references.md) and recorded in the threat
      model. (V-31.)
- [ ] **Free-form configuration that is persisted and returned is checked for secret values.** (V-32.)
- [ ] **An endpoint that causes code to run authenticates its caller.** This includes a deployed function's
      trigger and a sidecar's admin surface. (V-36, V-37, V-41.)
- [ ] **A version is immutable once published.** Nothing edits a published package, and what is hashed covers what
      is executed. (V-21.)

## 7. Defaults and leftovers

- [ ] **The default is the closed posture.** The zero value of a security enum, an omitted option and a null
      dependency all refuse. (V-14.)
- [ ] **No compatibility path.** Nothing is shipped, so there is no old data and no old caller. A fallback for a
      record that predates a field, a nullable dependency that is always supplied, or an overload kept for a
      caller that no longer exists is deleted, not guarded. (V-3, V-35, V-42.)
- [ ] **A security control has a test that fails when the control is removed.** Neuter the check, watch the test
      fail, restore it. A bug fix starts from a failing reproduction.

## See also

- [Threat model](threat-model.md)
- [Platform conventions](../guides/platform-conventions.md)
- [2026-08-07 security audit](../audits/2026-08-07-security-audit.md)