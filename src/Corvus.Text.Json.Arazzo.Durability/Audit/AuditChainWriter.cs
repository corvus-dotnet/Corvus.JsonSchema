// <copyright file="AuditChainWriter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Execution;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The one writer of an audit chain (ADR 0069): it numbers each record, stamps it, links it to the record before it by
/// hash, appends it to the sink, and signs the chain's head on a cadence. A control plane instance holds one writer, so
/// it owns its chain and coordinates with no other instance.
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
/// <para>
/// A head is a record of the chain like any other, whose signature vouches for every record before it. One is signed
/// when the unsigned records reach the cadence's count, when the oldest of them reaches the cadence's age, and when a
/// chain is left in good order. A head that cannot be signed or stored never fails the append that prompted it: the
/// records are already chained, the failure is reported, and the next tick tries again. What grows meanwhile is the
/// unsigned window.
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
    private readonly IExecutorPackageSigner? headSigner;
    private readonly AuditHeadOptions headOptions;
    private readonly Action<AuditHead>? onHeadSigned;
    private readonly Action<Exception>? onHeadFailed;
    private readonly ITimer? headTimer;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly byte[] chainId = new byte[AuditRecord.ChainIdLength];
    private readonly byte[] previousHash = new byte[AuditRecord.HashLength];
    private readonly byte[] continuesChain = new byte[AuditRecord.ChainIdLength];
    private readonly byte[] continuesHash = new byte[AuditRecord.HashLength];
    private IAuditChainStream? stream;
    private long nextSequence;
    private int unsignedRecords;
    private DateTimeOffset oldestUnsignedAt;
    private bool hasContinuation;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="AuditChainWriter"/> class.</summary>
    /// <param name="sink">The sink the writer's chains are stored in.</param>
    /// <param name="timeProvider">The clock records are stamped from and the head cadence runs on (defaults to the system clock).</param>
    /// <param name="maxRecordsPerChain">The number of records a chain holds before the writer opens the next.</param>
    /// <param name="headSigner">The audit's own signer, or <see langword="null"/> for a chain with no signed heads, whose tail no signature vouches for.</param>
    /// <param name="headOptions">The head cadence (defaults to <see cref="AuditHeadOptions.Default"/>).</param>
    /// <param name="onHeadSigned">Called with each head once it is stored, so the caller can publish it outside the sink as an anchor.</param>
    /// <param name="onHeadFailed">Called when a head could not be signed or stored.</param>
    public AuditChainWriter(
        IAuditSink sink,
        TimeProvider? timeProvider = null,
        long maxRecordsPerChain = DefaultMaxRecordsPerChain,
        IExecutorPackageSigner? headSigner = null,
        AuditHeadOptions? headOptions = null,
        Action<AuditHead>? onHeadSigned = null,
        Action<Exception>? onHeadFailed = null)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxRecordsPerChain, 1);
        this.sink = sink;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.maxRecordsPerChain = maxRecordsPerChain;
        this.headSigner = headSigner;
        this.headOptions = headOptions ?? AuditHeadOptions.Default;
        ArgumentOutOfRangeException.ThrowIfLessThan(this.headOptions.RecordsPerHead, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(this.headOptions.Interval, TimeSpan.Zero);
        this.onHeadSigned = onHeadSigned;
        this.onHeadFailed = onHeadFailed;
        if (headSigner is not null)
        {
            // The timer is what signs a quiet tail: without it a record appended before a lull would wait, unsigned, for
            // the next append. It ticks at the interval and signs only where a record has gone unsigned that long.
            this.headTimer = this.timeProvider.CreateTimer(static state => ((AuditChainWriter)state!).OnHeadTimer(), this, this.headOptions.Interval, this.headOptions.Interval);
        }
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
                await this.CloseChainAsync(inGoodOrder: true).ConfigureAwait(false);
            }

            if (this.stream is null)
            {
                await this.OpenChainAsync(cancellationToken).ConfigureAwait(false);
            }

            DateTimeOffset now = this.timeProvider.GetUtcNow();
            using PooledUtf8 line = this.RenderMutation(in entry, now);
            try
            {
                await this.stream!.AppendAsync(line.Memory, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await this.CloseChainAsync(inGoodOrder: false).ConfigureAwait(false);
                if (ex is OperationCanceledException)
                {
                    throw;
                }

                throw ThrowHelper.GetAuditAppendFailedException(ex);
            }

            this.Commit(line.Span);
            if (this.headSigner is not null)
            {
                if (this.unsignedRecords++ == 0)
                {
                    this.oldestUnsignedAt = now;
                }

                if (this.unsignedRecords >= this.headOptions.RecordsPerHead)
                {
                    await this.TrySignHeadAsync().ConfigureAwait(false);
                }
            }
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
            if (this.headTimer is not null)
            {
                await this.headTimer.DisposeAsync().ConfigureAwait(false);
            }

            await this.CloseChainAsync(inGoodOrder: true).ConfigureAwait(false);
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

    private static PooledUtf8 RentLine(ReadOnlySpan<byte> written)
    {
        byte[] rented = ArrayPool<byte>.Shared.Rent(written.Length + 1);
        written.CopyTo(rented);
        rented[written.Length] = (byte)'\n';
        return new PooledUtf8(rented, written.Length + 1);
    }

    private void OnHeadTimer() => _ = this.SignDueHeadAsync();

    private async Task SignDueHeadAsync()
    {
        try
        {
            await this.gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!this.disposed
                    && this.unsignedRecords > 0
                    && this.timeProvider.GetUtcNow() - this.oldestUnsignedAt >= this.headOptions.Interval)
                {
                    await this.TrySignHeadAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                this.gate.Release();
            }
        }
        catch (ObjectDisposedException)
        {
            // The writer was disposed between the tick and the gate; its close signed what there was to sign.
        }
    }

    private async ValueTask OpenChainAsync(CancellationToken cancellationToken)
    {
        Guid.NewGuid().TryFormat(this.chainId, out _, "N");
        this.previousHash.AsSpan().Fill((byte)'0');
        this.nextSequence = 0;
        this.unsignedRecords = 0;
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
    // took a record has nothing to continue from, so whatever continuation the writer already held carries over it. A
    // chain left in good order is signed first, so its last records are vouched for; an abandoned one cannot be, since
    // nothing can be soundly appended to it.
    private async ValueTask CloseChainAsync(bool inGoodOrder)
    {
        if (this.stream is null)
        {
            return;
        }

        if (inGoodOrder && this.unsignedRecords > 0)
        {
            await this.TrySignHeadAsync().ConfigureAwait(false);
        }

        if (this.stream is not { } open)
        {
            return;
        }

        this.stream = null;
        this.unsignedRecords = 0;
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

    // Signs the chain's tail and appends the head. It never throws: the records it would vouch for are already stored and
    // chained, so a head that cannot be signed is reported and tried again at the next tick. A head the sink half-stored
    // abandons the chain, exactly as a half-stored record does.
    private async ValueTask TrySignHeadAsync()
    {
        if (this.headSigner is null || this.stream is null)
        {
            return;
        }

        ExecutorPackageSignature signature;
        byte[] statement = ArrayPool<byte>.Shared.Rent(AuditRecord.MaxHeadStatementLength);
        try
        {
            int length = AuditRecord.WriteHeadStatement(statement, this.chainId, this.nextSequence, this.previousHash);
            signature = await this.headSigner.SignAsync(statement.AsMemory(0, length), CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            this.onHeadFailed?.Invoke(ex);
            return;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(statement);
        }

        long sequence = this.nextSequence;
        using PooledUtf8 line = this.RenderHead(in signature);
        try
        {
            await this.stream.AppendAsync(line.Memory, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            await this.CloseChainAsync(inGoodOrder: false).ConfigureAwait(false);
            this.onHeadFailed?.Invoke(ex);
            return;
        }

        // The anchor names the hash the head vouches for, which is the head's previous-hash, so it is read before the
        // commit moves the tail on to the head itself.
        AuditHead? anchor = this.onHeadSigned is null
            ? null
            : new AuditHead(Encoding.UTF8.GetString(this.chainId), sequence, Encoding.UTF8.GetString(this.previousHash), signature.Algorithm, signature.KeyId, Convert.ToBase64String(signature.Value.Span));
        this.Commit(line.Span);
        this.unsignedRecords = 0;
        if (anchor is { } head)
        {
            this.onHeadSigned!(head);
        }
    }

    private PooledUtf8 RenderMutation(in AuditEntry entry, DateTimeOffset now)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Utf8JsonWriter writer = workspace.RentWriterAndBuffer(LineBufferSize, out IByteBufferWriter buffer);
        try
        {
            bool first = this.nextSequence == 0 && this.hasContinuation;
            AuditRecord.WriteMutation(
                writer,
                this.chainId,
                this.nextSequence,
                now,
                this.previousHash,
                first ? this.continuesChain : default,
                first ? this.continuesHash : default,
                in entry);
            writer.Flush();
            return RentLine(buffer.WrittenSpan);
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(writer, buffer);
        }
    }

    private PooledUtf8 RenderHead(in ExecutorPackageSignature signature)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Utf8JsonWriter writer = workspace.RentWriterAndBuffer(LineBufferSize, out IByteBufferWriter buffer);
        try
        {
            AuditRecord.WriteHead(writer, this.chainId, this.nextSequence, this.timeProvider.GetUtcNow(), this.previousHash, in signature);
            writer.Flush();
            return RentLine(buffer.WrittenSpan);
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