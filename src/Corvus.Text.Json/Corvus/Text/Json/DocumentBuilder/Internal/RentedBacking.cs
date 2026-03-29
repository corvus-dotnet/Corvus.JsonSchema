// <copyright file="RentedBacking.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Buffers;

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Provides a fixed-size, rented backing structure for storing longer string values that
/// will not fit in a <see cref="SimpleTypesBacking"/>.
/// </summary>
/// <remarks>
/// This is typically used as a backing field in a <c>[MyJsonElementType].Builder.Source</c> struct.
/// </remarks>
public ref struct RentedBacking
#if NET
    : IDisposable
#endif
{
    /// <summary>
    /// Delegate for writing a value to a byte buffer.
    /// </summary>
    /// <typeparam name="T">The type of value to write.</typeparam>
    /// <param name="value">The value to write.</param>
    /// <param name="buffer">The buffer to write to.</param>
    /// <param name="written">The number of bytes written.</param>
    public delegate void Writer<T>(T value, Span<byte> buffer, out int written);

    private byte[] _buffer;

    private int _length;

    /// <summary>
    /// Initializes the rented backing with a value using the provided writer delegate.
    /// </summary>
    /// <typeparam name="T">The type of value to initialize with.</typeparam>
    /// <param name="backing">The backing to initialize.</param>
    /// <param name="minimumLength">The minimum length to rent from the array pool.</param>
    /// <param name="value">The value to write.</param>
    /// <param name="writer">The writer delegate to use for serialization.</param>
    public static void Initialize<T>(ref RentedBacking backing, int minimumLength, in T value, Writer<T> writer)
    {
        backing._buffer = ArrayPool<byte>.Shared.Rent(minimumLength);
        writer(value, backing._buffer.AsSpan(), out backing._length);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_buffer != null)
        {
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = null!;
            _length = 0;
        }
    }

    /// <summary>
    /// Gets the written value as a span
    /// </summary>
    /// <returns>The written value.</returns>
    public ReadOnlySpan<byte> Span()
    {
        if (_buffer == null)
        {
            throw new ObjectDisposedException(nameof(RentedBacking));
        }

        return _buffer.AsSpan(0, _length);
    }
}