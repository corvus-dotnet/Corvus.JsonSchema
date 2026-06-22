// <copyright file="ReadOnlyMemoryStream.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Collections.Concurrent;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A read-only, seekable <see cref="Stream"/> over a <see cref="ReadOnlyMemory{T}"/> — no buffer copy. Lets a driver that
/// uploads a BLOB from a stream (e.g. Microsoft.Data.SqlClient streaming a <c>VARBINARY(MAX)</c> parameter, or the Cosmos
/// SDK reading a request body) consume serialized UTF-8 without an exact owned <see cref="byte"/> array. The stream
/// <em>instances</em> are pooled (the per-write cost would otherwise be the stream object): <see cref="Rent"/> one over a
/// <see cref="PooledUtf8"/>'s borrowed memory, or <see cref="RentOwned"/> one that <em>owns</em> an <see cref="ArrayPool{T}"/>
/// buffer (returned on dispose, for callers that have no separate <see cref="PooledUtf8"/> to hold it). Hand it to the
/// command and <c>using</c>/<see cref="Stream.Dispose()"/> it once the (awaited) write completes — which returns the
/// instance (and any owned buffer) to the pool. <see cref="Dispose(bool)"/> is idempotent, so a driver that also disposes
/// the stream cannot double-pool it.
/// </summary>
public sealed class ReadOnlyMemoryStream : Stream
{
    // A soft cap so the pool cannot grow without bound under a write storm; excess instances are simply dropped to the GC.
    private const int MaxPooled = 64;

    private static readonly ConcurrentQueue<ReadOnlyMemoryStream> Pool = new();
    private static int pooledCount;

    private ReadOnlyMemory<byte> memory;
    private byte[]? ownedBuffer;
    private int position;
    private bool returned;

    private ReadOnlyMemoryStream()
    {
    }

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => true;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => this.memory.Length;

    /// <inheritdoc/>
    public override long Position
    {
        get => this.position;
        set => this.position = value < 0 || value > this.memory.Length ? throw new ArgumentOutOfRangeException(nameof(value)) : (int)value;
    }

    /// <summary>Rents a stream over <paramref name="memory"/> (reused from the pool when available). The stream borrows the
    /// memory and owns nothing — the caller keeps the backing buffer (e.g. a <see cref="PooledUtf8"/>) valid until dispose.</summary>
    /// <param name="memory">The document UTF-8 the stream reads from; must stay valid until the stream is disposed.</param>
    /// <returns>A stream positioned at the start of <paramref name="memory"/>.</returns>
    public static ReadOnlyMemoryStream Rent(ReadOnlyMemory<byte> memory)
        => RentCore(memory, null);

    /// <summary>Rents a stream that <em>owns</em> an <see cref="ArrayPool{T}"/>-rented buffer: <see cref="Dispose()"/> returns
    /// the buffer to the pool (as well as the stream instance). For callers that hand the stream off without a separate
    /// <see cref="PooledUtf8"/> to hold the buffer.</summary>
    /// <param name="rentedBuffer">The <see cref="ArrayPool{T}"/>-rented buffer (may be larger than <paramref name="length"/>); ownership transfers to the stream.</param>
    /// <param name="length">The number of UTF-8 bytes written.</param>
    /// <returns>A stream positioned at the start of the written bytes.</returns>
    public static ReadOnlyMemoryStream RentOwned(byte[] rentedBuffer, int length)
    {
        ArgumentNullException.ThrowIfNull(rentedBuffer);
        return RentCore(rentedBuffer.AsMemory(0, length), rentedBuffer);
    }

    private static ReadOnlyMemoryStream RentCore(ReadOnlyMemory<byte> memory, byte[]? ownedBuffer)
    {
        if (Pool.TryDequeue(out ReadOnlyMemoryStream? stream))
        {
            Interlocked.Decrement(ref pooledCount);
        }
        else
        {
            stream = new ReadOnlyMemoryStream();
        }

        stream.memory = memory;
        stream.ownedBuffer = ownedBuffer;
        stream.position = 0;
        stream.returned = false;
        return stream;
    }

    /// <inheritdoc/>
    public override int Read(Span<byte> buffer)
    {
        int remaining = this.memory.Length - this.position;
        if (remaining <= 0)
        {
            return 0;
        }

        int count = Math.Min(remaining, buffer.Length);
        this.memory.Span.Slice(this.position, count).CopyTo(buffer);
        this.position += count;
        return count;
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return this.Read(buffer.AsSpan(offset, count));
    }

    /// <inheritdoc/>
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        => cancellationToken.IsCancellationRequested
            ? ValueTask.FromCanceled<int>(cancellationToken)
            : new ValueTask<int>(this.Read(buffer.Span));

    /// <inheritdoc/>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return cancellationToken.IsCancellationRequested
            ? Task.FromCanceled<int>(cancellationToken)
            : Task.FromResult(this.Read(buffer.AsSpan(offset, count)));
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin)
    {
        long target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => this.position + offset,
            SeekOrigin.End => this.memory.Length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };

        if (target < 0 || target > this.memory.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        this.position = (int)target;
        return this.position;
    }

    /// <inheritdoc/>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!this.returned)
        {
            this.returned = true;
            byte[]? toReturn = this.ownedBuffer;
            this.memory = default;
            this.ownedBuffer = null;
            this.position = 0;
            if (toReturn is not null)
            {
                ArrayPool<byte>.Shared.Return(toReturn);
            }

            if (Interlocked.Increment(ref pooledCount) <= MaxPooled)
            {
                Pool.Enqueue(this);
            }
            else
            {
                Interlocked.Decrement(ref pooledCount);
            }
        }

        base.Dispose(disposing);
    }
}