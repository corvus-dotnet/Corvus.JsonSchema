// <copyright file="JsonElementHelpers.Reinterpret.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Provides helper methods and utilities for working with JSON elements.
/// </summary>
public static partial class JsonElementHelpers
{
    /// <summary>
    /// Reinterprets a source element as a different target element type, sharing the same
    /// underlying document and location. This is a zero-copy operation that creates a new
    /// struct view over the same data.
    /// </summary>
    /// <typeparam name="TSource">The source element type.</typeparam>
    /// <typeparam name="TTarget">The target element type.</typeparam>
    /// <param name="source">The source element to reinterpret.</param>
    /// <returns>A new instance of <typeparamref name="TTarget"/> backed by the same document location.</returns>
    /// <remarks>
    /// This uses a constrained call to access the parent document and index, avoiding
    /// boxing of the source struct to <see cref="IJsonElement"/>.
    /// </remarks>
    [CLSCompliant(false)]
    public static TTarget Reinterpret<TSource, TTarget>(in TSource source)
        where TSource : struct, IJsonElement<TSource>
        where TTarget : struct, IJsonElement<TTarget>
    {
#if NET
        return TTarget.CreateInstance(source.ParentDocument, source.ParentDocumentIndex);
#else
        return CreateInstance<TTarget>(source.ParentDocument, source.ParentDocumentIndex);
#endif
    }
}