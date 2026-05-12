// <copyright file="UriTemplateAndVerbTable.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.UriTemplates;

/// <summary>
/// Extensions to support <see cref="UriTemplateTable{TMatch}"/> matching with UriTemplate and Verb.
/// </summary>
/// <remarks>
/// This uses <see cref="MatchWithVerb{TMatch}"/> as the result type of the UriTemplateTable match to provide an
/// additional indirection through a verb.
/// </remarks>
public static class UriTemplateAndVerbTable
{
    /// <summary>
    /// Creates an instance of the <see cref="UriTemplateAndVerbTable{TMatch}.Builder"/>.
    /// </summary>
    /// <typeparam name="TMatch">The type of the match result.</typeparam>
    /// <returns>An instance of a <see cref="UriTemplateAndVerbTable{TMatch}.Builder"/>.</returns>
    public static UriTemplateAndVerbTable<TMatch>.Builder CreateBuilder<TMatch>() => new();
}