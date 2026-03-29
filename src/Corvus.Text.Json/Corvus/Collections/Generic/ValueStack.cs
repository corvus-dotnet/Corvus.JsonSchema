// <copyright file="ValueStack.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace System.Collections.Generic;

internal partial struct ValueStack<T>
{
    private Span<T> Span => _arrayFromPool.AsSpan();

    private T[] _arrayFromPool;

    private int _pos;

    public ValueStack(int capacity)
    {
        Grow(capacity);
    }

    public int Length
    {
        get => _pos;
        set
        {
            Debug.Assert(value >= 0);
            Debug.Assert(value <= Span.Length);
            _pos = value;
        }
    }

    public ref T this[int index]
    {
        get
        {
            Debug.Assert(index < _pos);
            return ref Span[index];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(T item)
    {
        int pos = _pos;

        // Workaround for https:// github.com/dotnet/runtime/issues/72004
        Span<T> span = Span;
        if ((uint)pos < (uint)span.Length)
        {
            span[pos] = item;
            _pos = pos + 1;
        }
        else
        {
            AddWithResize(item);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(scoped ReadOnlySpan<T> source)
    {
        int pos = _pos;
        Span<T> span = Span;
        if (source.Length == 1 && (uint)pos < (uint)span.Length)
        {
            span[pos] = source[0];
            _pos = pos + 1;
        }
        else
        {
            AppendMultiChar(source);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AppendMultiChar(scoped ReadOnlySpan<T> source)
    {
        if ((uint)(_pos + source.Length) > (uint)Span.Length)
        {
            Grow(Span.Length - _pos + source.Length);
        }

        source.CopyTo(Span.Slice(_pos));
        _pos += source.Length;
    }

    public void Insert(int index, scoped ReadOnlySpan<T> source)
    {
        Debug.Assert(index == 0, "Implementation currently only supports index == 0");

        if ((uint)(_pos + source.Length) > (uint)Span.Length)
        {
            Grow(source.Length);
        }

        Span.Slice(0, _pos).CopyTo(Span.Slice(source.Length));
        source.CopyTo(Span);
        _pos += source.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> AppendSpan(int length)
    {
        Debug.Assert(length >= 0);

        int pos = _pos;
        Span<T> span = Span;
        if ((ulong)(uint)pos + (ulong)(uint)length <= (ulong)(uint)span.Length) // same guard condition as in Span<T>.Slice on 64-bit
        {
            _pos = pos + length;
            return span.Slice(pos, length);
        }
        else
        {
            return AppendSpanWithGrow(length);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private Span<T> AppendSpanWithGrow(int length)
    {
        int pos = _pos;
        Grow(Span.Length - pos + length);
        _pos += length;
        return Span.Slice(pos, length);
    }

    // Hide uncommon path
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AddWithResize(T item)
    {
        Debug.Assert(_pos == Span.Length);
        int pos = _pos;
        Grow(1);
        Span[pos] = item;
        _pos = pos + 1;
    }

    public ReadOnlySpan<T> AsSpan()
    {
        return Span.Slice(0, _pos);
    }

    public bool TryCopyTo(Span<T> destination, out int itemsWritten)
    {
        if (Span.Slice(0, _pos).TryCopyTo(destination))
        {
            itemsWritten = _pos;
            return true;
        }

        itemsWritten = 0;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        T[]? toReturn = _arrayFromPool;
        if (toReturn != null)
        {
            _arrayFromPool = null!;

#if SYSTEM_PRIVATE_CORELIB
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                ArrayPool<T>.Shared.Return(toReturn, _pos);
            }
            else
            {
                ArrayPool<T>.Shared.Return(toReturn);
            }
#else
            if (!typeof(T).IsPrimitive)
            {
                Array.Clear(toReturn, 0, _pos);
            }

            ArrayPool<T>.Shared.Return(toReturn);
#endif
        }
    }

    // Note that consuming implementations depend on the list only growing if it's absolutely
    // required.  If the list is already large enough to hold the additional items be added,
    // it must not grow. The list is used in a number of places where the reference is checked
    // and it's expected to match the initial reference provided to the constructor if that
    // span was sufficiently large.
    [MemberNotNull(nameof(_arrayFromPool))]
    private void Grow(int additionalCapacityRequired = 1)
    {
        const int ArrayMaxLength = 0x7FFFFFC7; // same as Array.MaxLength

        // Double the size of the span.  If it's currently empty, default to size 4,
        // although it'll be increased in Rent to the pool's minimum bucket size.
        int nextCapacity = Math.Max(Span.Length != 0 ? Span.Length * 2 : 4, Span.Length + additionalCapacityRequired);

        // If the computed doubled capacity exceeds the possible length of an array, then we
        // want to downgrade to either the maximum array length if that's large enough to hold
        // an additional item, or the current length + 1 if it's larger than the max length, in
        // which case it'll result in an OOM when calling Rent below.  In the exceedingly rare
        // case where _span.Length is already int.MaxValue (in which case it couldn't be a managed
        // array), just use that same value again and let it OOM in Rent as well.
        if ((uint)nextCapacity > ArrayMaxLength)
        {
            nextCapacity = Math.Max(Math.Max(Span.Length + 1, ArrayMaxLength), Span.Length);
        }

        T[] array = ArrayPool<T>.Shared.Rent(nextCapacity);
        Span.CopyTo(array);

        T[]? toReturn = _arrayFromPool;
        _arrayFromPool = array;
        if (toReturn != null)
        {
#if SYSTEM_PRIVATE_CORELIB
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                ArrayPool<T>.Shared.Return(toReturn, _pos);
            }
            else
            {
                ArrayPool<T>.Shared.Return(toReturn);
            }
#else
            if (!typeof(T).IsPrimitive)
            {
                Array.Clear(toReturn, 0, _pos);
            }

            ArrayPool<T>.Shared.Return(toReturn);
#endif
        }
    }

    internal unsafe void Sort()
    {
#if NET
        Span.Slice(0, _pos).Sort();
#else
        T[] buffer = ArrayPool<T>.Shared.Rent(_pos);
        Span.Slice(0, _pos).CopyTo(buffer);
        Array.Sort(buffer, 0, _pos);
        buffer.AsSpan(0, _pos).CopyTo(Span);
#endif
    }
}