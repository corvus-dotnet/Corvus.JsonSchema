// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Buffers;
using System.Collections.Generic;

namespace Corvus.Text.Json.Tests;

internal static class BufferFactory
{
    public static ReadOnlySequence<byte> Create(params byte[][] buffers)
    {
        if (buffers.Length == 1)
            return new ReadOnlySequence<byte>(buffers[0]);
        var list = new List<Memory<byte>>();
        foreach (byte[] buffer in buffers)
            list.Add(buffer);
        return Create(list.ToArray());
    }

    public static ReadOnlySequence<byte> Create(IEnumerable<Memory<byte>> buffers)
    {
        return ReadOnlyBufferSegment.Create(buffers);
    }

    private class ReadOnlyBufferSegment : ReadOnlySequenceSegment<byte>
    {
        public static ReadOnlySequence<byte> Create(IEnumerable<Memory<byte>> buffers)
        {
            ReadOnlyBufferSegment segment = null;
            ReadOnlyBufferSegment first = null;
            foreach (Memory<byte> buffer in buffers)
            {
                var newSegment = new ReadOnlyBufferSegment()
                {
                    Memory = buffer,
                };

                if (segment != null)
                {
                    segment.Next = newSegment;
                    newSegment.RunningIndex = segment.RunningIndex + segment.Memory.Length;
                }
                else
                {
                    first = newSegment;
                }

                segment = newSegment;
            }

            first ??= segment = new ReadOnlyBufferSegment();

            return new ReadOnlySequence<byte>(first, 0, segment, segment.Memory.Length);
        }
    }
}