// <copyright file="Utf8ValueBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Accumulates the UTF-8 bytes of a value, such as a header value built from an array's items, in a
/// caller-supplied buffer (typically <c>stackalloc</c>), growing into the shared array pool only when the
/// value outgrows it.
/// </summary>
/// <remarks>
/// <para>
/// Dispose the builder (or declare it with <c>using</c>) once <see cref="WrittenSpan"/> has been consumed,
/// so any pooled array is returned. The written bytes are not valid after disposal.
/// </para>
/// </remarks>
public ref struct Utf8ValueBuilder
{
    private Span<byte> buffer;
    private byte[]? rented;
    private int length;

    /// <summary>
    /// Initializes a new instance of the <see cref="Utf8ValueBuilder"/> struct over an initial buffer.
    /// </summary>
    /// <param name="initialBuffer">The buffer to fill first; a pooled array takes over when it is full.</param>
    public Utf8ValueBuilder(Span<byte> initialBuffer)
    {
        this.buffer = initialBuffer;
        this.rented = null;
        this.length = 0;
    }

    /// <summary>
    /// Gets the bytes written so far.
    /// </summary>
    public readonly ReadOnlySpan<byte> WrittenSpan => this.buffer[..this.length];

    /// <summary>
    /// Gets the number of bytes written so far.
    /// </summary>
    public readonly int Length => this.length;

    /// <summary>
    /// Appends one byte.
    /// </summary>
    /// <param name="value">The byte.</param>
    public void Append(byte value)
    {
        if (this.length == this.buffer.Length)
        {
            this.Grow(1);
        }

        this.buffer[this.length++] = value;
    }

    /// <summary>
    /// Appends a span of bytes.
    /// </summary>
    /// <param name="value">The bytes.</param>
    public void Append(ReadOnlySpan<byte> value)
    {
        if (value.Length > this.buffer.Length - this.length)
        {
            this.Grow(value.Length);
        }

        value.CopyTo(this.buffer[this.length..]);
        this.length += value.Length;
    }

    /// <summary>
    /// Returns any pooled array and clears the builder.
    /// </summary>
    public void Dispose()
    {
        byte[]? toReturn = this.rented;
        this = default;
        if (toReturn is not null)
        {
            ArrayPool<byte>.Shared.Return(toReturn);
        }
    }

    private void Grow(int additional)
    {
        int required = checked(this.length + additional);
        int size = Math.Max(required, this.buffer.Length * 2);
        byte[] next = ArrayPool<byte>.Shared.Rent(size);
        this.buffer[..this.length].CopyTo(next);
        byte[]? previous = this.rented;
        this.rented = next;
        this.buffer = next;
        if (previous is not null)
        {
            ArrayPool<byte>.Shared.Return(previous);
        }
    }
}