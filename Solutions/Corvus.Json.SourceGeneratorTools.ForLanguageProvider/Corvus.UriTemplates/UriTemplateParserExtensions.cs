// <copyright file="UriTemplateParserExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;

namespace Corvus.UriTemplates;

/// <summary>
/// A callback for enumerating parameters from the cache.
/// </summary>
/// <typeparam name="TState">The type of the state for the callback.</typeparam>
/// <param name="name">The name of the parameter.</param>
/// <param name="value">The value of the parameter.</param>
/// <param name="state">The state for the callback.</param>
public delegate void EnumerateParametersCallback<TState>(ReadOnlySpan<char> name, ReadOnlySpan<char> value, ref TState state);

/// <summary>
/// A callback for enumerating parameters from the cache.
/// </summary>
/// <typeparam name="TState">The type of the state for the callback.</typeparam>
/// <param name="name">The parameter name.</param>
/// <param name="valueRange">The range in the input URI string at which the parameter was found.</param>
/// <param name="state">The state for the callback.</param>
public delegate void EnumerateParametersCallbackWithRange<TState>(ParameterName name, Range valueRange, ref TState state);

/// <summary>
/// Extension methods for <see cref="IUriTemplateParser"/>.
/// </summary>
public static class UriTemplateParserExtensions
{
    /// <summary>
    /// Enumerate the parameters in the parser.
    /// </summary>
    /// <typeparam name="TState">The type of the state for the callback.</typeparam>
    /// <param name="parser">The parser to use.</param>
    /// <param name="uri">The uri to parse.</param>
    /// <param name="callback">The callback to receive the enumerated parameters.</param>
    /// <param name="state">The state for the callback.</param>
    /// <param name="initialCapacity">The initial cache size, which should be greater than or equal to the expected number of parameters.
    /// It also provides the increment for the cache size should it be exceeded.</param>
    /// <returns><see langword="true"/> if the parser was successful, otherwise <see langword="false"/>.</returns>
    public static bool EnumerateParameters<TState>(this IUriTemplateParser parser, ReadOnlySpan<char> uri, EnumerateParametersCallback<TState> callback, ref TState state, int initialCapacity = 10)
    {
        return ParameterCache.EnumerateParameters(parser, uri, initialCapacity, callback, ref state);
    }

    /// <summary>
    /// Enumerate the parameters in the parser.
    /// </summary>
    /// <typeparam name="TState">The type of the state for the callback.</typeparam>
    /// <param name="parser">The parser to use.</param>
    /// <param name="uri">The uri to parse.</param>
    /// <param name="callback">The callback to receive the enumerated parameters.</param>
    /// <param name="state">The state for the callback.</param>
    /// <param name="initialCapacity">The initial cache size, which should be greater than or equal to the expected number of parameters.
    /// It also provides the increment for the cache size should it be exceeded.</param>
    /// <returns><see langword="true"/> if the parser was successful, otherwise <see langword="false"/>.</returns>
    public static bool EnumerateParameters<TState>(this IUriTemplateParser parser, string uri, EnumerateParametersCallback<TState> callback, ref TState state, int initialCapacity = 10)
    {
        return ParameterCache.EnumerateParameters(parser, uri.AsSpan(), initialCapacity, callback, ref state);
    }

    /// <summary>
    /// Enumerate the parameters in the parser.
    /// </summary>
    /// <typeparam name="TState">The type of the state for the callback.</typeparam>
    /// <param name="parser">The parser to use.</param>
    /// <param name="uri">The uri to parse.</param>
    /// <param name="callback">The callback to receive the enumerated parameters.</param>
    /// <param name="state">The state for the callback.</param>
    /// <param name="initialCapacity">The initial cache size, which should be greater than or equal to the expected number of parameters.
    /// It also provides the increment for the cache size should it be exceeded.</param>
    /// <returns><see langword="true"/> if the parser was successful, otherwise <see langword="false"/>.</returns>
    public static bool EnumerateParameters<TState>(this IUriTemplateParser parser, ReadOnlySpan<char> uri, EnumerateParametersCallbackWithRange<TState> callback, ref TState state, int initialCapacity = 10)
    {
        return ParameterByNameAndRangeCache.EnumerateParameters(parser, uri, initialCapacity, callback, ref state);
    }

    /// <summary>
    /// Enumerate the parameters in the parser.
    /// </summary>
    /// <typeparam name="TState">The type of the state for the callback.</typeparam>
    /// <param name="parser">The parser to use.</param>
    /// <param name="uri">The uri to parse.</param>
    /// <param name="callback">The callback to receive the enumerated parameters.</param>
    /// <param name="state">The state for the callback.</param>
    /// <param name="initialCapacity">The initial cache size, which should be greater than or equal to the expected number of parameters.
    /// It also provides the increment for the cache size should it be exceeded.</param>
    /// <returns><see langword="true"/> if the parser was successful, otherwise <see langword="false"/>.</returns>
    public static bool EnumerateParameters<TState>(this IUriTemplateParser parser, string uri, EnumerateParametersCallbackWithRange<TState> callback, ref TState state, int initialCapacity = 10)
    {
        return ParameterByNameAndRangeCache.EnumerateParameters(parser, uri.AsSpan(), initialCapacity, callback, ref state);
    }

    /// <summary>
    /// Gets the parameters from the URI template.
    /// </summary>
    /// <param name="parser">The parser to use.</param>
    /// <param name="uri">The uri to parse.</param>
    /// <param name="initialCapacity">The initial cache size, which should be greater than or equal to the expected number of parameters.
    /// It also provides the increment for the cache size should it be exceeded.</param>
    /// <param name="templateParameters">A <see cref="UriTemplateParameters"/>.</param>
    /// <returns><see langword="true"/> if parsing succeeded.</returns>
    public static bool TryGetUriTemplateParameters(
        this IUriTemplateParser parser,
        ReadOnlySpan<char> uri,
        int initialCapacity,
        [NotNullWhen(true)] out UriTemplateParameters? templateParameters)
    {
        return ParameterByNameAndRangeCache.TryGetUriTemplateParameters(
            parser, uri, initialCapacity, false, out templateParameters);
    }

    /// <summary>
    /// Gets the parameters from the URI template.
    /// </summary>
    /// <param name="parser">The parser to use.</param>
    /// <param name="uri">The uri to parse.</param>
    /// <param name="initialCapacity">The initial cache size, which should be greater than or equal to the expected number of parameters.
    /// It also provides the increment for the cache size should it be exceeded.</param>
    /// <param name="requiresRootedMatch">If true, then the template requires a rooted match and will not ignore prefixes. This is more efficient when using a fully-qualified template.</param>
    /// <param name="templateParameters">A <see cref="UriTemplateParameters"/>.</param>
    /// <returns><see langword="true"/> if parsing succeeded.</returns>
    public static bool TryGetUriTemplateParameters(
        this IUriTemplateParser parser,
        ReadOnlySpan<char> uri,
        int initialCapacity,
        bool requiresRootedMatch,
        [NotNullWhen(true)] out UriTemplateParameters? templateParameters)
    {
        return ParameterByNameAndRangeCache.TryGetUriTemplateParameters(
            parser, uri, initialCapacity, requiresRootedMatch, out templateParameters);
    }
}