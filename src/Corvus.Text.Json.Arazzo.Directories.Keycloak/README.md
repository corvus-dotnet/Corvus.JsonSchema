# Corvus.Text.Json.Arazzo.Directories.Keycloak

A [Keycloak](https://www.keycloak.org/) `IPrincipalDirectory` adapter for the Arazzo control plane
(design §16.5.4): resolves people / teams / roles to their exact deployment-stamped `sys:` identity
over the Keycloak **Admin REST API**, so an operator names a real grantee instead of hand-assembling
a `{dimension, value}` tuple.

- **Read-only.** Searches users / groups / realm roles; never writes to Keycloak.
- **Authentication.** A client-credentials grant (a service-account client granted the
  `view-users`/`query-*` realm-management roles) or a password grant (an admin user via `admin-cli`),
  whose secret is a `SecretRef` resolved through the deployment's `ISecretResolver` — never stored.
- **Bytes-to-bytes identity.** When the deployment supplies a span identity mapper, the adapter
  captures only the value/label + the mapper's declared attributes as UTF-8 and builds the identity
  straight into a pooled buffer — no managed string per attribute or tag.
- **Issuer dimension.** Every resolved principal is stamped with the configured, mapper-immutable
  `sys:iss` (via `DirectoryPrincipalProjector`), so its identities are disjoint from every other
  provider's by construction.

The deployment supplies an `IDirectoryIdentityMapper` that projects a raw Keycloak record (username,
attributes, group/role name) to the `sys:` identity its runtime stamps for the same principal.