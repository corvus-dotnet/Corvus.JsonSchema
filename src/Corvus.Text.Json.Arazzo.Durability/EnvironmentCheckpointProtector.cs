// <copyright file="EnvironmentCheckpointProtector.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>Resolves an environment's current checkpoint seal-key registration (ADR 0065), or <see langword="null"/>
/// for an unsealed environment. The control plane implements this over the environment store; the resolver owns any
/// caching, and returning a new key id is a rotation the router follows on the next seal.</summary>
/// <param name="environment">The environment name.</param>
/// <param name="cancellationToken">A cancellation token.</param>
/// <returns>The registration, or <see langword="null"/> when the environment is unsealed (or unknown).</returns>
public delegate ValueTask<EnvironmentSealKey?> EnvironmentSealKeyResolver(string environment, CancellationToken cancellationToken);

/// <summary>An environment's checkpoint seal-key registration (ADR 0065).</summary>
/// <param name="KeyId">The key id sealed envelopes name.</param>
/// <param name="SealKey">The public seal key (P-256 <c>SubjectPublicKeyInfo</c>).</param>
public readonly record struct EnvironmentSealKey(string KeyId, ReadOnlyMemory<byte> SealKey);

/// <summary>
/// The environment-routing checkpoint protector (ADR 0065). On save, a run pinned to a sealed environment is
/// sealed to that environment's registered key (resolved through the <see cref="EnvironmentSealKeyResolver"/>;
/// rotation follows the resolver); an unsealed environment, or an unpinned run, uses the deployment's baseline
/// protector. On load, a sealed envelope names its key id: when this process holds the matching open key (a
/// runner, with the environment's key from the tenant's custody) the envelope opens; when it does not (the
/// control plane's posture — it registers no open keys) the read refuses with the key id named, because state
/// sealed to a tenant's environment is unreadable here by key custody, not by policy. Baseline-protected blobs
/// fall through to the baseline protector unchanged.
/// </summary>
public sealed class EnvironmentCheckpointProtector : IEnvironmentAwareCheckpointProtector, IDisposable
{
    private readonly EnvironmentSealKeyResolver? sealKeyResolver;
    private readonly ICheckpointProtector baseline;
    private readonly Dictionary<string, SealedCheckpointProtector> openers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SealedCheckpointProtector> sealers = new(StringComparer.Ordinal);
    private readonly Lock sealersGate = new();
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="EnvironmentCheckpointProtector"/> class.</summary>
    /// <param name="baseline">The deployment's baseline protector for unpinned runs and unsealed environments —
    /// e.g. an <see cref="AesGcmCheckpointProtector"/> over a host-local key. Owned by the caller.</param>
    /// <param name="sealKeyResolver">Resolves a sealed environment's current seal-key registration; <see langword="null"/>
    /// on a host that never seals (a pure opener).</param>
    /// <param name="openKeys">The open keys this process holds, keyed by key id — a runner registers its
    /// environments' keys from the tenant's custody; the control plane registers none.</param>
    public EnvironmentCheckpointProtector(
        ICheckpointProtector baseline,
        EnvironmentSealKeyResolver? sealKeyResolver = null,
        IEnumerable<KeyValuePair<string, ReadOnlyMemory<byte>>>? openKeys = null)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        this.baseline = baseline;
        this.sealKeyResolver = sealKeyResolver;

        if (openKeys is not null)
        {
            foreach ((string keyId, ReadOnlyMemory<byte> openKey) in openKeys)
            {
                this.openers.Add(keyId, SealedCheckpointProtector.ForOpening(keyId, openKey.Span));
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ReadOnlyMemory<byte>> ProtectAsync(ReadOnlyMemory<byte> plaintext, WorkflowRunId id, string? environment, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);

        if (string.IsNullOrEmpty(environment) || this.sealKeyResolver is not { } resolver)
        {
            return await this.baseline.ProtectAsync(plaintext, id, cancellationToken).ConfigureAwait(false);
        }

        if (await resolver(environment, cancellationToken).ConfigureAwait(false) is not { } registration)
        {
            // The environment is unsealed: the deployment's baseline posture applies.
            return await this.baseline.ProtectAsync(plaintext, id, cancellationToken).ConfigureAwait(false);
        }

        return await this.SealerFor(registration).ProtectAsync(plaintext, id, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public ValueTask<ReadOnlyMemory<byte>> ProtectAsync(ReadOnlyMemory<byte> plaintext, WorkflowRunId id, CancellationToken cancellationToken)
        => this.ProtectAsync(plaintext, id, null, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<ReadOnlyMemory<byte>> UnprotectAsync(ReadOnlyMemory<byte> ciphertext, WorkflowRunId id, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);

        if (SealedCheckpointProtector.TryReadSealedKeyId(ciphertext.Span, out string keyId))
        {
            if (this.openers.TryGetValue(keyId, out SealedCheckpointProtector? opener))
            {
                return opener.UnprotectAsync(ciphertext, id, cancellationToken);
            }

            ThrowHelper.ThrowSealedCheckpointNoOpenKey(keyId);
        }

        return this.baseline.UnprotectAsync(ciphertext, id, cancellationToken);
    }

    /// <summary>Disposes the sealed protectors this router built; the baseline protector stays with its owner.</summary>
    public void Dispose()
    {
        if (!this.disposed)
        {
            foreach (SealedCheckpointProtector opener in this.openers.Values)
            {
                opener.Dispose();
            }

            lock (this.sealersGate)
            {
                foreach (SealedCheckpointProtector sealer in this.sealers.Values)
                {
                    sealer.Dispose();
                }
            }

            this.disposed = true;
        }
    }

    // The sealer for a registration, cached by key id (a rotation resolves to a new id and builds its sealer on
    // first use; stale entries are only ever a small parked import). An opener for the same key seals too.
    private SealedCheckpointProtector SealerFor(in EnvironmentSealKey registration)
    {
        if (this.openers.TryGetValue(registration.KeyId, out SealedCheckpointProtector? opener))
        {
            return opener;
        }

        lock (this.sealersGate)
        {
            if (!this.sealers.TryGetValue(registration.KeyId, out SealedCheckpointProtector? sealer))
            {
                sealer = SealedCheckpointProtector.ForSealing(registration.KeyId, registration.SealKey.Span);
                this.sealers.Add(registration.KeyId, sealer);
            }

            return sealer;
        }
    }
}