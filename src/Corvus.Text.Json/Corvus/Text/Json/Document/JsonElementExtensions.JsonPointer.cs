// <copyright file="JsonElementExtensions.JsonPointer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Diagnostics;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json;

/// <summary>
/// Extension methods for JSON Pointer (RFC 6901) resolution on <see cref="IJsonElement{T}"/> types.
/// </summary>
public static partial class JsonElementExtensions
{
    /// <summary>
    /// Tries to resolve the specified JSON Pointer (RFC 6901) against this element,
    /// returning the value at the target path if it exists.
    /// </summary>
    /// <typeparam name="T">The type of the element.</typeparam>
    /// <param name="element">The element at the root of the path.</param>
    /// <param name="utf8Pointer">The JSON Pointer as a UTF-8 byte span. Must be a valid RFC 6901 pointer
    /// (either the empty string, or a sequence of <c>/</c>-prefixed reference tokens).
    /// The <c>#</c>-prefixed URI fragment form is <b>not</b> accepted.</param>
    /// <param name="result">When this method returns <see langword="true"/>, contains the element at the
    /// target path; otherwise, the default value.</param>
    /// <returns><see langword="true"/> if the pointer was resolved successfully; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool TryResolvePointer<T>(this T element, ReadOnlySpan<byte> utf8Pointer, out T result)
        where T : struct, IJsonElement<T>
    {
        if (!Utf8JsonPointer.TryCreateJsonPointer(utf8Pointer, out Utf8JsonPointer pointer))
        {
            result = default;
            return false;
        }

        return pointer.TryResolve<T, T>(in element, out result);
    }

    /// <summary>
    /// Tries to resolve the specified JSON Pointer (RFC 6901) against this element,
    /// returning the value at the target path if it exists.
    /// </summary>
    /// <typeparam name="T">The type of the element.</typeparam>
    /// <param name="element">The element at the root of the path.</param>
    /// <param name="pointer">The JSON Pointer as a UTF-16 character span. Will be transcoded to UTF-8 internally.</param>
    /// <param name="result">When this method returns <see langword="true"/>, contains the element at the
    /// target path; otherwise, the default value.</param>
    /// <returns><see langword="true"/> if the pointer was resolved successfully; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool TryResolvePointer<T>(this T element, ReadOnlySpan<char> pointer, out T result)
        where T : struct, IJsonElement<T>
    {
        if (pointer.Length == 0)
        {
            return TryResolvePointer(element, ReadOnlySpan<byte>.Empty, out result);
        }

        int expectedByteCount = JsonReaderHelper.GetUtf8ByteCount(pointer);
        byte[]? rentedArray = null;
        Span<byte> utf8Buffer = expectedByteCount <= JsonConstants.StackallocByteThreshold
            ? stackalloc byte[JsonConstants.StackallocByteThreshold]
            : (rentedArray = ArrayPool<byte>.Shared.Rent(expectedByteCount));

        try
        {
            int actualByteCount = JsonReaderHelper.TranscodeHelper(pointer, utf8Buffer);
            return TryResolvePointer(element, utf8Buffer.Slice(0, actualByteCount), out result);
        }
        finally
        {
            if (rentedArray is not null)
            {
                ArrayPool<byte>.Shared.Return(rentedArray);
            }
        }
    }

    /// <summary>
    /// Tries to resolve the specified JSON Pointer (RFC 6901) against this element,
    /// returning the value at the target path if it exists.
    /// </summary>
    /// <typeparam name="T">The type of the element.</typeparam>
    /// <param name="element">The element at the root of the path.</param>
    /// <param name="pointer">The JSON Pointer as a string.</param>
    /// <param name="result">When this method returns <see langword="true"/>, contains the element at the
    /// target path; otherwise, the default value.</param>
    /// <returns><see langword="true"/> if the pointer was resolved successfully; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool TryResolvePointer<T>(this T element, string pointer, out T result)
        where T : struct, IJsonElement<T>
    {
        return TryResolvePointer(element, pointer.AsSpan(), out result);
    }

