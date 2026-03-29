// <copyright file="JsonReaderHelper.net8.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Buffers;
using System.Runtime.CompilerServices;

namespace Corvus.Text.Json;

internal static partial class JsonReaderHelper
{
    /// <summary>'"', '\',  or any control characters (i.e. 0 to 31).</summary>
    /// <remarks>https:// tools.ietf.org/html/rfc8259</remarks>
    private static readonly SearchValues<byte> s_controlQuoteBackslash = SearchValues.Create(
        "\u0000\u0001\u0002\u0003\u0004\u0005\u0006\u0007\u0008\u0009\u000A\u000B\u000C\u000D\u000E\u000F\u0010\u0011\u0012\u0013\u0014\u0015\u0016\u0017\u0018\u0019\u001A\u001B\u001C\u001D\u001E\u001F"u8 + // Any Control, < 32 (' ')
        "\""u8 + // Quote
        "\\"u8); // Backslash

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int IndexOfQuoteOrAnyControlOrBackSlash(this ReadOnlySpan<byte> span) =>
        span.IndexOfAny(s_controlQuoteBackslash);
}