// <copyright file="ArrayBufferWriterPolyfill.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NETSTANDARD2_0

using System.Buffers;

// On netstandard2.0, System.Buffers.ArrayBufferWriter<T> is not publicly available.
// This provides a minimal polyfill in the same namespace so that all existing unqualified
// references to ArrayBufferWriter<T> resolve without requiring #if guards at each call site.
namespace System.Buffers;

/// <summary>
/// A minimal polyfill for <c>ArrayBufferWriter{T}</c> on netstandard2.0.
/// Backed by an <see cref="ArrayPool{T}"/>-rented array that grows as needed.
/// </summary>
/// <typeparam name="T">The type of the buffer elements.</typeparam>
internal sealed class ArrayBufferWriter<T> : IBufferWriter<T>
{
    private T[] _buffer;
    private int _index;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArrayBufferWriter{T}"/> class.
    /// </summary>
    /// <param name="initialCapacity">The minimum initial capacity of the buffer.</param>
    public ArrayBufferWriter(int initialCapacity = 256)
    {
        _buffer = ArrayPool<T>.Shared.Rent(initialCapacity);
        _index = 0;
    }

    /// <summary>Gets the number of bytes written.</summary>
    public int WrittenCount => _index;

    /// <summary>Gets the written portion as a <see cref="ReadOnlyMemory{T}"/>.</summary>
    public ReadOnlyMemory<T> WrittenMemory => _buffer.AsMemory(0, _index);

    /// <summary>Gets the written portion as a <see cref="ReadOnlySpan{T}"/>.</summary>
    public ReadOnlySpan<T> WrittenSpan => _buffer.AsSpan(0, _index);

    /// <inheritdoc/>
    public void Advance(int count) => _index += count;

    /// <inheritdoc/>
    public Memory<T> GetMemory(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return _buffer.AsMemory(_index);
    }

    /// <inheritdoc/>
    public Span<T> GetSpan(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return _buffer.AsSpan(_index);
    }

    /// <summary>Resets the written position to zero without clearing the buffer.</summary>
    public void Clear() => _index = 0;

    private void EnsureCapacity(int sizeHint)
    {
        if (sizeHint <= 0)
        {
            sizeHint = 1;
        }

        if (_index + sizeHint <= _buffer.Length)
        {
            return;
        }

        int newSize = Math.Max(_buffer.Length * 2, _index + sizeHint);
        T[] newBuffer = ArrayPool<T>.Shared.Rent(newSize);
        _buffer.AsSpan(0, _index).CopyTo(newBuffer);
        ArrayPool<T>.Shared.Return(_buffer);
        _buffer = newBuffer;
    }
}

#endif