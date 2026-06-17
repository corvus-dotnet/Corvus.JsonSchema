// <copyright file="JsonMarshal.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;

namespace Corvus.Runtime.InteropServices;

/// <summary>
/// An unsafe class that provides a set of methods to access the underlying data representations of JSON types.
/// </summary>
public static class JsonMarshal
{
    /// <summary>
    /// Gets a <see cref="ReadOnlySpan{T}"/> view over the raw JSON data of the given <see cref="JsonProperty"/> name.
    /// </summary>
    /// <param name="property">The JSON property from which to extract the span.</param>
    /// <returns>The span containing the raw JSON data of the <paramref name="property"/> name. This will not include the enclosing quotes.</returns>
    /// <exception cref="ObjectDisposedException">The underlying <see cref="JsonDocument"/> has been disposed.</exception>
    /// <remarks>
    /// <para>
    /// While the method itself does check for disposal of the underlying <see cref="JsonDocument"/>,
    /// it is possible that it could be disposed after the method returns, which would result in
    /// the span pointing to a buffer that has been returned to the shared pool. Callers should take
    /// extra care to make sure that such a scenario isn't possible to avoid potential data corruption.
    /// </para>
    /// </remarks>
    [CLSCompliant(false)]
    public static ReadOnlySpan<byte> GetRawUtf8PropertyName<T>(JsonProperty<T> property)
        where T : struct, IJsonElement<T>
    {
        return property.RawNameSpan;
    }

    /// <summary>
    /// Gets a value indicating whether the raw UTF-8 property name contains escaped JSON text.
    /// </summary>
    /// <typeparam name="T">The type of the <see cref="IJsonElement"/>.</typeparam>
    /// <param name="property">The JSON property to inspect.</param>
    /// <returns><see langword="true"/> if the raw property name contains escaped JSON text; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">The underlying <see cref="JsonDocument"/> has been disposed.</exception>
    [CLSCompliant(false)]
    public static bool IsPropertyNameEscaped<T>(JsonProperty<T> property)
        where T : struct, IJsonElement<T>
    {
        property.Value.CheckValidInstance();
        return property.NameIsEscaped;
    }

    /// <summary>
    /// Gets a value indicating whether the raw UTF-8 value contains escaped JSON text.
    /// </summary>
    /// <typeparam name="T">The type of the <see cref="IJsonElement"/>.</typeparam>
    /// <param name="element">The JSON element to inspect.</param>
    /// <returns><see langword="true"/> if the raw value contains escaped JSON text; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">The underlying <see cref="JsonDocument"/> has been disposed.</exception>
    [CLSCompliant(false)]
    public static bool IsValueEscaped<T>(T element)
        where T : struct, IJsonElement
    {
        element.CheckValidInstance();
        return element.ParentDocument.ValueIsEscaped(element.ParentDocumentIndex, isPropertyName: false);
    }

    /// <summary>
    /// Gets a <see cref="ReadOnlySpan{T}"/> view over the raw JSON data of the given JSON element.
    /// </summary>
    /// <typeparam name="T">The type of the <see cref="IJsonElement"/>.</typeparam>
    /// <param name="element">The JSON element from which to extract the span.</param>
    /// <returns>The span containing the raw JSON data of<paramref name="element"/>.</returns>
    /// <exception cref="ObjectDisposedException">The underlying <see cref="JsonDocument"/> has been disposed.</exception>
    /// <remarks>
    /// While the method itself does check for disposal of the underlying <see cref="JsonDocument"/>,
    /// it is possible that it could be disposed after the method returns, which would result in
    /// the span pointing to a buffer that has been returned to the shared pool. Callers should take
    /// extra care to make sure that such a scenario isn't possible to avoid potential data corruption.
    /// </remarks>
    [CLSCompliant(false)]
    public static RawUtf8JsonString GetRawUtf8Value<T>(T element)
        where T : struct, IJsonElement
    {
        element.CheckValidInstance();
        return element.ParentDocument.GetRawValue(element.ParentDocumentIndex, includeQuotes: true);
    }

    /// <summary>
    /// Returns a fresh handle to a mutable JSON element whose cached document version is brought up to
    /// date with the parent document's current version, <strong>without</strong> re-validating the
    /// element's position.
    /// </summary>
    /// <typeparam name="T">The type of the mutable <see cref="IJsonElement"/>.</typeparam>
    /// <param name="element">The mutable element to refresh.</param>
    /// <returns>A new <typeparamref name="T"/> for the same element, stamped with the parent document's
    /// current version.</returns>
    /// <exception cref="ObjectDisposedException">The underlying <see cref="JsonDocument"/> has been disposed.</exception>
    /// <remarks>
    /// <para>
    /// This is a <strong>dangerous</strong>, high-performance helper. It is <strong>only</strong> safe
    /// to call when you know that your mutations cannot have changed <paramref name="element"/>'s index
    /// in its parent document. Unlike the other accessors here it deliberately does <strong>not</strong>
    /// validate the element first — refreshing a version-stale handle is the whole point — so the
    /// position is taken on trust.
    /// </para>
    /// <para>
    /// That guarantee holds <strong>only</strong> when the document has been mutated entirely
    /// <em>within this element's own subtree</em> (its descendant nodes) — never the element itself
    /// (e.g. replaced), an ancestor, or a <em>preceding sibling</em> (whose size change would shift this
    /// element). A descendant mutation only grows or shrinks the document at indices <em>after</em> the
    /// element's start row, so that start row — and hence the element's index — is unchanged, and only
    /// the cached version needs bringing up to date so subsequent staleness checks pass. Call it after
    /// any other kind of mutation and it returns a handle that <strong>silently addresses the wrong
    /// node</strong> — the memory-corruption class that version tracking exists to prevent. If you
    /// cannot prove the index is unchanged, re-navigate from the (always-live) root element instead.
    /// </para>
    /// <para>
    /// The canonical use is RFC 7396 merge-patch application, which re-mints the parent element after
    /// recursively merging into one of its child objects.
    /// </para>
    /// </remarks>
    [CLSCompliant(false)]
    public static T RefreshUnsafe<T>(in T element)
        where T : struct, IMutableJsonElement<T>
    {
        return ((IMutableJsonDocument)element.ParentDocument).RefreshElementUnsafe<T>(element.ParentDocumentIndex);
    }
}