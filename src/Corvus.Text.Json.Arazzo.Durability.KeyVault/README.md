# Corvus.Text.Json.Arazzo.Durability.KeyVault

An [Azure Key Vault](https://learn.microsoft.com/azure/key-vault/) `ICheckpointProtector` for
[Arazzo](https://github.com/OAI/Arazzo-Specification) workflow durability — application-level (Layer 2)
encryption at rest, independent of the backend's own at-rest encryption.

`KeyVaultCheckpointProtector` uses **envelope encryption**: each checkpoint is encrypted with a fresh random
data key (AES-GCM, with the run id bound as additional authenticated data), and that data key is wrapped by a
Key Vault key via the vault's `CryptographyClient`. The vault key never leaves the vault; the per-checkpoint
data key only exists transiently in process.

```csharp
var protector = new KeyVaultCheckpointProtector(
    new Uri("https://my-vault.vault.azure.net/keys/arazzo-kek/<version>"),
    new DefaultAzureCredential());

await using var store = new ProtectedWorkflowStateStore(
    await CosmosWorkflowStateStore.ConnectAsync(client), protector);
// ... use as IWorkflowStateStore / IWorkflowWaitIndex.
```

The default wrap algorithm is `RsaOaep256` (for an RSA vault key); pass another for an EC or Managed-HSM AES
key. **Key rotation** is handled by the vault — a new key version wraps fresh checkpoints while older versions
still unwrap existing ones, with no data re-encryption. Only the checkpoint payload is encrypted; the projected
index fields stay in the clear so wait/visibility queries still work (see the
`Corvus.Text.Json.Arazzo.Durability` README).

> The envelope framing and data-key lifecycle live in `EnvelopeCheckpointProtector` (core) and are covered by
> the core tests; this package adds only the vault wrap/unwrap.
