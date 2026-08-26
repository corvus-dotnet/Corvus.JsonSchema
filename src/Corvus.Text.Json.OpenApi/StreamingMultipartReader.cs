// <copyright file="StreamingMultipartReader.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// A forward-only multipart reader over a live stream. Parts are visited in wire
/// order; each part's body is exposed as a bounded forward-only <see cref="Stream"/>
/// that ends at the part's closing boundary, so a part of any size flows through a
/// fixed-size working buffer without being materialized.
/// </summary>
/// <remarks>
/// <para>
/// Boundary detection uses tail retention: when the working buffer holds no complete
/// delimiter, the last <c>delimiterLength - 1</c> bytes cannot be released to the
/// consumer because they might be a delimiter prefix split across a read boundary;
/// they are carried over to the next refill. This gives O(n) scanning that is correct
/// for arbitrary chunk splits.
/// </para>
/// <para>
/// The reader does not own the source stream; disposing the reader returns its rented
/// buffers only. Header spans remain valid until the next call to
/// <see cref="MoveNextPartAsync"/>, which also drains any unread remainder of the
/// current part's body.
/// </para>
/// </remarks>
public sealed class StreamingMultipartReader : IAsyncDisposable
{
    private const int MaxHeaderBlockBytes = 65_536;

    private readonly Stream source;
    private readonly byte[] delimiter;
    private readonly int delimiterLength;

    private byte[] buffer;
    private int start;
    private int filled;
    private bool sourceExhausted;

    private byte[]? headerBlock;
    private int headerNameStart;
    private int headerNameLength;
    private int headerFileNameStart;
    private int headerFileNameLength;
    private int headerContentTypeStart;
    private int headerContentTypeLength;

    private State state = State.SeekingDelimiter;
    private PartBodyStream? bodyStream;

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamingMultipartReader"/> class.
    /// </summary>
    /// <param name="source">The multipart body stream. The reader does not dispose it.</param>
    /// <param name="boundary">The boundary token (UTF-8, without the leading <c>--</c>).</param>
    /// <param name="bufferSize">The working buffer size. Values smaller than the minimum required for the boundary are raised to it.</param>
    public StreamingMultipartReader(Stream source, ReadOnlySpan<byte> boundary, int bufferSize = 16_384)
    {
        this.source = source;

        // The delimiter is "\r\n--" + boundary. The buffer is seeded with a virtual
        // leading CRLF so the first delimiter (which appears without one at the very
        // start of the body) matches the same pattern as every other one.
        this.delimiterLength = boundary.Length + 4;
        this.delimiter = ArrayPool<byte>.Shared.Rent(this.delimiterLength);
        "\r\n--"u8.CopyTo(this.delimiter);
        boundary.CopyTo(this.delimiter.AsSpan(4));

        int minimumSize = (this.delimiterLength * 2) + 64;
        this.buffer = ArrayPool<byte>.Shared.Rent(Math.Max(bufferSize, minimumSize));
        this.buffer[0] = (byte)'\r';
        this.buffer[1] = (byte)'\n';
        this.start = 0;
        this.filled = 2;
    }

    private enum State
    {
        SeekingDelimiter,
        AtDelimiterTail,
        InPart,
        Finished,
    }

    /// <summary>Gets the current part's form field name (empty for unnamed parts, e.g. multipart/mixed).</summary>
    public ReadOnlySpan<byte> CurrentName => this.HeaderSlice(this.headerNameStart, this.headerNameLength);

    /// <summary>Gets the current part's filename, if present.</summary>
    public ReadOnlySpan<byte> CurrentFileName => this.HeaderSlice(this.headerFileNameStart, this.headerFileNameLength);

    /// <summary>Gets the current part's Content-Type, if present.</summary>
    public ReadOnlySpan<byte> CurrentContentType => this.HeaderSlice(this.headerContentTypeStart, this.headerContentTypeLength);

    /// <summary>
    /// Gets a value indicating whether the current part is classified as binary
    /// (by filename presence or a non-JSON, non-text content type), using the same
    /// heuristic as the buffered readers.
    /// </summary>
    public bool CurrentIsBinary => MultipartFormReader.IsBinaryContentType(this.CurrentContentType, this.CurrentFileName);

    /// <summary>
    /// Gets the current part's body as a forward-only stream that ends at the part's
    /// closing boundary. Valid until the next call to <see cref="MoveNextPartAsync"/>.
    /// </summary>
    public Stream CurrentBodyStream => this.bodyStream ?? throw new InvalidOperationException("No current part. Call MoveNextPartAsync first.");

