// <copyright file="JsonElementHelpers.String.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
namespace Corvus.Text.Json.Internal;

/// <summary>
/// Helper methods for JSON numeric operations including equality comparisons, divisibility checks, and arithmetic operations.
/// </summary>
public static partial class JsonElementHelpers
{
    /// <summary>
    /// Count the runes in a UTF-8 string.
    /// </summary>
    /// <param name="utf8String">The UTF-8 string for which to count the runes.</param>
    /// <returns>The number of runes in the UTF-8 string.</returns>
    public static int CountRunes(ReadOnlySpan<byte> utf8String)
    {
        int count = 0;
        while (Rune.DecodeFromUtf8(utf8String, out _, out int bytesConsumed) == System.Buffers.OperationStatus.Done)
        {
            count++;
            utf8String = utf8String.Slice(bytesConsumed);
        }

        return count;
    }
}