    /// <summary>
    /// Tries to write the JSON Pointer (RFC 6901) of this element, relative to the root of its
    /// backing document, as UTF-8 bytes.
    /// </summary>
    /// <typeparam name="T">The type of the element.</typeparam>
    /// <param name="element">The element whose pointer to derive.</param>
    /// <param name="utf8Destination">The destination for the UTF-8 pointer text.</param>
    /// <param name="bytesWritten">When this method returns <see langword="true"/>, the number of bytes written; otherwise 0.</param>
    /// <returns><see langword="true"/> if the pointer was written; <see langword="false"/> if the
    /// destination was too small, or the element is a default instance.</returns>
    /// <remarks>
    /// The root element produces the empty pointer. Property names are unescaped and then
    /// pointer-escaped (<c>~</c> as <c>~0</c>, <c>/</c> as <c>~1</c>) per RFC 6901. This
    /// operation performs no heap allocation.
    /// </remarks>
    [CLSCompliant(false)]
    public static bool TryGetJsonPointer<T>(this T element, Span<byte> utf8Destination, out int bytesWritten)
        where T : struct, IJsonElement<T>
    {
        if (element.ParentDocument is null)
        {
            bytesWritten = 0;
            return false;
        }

        element.CheckValidInstance();
        return element.ParentDocument.TryGetJsonPointer(element.ParentDocumentIndex, utf8Destination, out bytesWritten, out _);
    }

    /// <summary>
    /// Tries to write the JSON Pointer (RFC 6901) of this element, relative to the root of its
    /// backing document, as UTF-16 characters.
    /// </summary>
    /// <typeparam name="T">The type of the element.</typeparam>
    /// <param name="element">The element whose pointer to derive.</param>
    /// <param name="destination">The destination for the pointer text.</param>
    /// <param name="charsWritten">When this method returns <see langword="true"/>, the number of characters written; otherwise 0.</param>
    /// <returns><see langword="true"/> if the pointer was written; <see langword="false"/> if the
    /// destination was too small, or the element is a default instance.</returns>
    /// <remarks>
    /// The root element produces the empty pointer. Property names are unescaped and then
    /// pointer-escaped (<c>~</c> as <c>~0</c>, <c>/</c> as <c>~1</c>) per RFC 6901. This
    /// operation performs no heap allocation.
    /// </remarks>
    [CLSCompliant(false)]
    public static bool TryGetJsonPointer<T>(this T element, Span<char> destination, out int charsWritten)
        where T : struct, IJsonElement<T>
    {
        if (element.ParentDocument is null)
        {
            charsWritten = 0;
            return false;
        }

        element.CheckValidInstance();
        return element.ParentDocument.TryGetJsonPointer(element.ParentDocumentIndex, destination, out charsWritten, out _);
    }

    /// <summary>
    /// Gets the JSON Pointer (RFC 6901) of this element, relative to the root of its backing
    /// document, as a string.
    /// </summary>
    /// <typeparam name="T">The type of the element.</typeparam>
    /// <param name="element">The element whose pointer to derive.</param>
    /// <returns>The JSON Pointer. The root element produces the empty string.</returns>
    /// <exception cref="InvalidOperationException">
    /// The element is a default instance.
    /// </exception>
    /// <remarks>
    /// Property names are unescaped and then pointer-escaped (<c>~</c> as <c>~0</c>,
    /// <c>/</c> as <c>~1</c>) per RFC 6901.
    /// </remarks>
    [CLSCompliant(false)]
    public static string GetJsonPointer<T>(this T element)
        where T : struct, IJsonElement<T>
    {
        element.CheckValidInstance();
        return GetJsonPointerCore(element.ParentDocument, element.ParentDocumentIndex);
    }

    /// <summary>
    /// Derives the JSON Pointer of the element at the specified index as a string, trying a
    /// stack buffer first and renting the exact required size when the pointer is longer.
    /// </summary>
    /// <param name="parent">The document backing the element.</param>
    /// <param name="index">The index of the element.</param>
    /// <returns>The JSON Pointer.</returns>
    internal static string GetJsonPointerCore(IJsonDocument parent, int index)
    {
        Span<char> buffer = stackalloc char[JsonConstants.StackallocCharThreshold];
        if (parent.TryGetJsonPointer(index, buffer, out int written, out int required))
        {
            return buffer.Slice(0, written).ToString();
        }

        char[] rented = ArrayPool<char>.Shared.Rent(required);
        try
        {
            bool success = parent.TryGetJsonPointer(index, rented, out written, out _);
            Debug.Assert(success, "The exact required size must always fit");
            return rented.AsSpan(0, written).ToString();
        }
        finally
        {
            ArrayPool<char>.Shared.Return(rented);
        }
    }
}