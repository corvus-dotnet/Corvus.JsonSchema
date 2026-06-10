// <copyright file="KmsCheckpointProtector.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;

namespace Corvus.Text.Json.Arazzo.Durability.Kms;

/// <summary>
/// An <see cref="ICheckpointProtector"/> that envelope-encrypts checkpoints using an AWS KMS key as the
/// key-encryption key: each checkpoint gets a fresh data key (AES-GCM, run id bound as additional authenticated
/// data), and that data key is wrapped/unwrapped by KMS. The data key only exists transiently in process; the
/// KMS key never leaves KMS.
/// </summary>
/// <remarks>
/// The run id is also passed as the KMS <em>encryption context</em>, binding the wrapped data key to the run at
/// the KMS layer (in addition to the AES-GCM binding). Key rotation is handled by KMS — rotated keys still
/// decrypt data wrapped under earlier key material, with no data re-encryption.
/// </remarks>
public sealed class KmsCheckpointProtector : EnvelopeCheckpointProtector
{
    private const string RunIdContextKey = "arazzo-run-id";

    private readonly IAmazonKeyManagementService kms;
    private readonly string keyId;

    /// <summary>Initializes a new instance of the <see cref="KmsCheckpointProtector"/> class.</summary>
    /// <param name="kms">An AWS KMS client (the caller owns it).</param>
    /// <param name="keyId">The KMS key id, ARN, or alias used to wrap data keys.</param>
    public KmsCheckpointProtector(IAmazonKeyManagementService kms, string keyId)
    {
        ArgumentNullException.ThrowIfNull(kms);
        ArgumentException.ThrowIfNullOrEmpty(keyId);
        this.kms = kms;
        this.keyId = keyId;
    }

    /// <inheritdoc/>
    protected override async ValueTask<ReadOnlyMemory<byte>> WrapAsync(ReadOnlyMemory<byte> dataKey, WorkflowRunId id, CancellationToken cancellationToken)
    {
        var request = new EncryptRequest
        {
            KeyId = this.keyId,
            Plaintext = new MemoryStream(dataKey.ToArray()),
            EncryptionContext = new Dictionary<string, string> { [RunIdContextKey] = id.Value },
        };
        EncryptResponse response = await this.kms.EncryptAsync(request, cancellationToken).ConfigureAwait(false);
        return response.CiphertextBlob.ToArray();
    }

    /// <inheritdoc/>
    protected override async ValueTask<ReadOnlyMemory<byte>> UnwrapAsync(ReadOnlyMemory<byte> wrappedDataKey, WorkflowRunId id, CancellationToken cancellationToken)
    {
        var request = new DecryptRequest
        {
            KeyId = this.keyId,
            CiphertextBlob = new MemoryStream(wrappedDataKey.ToArray()),
            EncryptionContext = new Dictionary<string, string> { [RunIdContextKey] = id.Value },
        };
        DecryptResponse response = await this.kms.DecryptAsync(request, cancellationToken).ConfigureAwait(false);
        return response.Plaintext.ToArray();
    }
}