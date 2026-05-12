// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
using System.Runtime.CompilerServices;

namespace System.Collections.Generic;

/// <summary>
/// These public methods are required by RegexWriter.
/// </summary>
internal partial struct ValueListBuilder<T>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Pop()
    {
        _pos--;
        return _span[_pos];
    }
}