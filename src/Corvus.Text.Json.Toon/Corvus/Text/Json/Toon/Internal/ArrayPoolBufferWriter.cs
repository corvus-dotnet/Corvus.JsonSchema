using System.Buffers;
using System.Runtime.CompilerServices;

#if STJ && TOON
namespace Corvus.Toon.Internal;
#else
namespace Corvus.Text.Json.Toon.Internal;
#endif

internal sealed class ArrayPoolBufferWriter : IBufferWriter<byte>, IDisposable
{
    private byte[] buffer;
    private int index;

    public ArrayPoolBufferWriter(int initialCapacity)
    {
        this.buffer = ArrayPool<byte>.Shared.Rent(Math.Max(initialCapacity, 256));
    }

    public ReadOnlySpan<byte> WrittenSpan
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.buffer.AsSpan(0, this.index);
    }

    public void Advance(int count)
    {
        this.index += count;
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

    public void Dispose()
    {
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