    /// <summary>
    /// Advances to the next part, draining any unread remainder of the current part's
    /// body. Returns <see langword="false"/> once the final boundary has been reached.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> if positioned on a part; otherwise <see langword="false"/>.</returns>
    /// <exception cref="InvalidDataException">The body is not well-formed multipart content.</exception>
    public async ValueTask<bool> MoveNextPartAsync(CancellationToken cancellationToken = default)
    {
        if (this.state == State.Finished)
        {
            return false;
        }

        if (this.state == State.InPart)
        {
            await this.DrainCurrentBodyAsync(cancellationToken).ConfigureAwait(false);
        }
        else if (this.state == State.SeekingDelimiter)
        {
            // Skip the preamble: everything up to and including the first delimiter.
            await this.SkipToDelimiterAsync(cancellationToken).ConfigureAwait(false);
        }

        // Otherwise the reader is already positioned at a delimiter tail (the current
        // part's body was read to completion, consuming its delimiter).

        // Positioned immediately after a delimiter: "--" means final boundary,
        // otherwise a CRLF introduces the part headers.
        await this.EnsureAsync(2, cancellationToken).ConfigureAwait(false);
        if (this.filled - this.start >= 2
            && this.buffer[this.start] == (byte)'-'
            && this.buffer[this.start + 1] == (byte)'-')
        {
            this.state = State.Finished;
            return false;
        }

        if (this.filled - this.start < 2
            || this.buffer[this.start] != (byte)'\r'
            || this.buffer[this.start + 1] != (byte)'\n')
        {
            throw new InvalidDataException("Malformed multipart body: expected CRLF or '--' after a boundary.");
        }

        this.start += 2;

        await this.ReadHeaderBlockAsync(cancellationToken).ConfigureAwait(false);
        this.state = State.InPart;
        this.bodyStream = new PartBodyStream(this);
        return true;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        ArrayPool<byte>.Shared.Return(this.buffer);
        ArrayPool<byte>.Shared.Return(this.delimiter);
        if (this.headerBlock is { } rented)
        {
            this.headerBlock = null;
            ArrayPool<byte>.Shared.Return(rented);
        }

        this.state = State.Finished;
        return ValueTask.CompletedTask;
    }

    private ReadOnlySpan<byte> HeaderSlice(int offset, int length)
        => this.headerBlock is { } block && length > 0
            ? new ReadOnlySpan<byte>(block, offset, length)
            : ReadOnlySpan<byte>.Empty;

    private ReadOnlySpan<byte> Delimiter => new(this.delimiter, 0, this.delimiterLength);

    /// <summary>
    /// Reads part body bytes into <paramref name="destination"/>, stopping at the
    /// part's closing delimiter. Returns 0 once the part body is complete, leaving
    /// the reader positioned immediately after the delimiter.
    /// </summary>
    private async ValueTask<int> ReadPartBytesAsync(Memory<byte> destination, CancellationToken cancellationToken)
    {
        if (destination.IsEmpty)
        {
            return 0;
        }

        while (true)
        {
            ReadOnlySpan<byte> window = this.buffer.AsSpan(this.start, this.filled - this.start);
            int idx = window.IndexOf(this.Delimiter);
            if (idx == 0)
            {
                // The body is complete; consume the delimiter.
                this.start += this.delimiterLength;
                return 0;
            }

            int releasable = idx > 0 ? idx : window.Length - (this.delimiterLength - 1);
            if (releasable > 0)
            {
                int toCopy = Math.Min(destination.Length, releasable);
                this.buffer.AsSpan(this.start, toCopy).CopyTo(destination.Span);
                this.start += toCopy;
                return toCopy;
            }

            if (!await this.RefillAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidDataException("Malformed multipart body: the final boundary was not found.");
            }
        }
    }

    private async ValueTask DrainCurrentBodyAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            ReadOnlySpan<byte> window = this.buffer.AsSpan(this.start, this.filled - this.start);
            int idx = window.IndexOf(this.Delimiter);
            if (idx >= 0)
            {
                this.start += idx + this.delimiterLength;
                return;
            }

            // Everything except a possible delimiter prefix tail is discardable.
            int discardable = window.Length - (this.delimiterLength - 1);
            if (discardable > 0)
            {
                this.start += discardable;
            }

