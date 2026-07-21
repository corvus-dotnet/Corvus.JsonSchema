# ADR 0013. Step-output disclosure tier

Date: 2026-07-21. Status: **Accepted**. Scope: reading a run's step outputs. Builds on
[0001](0001-two-plane-access-model.md) and [0004](0004-fail-closed-non-disclosing-enforcement.md). This
records why reading a run's step outputs is a stronger grant than reading the run, gated per version by an
output-sensitivity classification, and audited on every read.

## Context

Reading that a run exists and its status is one thing. Reading the step outputs it carried, which may hold
personal data, secrets a source returned, or the content of a decision, is another. A caller who may list and
monitor runs should not automatically see inside every run's outputs. Some workflows carry sensitive outputs
and some do not, so the tier has to be settable per workflow version, and because it is a disclosure boundary,
every read across it should leave a trail.

### Grounded architectural facts

- **A stronger scope gates output reads.** `runs:outputs:read`
  (`ControlPlane.Server/ControlPlaneAuthorization.cs`, `RunsOutputsRead = "runs:outputs:read"`) is required to
  read step outputs, on top of `runs:read`. It is a disclosure tier ANDed above the read scope, not a
  substitute for it.
- **Sensitivity is a per-version classification.** A catalog version carries an `OutputsSensitivity`
  (`Standard` or `Sensitive`), settable through the catalog update path
  (`ArazzoControlPlaneCatalogHandler.cs`, design §14). A `Sensitive` version's step-output payloads are
  withheld from a caller who holds `runs:read` but not `runs:outputs:read`.
- **Every read is audited.** A high-sensitivity read emits a `workflow.journal.read` span
  (`SensitiveReadAudit`, design §860), recording whether the payloads were returned in full, redacted, or
  refused.

## Decision

Reading a run's step outputs is a **separate disclosure tier**.

- It requires `runs:outputs:read`, a scope ANDed above `runs:read`. Holding read on runs does not confer
  read on their outputs.
- The boundary is set per workflow version by an `OutputsSensitivity` classification. A `Sensitive` version's
  outputs are redacted from a caller below the stronger grant, rather than the whole run being hidden.
- Every read across the boundary is audited with a `workflow.journal.read` span recording the disclosure
  outcome (full, redacted, or refused).

## Consequences

- Monitoring a run and reading its outputs are distinct rights. A dashboard role can watch run status without
  seeing inside sensitive runs.
- Redaction, not absence, is the behaviour for a sensitive run below the stronger grant. The run is still
  visible (subject to reach, [0004](0004-fail-closed-non-disclosing-enforcement.md)); its sensitive payloads
  are withheld. This differs from reach, where an out-of-reach row is absent entirely.
- The classification is authored where the workflow is governed (the catalog version), so a workflow that
  handles personal data can be marked once and every run of it inherits the tier.
- The audit trail makes each sensitive disclosure attributable, which is the governance-audit posture applied
  to reads as well as mutations.
