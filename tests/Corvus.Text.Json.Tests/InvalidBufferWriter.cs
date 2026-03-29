// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Buffers;
using System.Runtime.CompilerServices;

namespace Corvus.Text.Json.Tests;

internal class InvalidBufferWriter : IBufferWriter<byte>
{
    public InvalidBufferWriter()
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int bytes)
    {
    }

    public Memory<byte> GetMemory(int minimumLength = 0) => new byte[10];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetSpan(int minimumLength = 0) => new byte[10];
}