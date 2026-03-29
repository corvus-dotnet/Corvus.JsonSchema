// <copyright file="ValueStack.Pop.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Runtime.CompilerServices;

namespace System.Collections.Generic;

internal partial struct ValueStack<T>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Peek()
    {
        return Span[_pos - 1];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Pop()
    {
        _pos--;
        return Span[_pos];
    }
}