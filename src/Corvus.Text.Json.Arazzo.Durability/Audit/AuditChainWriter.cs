// <copyright file="AuditChainWriter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Security.Cryptography;
using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The one writer of an audit chain (ADR 0069): it numbers each record, stamps it, links it to the record before it by
/// hash, and appends it to the sink. A control plane instance holds one writer, so it owns its chain and coordinates with
/// no other instance.
/// </summary>
/// <remarks>
/// <para>
/// Appends are serialized, because a record's hash covers the record before it: the order records are hashed in is the
/// order they are stored in. The gate is held across the sink's write, so governance mutations on one instance queue
/// behind the sink. Reads and run execution never come here.
/// </para>
/// <para>
/// A chain is abandoned when an append to it fails, a cancelled one included. How much of the failed line reached the
/// sink is unknown, so nothing can be soundly chained after it. The next append opens a new chain whose first record names
/// the abandoned chain and the hash of the last record this writer knows it appended there. The same happens, without a
/// failure, when a chain reaches its record limit. A verifier therefore reads an abandoned chain's torn tail as the end
/// of that chain, and finds where the evidence continues.
/// </para>
/// </remarks>
public sealed class AuditChainWriter : IAsyncDisposable
{
    /// <summary>The default number of records a chain holds before the writer opens the next.</summary>
    public const long DefaultMaxRecordsPerChain = 40_000;

    private const int LineBufferSize = 512;

    private readonly IAuditSink sink;
    private readonly TimeProvider timeProvider;
    private readonly long maxRecordsPerChain;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly byte[] chainId = new byte[AuditRecord.ChainIdLength];
    private readonly byte[] previousHash = new byte[AuditRecord.HashLength];
    private readonly byte[] continuesChain = new byte[AuditRecord.ChainIdLength];
    private readonly byte[] continuesHash = new byte[AuditRecord.HashLength];
    private IAuditChainStream? stream;
    private long nextSequence;
    private bool hasContinuation;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="AuditChainWriter"/> class.</summary>
    /// <param name="sink">The sink the writer's chains are stored in.</param>
    /// <param name="timeProvider">The clock records are stamped from (defaults to the system clock).</param>
    /// <param name="maxRecordsPerChain">The number of records a chain holds before the writer opens the next.</param>
    public AuditChainWriter(IAuditSink sink, TimeProvider? timeProvider = null, long maxRecordsPerChain = DefaultMaxRecordsPerChain)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxRecordsPerChain, 1);
        this.sink = sink;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.maxRecordsPerChain = maxRecordsPerChain;
    }

    /// <summary>Appends one record to the writer's chain, opening a chain first where none is open.</summary>
    /// <param name="entry">What happened.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the record is stored.</returns>
    /// <exception cref="AuditAppendException">The sink refused the chain or the record. The chain is abandoned.</exception>
    public async ValueTask AppendAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);

            if (this.stream is not null && this.nextSequence >= this.maxRecordsPerChain)
            {
                await this.CloseChainAsync().ConfigureAwait(false);
            }

            if (this.stream is null)
            {
                await this.OpenChainAsync(cancellationToken).ConfigureAwait(false);
            }

            using PooledUtf8 line = this.RenderLine(in entry);
            try
            {
                await this.stream!.AppendAsync(line.Memory, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await this.CloseChainAsync().ConfigureAwait(false);
                if (ex is OperationCanceledException)
                {
                    throw;
                }

                throw ThrowHelper.GetAuditAppendFailedException(ex);
            }

            this.Commit(line.Span);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await this.gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            await this.CloseChainAsync().ConfigureAwait(false);
        }
        finally
        {
            this.gate.Release();
        }
    }

    private static void WriteHex(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        ReadOnlySpan<byte> digits = "0123456789abcdef"u8;
        for (int i = 0; i < source.Length; i++)
        {
            destination[i * 2] = digits[source[i] >> 4];
            destination[(i * 2) + 1] = digits[source[i] & 0xF];
        }
    }

    private async ValueTask OpenChainAsync(CancellationToken cancellationToken)
    {
        Guid.NewGuid().TryFormat(this.chainId, out _, "N");
        this.previousHash.AsSpan().Fill((byte)'0');
        this.nextSequence = 0;
        try
        {
            this.stream = await this.sink.CreateChainAsync(this.chainId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw ThrowHelper.GetAuditAppendFailedException(ex);
        }
    }

    // Leaves the open chain, remembering where it got to so the next chain's first record can say so. A chain that never
    // took a record has nothing to continue from, so whatever continuation the writer already held carries over it.
    private async ValueTask CloseChainAsync()
    {
        if (this.stream is not { } open)
        {
            return;
        }

        this.stream = null;
        if (this.nextSequence > 0)
        {
            this.chainId.CopyTo(this.continuesChain, 0);
            this.previousHash.CopyTo(this.continuesHash, 0);
            this.hasContinuation = true;
        }

        try
        {
            await open.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The chain is already left; a sink that also fails to close it changes nothing the writer can act on.
        }
    }

    private PooledUtf8 RenderLine(in AuditEntry entry)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Utf8JsonWriter writer = workspace.RentWriterAndBuffer(LineBufferSize, out IByteBufferWriter buffer);
        try
        {
            bool first = this.nextSequence == 0 && this.hasContinuation;
            AuditRecord.Write(
                writer,
                this.chainId,
                this.nextSequence,
                this.timeProvider.GetUtcNow(),
                this.previousHash,
                first ? this.continuesChain : default,
                first ? this.continuesHash : default,
                in entry);
            writer.Flush();
            ReadOnlySpan<byte> written = buffer.WrittenSpan;
            byte[] rented = ArrayPool<byte>.Shared.Rent(written.Length + 1);
            written.CopyTo(rented);
            rented[written.Length] = (byte)'\n';
            return new PooledUtf8(rented, written.Length + 1);
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(writer, buffer);
        }
    }

    // The stored line is now the chain's tail: its hash, over the record without the line feed, links the next record.
    private void Commit(ReadOnlySpan<byte> line)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(line[..^1], hash);
        WriteHex(hash, this.previousHash);
        this.nextSequence++;
    }
}