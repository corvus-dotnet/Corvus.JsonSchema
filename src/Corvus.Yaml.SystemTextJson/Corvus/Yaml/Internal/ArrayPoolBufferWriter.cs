// <copyright file="ArrayPoolBufferWriter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Runtime.CompilerServices;

namespace Corvus.Yaml.Internal;

/// <summary>
/// A minimal <see cref="IBufferWriter{T}"/>-based writer backed by <see cref="ArrayPool{T}"/>.
/// Provides <see cref="WrittenSpan"/> and <see cref="WrittenMemory"/> for reading
/// the bytes written so far, and returns the rented array on <see cref="Dispose"/>.
/// </summary>
internal sealed class ArrayPoolBufferWriter : IBufferWriter<byte>, IDisposable
{
    private byte[] buffer;
    private int index;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArrayPoolBufferWriter"/> class.
    /// </summary>
    /// <param name="initialCapacity">The minimum initial capacity of the backing buffer.</param>
    public ArrayPoolBufferWriter(int initialCapacity)
    {
        this.buffer = ArrayPool<byte>.Shared.Rent(Math.Max(initialCapacity, 256));
        this.index = 0;
    }

    /// <summary>
    /// Gets a <see cref="ReadOnlySpan{T}"/> over the bytes written so far.
    /// </summary>
    public ReadOnlySpan<byte> WrittenSpan
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.buffer.AsSpan(0, this.index);
    }

    /// <summary>
    /// Gets a <see cref="ReadOnlyMemory{T}"/> over the bytes written so far.
    /// </summary>
    public ReadOnlyMemory<byte> WrittenMemory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.buffer.AsMemory(0, this.index);
    }

    /// <inheritdoc/>
    public void Advance(int count)
    {
        this.index += count;
    }

    /// <inheritdoc/>
    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        this.EnsureCapacity(sizeHint);
        return this.buffer.AsMemory(this.index);
    }

    /// <inheritdoc/>
    public Span<byte> GetSpan(int sizeHint = 0)
    {
        this.EnsureCapacity(sizeHint);
        return this.buffer.AsSpan(this.index);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        byte[] toReturn = this.buffer;
        this.buffer = [];
        this.index = 0;
        ArrayPool<byte>.Shared.Return(toReturn);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureCapacity(int sizeHint)
    {
        if (sizeHint <= 0)
        {
            sizeHint = 1;
        }

        if (sizeHint <= this.buffer.Length - this.index)
        {
            return;
        }

        this.Grow(sizeHint);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Grow(int sizeHint)
    {
        int newSize = Math.Max(this.buffer.Length * 2, this.index + sizeHint);
        byte[] newBuffer = ArrayPool<byte>.Shared.Rent(newSize);
        this.buffer.AsSpan(0, this.index).CopyTo(newBuffer);
        ArrayPool<byte>.Shared.Return(this.buffer);
        this.buffer = newBuffer;
    }
}