// <copyright file="Utf8JsonPointer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json;

public readonly ref struct Utf8JsonPointer
{
    private readonly ReadOnlySpan<byte> _jsonPointer;

    private Utf8JsonPointer(ReadOnlySpan<byte> jsonPointer)
    {
        _jsonPointer = jsonPointer;
        IsValid = Utf8JsonPointerTools.Validate(jsonPointer);
    }

    /// <summary>
    /// Gets a value indicating whether this is a valid IRI.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// Tries to create a new UTF-8 JSON Pointer from the specified UTF-8 bytes.
    /// </summary>
    /// <param name="jsonPointer">The UTF-8 bytes from which to create the UTF-8 JSON Pointer.</param>
    /// <param name="utf8JsonPointer">When this method returns, contains the created UTF-8 JSON Pointer if successful; otherwise, the default value.</param>
    /// <returns><see langword="true"/> if the UTF-8 JSON Pointer was created successfully; otherwise, <see langword="false"/>.</returns>
    public static bool TryCreateJsonPointer(ReadOnlySpan<byte> jsonPointer, out Utf8JsonPointer utf8JsonPointer)
    {
        utf8JsonPointer = new(jsonPointer);
        return utf8JsonPointer.IsValid;
    }

    /// <summary>
    /// Try to resolve the path specified by this JSON Pointer against the provided JSON element, returning the value at that path if it exists.
    /// </summary>
    /// <typeparam name="T">The type of the element at the root of the path.</typeparam>
    /// <typeparam name="TResult">The type of the element at the target.</typeparam>
    /// <param name="jsonElement">The element at the root of the path.</param>
    /// <param name="value">The value at the target path if it exists.</param>
    /// <returns><see langword="true"/> if the value was resolved successfully; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public bool TryResolve<T, TResult>(in T jsonElement, out TResult value)
        where T : struct, IJsonElement<T>
        where TResult : struct, IJsonElement<TResult>
    {
        if (!IsValid)
        {
            value = default;
            return false;
        }

        jsonElement.CheckValidInstance();

        return jsonElement.ParentDocument.TryResolveJsonPointer(_jsonPointer, jsonElement.ParentDocumentIndex, out value);
    }

    /// <summary>
    /// Returns an enumerator that yields the encoded reference tokens (segments) of this JSON Pointer.
    /// </summary>
    /// <returns>A <see cref="JsonPointerEncodedSegmentEnumerator"/> that yields each segment as a
    /// <see cref="ReadOnlySpan{T}">ReadOnlySpan&lt;byte&gt;</see>. Escape sequences (<c>~0</c>, <c>~1</c>)
    /// are preserved; use <see cref="DecodeSegment(ReadOnlySpan{byte}, Span{byte})"/> to decode them.</returns>
    /// <remarks>
    /// <para>
    /// An empty pointer (root) yields zero segments. The pointer <c>"/"</c> yields a single empty segment
    /// (the property whose name is the empty string). See <see cref="JsonPointerEncodedSegmentEnumerator"/>
    /// for full enumeration semantics.
    /// </para>
    /// </remarks>
    public JsonPointerEncodedSegmentEnumerator EnumerateEncodedSegments()
    {
        return new JsonPointerEncodedSegmentEnumerator(_jsonPointer);
    }

    /// <summary>
    /// Decodes a single JSON Pointer segment, unescaping <c>~1</c> to <c>/</c> and <c>~0</c> to <c>~</c>
    /// as specified by RFC 6901.
    /// </summary>
    /// <param name="encodedSegment">The encoded pointer segment (without the leading <c>/</c>).</param>
    /// <param name="decodedSegment">The destination span to receive the decoded segment. Must be at least as long as <paramref name="encodedSegment"/>.</param>
    /// <returns>The number of bytes written to <paramref name="decodedSegment"/>.</returns>
    public static int DecodeSegment(ReadOnlySpan<byte> encodedSegment, Span<byte> decodedSegment)
    {
        return Utf8JsonPointerTools.DecodePointer(encodedSegment, decodedSegment);
    }

    /// <summary>
    /// Try to resolve the path specified by this JSON Pointer against the provided JSON element,
    /// returning the 1-based line number and character offset of the target element in the original source document.
    /// </summary>
    /// <typeparam name="T">The type of the element at the root of the path.</typeparam>
    /// <param name="jsonElement">The element at the root of the path.</param>
    /// <param name="line">When this method returns, contains the 1-based line number if successful.</param>
    /// <param name="charOffset">When this method returns, contains the 1-based character offset within the line if successful.</param>
    /// <param name="lineByteOffset">When this method returns, contains the byte offset of the start of the line if successful.</param>
    /// <returns><see langword="true"/> if the pointer was resolved and the line and offset were determined; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public bool TryGetLineAndOffset<T>(in T jsonElement, out int line, out int charOffset, out long lineByteOffset)
        where T : struct, IJsonElement<T>
    {
        if (!IsValid)
        {
            line = 0;
            charOffset = 0;
            lineByteOffset = 0;
            return false;
        }

        jsonElement.CheckValidInstance();

        return jsonElement.ParentDocument.TryGetLineAndOffsetForPointer(_jsonPointer, jsonElement.ParentDocumentIndex, out line, out charOffset, out lineByteOffset);
    }
}