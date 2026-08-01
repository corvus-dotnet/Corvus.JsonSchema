// <copyright file="PassthroughCheckpointProtector.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The identity <see cref="ICheckpointProtector"/>: checkpoints pass to the store as-is. This is the baseline for a
/// deployment that configures no at-rest protection key — exactly the posture of an unwrapped store — while the
/// <see cref="EnvironmentCheckpointProtector"/> router above it still seals every run pinned to a sealed environment
/// (ADR 0065). It exists so sealing never depends on a deployment also opting into baseline encryption.
/// </summary>
public sealed class PassthroughCheckpointProtector : ICheckpointProtector
{
    /// <summary>The shared instance (the type is stateless).</summary>
    public static readonly PassthroughCheckpointProtector Instance = new();

    /// <inheritdoc/>
    public ValueTask<ReadOnlyMemory<byte>> ProtectAsync(ReadOnlyMemory<byte> plaintext, WorkflowRunId id, CancellationToken cancellationToken)
        => new(plaintext);

    /// <inheritdoc/>
    public ValueTask<ReadOnlyMemory<byte>> UnprotectAsync(ReadOnlyMemory<byte> ciphertext, WorkflowRunId id, CancellationToken cancellationToken)
        => new(ciphertext);
}