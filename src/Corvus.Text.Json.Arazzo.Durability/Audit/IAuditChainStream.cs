// <copyright file="IAuditChainStream.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>The append stream of one audit chain (ADR 0069). Not thread-safe: its one writer serializes its appends.</summary>
public interface IAuditChainStream : IAsyncDisposable
{
    /// <summary>
    /// Appends one record line durably. When the returned task completes the line is stored; when it throws, how much of
    /// the line was stored is unknown, and the writer abandons the chain.
    /// </summary>
    /// <param name="line">One record's UTF-8 bytes, ending in a line feed.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the line is stored.</returns>
    ValueTask AppendAsync(ReadOnlyMemory<byte> line, CancellationToken cancellationToken);
}