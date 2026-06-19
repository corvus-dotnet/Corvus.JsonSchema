# Corvus.Text.Json.Arazzo.Directory

The pluggable external-directory / IdP search seam for the Arazzo control plane (design §16.5.4).

`IPrincipalDirectory` resolves real people/teams/roles to their exact deployment-stamped `sys:` identity for the grantee picker, so an operator names a real principal instead of hand-assembling a `{dimension, value}` tuple. An adapter fetches raw `DirectoryRecord`s and projects each to a `ResolvedPrincipal` through the deployment-supplied `IDirectoryIdentityMapper`; it authenticates with a credential resolved from a `SecretRef` via the deployment's `ISecretResolver` (the §13 secret boundary — no new secret store).

Protocol adapters (LDAP/AD, Keycloak, Microsoft Entra, Okta, Google Workspace, SCIM 2.0) ship as separate packages that reference this one.
