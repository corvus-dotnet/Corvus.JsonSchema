// <copyright file="TextResponseBody.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Holds the state of a text/plain response body for a generated response struct.
/// The live content stream is retained and only buffered on first access, so no
/// network read happens until the caller consumes the body. First consumption wins:
/// reading <see cref="Stream"/> directly consumes the body, after which the buffered
/// accessors see an empty body; buffering consumes the stream.
/// </summary>
/// <remarks>
/// <para>
/// This is a class deliberately: generated response types are structs, and buffering
/// from the async accessor must be visible through every copy of the struct, including
/// the copy captured by the async state machine.
/// </para>
/// </remarks>
public sealed class TextResponseBody
{
    private Stream? stream;
    private byte[]? buffer;
    private int length;
    private string? cached;

    /// <summary>
    /// Initializes a new instance of the <see cref="TextResponseBody"/> class over the
    /// live response content stream.
    /// </summary>
    /// <param name="stream">The response content stream.</param>
    public TextResponseBody(Stream stream)
    {
        this.stream = stream;
    }

    /// <summary>
    /// Gets the live response stream, or <see langword="null"/> once the body has been
    /// buffered by another accessor. Reading it directly consumes the body.
    /// </summary>
    public Stream? Stream => this.buffer is null ? this.stream : null;

    /// <summary>
    /// Gets the response text, buffering the body with a synchronous read on first
    /// access. Prefer <see cref="GetTextAsync"/>.
    /// </summary>
    public string? Text
    {
        get
        {
            this.EnsureBuffered();
            return this.GetCachedString();
        }
    }

    /// <summary>
    /// Gets the raw UTF-8 bytes of the response body, buffering it with a synchronous
    /// read on first access. Prefer <see cref="GetTextAsync"/>.
    /// </summary>
    public ReadOnlySpan<byte> Utf8Bytes
    {
        get
        {
            this.EnsureBuffered();
            return this.buffer is not null
                ? new ReadOnlySpan<byte>(this.buffer, 0, this.length)
                : ReadOnlySpan<byte>.Empty;
        }
    }

    /// <summary>
    /// Gets the response text, buffering the body asynchronously on first call.
    /// Subsequent calls return the cached value.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The response text, or <see langword="null"/> if there is no body.</returns>
    public async ValueTask<string?> GetTextAsync(CancellationToken cancellationToken = default)
    {
        if (this.buffer is null && this.stream is { } source)
        {
            byte[] buf = ArrayPool<byte>.Shared.Rent(4096);
            int bytesRead = 0;
            int read;
            while ((read = await source.ReadAsync(buf.AsMemory(bytesRead, buf.Length - bytesRead), cancellationToken).ConfigureAwait(false)) > 0)
            {
                bytesRead += read;
                if (bytesRead == buf.Length)
                {
                    byte[] larger = ArrayPool<byte>.Shared.Rent(buf.Length * 2);
                    Array.Copy(buf, larger, bytesRead);
                    ArrayPool<byte>.Shared.Return(buf);
                    buf = larger;
                }
            }

            this.buffer = buf;
            this.length = bytesRead;
            this.stream = null;
        }

        return this.GetCachedString();
    }

    /// <summary>
    /// Returns the rented buffer to the pool, if any. Called from the response's
    /// dispose path; the body must not be accessed afterwards.
    /// </summary>
    public void ReturnBuffer()
    {
        if (this.buffer is { } rented)
        {
            this.buffer = null;
            ArrayPool<byte>.Shared.Return(rented);
        }

        this.stream = null;
    }

    private void EnsureBuffered()
    {
        if (this.buffer is not null || this.stream is null)
        {
            return;
        }

        byte[] buf = ArrayPool<byte>.Shared.Rent(4096);
        int bytesRead = 0;
        int read;
        while ((read = this.stream.Read(buf, bytesRead, buf.Length - bytesRead)) > 0)
        {
            bytesRead += read;
            if (bytesRead == buf.Length)
            {
                byte[] larger = ArrayPool<byte>.Shared.Rent(buf.Length * 2);
                Array.Copy(buf, larger, bytesRead);
                ArrayPool<byte>.Shared.Return(buf);
                buf = larger;
            }
        }

        this.buffer = buf;
        this.length = bytesRead;
        this.stream = null;
    }

    private string? GetCachedString()
    {
        if (this.buffer is null)
        {
            return null;
        }

        return this.cached ??= System.Text.Encoding.UTF8.GetString(this.buffer, 0, this.length);
    }
}