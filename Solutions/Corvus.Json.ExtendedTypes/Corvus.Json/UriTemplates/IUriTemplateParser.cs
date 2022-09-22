// <copyright file="IUriTemplateParser.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.UriTemplates;

/// <summary>
/// A callback when a parameter is found.
/// </summary>
/// <param name="reset">Whether to reset the parameters that we have seen so far.</param>
/// <param name="name">The name of the parameter.</param>
/// <param name="value">The string representation of the parameter.</param>
public delegate void ParameterCallback(bool reset, ReadOnlySpan<char> name, ReadOnlySpan<char> value);

/// <summary>
/// The interface implemented by an URI parser.
/// </summary>
public interface IUriTemplateParser
{
    /// <summary>
    /// Parses the given URI, calling your parameter callback for each named parameter discovered.
    /// </summary>
    /// <param name="uri">The URI to parse.</param>
    /// <param name="parameterCallback">Called by the parser for each parameter that is discovered.</param>
    /// <returns><see langword="true"/> if the uri was successfully parsed, otherwise false.</returns>
    /// <remarks>
    /// <para>
    /// This is a low-allocation operation, but you should take care with your implementation of your
    /// <see cref="ParameterCallback"/> if you wish to minimize allocation in your call tree.
    /// </para>
    /// <para>
    /// The parameter callbacks occur as the parameters are matched. If the parse operation ultimately fails,
    /// those parameters are invalid, and should be disregarded.
    /// </para>
    /// </remarks>
    bool ParseUri(ReadOnlySpan<char> uri, ParameterCallback parameterCallback);
}