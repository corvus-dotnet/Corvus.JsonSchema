// <copyright file="MatchWithVerb.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.UriTemplates;

/// <summary>
/// Factory for <see cref="MatchWithVerb{TMatch}"/>.
/// </summary>
public static class MatchWithVerb
{
    /// <summary>
    /// Gets a new builder.
    /// </summary>
    /// <typeparam name="TMatch">The type of the match.</typeparam>
    /// <returns>An instance of a builder for a verb matcher.</returns>
    public static MatchWithVerb<TMatch>.Builder CreateBuilder<TMatch>() => new();

    /// <summary>
    /// Gets a new builder.
    /// </summary>
    /// <typeparam name="TMatch">The type of the match.</typeparam>
    /// <param name="initialCapacity">The initial capacity of the dicionary.</param>
    /// <returns>An instance of a builder for a verb matcher.</returns>
    public static MatchWithVerb<TMatch>.Builder CreateBuilder<TMatch>(int initialCapacity) => new(initialCapacity);
}