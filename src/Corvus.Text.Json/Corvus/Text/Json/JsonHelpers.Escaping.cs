// <copyright file="JsonHelpers.Escaping.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;

namespace Corvus.Text.Json;

internal static partial class JsonHelpers
{
    /// <summary>
    /// Escapes the specified UTF-8 value using the provided encoder.
    /// </summary>
    /// <param name="utf8Value">The UTF-8 value to escape.</param>
    /// <param name="firstEscapeIndexVal">The index of the first character that needs escaping.</param>
    /// <param name="encoder">The JavaScript encoder to use for escaping.</param>
    /// <returns>A byte array containing the escaped value.</returns>
    public static byte[] EscapeValue(
        ReadOnlySpan<byte> utf8Value,
        int firstEscapeIndexVal,
        JavaScriptEncoder? encoder)
    {
        Debug.Assert(int.MaxValue / JsonConstants.MaxExpansionFactorWhileEscaping >= utf8Value.Length);
        Debug.Assert(firstEscapeIndexVal >= 0 && firstEscapeIndexVal < utf8Value.Length);

        byte[]? valueArray = null;

        int length = JsonWriterHelper.GetMaxEscapedLength(utf8Value.Length, firstEscapeIndexVal);

        Span<byte> escapedValue = length <= JsonConstants.StackallocByteThreshold ?
            stackalloc byte[JsonConstants.StackallocByteThreshold] :
            (valueArray = ArrayPool<byte>.Shared.Rent(length));

        JsonWriterHelper.EscapeString(utf8Value, escapedValue, firstEscapeIndexVal, encoder, out int written);

        byte[] escapedString = escapedValue.Slice(0, written).ToArray();

        if (valueArray != null)
        {
            ArrayPool<byte>.Shared.Return(valueArray);
        }

        return escapedString;
    }

    /// <summary>
    /// Gets the escaped property name section for the specified UTF-8 value.
    /// </summary>
    /// <param name="utf8Value">The UTF-8 value to escape.</param>
    /// <param name="encoder">The JavaScript encoder to use for escaping.</param>
    /// <returns>A byte array containing the escaped property name section.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] GetEscapedPropertyNameSection(ReadOnlySpan<byte> utf8Value, JavaScriptEncoder? encoder)
    {
        int idx = JsonWriterHelper.NeedsEscaping(utf8Value, encoder);

        if (idx != -1)
        {
            return GetEscapedPropertyNameSection(utf8Value, idx, encoder);
        }
        else
        {
            return GetPropertyNameSection(utf8Value);
        }
    }

    private static byte[] GetEscapedPropertyNameSection(
        ReadOnlySpan<byte> utf8Value,
        int firstEscapeIndexVal,
        JavaScriptEncoder? encoder)
    {
        Debug.Assert(int.MaxValue / JsonConstants.MaxExpansionFactorWhileEscaping >= utf8Value.Length);
        Debug.Assert(firstEscapeIndexVal >= 0 && firstEscapeIndexVal < utf8Value.Length);

        byte[]? valueArray = null;

        int length = JsonWriterHelper.GetMaxEscapedLength(utf8Value.Length, firstEscapeIndexVal);

        Span<byte> escapedValue = length <= JsonConstants.StackallocByteThreshold ?
            stackalloc byte[JsonConstants.StackallocByteThreshold] :
            (valueArray = ArrayPool<byte>.Shared.Rent(length));

        JsonWriterHelper.EscapeString(utf8Value, escapedValue, firstEscapeIndexVal, encoder, out int written);

        byte[] propertySection = GetPropertyNameSection(escapedValue.Slice(0, written));

        if (valueArray != null)
        {
            ArrayPool<byte>.Shared.Return(valueArray);
        }

        return propertySection;
    }

    private static byte[] GetPropertyNameSection(ReadOnlySpan<byte> utf8Value)
    {
        int length = utf8Value.Length;
        byte[] propertySection = new byte[length + 3];

        propertySection[0] = JsonConstants.Quote;
        utf8Value.CopyTo(propertySection.AsSpan(1, length));
        propertySection[++length] = JsonConstants.Quote;
        propertySection[++length] = JsonConstants.KeyValueSeparator;

        return propertySection;
    }
}