            if (!await this.RefillAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidDataException("Malformed multipart body: the final boundary was not found.");
            }
        }
    }

    private async ValueTask SkipToDelimiterAsync(CancellationToken cancellationToken)
    {
        // Identical scan to draining a body: the preamble is discarded up to and
        // including the first delimiter (the seeded CRLF covers a body that starts
        // directly with the dashed boundary).
        await this.DrainCurrentBodyAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask ReadHeaderBlockAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            ReadOnlySpan<byte> window = this.buffer.AsSpan(this.start, this.filled - this.start);
            int idx = window.IndexOf("\r\n\r\n"u8);
            if (idx >= 0)
            {
                this.CaptureHeaders(window[..idx]);
                this.start += idx + 4;
                return;
            }

            if (window.Length > MaxHeaderBlockBytes)
            {
                throw new InvalidDataException("Malformed multipart body: part header block exceeds the maximum size.");
            }

            if (!await this.RefillAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidDataException("Malformed multipart body: part headers are truncated.");
            }
        }
    }

    private void CaptureHeaders(ReadOnlySpan<byte> headers)
    {
        if (this.headerBlock is null || this.headerBlock.Length < headers.Length)
        {
            if (this.headerBlock is { } old)
            {
                ArrayPool<byte>.Shared.Return(old);
            }

            this.headerBlock = ArrayPool<byte>.Shared.Rent(Math.Max(headers.Length, 256));
        }

        headers.CopyTo(this.headerBlock);

        MultipartFormReader.ParseHeaders(
            this.headerBlock.AsSpan(0, headers.Length),
            out ReadOnlySpan<byte> name,
            out ReadOnlySpan<byte> fileName,
            out ReadOnlySpan<byte> contentType);

        (this.headerNameStart, this.headerNameLength) = OffsetOf(this.headerBlock, name);
        (this.headerFileNameStart, this.headerFileNameLength) = OffsetOf(this.headerBlock, fileName);
        (this.headerContentTypeStart, this.headerContentTypeLength) = OffsetOf(this.headerBlock, contentType);

        static (int Start, int Length) OffsetOf(byte[] block, ReadOnlySpan<byte> slice)
        {
            if (slice.IsEmpty)
            {
                return (0, 0);
            }

            block.AsSpan().Overlaps(slice, out int offset);
            return (offset, slice.Length);
        }
    }

    /// <summary>
    /// Compacts the buffer and reads more from the source. Returns
    /// <see langword="false"/> if the source is exhausted and nothing was read.
    /// </summary>
    private async ValueTask<bool> RefillAsync(CancellationToken cancellationToken)
    {
        if (this.start > 0)
        {
            this.buffer.AsSpan(this.start, this.filled - this.start).CopyTo(this.buffer);
            this.filled -= this.start;
            this.start = 0;
        }

        if (this.filled == this.buffer.Length)
        {
            // The retained window fills the buffer (an oversized header block scan);
            // grow so the scan can make progress.
            byte[] larger = ArrayPool<byte>.Shared.Rent(this.buffer.Length * 2);
            this.buffer.AsSpan(0, this.filled).CopyTo(larger);
            ArrayPool<byte>.Shared.Return(this.buffer);
            this.buffer = larger;
        }

        if (this.sourceExhausted)
        {
            return false;
        }

        int read = await this.source.ReadAsync(this.buffer.AsMemory(this.filled, this.buffer.Length - this.filled), cancellationToken).ConfigureAwait(false);
        if (read == 0)
        {
            this.sourceExhausted = true;
            return false;
        }

        this.filled += read;
        return true;
    }

    private async ValueTask EnsureAsync(int count, CancellationToken cancellationToken)
    {
        while (this.filled - this.start < count)
        {
            if (!await this.RefillAsync(cancellationToken).ConfigureAwait(false))
            {
                return;
            }
        }
    }

    private sealed class PartBodyStream : Stream
    {
        private readonly StreamingMultipartReader reader;
        private bool completed;

        public PartBodyStream(StreamingMultipartReader reader)
        {
            this.reader = reader;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (this.completed || !ReferenceEquals(this.reader.bodyStream, this))
            {
                return 0;
            }

            int read = await this.reader.ReadPartBytesAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                this.completed = true;

                // The delimiter has been consumed; the reader is positioned for the
                // next MoveNextPartAsync without a drain.
                this.reader.state = State.AtDelimiterTail;
            }

            return read;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => this.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override int Read(byte[] buffer, int offset, int count)
            => this.ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}