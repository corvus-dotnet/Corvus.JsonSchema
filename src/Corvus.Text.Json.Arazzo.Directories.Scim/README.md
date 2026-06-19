# Corvus.Text.Json.Arazzo.Directories.Scim

A [SCIM 2.0](https://datatracker.ietf.org/doc/html/rfc7644) `IPrincipalDirectory` adapter for the
Arazzo control plane (design §16.5.4): resolves people / teams / roles to their exact
deployment-stamped `sys:` identity over a SCIM 2.0 service provider, so an operator names a real
grantee instead of hand-assembling a `{dimension, value}` tuple.

- **Read-only.** Searches the configured SCIM resource types (`/Users`, `/Groups`, or a custom
  resource) with a SCIM filter (`userName sw "al"`); never writes to the provider.
- **Authentication.** A bearer token whose value is a `SecretRef` resolved through the deployment's
  `ISecretResolver` — never stored. The reference is resolved at the point of each search and the
  revealed material is dropped immediately (the §13 boundary).
- **Issuer dimension.** Every resolved principal is stamped with the configured, mapper-immutable
  `sys:iss` (via `DirectoryPrincipalProjector`), so its identities are disjoint from every other
  provider's by construction.

The deployment supplies an `IDirectoryIdentityMapper` that projects a raw SCIM record (the flattened
core + extension attributes — e.g. `userName`, `name.givenName`, `emails`, the enterprise extension's
`organization`) to the `sys:` identity its runtime stamps for the same principal.