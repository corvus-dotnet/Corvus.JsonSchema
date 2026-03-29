// <copyright file="Utf8UriReferenceValue.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Buffers;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json;

/// <summary>
/// A UTF-8 URI reference value that has been parsed from a JSON document.
/// </summary>
/// <remarks>
/// This type should be used in a using declaration to ensure that the underlying memory is released when it is no longer needed.
/// </remarks>
public readonly struct Utf8UriReferenceValue
    : IDisposable
{
    private readonly Utf8UriOffset _offsets;

    private readonly Utf8UriTools.Flags _flags;

    private readonly ReadOnlyMemory<byte> _bytes;

    private readonly byte[]? _extraRentedArrayPoolBytes;

    private Utf8UriReferenceValue(ReadOnlyMemory<byte> bytes, byte[]? extraRentedArrayPoolBytes, Utf8UriOffset offsets, Utf8UriTools.Flags flags)
    {
        _bytes = bytes;
        _extraRentedArrayPoolBytes = extraRentedArrayPoolBytes;
        _offsets = offsets;
        _flags = flags;
    }

    /// <summary>
    /// Gets the UTF-8 URI reference value.
    /// </summary>
    public Utf8UriReference UriReference => Utf8UriReference.CreateUriReferenceUnsafe(_bytes, _offsets, _flags);

    /// <summary>
    /// Tries to get the value of the element at the specified index as a <see cref="Utf8UriReferenceValue"/>.
    /// </summary>
    /// <typeparam name="T">The type of the document.</typeparam>
    /// <param name="index">The index of the element.</param>
    /// <param name="value">The <see cref="Utf8UriReferenceValue"/> value.</param>
    /// <returns><c>true</c> if the value was retrieved; otherwise, <c>false</c>.</returns>
    [CLSCompliant(false)]
    public static bool TryGetValue<T>(in T jsonDocument, int index, out Utf8UriReferenceValue value)
        where T : IJsonDocument
    {
        if (jsonDocument.GetJsonTokenType(index) != JsonTokenType.String)
        {
            value = default;
            return false;
        }

        using UnescapedUtf8JsonString stringBacking = jsonDocument.GetUtf8JsonString(index, JsonTokenType.String);
        ReadOnlyMemory<byte> bytes = stringBacking.TakeOwnership(out byte[]? extraRentedArrayPoolBytes);
        if (Utf8UriReference.TryCreateUriReference(bytes.Span, out Utf8UriReference utf8UriReference))
        {
            value = new Utf8UriReferenceValue(bytes, extraRentedArrayPoolBytes, utf8UriReference._offsets, utf8UriReference._flags);
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Disposes the underlying resources used to store the UTF-8 string backing the URI reference value.
    /// </summary>
    public void Dispose()
    {
        if (_extraRentedArrayPoolBytes is not null)
        {
            ArrayPool<byte>.Shared.Return(_extraRentedArrayPoolBytes);
        }
    }
}