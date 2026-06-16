# Corvus.Text.Json.Arazzo.Durability.Vault

A [HashiCorp Vault](https://www.vaultproject.io/) `ISecretResolver` for Arazzo source credentials
(design §13). The runner dereferences a `vault://` secret reference to live secret material at
transport-bind time; the control plane and the Arazzo durability stores never read it.

A reference is `vault://<mount>/<path>#<field>` over a **KV v2** secrets engine — the first locator
segment is the engine mount point, the rest is the secret path, and the `#field` names which key of
the secret's data map to read (a KV secret is a key→value map). The current secret version is read.
Supply a caller-configured `IVaultClient` (Vault address + a least-privileged auth method with
read access to the path).
