// <copyright file="IEnvironmentAwareCheckpointProtector.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// An <see cref="ICheckpointProtector"/> that routes protection by the run's environment (ADR 0065): a sealed
/// environment's checkpoints are sealed to that environment's registered key, an unsealed environment's fall back
/// to the deployment's baseline protector. <see cref="ProtectedWorkflowStateStore"/> detects this capability the
/// way it detects the store capabilities: when the protector implements it, every save passes the run's
/// environment from the index projection; reads stay on the environment-less overload, because a sealed envelope
/// names the key it was sealed to and routes itself.
/// </summary>
public interface IEnvironmentAwareCheckpointProtector : ICheckpointProtector
{
    /// <summary>Encrypts a checkpoint for storage, routed by the run's environment.</summary>
    /// <param name="plaintext">The serialized checkpoint bytes.</param>
    /// <param name="id">The run the checkpoint belongs to (bind the ciphertext to it).</param>
    /// <param name="environment">The environment the run is pinned to, or <see langword="null"/> for an unpinned run
    /// (protected by the baseline protector).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The protected bytes to hand to the store.</returns>
    ValueTask<ReadOnlyMemory<byte>> ProtectAsync(ReadOnlyMemory<byte> plaintext, WorkflowRunId id, string? environment, CancellationToken cancellationToken);
}