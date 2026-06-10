# Corvus.Text.Json.Arazzo.Durability.Kms

An [AWS KMS](https://docs.aws.amazon.com/kms/) `ICheckpointProtector` for
[Arazzo](https://github.com/OAI/Arazzo-Specification) workflow durability — application-level (Layer 2)
encryption at rest, independent of the backend's own at-rest encryption.

`KmsCheckpointProtector` uses **envelope encryption**: each checkpoint is encrypted with a fresh random data
key (AES-GCM, with the run id bound as additional authenticated data), and that data key is wrapped by an AWS
KMS key. The KMS key never leaves KMS; the per-checkpoint data key only exists transiently in process. The run
id is also supplied as the KMS **encryption context**, binding the wrapped data key to the run at the KMS layer.

```csharp
var protector = new KmsCheckpointProtector(
    new AmazonKeyManagementServiceClient(),
    "arn:aws:kms:eu-west-2:111122223333:key/<key-id>");

await using var store = new ProtectedWorkflowStateStore(
    await PostgresWorkflowStateStore.ConnectAsync(dataSource), protector);
// ... use as IWorkflowStateStore / IWorkflowWaitIndex.
```

**Key rotation** is handled by KMS — rotated keys still decrypt data wrapped under earlier key material, with no
data re-encryption. Only the checkpoint payload is encrypted; the projected index fields stay in the clear so
wait/visibility queries still work (see the `Corvus.Text.Json.Arazzo.Durability` README).

> The envelope framing and data-key lifecycle live in `EnvelopeCheckpointProtector` (core); this package adds
> only the KMS wrap/unwrap. Its tests run the full store-conformance suite through the protector against a real
> KMS (LocalStack).
