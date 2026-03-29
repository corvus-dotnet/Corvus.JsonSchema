// <copyright file="JsonElementHelpers.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Buffers;

#if !NET
using System.Collections.Concurrent;
#endif

using System.Diagnostics;

#if !NET
using System.Reflection;
using System.Reflection.Emit;
using Corvus.Text.Json.Internal;
#endif

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Provides helper methods and utilities for working with JSON elements, including property manipulation,
/// type conversions, string operations, and element metadata retrieval.
/// </summary>
public static partial class JsonElementHelpers
{
    /// <summary>
    /// Sets a property value on a target element.
    /// </summary>
    /// <typeparam name="TTarget">The type of the target element.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="targetElement">The target element instance.</param>
    /// <param name="property">The property to set.</param>
    [CLSCompliant(false)]
    public static void SetPropertyUnsafe<TTarget, TValue>(TTarget targetElement, JsonProperty<TValue> property)
        where TTarget : struct, IMutableJsonElement<TTarget>
        where TValue : struct, IJsonElement<TValue>
    {
        using UnescapedUtf8JsonString name = property.Utf8NameSpan;
        var targetParentDocument = (IMutableJsonDocument)targetElement.ParentDocument;
        var cvb = ComplexValueBuilder.Create(targetParentDocument, 30);
        if (targetElement.ParentDocument.TryGetNamedPropertyValue(targetElement.ParentDocumentIndex, name.Span, out TValue value))
        {
            // We are going to replace just the value
            cvb.AddItem(property.Value);
            targetParentDocument.OverwriteAndDispose(
                targetElement.ParentDocumentIndex,
                value.ParentDocumentIndex,
                value.ParentDocumentIndex + value.ParentDocument.GetDbSize(value.ParentDocumentIndex, true),
                1,
                ref cvb);
        }
        else
        {
            cvb.AddProperty(name.Span, property.Value, escapeName: true, nameRequiresUnescaping: false);
            int endIndex = targetElement.ParentDocumentIndex + targetParentDocument.GetDbSize(targetElement.ParentDocumentIndex, false);
            targetParentDocument.InsertAndDispose(targetElement.ParentDocumentIndex, endIndex, ref cvb);
        }
    }

    /// <summary>
    /// Removes a property value from a target element.
    /// </summary>
    /// <param name="targetElement">The target element instance.</param>
    /// <param name="propertyName">The name of the property to remove.</param>
    /// <returns><see langword="true"/> if the property was found and removed; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool RemovePropertyUnsafe(IMutableJsonDocument parentDocument, int parentDocumentIndex, ReadOnlySpan<char> propertyName)
    {
        if (parentDocument.TryGetNamedPropertyValueIndex(parentDocumentIndex, propertyName, out int index))
        {
            // We are going to replace just the value
            parentDocument.RemoveRange(
                parentDocumentIndex,
                index - DbRow.Size, // Start with the property name
                index + parentDocument.GetDbSize(index, true),
                1);

            return true;
        }

        return false;
    }

    /// <summary>
    /// Removes a property value from a target element.
    /// </summary>
    /// <param name="targetElement">The target element instance.</param>
    /// <param name="propertyName">The name of the property to remove.</param>
    /// <returns><see langword="true"/> if the property was found and removed; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool RemovePropertyUnsafe(IMutableJsonDocument parentDocument, int parentDocumentIndex, ReadOnlySpan<byte> propertyName)
    {
        if (parentDocument.TryGetNamedPropertyValueIndex(parentDocumentIndex, propertyName, out int index))
        {
            // We are going to replace just the value
            parentDocument.RemoveRange(
                parentDocumentIndex,
                index - DbRow.Size, // Start with the property name
                index + parentDocument.GetDbSize(index, true),
                1);

            return true;
        }

        return false;
    }

    /// <summary>
    /// Removes a range of items from an array element.
    /// </summary>
    /// <typeparam name="TArray">The type of the array element.</typeparam>
    /// <param name="arrayElement">The array element instance.</param>
    /// <param name="startIndex">The zero-based index at which to begin removing items.</param>
    /// <param name="count">The number of items to remove.</param>
    /// <exception cref="InvalidOperationException">
    /// The element's <see cref="JsonValueKind"/> is not <see cref="JsonValueKind.Array"/>,
    /// or the element reference is stale due to document mutations.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="startIndex"/> is negative or greater than the current array length,
    /// or <paramref name="count"/> is negative or causes the operation to exceed the array bounds.
    /// </exception>
    [CLSCompliant(false)]
    public static void RemoveRangeUnsafe<TArray>(TArray arrayElement, int startIndex, int count)
        where TArray : struct, IMutableJsonElement<TArray>
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        if (count == 0)
        {
            return; // Nothing to remove
        }

        var parentDocument = (IMutableJsonDocument)arrayElement.ParentDocument;
        int arrayLength = parentDocument.GetArrayLength(arrayElement.ParentDocumentIndex);

        if (startIndex < 0 || startIndex > arrayLength)
        {
            throw new ArgumentOutOfRangeException(nameof(startIndex));
        }

