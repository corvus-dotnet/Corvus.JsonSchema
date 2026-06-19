# Corvus.Text.Json.Arazzo.Directories.Okta

An [Okta](https://www.okta.com/) `IPrincipalDirectory` adapter for the Arazzo control plane
(design §16.5.4): resolves people / teams / roles to their exact deployment-stamped `sys:` identity
over the **Okta Management API**, so an operator names a real grantee instead of hand-assembling a
`{dimension, value}` tuple.

- **Read-only.** Searches users / groups / roles with an Okta `search=profile.<attr> sw "..."`
  prefix expression; never writes to the org.
- **Authentication.** An SSWS API token presented as `Authorization: SSWS <token>`, whose value is a
  `SecretRef` resolved through the deployment's `ISecretResolver` — never stored. The token is the
  secret itself, so it is resolved at the point of each search and dropped immediately (the §13
  boundary); it is never cached.
- **Issuer dimension.** Every resolved principal is stamped with the configured, mapper-immutable
  `sys:iss` (via `DirectoryPrincipalProjector`), so its identities are disjoint from every other
  provider's by construction.

The deployment supplies an `IDirectoryIdentityMapper` that projects a raw Okta record (the user's
`profile.*` attributes — `login`, `firstName`, `lastName`, `email`, and any custom attribute such as
a tenant) to the `sys:` identity its runtime stamps for the same principal.