// <copyright file="JsonElement.JsonPointer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json;

public readonly partial struct JsonElement
{
    /// <summary>
    /// Tries to write the JSON Pointer (RFC 6901) of this element, relative to the root of its
    /// backing document, as UTF-8 bytes.
    /// </summary>
    /// <param name="utf8Destination">The destination for the UTF-8 pointer text.</param>
    /// <param name="bytesWritten">When this method returns <see langword="true"/>, the number of bytes written; otherwise 0.</param>
    /// <returns><see langword="true"/> if the pointer was written; <see langword="false"/> if the
    /// destination was too small, or this is a default <see cref="JsonElement"/>.</returns>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    /// <remarks>
    /// <para>
    /// The root element produces the empty pointer. Property names are unescaped and then
    /// pointer-escaped (<c>~</c> as <c>~0</c>, <c>/</c> as <c>~1</c>) per RFC 6901. This
    /// operation performs no heap allocation.
    /// </para>
    /// <para>
    /// The pointer is relative to the root of the document that backs this element. In a
    /// <see cref="JsonWorkspace"/>, an element obtained from another document referenced by a
    /// builder reports its location within that referenced document.
    /// </para>
    /// </remarks>
    public bool TryGetJsonPointer(Span<byte> utf8Destination, out int bytesWritten)
    {
        if (_parent is null)
        {
            bytesWritten = 0;
            return false;
        }

        return _parent.TryGetJsonPointer(_idx, utf8Destination, out bytesWritten, out _);
    }

    /// <summary>
    /// Tries to write the JSON Pointer (RFC 6901) of this element, relative to the root of its
    /// backing document, as UTF-16 characters.
    /// </summary>
    /// <param name="destination">The destination for the pointer text.</param>
    /// <param name="charsWritten">When this method returns <see langword="true"/>, the number of characters written; otherwise 0.</param>
    /// <returns><see langword="true"/> if the pointer was written; <see langword="false"/> if the
    /// destination was too small, or this is a default <see cref="JsonElement"/>.</returns>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    /// <remarks>
    /// <para>
    /// The root element produces the empty pointer. Property names are unescaped and then
    /// pointer-escaped (<c>~</c> as <c>~0</c>, <c>/</c> as <c>~1</c>) per RFC 6901. This
    /// operation performs no heap allocation.
    /// </para>
    /// <para>
    /// The pointer is relative to the root of the document that backs this element. In a
    /// <see cref="JsonWorkspace"/>, an element obtained from another document referenced by a
    /// builder reports its location within that referenced document.
    /// </para>
    /// </remarks>
    public bool TryGetJsonPointer(Span<char> destination, out int charsWritten)
    {
        if (_parent is null)
        {
            charsWritten = 0;
            return false;
        }

        return _parent.TryGetJsonPointer(_idx, destination, out charsWritten, out _);
    }

    /// <summary>
    /// Gets the JSON Pointer (RFC 6901) of this element, relative to the root of its backing
    /// document, as a string.
    /// </summary>
    /// <returns>The JSON Pointer. The root element produces the empty string.</returns>
    /// <exception cref="InvalidOperationException">
    /// This is a default <see cref="JsonElement"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The parent <see cref="JsonDocument"/> has been disposed.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Property names are unescaped and then pointer-escaped (<c>~</c> as <c>~0</c>,
    /// <c>/</c> as <c>~1</c>) per RFC 6901.
    /// </para>
    /// <para>
    /// The pointer is relative to the root of the document that backs this element. In a
    /// <see cref="JsonWorkspace"/>, an element obtained from another document referenced by a
    /// builder reports its location within that referenced document.
    /// </para>
    /// </remarks>
    public string GetJsonPointer()
    {
        CheckValidInstance();
        return JsonElementExtensions.GetJsonPointerCore(_parent, _idx);
    }
}