        if (startIndex + count > arrayLength)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        // Get the first element in the range to determine the start index
        parentDocument.GetArrayIndexElement(arrayElement.ParentDocumentIndex, startIndex, out _, out int rangeStartIndex);

        // Get the last element in the range to determine the end index
        parentDocument.GetArrayIndexElement(arrayElement.ParentDocumentIndex, startIndex + count - 1, out IJsonDocument lastElementDoc, out int lastElementIndex);
        int rangeEndIndex = lastElementIndex + lastElementDoc.GetDbSize(lastElementIndex, true);

        // Remove the range using the document's RemoveRange method
        parentDocument.RemoveRange(arrayElement.ParentDocumentIndex, rangeStartIndex, rangeEndIndex, count);
    }

    /// <summary>
    /// Removes the first array element that equals the specified item.
    /// </summary>
    /// <typeparam name="TArray">The type of the array element.</typeparam>
    /// <typeparam name="T">The type of the item to find and remove.</typeparam>
    /// <param name="arrayElement">The array element instance.</param>
    /// <param name="item">The item to find and remove.</param>
    /// <returns><see langword="true"/> if an element was found and removed; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool RemoveFirstUnsafe<TArray, T>(TArray arrayElement, in T item)
        where TArray : struct, IMutableJsonElement<TArray>
        where T : struct, IJsonElement<T>
    {
        var parentDocument = (IMutableJsonDocument)arrayElement.ParentDocument;
        int arrayLength = parentDocument.GetArrayLength(arrayElement.ParentDocumentIndex);

        if (arrayLength == 0)
        {
            return false;
        }

        ArrayEnumerator<T> enumerator = new(parentDocument, arrayElement.ParentDocumentIndex);
        for (int logicalIndex = 0; enumerator.MoveNext(); logicalIndex++)
        {
            T current = enumerator.Current;
            if (DeepEquals(in current, in item))
            {
                RemoveRangeUnsafe(arrayElement, logicalIndex, 1);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Removes a items from an array element which match a predicate.
    /// </summary>
    /// <typeparam name="TArray">The type of the array element.</typeparam>
    /// <param name="arrayElement">The array element instance.</param>
    /// <param name="predicate">The predicate to apply to each element to determine if it should be removed.</param>
    /// <exception cref="InvalidOperationException">
    /// The element's <see cref="JsonValueKind"/> is not <see cref="JsonValueKind.Array"/>,
    /// or the element reference is stale due to document mutations.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="startIndex"/> is negative or greater than the current array length,
    /// or <paramref name="count"/> is negative or causes the operation to exceed the array bounds.
    /// </exception>
    [CLSCompliant(false)]
    public static void RemoveWhereUnsafe<TArray, T>(TArray arrayElement, JsonPredicate<T> predicate)
        where TArray : struct, IMutableJsonElement<TArray>
        where T : struct, IJsonElement<T>
    {
        if (predicate is null)
        {
            ThrowHelper.ThrowArgumentNullException(nameof(predicate));
        }

        var parentDocument = (IMutableJsonDocument)arrayElement.ParentDocument;
        int arrayLength = parentDocument.GetArrayLength(arrayElement.ParentDocumentIndex);

        if (arrayLength == 0)
        {
            return; // Nothing to process
        }

        // Enumerate backwards through the array
        ArrayReverseEnumerator enumerator = new(parentDocument, arrayElement.ParentDocumentIndex);

        int consecutiveBlockStartIndex = -1; // Start index of first item in consecutive block
        int consecutiveBlockEndIndex = -1;   // End index of last item in consecutive block
        int consecutiveCount = 0;

        while (enumerator.MoveNext())
        {
#if NET
            T currentElement = T.CreateInstance(parentDocument, enumerator.CurrentIndex);
#else
            T currentElement = JsonElementHelpers.CreateInstance<T>(parentDocument, enumerator.CurrentIndex);
#endif

            if (predicate(in currentElement))
            {
                // This element should be removed
                int currentStartIndex = enumerator.CurrentIndex;
                int currentEndIndex;

                if (consecutiveCount == 0)
                {
                    // Start of a new consecutive block
                    consecutiveBlockStartIndex = currentStartIndex;

                    // For the end index, we need the end of this element
                    currentEndIndex = enumerator.CurrentEndIndex + DbRow.Size;
                    consecutiveBlockEndIndex = currentEndIndex;
                    consecutiveCount = 1;
                }
                else
                {
                    // Check if this element is consecutive with the previous block
                    currentEndIndex = enumerator.CurrentEndIndex + DbRow.Size;

                    Debug.Assert(currentEndIndex == consecutiveBlockStartIndex, "The element must be consecutive with the previous block.");
                    consecutiveBlockStartIndex = currentStartIndex;
                    consecutiveCount++;
                }
            }
            else
            {
                // This element should not be removed
                if (consecutiveCount > 0)
                {
                    // Remove the accumulated consecutive block
                    int elementsInBlock = consecutiveCount;
                    parentDocument.RemoveRange(arrayElement.ParentDocumentIndex, consecutiveBlockStartIndex, consecutiveBlockEndIndex, elementsInBlock);
                    consecutiveCount = 0;
                }
            }
        }

        // Handle any remaining consecutive block at the end
        if (consecutiveCount > 0)
        {
            int elementsInBlock = consecutiveCount;
            parentDocument.RemoveRange(arrayElement.ParentDocumentIndex, consecutiveBlockStartIndex, consecutiveBlockEndIndex, elementsInBlock);
        }
    }

    /// <summary>
    /// Applies all properties from a source JSON object element to a target JSON object element.
    /// </summary>
    /// <typeparam name="TTarget">The type of the target element implementing <see cref="IMutableJsonElement{TTarget}"/>.</typeparam>
    /// <typeparam name="TSource">The type of the source element implementing <see cref="IJsonElement{TSource}"/>.</typeparam>
    /// <param name="targetElement">The target JSON object element to which properties will be applied.</param>
    /// <param name="sourceElement">The source JSON object element from which properties will be copied.</param>
    /// <exception cref="InvalidOperationException">
    /// The source element's <see cref="JsonValueKind"/> is not <see cref="JsonValueKind.Object"/>.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This method performs a merge of properties from the source JSON object
    /// to the target JSON object. Each property from the source object is copied to the target object,
    /// replacing any existing properties with the same name.
    /// </para>
    /// <para>
    /// The source element must be a JSON object element. The target element is assumed to be valid
    /// and is not validated by this method.
    /// </para>
    /// <para>
    /// This method is not CLS-compliant due to its generic constraint requirements.
    /// </para>
    /// </remarks>
    [CLSCompliant(false)]
    public static void ApplyUnsafe<TTarget, TSource>(TTarget targetElement, in TSource sourceElement)
        where TTarget : struct, IMutableJsonElement<TTarget>
        where TSource : struct, IJsonElement<TSource>
    {
        // Validate that the source value is an object
        if (sourceElement.TokenType != JsonTokenType.StartObject)
        {
            throw new InvalidOperationException();
        }

        var enumerator = new ObjectEnumerator<JsonElement>(sourceElement.ParentDocument, sourceElement.ParentDocumentIndex);

        while (enumerator.MoveNext())
        {
            JsonElementHelpers.SetPropertyUnsafe(targetElement, enumerator.Current);
        }
    }

    /// <summary>
    /// Converts a <see cref="JsonTokenType"/> to its corresponding <see cref="JsonValueKind"/>.
    /// </summary>
    /// <param name="tokenType">The token type to convert.</param>
    /// <returns>The corresponding value kind.</returns>
    public static JsonValueKind ToValueKind(this JsonTokenType tokenType)
    {
        switch (tokenType)
        {
            case JsonTokenType.None:
                return JsonValueKind.Undefined;

            case JsonTokenType.StartArray:
                return JsonValueKind.Array;

            case JsonTokenType.StartObject:
                return JsonValueKind.Object;

            case JsonTokenType.String:
            case JsonTokenType.Number:
            case JsonTokenType.True:
            case JsonTokenType.False:
            case JsonTokenType.Null:

                // This is the offset between the set of literals within JsonValueType and JsonTokenType
                // Essentially: JsonTokenType.Null - JsonValueType.Null
                return (JsonValueKind)((byte)tokenType - 4);

            default:
                Debug.Fail($"No mapping for token type {tokenType}");
                return JsonValueKind.Undefined;
        }
    }

    /// <summary>
    /// Gets the length of a UTF-8 encoded string in characters (not bytes).
    /// </summary>
    /// <param name="span">The UTF-8 encoded byte span.</param>
    /// <returns>The number of Unicode characters in the string.</returns>
    /// <exception cref="ArgumentException">Thrown when the span contains invalid UTF-8 sequences.</exception>
    public static int GetUtf8StringLength(ReadOnlySpan<byte> span)
    {
        if (span.Length == 0)
        {
            return 0;
        }

        int length = 0;
        ReadOnlySpan<byte> currentSpan = span;
        do
        {
            OperationStatus status = Rune.DecodeFromUtf8(currentSpan, out _, out int bytesConsumed);
            if (status != OperationStatus.Done)
            {
                ThrowHelper.ThrowArgumentException_InvalidUTF8(span);
            }

            currentSpan = currentSpan.Slice(bytesConsumed);
            length++;
        }
        while (currentSpan.Length > 0);

        return length;
    }

    /// <summary>
    /// Gets the parent document and document index for a JSON element.
    /// </summary>
    /// <typeparam name="TElement">The type of the JSON element.</typeparam>
    /// <param name="value">The JSON element value.</param>
    /// <returns>A tuple containing the parent document and the document index.</returns>
    [CLSCompliant(false)]
    public static (IJsonDocument parentDocument, int parentDocumentIndex) GetParentDocumentAndIndex<TElement>(TElement value)
        where TElement : struct, IJsonElement<TElement>
    {
        value.CheckValidInstance();
        return (value.ParentDocument, value.ParentDocumentIndex);
    }
}