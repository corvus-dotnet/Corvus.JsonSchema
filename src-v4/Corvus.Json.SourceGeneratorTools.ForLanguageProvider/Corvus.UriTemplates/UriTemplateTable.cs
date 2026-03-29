// <copyright file="UriTemplateTable.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.UriTemplates;

/// <summary>
/// Create builders for a <see cref="UriTemplateTable{TMatch}"/>.
/// </summary>
public static class UriTemplateTable
{
    /// <summary>
    /// Create an instance of a URI template table builder.
    /// </summary>
    /// <typeparam name="TMatch">The type of the match values.</typeparam>
    /// <returns>A builder for a URI template table.</returns>
    public static UriTemplateTable<TMatch>.Builder CreateBuilder<TMatch>()
    {
        return new UriTemplateTable<TMatch>.Builder();
    }

    /// <summary>
    /// Create an instance of a URI template table builder.
    /// </summary>
    /// <typeparam name="TMatch">The type of the match values.</typeparam>
    /// <param name="initialCapacity">The initial capacity of the table.</param>
    /// <returns>A builder for a URI template table.</returns>
    public static UriTemplateTable<TMatch>.Builder CreateBuilder<TMatch>(int initialCapacity)
    {
        return new UriTemplateTable<TMatch>.Builder(initialCapacity);
    }
}