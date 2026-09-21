// <copyright file="IAuditSink.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Append-only storage for audit chains, outside the operational store (ADR 0069). A sink holds any number of chains and
/// each chain has exactly one writer, so a sink never orders appends between writers: a control plane instance opens its
/// own chain and appends to it alone. A writer's chains are kept under its id, in the order they were opened, which a
/// chain's id gives: it is a version 7 UUID.
/// </summary>
/// <remarks>
/// A sink stores the bytes it is given and nothing else. The chain, its hashes and its record shape belong to
/// <see cref="AuditChainWriter"/>, so every sink carries the same evidence and <see cref="AuditChainVerifier"/> reads any
/// of them.
/// </remarks>
public interface IAuditSink
{
    /// <summary>Creates a new, empty chain. Fails where a chain of that id already exists: a chain is never reopened.</summary>
    /// <param name="writerId">The id of the writer the chain belongs to, as UTF-8.</param>
    /// <param name="chainId">The chain's id, as UTF-8 (32 lowercase hex digits).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The chain's append stream, which the caller owns and disposes.</returns>
    ValueTask<IAuditChainStream> CreateChainAsync(ReadOnlyMemory<byte> writerId, ReadOnlyMemory<byte> chainId, CancellationToken cancellationToken);

    /// <summary>
    /// Opens the last chain a writer opened, for reading, so that a writer that starts again can say in its next chain
    /// where its last one got to. It is read and never appended to: a chain belongs to the process that opened it.
    /// </summary>
    /// <param name="writerId">The writer's id, as UTF-8.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The chain's bytes, which the caller disposes, or <see langword="null"/> where the writer has no chain.</returns>
    ValueTask<Stream?> OpenLastChainAsync(ReadOnlyMemory<byte> writerId, CancellationToken cancellationToken);
}