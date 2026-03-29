// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Buffers;

namespace Corvus.Text.Json.Tests;

internal class BufferSegment<T> : ReadOnlySequenceSegment<T>
{
    public BufferSegment(ReadOnlyMemory<T> memory)
    {
        Memory = memory;
    }

    public BufferSegment<T> Append(ReadOnlyMemory<T> memory)
    {
        var segment = new BufferSegment<T>(memory)
        {
            RunningIndex = RunningIndex + Memory.Length
        };
        Next = segment;
        return segment;
    }
}