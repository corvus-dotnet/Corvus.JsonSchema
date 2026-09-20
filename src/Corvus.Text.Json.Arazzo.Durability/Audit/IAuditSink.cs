// <copyright file="IAuditSink.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Append-only storage for audit chains, outside the operational store (ADR 0069). A sink holds any number of chains and
/// each chain has exactly one writer, so a sink never orders appends between writers: a control plane instance opens its
/// own chain and appends to it alone.
/// </summary>
/// <remarks>
/// A sink stores the bytes it is given and nothing else. The chain, its hashes and its record shape belong to
/// <see cref="AuditChainWriter"/>, so every sink carries the same evidence and <see cref="AuditChainVerifier"/> reads any
/// of them.
/// </remarks>
public interface IAuditSink
{
    /// <summary>Creates a new, empty chain. Fails where a chain of that id already exists: a chain is never reopened.</summary>
    /// <param name="chainId">The chain's id, as UTF-8 (32 lowercase hex digits).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The chain's append stream, which the caller owns and disposes.</returns>
    ValueTask<IAuditChainStream> CreateChainAsync(ReadOnlyMemory<byte> chainId, CancellationToken cancellationToken);
}