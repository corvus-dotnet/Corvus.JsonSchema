// <copyright file="JsonElementHelpers.DeepEquals.cs" company="Endjin Limited">
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

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Helper methods for performing deep equality comparisons between JSON elements and their descendant values.
/// </summary>
public static partial class JsonElementHelpers
{
    /// <summary>
    /// Compares the values of two <see cref="IJsonElement"/> values for equality, including the values of all descendant elements.
    /// </summary>
    /// <typeparam name="TLeft">The type of the first <see cref="IJsonElement"/>.</typeparam>
    /// <typeparam name="TRight">The type of the second <see cref="IJsonElement"/>.</typeparam>
    /// <param name="element1">The first <see cref="IJsonElement"/> to compare.</param>
    /// <param name="element2">The second <see cref="IJsonElement"/> to compare.</param>
    /// <returns><see langword="true"/> if the two values are equal; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// Deep equality of two JSON values is defined as follows:
    /// <list type="bullet">
    /// <item>JSON values of different kinds are not equal.</item>
    /// <item>JSON constants <see langword="null"/>, <see langword="false"/>, and <see langword="true"/> only equal themselves.</item>
    /// <item>JSON numbers are equal if and only if they have they have equivalent decimal representations, with no rounding being used.</item>
    /// <item>JSON strings are equal if and only if they are equal using ordinal string comparison.</item>
    /// <item>JSON arrays are equal if and only if they are of equal length and each of their elements are pairwise equal.</item>
    /// <item>
    /// JSON objects are equal if and only if they have the same number of properties and each property in the first object
    /// has a corresponding property in the second object with the same name and equal value. The order of properties is not
    /// significant. Repeated properties are not supported, though they will resolve each value in the second instance to the
    /// last value in the first instance.
    /// </item>
    /// </list>
    /// </remarks>
    [CLSCompliant(false)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool DeepEquals<TLeft, TRight>(in TLeft element1, in TRight element2)
        where TLeft : struct, IJsonElement
        where TRight : struct, IJsonElement
    {
        if (element1.ParentDocument == null)
        {
            return element2.ParentDocument == null;
        }

        return DeepEquals(
            element1.TokenType,
            element2.TokenType,
            element1.ParentDocument,
            element2.ParentDocument,
            element1.ParentDocumentIndex,
            element2.ParentDocumentIndex);
    }

    /// <summary>
    /// Compares the values of two <see cref="IJsonElement"/> values for equality, including the values of all descendant elements.
    /// </summary>
    /// <typeparam name="TLeft">The type of the first <see cref="IJsonElement"/>.</typeparam>
    /// <param name="element1">The first <see cref="IJsonElement"/> to compare.</param>
    /// <param name="element2TokenType">The token type of the second JSON element.</param>
    /// <param name="element2ParentDocument">The parent document containing the second JSON element.</param>
    /// <param name="element2ParentDocumentIndex">The index of the second JSON element within its parent document.</param>
    /// <returns><see langword="true"/> if the two values are equal; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// Deep equality of two JSON values is defined as follows:
    /// <list type="bullet">
    /// <item>JSON values of different kinds are not equal.</item>
    /// <item>JSON constants <see langword="null"/>, <see langword="false"/>, and <see langword="true"/> only equal themselves.</item>
    /// <item>JSON numbers are equal if and only if they have they have equivalent decimal representations, with no rounding being used.</item>
    /// <item>JSON strings are equal if and only if they are equal using ordinal string comparison.</item>
    /// <item>JSON arrays are equal if and only if they are of equal length and each of their elements are pairwise equal.</item>
    /// <item>
    /// JSON objects are equal if and only if they have the same number of properties and each property in the first object
    /// has a corresponding property in the second object with the same name and equal value. The order of properties is not
    /// significant. Repeated properties are not supported, though they will resolve each value in the second instance to the
    /// last value in the first instance.
    /// </item>
    /// </list>
    /// </remarks>
    [CLSCompliant(false)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool DeepEqualsNoParentDocumentCheck<TLeft>(in TLeft element1, JsonTokenType element2TokenType, IJsonDocument element2ParentDocument, int element2ParentDocumentIndex)
        where TLeft : struct, IJsonElement
    {
        return DeepEquals(
            element1.TokenType,
            element2TokenType,
            element1.ParentDocument,
            element2ParentDocument,
            element1.ParentDocumentIndex,
            element2ParentDocumentIndex);
    }

    /// <summary>
    /// Compares the values of two JSON values for equality, including the values of all descendant elements.
    /// </summary>
    /// <param name="element1ParentDocument">The parent document containing the first JSON element.</param>
    /// <param name="element1ParentDocumentIndex">The index of the first JSON element within its parent document.</param>
    /// <param name="element2ParentDocument">The parent document containing the second JSON element.</param>
    /// <param name="element2ParentDocumentIndex">The index of the second JSON element within its parent document.</param>
    /// <returns><see langword="true"/> if the two values are equal; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// Deep equality of two JSON values is defined as follows:
    /// <list type="bullet">
    /// <item>JSON values of different kinds are not equal.</item>
    /// <item>JSON constants <see langword="null"/>, <see langword="false"/>, and <see langword="true"/> only equal themselves.</item>
    /// <item>JSON numbers are equal if and only if they have they have equivalent decimal representations, with no rounding being used.</item>
    /// <item>JSON strings are equal if and only if they are equal using ordinal string comparison.</item>
    /// <item>JSON arrays are equal if and only if they are of equal length and each of their elements are pairwise equal.</item>
    /// <item>
    /// JSON objects are equal if and only if they have the same number of properties and each property in the first object
    /// has a corresponding property in the second object with the same name and equal value. The order of properties is not
    /// significant. Repeated properties are not supported, though they will resolve each value in the second instance to the
    /// last value in the first instance.
    /// </item>
    /// </list>
    /// </remarks>
    [CLSCompliant(false)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool DeepEqualsNoParentDocumentCheck(IJsonDocument element1ParentDocument, int element1ParentDocumentIndex, IJsonDocument element2ParentDocument, int element2ParentDocumentIndex)
    {
        return DeepEquals(
            element1ParentDocument.GetJsonTokenType(element1ParentDocumentIndex),
            element2ParentDocument.GetJsonTokenType(element2ParentDocumentIndex),
            element1ParentDocument,
            element2ParentDocument,
            element1ParentDocumentIndex,
            element2ParentDocumentIndex);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool DeepEqualsNoParentDocumentCheck<TLeft, TRight>(in TLeft element1, in TRight element2)
        where TLeft : struct, IJsonElement
        where TRight : struct, IJsonElement
    {
        return DeepEquals(
            element1.TokenType,
            element2.TokenType,
            element1.ParentDocument,
            element2.ParentDocument,
            element1.ParentDocumentIndex,
            element2.ParentDocumentIndex);
    }

    /// <summary>
    /// Compares the values of two JSON elements for equality based on their value kinds and document information.
    /// </summary>
    /// <param name="element1TokenType">The token type of the first JSON element.</param>
    /// <param name="element2TokenType">The token type of the second JSON element.</param>
    /// <param name="element1ParentDocument">The parent document containing the first JSON element.</param>
    /// <param name="element2ParentDocument">The parent document containing the second JSON element.</param>
    /// <param name="element1ParentDocumentIndex">The index of the first JSON element within its parent document.</param>
    /// <param name="element2ParentDocumentIndex">The index of the second JSON element within its parent document.</param>
    /// <returns><see langword="true"/> if the two JSON elements are equal; otherwise, <see langword="false"/>.</returns>
    private static bool DeepEquals(
        JsonTokenType element1TokenType,
        JsonTokenType element2TokenType,
        IJsonDocument element1ParentDocument,
        IJsonDocument element2ParentDocument,
        int element1ParentDocumentIndex,
        int element2ParentDocumentIndex)
    {
        if (element1TokenType != element2TokenType)
        {
            return false;
        }

        switch (element1TokenType)
        {
            case JsonTokenType.Null or JsonTokenType.False or JsonTokenType.True:
                return true;

            case JsonTokenType.Number:
            {
                return AreEqualJsonNumbers(
                    element1ParentDocument.GetRawSimpleValueUnsafe(element1ParentDocumentIndex).Span,
                    element2ParentDocument.GetRawSimpleValueUnsafe(element2ParentDocumentIndex).Span);
            }

            case JsonTokenType.String:
            {
                if (element2ParentDocument.ValueIsEscaped(element2ParentDocumentIndex, isPropertyName: false))
                {
                    if (element1ParentDocument.ValueIsEscaped(element1ParentDocumentIndex, isPropertyName: false))
                    {
                        // Need to unescape and compare both inputs.
                        return JsonReaderHelper.UnescapeAndCompareBothInputs(
                            element1ParentDocument.GetRawSimpleValueUnsafe(element1ParentDocumentIndex).Span,
                            element2ParentDocument.GetRawSimpleValueUnsafe(element2ParentDocumentIndex).Span);
                    }

                    // Note that we do not require the TokenType null test of the JsonElement ValueEquals, as this is TokenType string
                    // Swap values so that unescaping is handled by the LHS.
                    return element2ParentDocument.TextEquals(
                        element2ParentDocumentIndex,
                        element1ParentDocument.GetRawSimpleValueUnsafe(element1ParentDocumentIndex).Span,
                        isPropertyName: false,
                        shouldUnescape: true);
                }

                // As above, note that we do not require the TokenType null test of the JsonElement ValueEquals, as this is TokenType string
                return element1ParentDocument.TextEquals(
                    element1ParentDocumentIndex,
                    element2ParentDocument.GetRawSimpleValueUnsafe(element2ParentDocumentIndex).Span,
                    isPropertyName: false,
                    shouldUnescape: true);
            }

            case JsonTokenType.StartArray:
            {
                ArrayEnumerator<JsonElement> arrayEnumerator2 = new(element2ParentDocument, element2ParentDocumentIndex);
                var arrayEnumerator1 = new ArrayEnumerator<JsonElement>(element1ParentDocument, element1ParentDocumentIndex);
                while (arrayEnumerator1.MoveNext())
                {
                    if (!arrayEnumerator2.MoveNext())
                    {
                        return false;
                    }

                    if (!DeepEqualsNoParentDocumentCheck(arrayEnumerator1.Current, arrayEnumerator2.Current))
                    {
                        return false;
                    }
                }

                return !arrayEnumerator2.MoveNext();
            }

            case JsonTokenType.StartObject:
            {
                ObjectEnumerator<JsonElement> objectEnumerator1 = new(element1ParentDocument, element1ParentDocumentIndex);
                ObjectEnumerator<JsonElement> objectEnumerator2 = new(element2ParentDocument, element2ParentDocumentIndex);

                // Two JSON objects are considered equal if they define the same set of properties.
                // Start optimistically with pairwise comparison, but fall back to unordered
                // comparison as soon as a mismatch is encountered.
                while (objectEnumerator1.MoveNext())
                {
                    if (!objectEnumerator2.MoveNext())
                    {
                        return false;
                    }

                    JsonProperty<JsonElement> prop1 = objectEnumerator1.Current;
                    JsonProperty<JsonElement> prop2 = objectEnumerator2.Current;

                    if (!NameEquals(prop1, prop2))
                    {
                        // We have our first mismatch, fall back to unordered comparison.
                        return element1ParentDocument.GetPropertyCount(element1ParentDocumentIndex) == element2ParentDocument.GetPropertyCount(element2ParentDocumentIndex) && UnorderedObjectDeepEquals(element1ParentDocument, element1ParentDocumentIndex, ref objectEnumerator2);
                    }

                    if (!DeepEqualsNoParentDocumentCheck(prop1.Value, prop2.Value))
                    {
                        return false;
                    }
                }

                return !objectEnumerator2.MoveNext();
            }

            default:
                Debug.Fail("Unexpected token type.");
                return false;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool NameEquals(JsonProperty<JsonElement> left, JsonProperty<JsonElement> right)
    {
        if (right.NameIsEscaped)
        {
            if (left.NameIsEscaped)
            {
                // Need to unescape and compare both inputs.
                return JsonReaderHelper.UnescapeAndCompareBothInputs(left.RawNameSpan, right.RawNameSpan);
            }

            // Swap values so that unescaping is handled by the LHS
            (left, right) = (right, left);
        }

        return left.NameEquals(right.RawNameSpan);
    }

    private static bool UnorderedObjectDeepEquals(IJsonDocument element1ParentDocument, int element1ParentDocumentIndex, ref ObjectEnumerator<JsonElement> objectEnumerator2)
    {
        // JsonElement objects allow duplicate property names, which is optional per the JSON RFC.
        // Even though this implementation of equality does not take property ordering into account,
        // duplicate, out of order properties resolve the value in the second instance to the last value
        // in the first instance. This differs from the System.Text.Json.JsonElement implementation, which supports duplicate
        // property names, if they are in order.
        // Note that this is because we *do not* support duplicate property names in our JSON Schema implementation.
        element1ParentDocument.EnsurePropertyMap(element1ParentDocumentIndex);

        Span<byte> buffer = stackalloc byte[JsonConstants.StackallocByteThreshold];

        do
        {
            JsonProperty<JsonElement> right = objectEnumerator2.Current;
            JsonElement leftValue;
            if (right.NameIsEscaped)
            {
                ReadOnlySpan<byte> rightNameSpan = right.RawNameSpan;
                int index = rightNameSpan.IndexOf(JsonConstants.BackSlash);
                Debug.Assert(index >= 0, "the name is not escaped");

                byte[]? unescapedRightNameArray = null;

                Span<byte> unescapedRightNameSpan = rightNameSpan.Length <= JsonConstants.StackallocByteThreshold ?
                    buffer :
                    (unescapedRightNameArray = ArrayPool<byte>.Shared.Rent(rightNameSpan.Length));

                JsonReaderHelper.Unescape(rightNameSpan, unescapedRightNameSpan, index, out int written);
                unescapedRightNameSpan = unescapedRightNameSpan.Slice(0, written);
                Debug.Assert(!unescapedRightNameSpan.IsEmpty);

                try
                {
                    if (!element1ParentDocument.TryGetNamedPropertyValue(element1ParentDocumentIndex, unescapedRightNameSpan, out leftValue) ||
                        !DeepEquals(leftValue, right.Value))
                    {
                        return false;
                    }
                }
                finally
                {
                    if (unescapedRightNameArray != null)
                    {
                        unescapedRightNameSpan.Clear();
                        ArrayPool<byte>.Shared.Return(unescapedRightNameArray);
                    }
                }
            }
            else
            {
                if (!element1ParentDocument.TryGetNamedPropertyValue(element1ParentDocumentIndex, right.RawNameSpan, out leftValue) ||
                    !DeepEquals(leftValue, right.Value))
                {
                    return false;
                }
            }
        }
        while (objectEnumerator2.MoveNext());

        return true;
    }
}