# Corvus.Text.Json.Arazzo.Directories.Google

A [Google Workspace](https://workspace.google.com/) `IPrincipalDirectory` adapter for the Arazzo
control plane (design §16.5.4): resolves people / teams / roles to their exact deployment-stamped
`sys:` identity over the **Google Admin SDK Directory API**, so an operator names a real grantee
instead of hand-assembling a `{dimension, value}` tuple.

- **Read-only.** Searches users / groups / roles with the Directory API's `query` prefix syntax;
  never writes to the directory.
- **Authentication.** A service account with **domain-wide delegation**: the adapter mints a signed
  JWT (RS256) and exchanges it for an access token impersonating an admin subject. The service
  account's private key is a `SecretRef` resolved through the deployment's `ISecretResolver` — never
  stored. The access token is fetched once and cached until just before expiry (single-flight).
- **Bytes-to-bytes identity.** When the deployment supplies a span identity mapper, the adapter
  captures only the value/label + the mapper's declared attributes as UTF-8 and builds the identity
  straight into a pooled buffer — no managed string per attribute or tag.
- **Issuer dimension.** Every resolved principal is stamped with the configured, mapper-immutable
  `sys:iss` (via `DirectoryPrincipalProjector`), so its identities are disjoint from every other
  provider's by construction.

The deployment supplies an `IDirectoryIdentityMapper` that projects a raw Google record (e.g.
`primaryEmail`, `name.fullName`, `orgUnitPath`, an `organizations[]` value, or a custom schema field)
to the `sys:` identity its runtime stamps for the same principal.