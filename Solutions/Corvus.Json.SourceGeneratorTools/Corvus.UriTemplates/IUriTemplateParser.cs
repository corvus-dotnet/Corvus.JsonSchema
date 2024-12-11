// <copyright file="IUriTemplateParser.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;

namespace Corvus.UriTemplates;

/// <summary>
/// A callback when a parameter is found, in which the match is identified with a <see cref="ReadOnlySpan{Char}"/>.
/// </summary>
/// <typeparam name="TState">The type of the state to pass.</typeparam>
/// <param name="reset">Whether to reset the parameters that we have seen so far.</param>
/// <param name="name">The name of the parameter.</param>
/// <param name="value">The string representation of the parameter.</param>
/// <param name="state">The state to pass.</param>
public delegate void ParameterCallback<TState>(bool reset, ReadOnlySpan<char> name, ReadOnlySpan<char> value, ref TState state);

/// <summary>
/// A callback when a parameter is found, in which the match is identified with a <see cref="Range"/>.
/// </summary>
/// <typeparam name="TState">The type of the state to pass.</typeparam>
/// <param name="reset">Whether to reset the parameters that we have seen so far.</param>
/// <param name="nameHandle">Identifies the parameter name.</param>
/// <param name="valueRange">The range in the input URI string at which the parameter was found.</param>
/// <param name="state">The state to pass.</param>
public delegate void ParameterCallbackWithRange<TState>(bool reset, ParameterName nameHandle, Range valueRange, ref TState state);

/// <summary>
/// The interface implemented by an URI parser.
/// </summary>
public interface IUriTemplateParser
{
    /// <summary>
    /// Determines if the UriTemplate matches the given URI.
    /// </summary>
    /// <param name="uri">The URI to match.</param>
    /// <param name="requiresRootedMatch">If true, then the template requires a rooted match and will not ignore prefixes. This is more efficient when using a fully-qualified template.</param>
    /// <returns><see langword="true"/> if the template is a match for the URI.</returns>
    bool IsMatch(in ReadOnlySpan<char> uri, bool requiresRootedMatch = false);

    /// <summary>
    /// Parses the given URI, calling your parameter callback for each named parameter discovered.
    /// </summary>
    /// <typeparam name="TState">The type of the state to pass.</typeparam>
    /// <param name="uri">The URI to parse.</param>
    /// <param name="parameterCallback">Called by the parser for each parameter that is discovered.</param>
    /// <param name="state">The state to pass to the callback.</param>
    /// <param name="requiresRootedMatch">If true, then the template requires a rooted match and will not ignore prefixes. This is more efficient when using a fully-qualified template.</param>
    /// <returns><see langword="true"/> if the uri was successfully parsed, otherwise false.</returns>
    /// <remarks>
    /// <para>
    /// This is a low-allocation operation, but you should take care with your implementation of your
    /// <see cref="ParameterCallback{T}"/> if you wish to minimize allocation in your call tree.
    /// </para>
    /// <para>
    /// The parameter callbacks occur as the parameters are matched. If the parse operation ultimately fails,
    /// those parameters are invalid, and should be disregarded.
    /// </para>
    /// </remarks>
    bool ParseUri<TState>(in ReadOnlySpan<char> uri, ParameterCallback<TState> parameterCallback, ref TState state, bool requiresRootedMatch = false);

    /// <summary>
    /// Parses the given URI, calling your parameter callback for each named parameter discovered.
    /// </summary>
    /// <typeparam name="TState">The type of the state to pass.</typeparam>
    /// <param name="uri">The URI to parse.</param>
    /// <param name="parameterCallback">Called by the parser for each parameter that is discovered.</param>
    /// <param name="state">The state to pass to the callback.</param>
    /// <param name="requiresRootedMatch">If true, then the template requires a rooted match and will not ignore prefixes. This is more efficient when using a fully-qualified template.</param>
    /// <returns><see langword="true"/> if the uri was successfully parsed, otherwise false.</returns>
    /// <remarks>
    /// <para>
    /// This is a low-allocation operation, but you should take care with your implementation of your
    /// <see cref="ParameterCallback{T}"/> if you wish to minimize allocation in your call tree.
    /// </para>
    /// <para>
    /// The parameter callbacks occur as the parameters are matched. If the parse operation ultimately fails,
    /// those parameters are invalid, and should be disregarded.
    /// </para>
    /// </remarks>
    /// <exception cref="NotImplementedException">
    /// This method was added in 1.3, so libraries that depend on an older version, and which implement this
    /// interface will not have this method available. In most cases, the implementation of this interface
    /// will be supplied by this library, and so all methods will be available, but it is virtual to support
    /// the rare case where someone has implemented their own version against an older version of the library.
    /// This exception will be thrown if that is the case.
    /// </exception>
#if NET8_0_OR_GREATER
    virtual bool ParseUri<TState>(in ReadOnlySpan<char> uri, ParameterCallbackWithRange<TState> parameterCallback, ref TState state, bool requiresRootedMatch = false) =>
        throw new NotImplementedException();
#else
    bool ParseUri<TState>(in ReadOnlySpan<char> uri, ParameterCallbackWithRange<TState> parameterCallback, ref TState state, bool requiresRootedMatch = false);
#endif

#if !NET8_0_OR_GREATER
    /// <summary>
    /// Determines if the UriTemplate matches the given URI.
    /// </summary>
    /// <param name="uri">The URI to match.</param>
    /// <param name="requiresRootedMatch">If true, then the template requires a rooted match and will not ignore prefixes. This is more efficient when using a fully-qualified template.</param>
    /// <returns><see langword="true"/> if the template is a match for the URI.</returns>
    public bool IsMatch(string uri, bool requiresRootedMatch = false);

