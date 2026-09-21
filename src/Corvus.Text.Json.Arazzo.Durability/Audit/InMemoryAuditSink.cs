// <copyright file="InMemoryAuditSink.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// An audit sink held in memory (ADR 0069), for tests and for hosts that only need the records for the life of the
/// process. It is not evidence: it is in the process it records.
/// </summary>
public sealed class InMemoryAuditSink : IAuditSink
{
    private readonly Lock sync = new();
    private readonly List<string> chainIds = [];
    private readonly Dictionary<string, MemoryStream> chains = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> writers = new(StringComparer.Ordinal);

    /// <summary>Gets the ids of the chains created so far, in creation order.</summary>
    public IReadOnlyList<string> ChainIds
    {
        get
        {
            lock (this.sync)
            {
                return [.. this.chainIds];
            }
        }
    }

    /// <summary>Copies a chain's stored bytes as they stand.</summary>
    /// <param name="chainId">The chain's id.</param>
    /// <returns>The chain's JSON Lines bytes.</returns>
    public byte[] Snapshot(string chainId)
    {
        lock (this.sync)
        {
            return this.chains[chainId].ToArray();
        }
    }

    /// <inheritdoc/>
    public ValueTask<IAuditChainStream> CreateChainAsync(ReadOnlyMemory<byte> writerId, ReadOnlyMemory<byte> chainId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string id = Encoding.UTF8.GetString(chainId.Span);
        var chain = new MemoryStream();
        lock (this.sync)
        {
            this.chains.Add(id, chain);
            this.chainIds.Add(id);
            this.writers.Add(id, Encoding.UTF8.GetString(writerId.Span));
        }

        return new ValueTask<IAuditChainStream>(new Chain(this.sync, chain));
    }

    /// <inheritdoc/>
    public ValueTask<Stream?> OpenLastChainAsync(ReadOnlyMemory<byte> writerId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string writer = Encoding.UTF8.GetString(writerId.Span);
        lock (this.sync)
        {
            string? last = null;
            foreach (string id in this.chainIds)
            {
                if (this.writers[id] == writer && (last is null || string.CompareOrdinal(id, last) > 0))
                {
                    last = id;
                }
            }

            return new ValueTask<Stream?>(last is null ? null : new MemoryStream(this.chains[last].ToArray(), writable: false));
        }
    }

    private sealed class Chain(Lock sync, MemoryStream chain) : IAuditChainStream
    {
        public ValueTask AppendAsync(ReadOnlyMemory<byte> line, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (sync)
            {
                chain.Write(line.Span);
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}