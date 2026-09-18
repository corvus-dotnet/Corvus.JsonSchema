// <copyright file="BoundedResponseStream.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.OpenApi.HttpTransport;

/// <summary>
/// A read-only view of a response body that fails once more than a fixed number of bytes have been read.
/// </summary>
/// <remarks>
/// <para>
/// The count is of bytes delivered to the reader, so it bounds a chunked response and a response
/// whose <c>Content-Length</c> understates its body just as it bounds an accurately declared one.
/// Reading exactly the maximum succeeds. The read that would take the total past it throws an
/// <see cref="ApiResponseTooLargeException"/>.
/// </para>
/// </remarks>
internal sealed class BoundedResponseStream : Stream
{
    private readonly Stream inner;
    private readonly long maxLength;
    private long totalRead;

    /// <summary>
    /// Initializes a new instance of the <see cref="BoundedResponseStream"/> class.
    /// </summary>
    /// <param name="inner">The response body. This stream owns it and disposes it.</param>
    /// <param name="maxLength">The largest number of bytes that may be read.</param>
    public BoundedResponseStream(Stream inner, long maxLength)
    {
        this.inner = inner;
        this.maxLength = maxLength;
    }

    /// <inheritdoc/>
    public override bool CanRead => this.inner.CanRead;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
        => this.Count(this.inner.Read(buffer, offset, count));

    /// <inheritdoc/>
    public override int Read(Span<byte> buffer)
        => this.Count(this.inner.Read(buffer));

    /// <inheritdoc/>
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => this.Count(await this.inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false));

    /// <inheritdoc/>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        => this.Count(await this.inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false));

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override ValueTask DisposeAsync() => this.inner.DisposeAsync();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.inner.Dispose();
        }

        base.Dispose(disposing);
    }

    private int Count(int read)
    {
        this.totalRead += read;
        if (this.totalRead > this.maxLength)
        {
            ThrowHelper.ThrowApiResponseTooLarge(this.maxLength);
        }

        return read;
    }
}