// <copyright file="JsonMarshal.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
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
}