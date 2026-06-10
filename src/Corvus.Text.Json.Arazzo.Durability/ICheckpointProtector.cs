// <copyright file="ICheckpointProtector.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Encrypts and decrypts the opaque checkpoint bytes so they can be stored unreadable to the backend
/// (application-level encryption at rest). A <see cref="ProtectedWorkflowStateStore"/> applies an
/// implementation around any <see cref="IWorkflowStateStore"/>; because every backend treats the checkpoint as
/// an opaque blob it round-trips byte-for-byte, so one protector works for all of them.
/// </summary>
/// <remarks>
/// Only the checkpoint payload is protected — the projected <see cref="WorkflowRunIndexEntry"/> fields are
/// stored in the clear so the backend can still answer wait/visibility queries. Implementations should bind the
/// ciphertext to the run (for example as additional authenticated data) so a checkpoint cannot be substituted
/// between runs, and should fail closed (throw) on any authentication/integrity failure. Implementations must
/// be safe to call concurrently.
/// </remarks>
public interface ICheckpointProtector
{
    /// <summary>Encrypts a checkpoint for storage.</summary>
    /// <param name="plaintext">The serialized checkpoint bytes.</param>
    /// <param name="id">The run the checkpoint belongs to (bind the ciphertext to it).</param>
    /// <returns>The protected bytes to hand to the store.</returns>
    ReadOnlyMemory<byte> Protect(ReadOnlySpan<byte> plaintext, WorkflowRunId id);

    /// <summary>Decrypts a checkpoint read back from storage.</summary>
    /// <param name="ciphertext">The protected bytes returned by the store.</param>
    /// <param name="id">The run the checkpoint belongs to (must match what <see cref="Protect"/> bound).</param>
    /// <returns>The original serialized checkpoint bytes.</returns>
    /// <exception cref="System.Security.Cryptography.CryptographicException">The ciphertext failed authentication (tampered, wrong key, or wrong run).</exception>
    ReadOnlyMemory<byte> Unprotect(ReadOnlySpan<byte> ciphertext, WorkflowRunId id);
}