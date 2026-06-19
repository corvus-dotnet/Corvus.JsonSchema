# Corvus.Text.Json.Arazzo.Directory.Ldap

An LDAP / Active Directory `IPrincipalDirectory` adapter for the Arazzo control plane (design §16.5.4).

Searches a directory (AD, OpenLDAP, FreeIPA) for people/teams/roles by a value-attribute prefix and projects each entry to its deployment-stamped `sys:` identity via the supplied `IDirectoryIdentityMapper`. Binds as a read-only service account whose password is a `SecretRef` resolved through the deployment's `ISecretResolver` (the §13 secret boundary). Uses a pure-managed, cross-platform LDAP client (no native `libldap`).
