# ADR 0016. `ControlPlaneSecurityMode`: one explicit posture, no insecure default

Date: 2026-07-21. Status: **Accepted**. Scope: how a deployment selects which security planes are enforced.
Builds on [0001](0001-two-plane-access-model.md). This records why the two planes are selected by one required,
explicit mode rather than two independently-optional flags, so a deployment can never end up open, or with
capability but no reach, by forgetting to configure something.

## Context

[0001](0001-two-plane-access-model.md) makes capability and reach independent, so a deployment can run one,
both, or neither. The question is how it chooses. The original shape was two independent booleans,
`requireAuthorization` and `rowSecurity`, each defaulting to off. The security review (finding F2) flagged the
failure mode: a deployment that forgets to set them gets an open control plane, and a deployment that turns on
authorization but forgets row security gets capability scopes with no tenant isolation, which is the
cross-tenant exposure. Both are reached by omission, which is the worst way to reach an insecure state.

### Grounded architectural facts

- **One explicit enum, no default.** `ControlPlaneSecurityMode`
  (`ControlPlane.Server/ControlPlaneSecurityMode.cs`, design §17.4) replaces the two optional flags. There is
  no default value; a deployment must name its posture.
- **Four modes over three axes** (authentication, capability-scope gating, row reach):
  - `Open`: unauthenticated and unrestricted (full `AccessContext.System` reach), for local development and
    trusted networks only, logged loudly at startup. A row-security policy must not be supplied.
  - `Scoped`: authentication, capability-scope gating, and per-row reach from a policy. The production posture.
    A policy is required.
  - `ScopesOnly`: authentication and capability gating, but unrestricted reach, for a genuinely single-tenant
    deployment. A policy must not be supplied.
  - `RowSecurityOnly`: authentication and per-row reach, but no capability gating. A policy is required.
- **The modes fail construction, not silently.** A mode that requires a policy fails if none is supplied, and
  a mode that forbids one fails if one is, so a contradictory configuration is a startup error rather than a
  silently-ignored flag.

## Options

**Two independent optional flags (the original, rejected).** `requireAuthorization` and `rowSecurity`, each
defaulting to off. Rejected: it is insecure by omission. Forgetting either yields an open or a
reach-less-but-scoped control plane, and F2 showed both are easy to reach by accident.

**One required, explicit mode (chosen).** A single enum with no default, whose value names the exact posture
and whose construction rejects a policy that does not match the mode.

## Decision

A deployment selects its security posture with one required `ControlPlaneSecurityMode`. There is no default,
so the deployment must state its posture explicitly, and it can never fall into an open or a reach-less
posture by omission. The mode's construction enforces the policy-required or policy-forbidden invariant, so an
inconsistent configuration fails at startup rather than at the first cross-tenant request.

## Consequences

- The insecure-by-omission path is closed. There is no combination of forgotten flags that yields an open or a
  scope-without-reach control plane, because there are no optional flags.
- The two planes of [0001](0001-two-plane-access-model.md) remain independent, but only along the four
  meaningful combinations the enum names. A mode that would give reach without authentication is not offered.
- `Open` is available for local development and trusted networks, but it is an explicit, loudly-logged choice,
  not a default, so it cannot ship to production unnoticed.
- The mode is per-deployment configuration alongside the shell ([0006](0006-deployment-access-control-shell.md))
  and the authentication scheme.
