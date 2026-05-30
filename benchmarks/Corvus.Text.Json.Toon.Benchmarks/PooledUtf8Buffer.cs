// <copyright file="PooledUtf8Buffer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Runtime.CompilerServices;

namespace Corvus.Text.Json.Toon.Benchmarks;

internal sealed class PooledUtf8Buffer : IBufferWriter<byte>, IDisposable
{
    private byte[] buffer;
    private int index;

    public PooledUtf8Buffer(int initialCapacity)
    {
        this.buffer = ArrayPool<byte>.Shared.Rent(Math.Max(initialCapacity, 256));
    }

    public int WrittenCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.index;
    }

    public void Advance(int count)
    {
        this.index += count;
    }

    public void Clear()
    {
        this.index = 0;
    }

    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        this.EnsureCapacity(sizeHint);
        return this.buffer.AsMemory(this.index);
    }

    public Span<byte> GetSpan(int sizeHint = 0)
    {
        this.EnsureCapacity(sizeHint);
        return this.buffer.AsSpan(this.index);
    }

    public byte[] ToArray()
    {
        return this.buffer.AsSpan(0, this.index).ToArray();
    }

    public void Dispose()
    {
        if (this.buffer.Length == 0)
        {
            return;
        }

        byte[] toReturn = this.buffer;
        this.buffer = [];
        this.index = 0;
        ArrayPool<byte>.Shared.Return(toReturn);
    }

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

        int newSize = Math.Max(this.buffer.Length * 2, this.index + sizeHint);
        byte[] newBuffer = ArrayPool<byte>.Shared.Rent(newSize);
        this.buffer.AsSpan(0, this.index).CopyTo(newBuffer);
        ArrayPool<byte>.Shared.Return(this.buffer);
        this.buffer = newBuffer;
    }
}