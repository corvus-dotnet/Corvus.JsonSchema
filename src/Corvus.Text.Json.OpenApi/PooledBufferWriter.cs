// <copyright file="PooledBufferWriter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// A lightweight <see cref="IBufferWriter{T}"/> backed by rented <see cref="ArrayPool{T}"/>
/// arrays. Avoids the heap allocation that <see cref="ArrayBufferWriter{T}"/> incurs for
/// its internal buffer.
/// </summary>
/// <remarks>
/// <para>
/// Use <see cref="FormFieldReader.Rent"/> / <see cref="FormFieldReader.Return"/> semantics:
/// call <see cref="Dispose"/> (or use a <c>using</c> statement) to return the rented buffer
/// to the pool.
/// </para>
/// </remarks>
internal sealed class PooledBufferWriter : IBufferWriter<byte>, IDisposable
{
    private byte[] buffer;
    private int written;

    /// <summary>
    /// Initializes a new instance of the <see cref="PooledBufferWriter"/> class
    /// with a rented buffer of at least <paramref name="initialCapacity"/> bytes.
    /// </summary>
    /// <param name="initialCapacity">The minimum initial buffer size.</param>
    public PooledBufferWriter(int initialCapacity)
    {
        this.buffer = FormFieldReader.Rent(Math.Max(initialCapacity, 256));
        this.written = 0;
    }

    /// <summary>
    /// Gets the written bytes as a <see cref="ReadOnlyMemory{T}"/>.
    /// </summary>
    public ReadOnlyMemory<byte> WrittenMemory => this.buffer.AsMemory(0, this.written);

    /// <inheritdoc/>
    public void Advance(int count)
    {
        this.written += count;
    }

    /// <inheritdoc/>
    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        this.EnsureCapacity(sizeHint);
        return this.buffer.AsMemory(this.written);
    }

    /// <inheritdoc/>
    public Span<byte> GetSpan(int sizeHint = 0)
    {
        this.EnsureCapacity(sizeHint);
        return this.buffer.AsSpan(this.written);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        byte[] toReturn = this.buffer;
        this.buffer = [];
        this.written = 0;

        if (toReturn.Length > 0)
        {
            FormFieldReader.Return(toReturn);
        }
    }

    private void EnsureCapacity(int sizeHint)
    {
        int required = this.written + Math.Max(sizeHint, 1);
        if (required <= this.buffer.Length)
        {
            return;
        }

        int newSize = Math.Max(this.buffer.Length * 2, required);
        byte[] newBuffer = FormFieldReader.Rent(newSize);
        this.buffer.AsSpan(0, this.written).CopyTo(newBuffer);
        FormFieldReader.Return(this.buffer);
        this.buffer = newBuffer;
    }
}