# ADR 0015. The access overview is server-aggregated

Date: 2026-07-21. Status: **Accepted**. Scope: how the "who can do what, where" overview for one grantee is
computed. Builds on [0001](0001-two-plane-access-model.md), [0003](0003-membership-matching-over-canonical-identity.md),
and [0006](0006-deployment-access-control-shell.md). This records why the overview is computed on the server,
against the same identity and prefix handling that enforcement uses, rather than re-derived in the client.

## Context

An administrator needs to see, for one grantee, everything they can do and where: the capability scopes they
hold, the reach bindings that match them, the workflows and environments they administer, and the credentials
their runs may use. The value of this view is that it is correct. If the client re-derived it from raw
bindings, it could disagree with what the server actually enforces, because the client does not stamp the
internal identity, does not apply the shell, and does not resolve membership the way the server does. A view
that can disagree with enforcement is worse than no view.

### Grounded architectural facts

- **The overview is computed server-side.** `HandleGetAccessGrantsAsync` and its keyset-paged sub-resources
  (`HandleGetAccessGrantsReachAsync`, `HandleGetAccessGrantsAdministeredAsync`,
  `HandleGetAccessGrantsCredentialsAsync`, `ControlPlane.Server/ArazzoControlPlaneSecurityHandler.cs`) compute
  the grantee's capabilities, matching reach bindings, administered workflows and environments, and usable
  credentials. The client fetches the aggregate; it does not re-derive it.
- **It uses the same prefix handling as enforcement.** The overview matches a binding's clause against the
  grantee's identity by stripping the deployment's internal prefix through `ControlPlaneAccess.StripInternalPrefix`
  (`ControlPlane.Server/ControlPlaneRowSecurity.cs`), the same operation the resolver uses, so the overview's
  match is the resolver's match.
- **It resolves the grantee's internal identity the way enforcement does.** The grantee token carries the
  operator-facing (prefix-stripped) identity; the handler resolves it back to the internal `sys:` form before
  matching, so administration and usable-credential resolve against the digest enforcement keys on
  ([0003](0003-membership-matching-over-canonical-identity.md), [0006](0006-deployment-access-control-shell.md)).

## Decision

The access overview is **computed on the server**, against the same identity resolution, prefix stripping,
and membership matching that enforcement uses. The client requests the aggregate for a grantee and renders
it. It does not fetch raw bindings and re-derive the answer. Because the overview runs the same match as the
resolver, the view cannot disagree with what the server enforces.

## Consequences

- The overview is trustworthy. What it shows for a grantee is what enforcement will do for that grantee,
  because both run the same match.
- The client stays thin. It renders capabilities, reach, administration, and credential usage, and its only
  mutation is revoking a binding. It carries no copy of the resolver.
- Because the unbounded parts (reach bindings, administered workflows, usable credentials) can be large, they
  are keyset-paged sub-resources, while the bounded summary (grantee, capabilities, administered
  environments) is one call. This is the shape delivered for the paged access-overview.
- Internal `sys:` tags are stripped from the overview's output, as from any client response
  ([0006](0006-deployment-access-control-shell.md)), and re-derived server-side when the overview needs to
  match, which is precisely why the computation cannot move to the client.
