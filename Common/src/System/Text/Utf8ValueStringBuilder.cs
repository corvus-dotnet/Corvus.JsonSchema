// <copyright file="ValueStringBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <license>
// Derived from code Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See https:// github.com/dotnet/runtime/blob/c1049390d5b33483203f058b0e1457d2a1f62bf4/src/libraries/Common/src/System/Text/ValueStringBuilder.cs
// </license>
using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Corvus.Text;

internal ref partial struct Utf8ValueStringBuilder
{
    private byte[]? _arrayToReturnToPool;

    private Span<byte> _bytes;

    private int _pos;

    public Utf8ValueStringBuilder(Span<byte> initialBuffer)
    {
        _arrayToReturnToPool = null;
        _bytes = initialBuffer;
        _pos = 0;
    }

    public Utf8ValueStringBuilder(int initialCapacity)
    {
        _arrayToReturnToPool = ArrayPool<byte>.Shared.Rent(initialCapacity);
        _bytes = _arrayToReturnToPool;
        _pos = 0;
    }

    public int Length
    {
        get
        {
            if (_pos < 0) { throw new ObjectDisposedException("Either Dispose or GetRentedBuffer or GetRentedBufferAndLength has already been called"); }
            return _pos;
        }

        set
        {
            Debug.Assert(value >= 0);
            Debug.Assert(value <= _bytes.Length);
            _pos = value;
        }
    }

    public int Capacity => _bytes.Length;

    /// <summary>
    /// Return the underlying rented buffer, if any, and the length of the string. This also
    /// disposes the instance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Once you have retrieved this, you must not make any further use of
    /// <see cref="Utf8ValueStringBuilder"/>. You should call <see cref="ReturnRentedBuffer(byte[]?)"/>
    /// once you no longer require the buffer.
    /// </para>
    /// </remarks>
    public (byte[]? Buffer, int Length) GetRentedBufferAndLengthAndDispose()
    {
        byte[]? result = _arrayToReturnToPool;
        int length = Length;
        SetToDisposed();
        return (result, length);
    }

    /// <summary>
    /// Returns the buffer retrieved from <see cref="Rentedbytes"/>.
    /// </summary>
    /// <param name="buffer">The buffer to return.</param>
    public static void ReturnRentedBuffer(byte[]? buffer)
    {
        ReturnRentedBuffer(buffer, false);
    }

    /// <summary>
    /// Returns the buffer retrieved from <see cref="Rentedbytes"/>.
    /// </summary>
    /// <param name="buffer">The buffer to return.</param>
    /// <param name="clearBuffer">If <see langword="true"/> then clear the buffer when returned.</param>
    public static void ReturnRentedBuffer(byte[]? buffer, bool clearBuffer)
    {
        if (buffer is byte[] b)
        {
            ArrayPool<byte>.Shared.Return(b, clearBuffer);
        }
    }

    public void EnsureCapacity(int capacity)
    {
        // This is not expected to be called this with negative capacity
        Debug.Assert(capacity >= 0);

        // If the caller has a bug and calls this with negative capacity, make sure to call Grow to throw an exception.
        if ((uint)capacity > (uint)_bytes.Length)
            Grow(capacity - _pos);
    }

    /// <summary>
    /// Get a pinnable reference to the builder.
    /// Does not ensure there is a null byte after <see cref="Length"/>
    /// This overload is pattern matched in the C# 7.3+ compiler so you can omit
    /// the explicit method call, and write eg "fixed (byte* c = builder)"
    /// </summary>
    public ref byte GetPinnableReference()
    {
        return ref MemoryMarshal.GetReference(_bytes);
    }

    /// <summary>
    /// Get a pinnable reference to the builder.
    /// </summary>
    /// <param name="terminate">Ensures that the builder has a null byte after <see cref="Length"/></param>
    public ref byte GetPinnableReference(bool terminate)
    {
        if (terminate)
        {
            EnsureCapacity(Length + 1);
            _bytes[Length] = (byte)'\0';
        }

        return ref MemoryMarshal.GetReference(_bytes);
    }

    public ref byte this[int index]
    {
        get
        {
            Debug.Assert(index < _pos);
            return ref _bytes[index];
        }
    }

    public ReadOnlyMemory<byte> CreateMemoryAndDispose()
    {
        ReadOnlyMemory<byte> s = _bytes.Slice(0, _pos).ToArray();
        Dispose();
        return s;
    }

    /// <summary>Returns the underlying storage of the builder.</summary>
    public Span<byte> RawBytes => _bytes;

    /// <summary>
    /// Slice the builder to remove the first <paramref name="start"/> bytes.
    /// </summary>
    /// <param name="start">The number of bytes to slice from the start.</param>
    public void ApplySlice(int start)
    {
        if (start > _pos)
        {
            throw new ArgumentOutOfRangeException(nameof(start));
        }

        _bytes = _bytes.Slice(start);
        _pos -= start;
    }

    /// <summary>
    /// Slice the builder to the specified range.
    /// </summary>
    /// <param name="start">The number of bytes to slice from the start.</param>
    /// <param name="length">The final length of the span.</param>
    public void ApplySlice(int start, int length)
    {
        if (start + length > _pos)
        {
            throw new ArgumentOutOfRangeException(nameof(start));
        }

        _bytes = _bytes.Slice(start);
        _pos = length;
    }

    /// <summary>
    /// Returns a span around the contents of the builder.
    /// </summary>
    /// <param name="terminate">Ensures that the builder has a null byte after <see cref="Length"/></param>
    public ReadOnlySpan<byte> AsSpan(bool terminate)
    {
        if (terminate)
        {
            EnsureCapacity(Length + 1);
            _bytes[Length] = (byte)'\0';
        }

        return _bytes.Slice(0, _pos);
    }

    public ReadOnlySpan<byte> AsSpan() => _bytes.Slice(0, _pos);

    public ReadOnlySpan<byte> AsSpan(int start) => _bytes.Slice(start, _pos - start);

    public ReadOnlySpan<byte> AsSpan(int start, int length) => _bytes.Slice(start, length);

    /// <summary>
    /// Returns the contents of the builder as a <see cref="ReadOnlyMemory{T}"/>, renting the underlying buffer if it
    /// is not already backed by a rented buffer.
    /// </summary>
    /// <returns>The value as a <see cref="ReadOnlyMemory{byte}"/></returns>
    public ReadOnlyMemory<byte> AsMemory()
    {
        EnsureRented();
        return _arrayToReturnToPool.AsMemory(0, _pos);
    }

    /// <summary>
    /// Returns the contents of the builder as a <see cref="ReadOnlyMemory{T}"/>, renting the underlying buffer if it
    /// is not already backed by a rented buffer.
    /// </summary>
    /// <param name="start">The index within the string at which to start.</param>
    /// <returns>The value as a <see cref="ReadOnlyMemory{byte}"/></returns>
    public ReadOnlyMemory<byte> AsMemory(int start)
    {
        EnsureRented();
        return _arrayToReturnToPool.AsMemory(start, _pos - start);
    }

    /// <summary>
    /// Returns the contents of the builder as a <see cref="ReadOnlyMemory{T}"/>, renting the underlying buffer if it
    /// is not already backed by a rented buffer.
    /// </summary>
    /// <param name="start">The index within the string at which to start.</param>
    /// <param name="length">The length of the span to return.</param>
    /// <returns>The value as a <see cref="ReadOnlyMemory{byte}"/></returns>
    public ReadOnlyMemory<byte> AsMemory(int start, int length)
    {
        EnsureRented();
        return _arrayToReturnToPool.AsMemory(start, length);
    }

    public bool TryCopyTo(Span<byte> destination, out int bytesWritten)
    {
        if (_bytes.Slice(0, _pos).TryCopyTo(destination))
        {
            bytesWritten = _pos;
            Dispose();
            return true;
        }
        else
        {
            bytesWritten = 0;
            Dispose();
            return false;
        }
    }

    public void Insert(int index, byte value, int count)
    {
        if (_pos > _bytes.Length - count)
        {
            Grow(count);
        }

        int remaining = _pos - index;
        _bytes.Slice(index, remaining).CopyTo(_bytes.Slice(index + count));
        _bytes.Slice(index, count).Fill(value);
        _pos += count;
    }

    public void Insert(int index, scoped ReadOnlySpan<byte> s)
    {
        int count = s.Length;

        if (_pos > _bytes.Length - count)
        {
            Grow(count);
        }

        int remaining = _pos - index;
        _bytes.Slice(index, remaining).CopyTo(_bytes.Slice(index + count));
        s.CopyTo(_bytes.Slice(index));
        _pos += count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(byte c)
    {
        int pos = _pos;
        Span<byte> bytes = _bytes;
        if ((uint)pos < (uint)bytes.Length)
        {
            bytes[pos] = c;
            _pos = pos + 1;
        }
        else
        {
            GrowAndAppend(c);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(scoped ReadOnlySpan<byte> s)
    {
        int pos = _pos;
        if (s.Length == 1 && (uint)pos < (uint)_bytes.Length)
        {
            _bytes[pos] = s[0];
            _pos = pos + 1;
        }
        else
        {
            AppendSlow(s);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(int value, StandardFormat format = default)
    {
        const int ExpandInt32String = 14;
        int bytesWritten;
        while (!Utf8Formatter.TryFormat(value, _bytes.Slice(_pos), out bytesWritten, format))
        {
            Grow(ExpandInt32String);
        }

        _pos += bytesWritten;
    }

    private void AppendSlow(scoped ReadOnlySpan<byte> s)
    {
        int pos = _pos;
        if (pos > _bytes.Length - s.Length)
        {
            Grow(s.Length);
        }

        s.CopyTo(_bytes.Slice(pos));
        _pos += s.Length;
    }

    public void Append(byte c, int count)
    {
        if (_pos > _bytes.Length - count)
        {
            Grow(count);
        }

        Span<byte> dst = _bytes.Slice(_pos, count);
        for (int i = 0; i < dst.Length; i++)
        {
            dst[i] = c;
        }

        _pos += count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> AppendSpan(int length)
    {
        int origPos = _pos;
        if (origPos > _bytes.Length - length)
        {
            Grow(length);
        }

        _pos = origPos + length;
        return _bytes.Slice(origPos, length);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void GrowAndAppend(byte c)
    {
        Grow(1);
        Append(c);
    }

    /// <summary>
    /// Resize the internal buffer either by doubling current buffer size or
    /// by adding <paramref name="additionalCapacityBeyondPos"/> to
    /// <see cref="_pos"/> whichever is greater.
    /// </summary>
    /// <param name="additionalCapacityBeyondPos">
    /// Number of bytes requested beyond current position.
    /// </param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Grow(int additionalCapacityBeyondPos)
    {
        Debug.Assert(additionalCapacityBeyondPos > 0);
        Debug.Assert(_bytes.Length < _pos + additionalCapacityBeyondPos, "Grow called incorrectly, no resize is needed.");

        // Debug.Assert(_pos > _bytes.Length - additionalCapacityBeyondPos, "Grow called incorrectly, no resize is needed.");
        const uint ArrayMaxLength = 0x7FFFFFC7; // same as Array.MaxLength

        // Increase to at least the required size (_pos + additionalCapacityBeyondPos), but try
        // to double the size if possible, bounding the doubling to not go beyond the max array length.
        int newCapacity = (int)Math.Max(
            (uint)(_pos + additionalCapacityBeyondPos),
            Math.Min((uint)_bytes.Length * 2, ArrayMaxLength));

        // Make sure to let Rent throw an exception if the caller has a bug and the desired capacity is negative.
        // This could also go negative if the actual required length wraps around.
        byte[] poolArray = ArrayPool<byte>.Shared.Rent(newCapacity);

        _bytes.Slice(0, _pos).CopyTo(poolArray);

        byte[]? toReturn = _arrayToReturnToPool;
        _bytes = _arrayToReturnToPool = poolArray;
        if (toReturn != null)
        {
            ArrayPool<byte>.Shared.Return(toReturn);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        byte[]? toReturn = _arrayToReturnToPool;
        SetToDisposed();
        if (toReturn != null)
        {
            ArrayPool<byte>.Shared.Return(toReturn, true);
        }
    }

    /// <summary>
    /// Puts the object into a state preventing accidental continued use an a pool already returned
    /// to an array, or attempting to retrieve information from the object after it has either
    /// been disposed, or had its buffer returned to the pool as a result of calling
    /// <see cref="ToString"/>.
    /// </summary>
    private void SetToDisposed()
    {
        this = default(Utf8ValueStringBuilder) with { _pos = -1 };
    }

    private void EnsureRented()
    {
        if (_arrayToReturnToPool == null)
        {
            _arrayToReturnToPool = ArrayPool<byte>.Shared.Rent(_bytes.Length);
            _bytes.Slice(0, _pos).CopyTo(_arrayToReturnToPool);
            _bytes = _arrayToReturnToPool;
        }
    }
}