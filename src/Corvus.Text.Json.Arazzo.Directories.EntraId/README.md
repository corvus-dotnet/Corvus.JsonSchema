# Corvus.Text.Json.Arazzo.Directories.EntraId

A [Microsoft Entra ID](https://learn.microsoft.com/entra/identity/) (Microsoft Graph)
`IPrincipalDirectory` adapter for the Arazzo control plane (design §16.5.4): resolves people /
teams / roles to their exact deployment-stamped `sys:` identity over the **Microsoft Graph API**,
so an operator names a real grantee instead of hand-assembling a `{dimension, value}` tuple.

- **Read-only.** Searches users / groups / directory roles with a Graph `$filter=startsWith(...)`
  prefix query; never writes to the directory.
- **Authentication.** An OAuth 2.0 client-credentials grant (an app registration granted the
  `User.Read.All` / `GroupMember.Read.All` / `Directory.Read.All` application permissions it needs),
  whose client secret is a `SecretRef` resolved through the deployment's `ISecretResolver` — never
  stored. The access token is fetched once and cached until just before expiry (single-flight).
- **Attribute projection.** When the deployment's `IDirectoryIdentityMapper` declares the attributes
  it reads, the adapter asks Graph for only those (`$select`), so a search neither over-fetches over
  the wire nor over-materialises on parse.
- **Bytes-to-bytes identity.** When the deployment supplies a span identity mapper, the adapter
  captures only the value/label + the mapper's declared attributes as UTF-8 and builds the identity
  straight into a pooled buffer — no managed string per attribute or tag.
- **Issuer dimension.** Every resolved principal is stamped with the configured, mapper-immutable
  `sys:iss` (via `DirectoryPrincipalProjector`), so its identities are disjoint from every other
  provider's by construction.

The deployment supplies an `IDirectoryIdentityMapper` that projects a raw Graph record (e.g.
`mailNickname`, `displayName`, `department`, an `onPremisesExtensionAttributes` value, or a directory
extension) to the `sys:` identity its runtime stamps for the same principal.