    /// <summary>
    /// Parses the given URI, calling your parameter callback for each named parameter discovered.
    /// </summary>
    /// <typeparam name="TState">The type of the state to pass.</typeparam>
    /// <param name="uri">The URI to parse.</param>
    /// <param name="parameterCallback">Called by the parser for each parameter that is discovered.</param>
    /// <param name="state">The state to pass to the callback.</param>
    /// <param name="requiresRootedMatch">If true, then the template requires a rooted match and will not ignore prefixes. This is more efficient when using a fully-qualified template.</param>
    /// <returns><see langword="true"/> if the uri was successfully parsed, otherwise false.</returns>
    /// <remarks>
    /// <para>
    /// This is a low-allocation operation, but you should take care with your implementation of your
    /// <see cref="ParameterCallback{T}"/> if you wish to minimize allocation in your call tree.
    /// </para>
    /// <para>
    /// The parameter callbacks occur as the parameters are matched. If the parse operation ultimately fails,
    /// those parameters are invalid, and should be disregarded.
    /// </para>
    /// </remarks>
    bool ParseUri<TState>(string uri, ParameterCallback<TState> parameterCallback, ref TState state, bool requiresRootedMatch = false);

    /// <summary>
    /// Parses the given URI, calling your parameter callback for each named parameter discovered.
    /// </summary>
    /// <typeparam name="TState">The type of the state to pass.</typeparam>
    /// <param name="uri">The URI to parse.</param>
    /// <param name="parameterCallback">Called by the parser for each parameter that is discovered.</param>
    /// <param name="state">The state to pass to the callback.</param>
    /// <param name="requiresRootedMatch">If true, then the template requires a rooted match and will not ignore prefixes. This is more efficient when using a fully-qualified template.</param>
    /// <returns><see langword="true"/> if the uri was successfully parsed, otherwise false.</returns>
    /// <remarks>
    /// <para>
    /// This is a low-allocation operation, but you should take care with your implementation of your
    /// <see cref="ParameterCallback{T}"/> if you wish to minimize allocation in your call tree.
    /// </para>
    /// <para>
    /// The parameter callbacks occur as the parameters are matched. If the parse operation ultimately fails,
    /// those parameters are invalid, and should be disregarded.
    /// </para>
    /// </remarks>
    bool ParseUri<TState>(string uri, ParameterCallbackWithRange<TState> parameterCallback, ref TState state, bool requiresRootedMatch = false);
#endif
}

/// <summary>
/// Provides access to a parameter name.
/// </summary>
public readonly struct ParameterName : IEquatable<ParameterName>
{
    private readonly string escapedUriTemplate;
    private readonly Range range;

    /// <summary>
    /// Creates a <see cref="ParameterName"/>.
    /// </summary>
    /// <param name="escapedUriTemplate">The escaped URI template containing the parameter name.</param>
    /// <param name="range">Range in the escaped URI template containing the parameter name.</param>
    internal ParameterName(string escapedUriTemplate, Range range)
    {
        this.escapedUriTemplate = escapedUriTemplate;
        this.range = range;
    }

    /// <summary>
    /// Gets the parameter name.
    /// </summary>
    public ReadOnlySpan<char> Span => this.escapedUriTemplate.AsSpan()[this.range];

#pragma warning disable IDE0024, CS1591 // Overzealous documentation warnings.
    public static bool operator ==(ParameterName left, ParameterName right) => left.Equals(right);

    public static bool operator !=(ParameterName left, ParameterName right) => !left.Equals(right);
#pragma warning restore IDE0024, CS1591

    /// <inheritdoc/>
    public bool Equals(ParameterName other) => this.escapedUriTemplate == other.escapedUriTemplate && this.range.Equals(other.range);

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is ParameterName other && this.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() =>
#if NET8_0_OR_GREATER
        string.GetHashCode(this.Span);
#else
        this.Span.ToString().GetHashCode();
#endif
}