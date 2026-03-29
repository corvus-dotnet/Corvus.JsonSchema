// <copyright file="SimpleTypesBacking.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Diagnostics;

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Provides a fixed-size backing structure for storing simple numeric, null and boolean values.
/// for <see cref="IJsonElement"/> creation.
/// </summary>
/// <remarks>
/// This is typically used as a backing field in a <c>[MyJsonElementType].Builder.Source</c> struct.
/// </remarks>
public ref struct SimpleTypesBacking
{
    private FixedSizeSimpleTypesBuffer _buffer;

    private int _length;

    /// <summary>
    /// Delegate for writing a value to a byte buffer.
    /// </summary>
    /// <typeparam name="T">The type of value to write.</typeparam>
    /// <param name="value">The value to write.</param>
    /// <param name="buffer">The buffer to write to.</param>
    /// <param name="written">The number of bytes written.</param>
    public delegate void Writer<T>(T value, Span<byte> buffer, out int written);

    /// <summary>
    /// Initializes the backing with a value using the provided writer delegate.
    /// </summary>
    /// <typeparam name="T">The type of value to initialize with.</typeparam>
    /// <param name="backing">The backing to initialize.</param>
    /// <param name="value">The value to write.</param>
    /// <param name="writer">The writer delegate to use for serialization.</param>
    public static void Initialize<T>(ref SimpleTypesBacking backing, in T value, Writer<T> writer)
    {
        writer(value, backing._buffer.AsSpan(), out backing._length);
    }

    /// <summary>
    /// Gets the written value as a span
    /// </summary>
    /// <returns>The written value.</returns>
    public ReadOnlySpan<byte> Span()
    {
        return _buffer.AsSpan(0, _length);
    }

    private unsafe struct FixedSizeSimpleTypesBuffer
    {
        public fixed byte _buffer[JsonConstants.StackallocByteThreshold];

        public Span<byte> AsSpan(int start, int length)
        {
            Debug.Assert(start >= 0 && (start + length) <= JsonConstants.StackallocByteThreshold);
            fixed (byte* pBuffer = _buffer)
            {
                return new Span<byte>(pBuffer + start, length);
            }
        }

        public Span<byte> AsSpan()
        {
            fixed (byte* pBuffer = _buffer)
            {
                return new Span<byte>(pBuffer, JsonConstants.StackallocByteThreshold);
            }
        }
    }
}