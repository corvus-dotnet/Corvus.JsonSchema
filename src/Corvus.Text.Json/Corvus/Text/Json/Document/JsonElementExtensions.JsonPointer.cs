// <copyright file="JsonElementExtensions.JsonPointer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
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
}