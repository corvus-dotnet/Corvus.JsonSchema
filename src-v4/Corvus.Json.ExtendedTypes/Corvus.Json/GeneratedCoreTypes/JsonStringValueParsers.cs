// <copyright file="JsonStringValueParsers.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;

namespace Corvus.Json;

/// <summary>
///   A delegate to a method that attempts to represent a JSON string as a given type.
/// </summary>
/// <typeparam name="TState">The type of the state for the parser.</typeparam>
/// <typeparam name="TResult">The type of the resulting value.</typeparam>
/// <param name="span">The UTF8-encoded JSON string. This may be encoded or decoded depending on context.</param>
/// <param name="state">The state for the parser.</param>
/// <param name="value">The resulting value.</param>
/// <remarks>
///   This method does not create a representation of values other than JSON strings.
/// </remarks>
/// <returns>
///   <see langword="true"/> if the string can be represented as the given type,
///   <see langword="false"/> otherwise.
/// </returns>
public delegate bool Utf8Parser<TState, TResult>(ReadOnlySpan<byte> span, in TState state, [NotNullWhen(true)] out TResult? value);

/// <summary>
///   A delegate to a method that attempts to represent a JSON string as a given type.
/// </summary>
/// <typeparam name="TState">The type of the state for the parser.</typeparam>
/// <typeparam name="TResult">The type of the resulting value.</typeparam>
/// <param name="span">The JSON string. This will always be in its decoded form.</param>
/// <param name="state">The state for the parser.</param>
/// <param name="value">The resulting value.</param>
/// <remarks>
///   This method does not create a representation of values other than JSON strings.
/// </remarks>
/// <returns>
///   <see langword="true"/> if the string can be represented as the given type,
///   <see langword="false"/> otherwise.
/// </returns>
public delegate bool Parser<TState, TResult>(ReadOnlySpan<char> span, in TState state, [NotNullWhen(true)] out TResult? value);

/// <summary>
///   A delegate to a method that attempts to represent a JSON string as a given type.
/// </summary>
/// <typeparam name="TState">The type of the state for the parser.</typeparam>
/// <typeparam name="TResult">The type of the resulting value.</typeparam>
/// <param name="name">The UTF8-encoded JSON property name. This may be encoded or decoded depending on context.</param>
/// <param name="span">The UTF8-encoded JSON string. This may be encoded or decoded depending on context.</param>
/// <param name="state">The state for the parser.</param>
/// <param name="value">The resulting value.</param>
/// <remarks>
///   This method does not create a representation of values other than JSON strings.
/// </remarks>
/// <returns>
///   <see langword="true"/> if the string can be represented as the given type,
///   <see langword="false"/> otherwise.
/// </returns>
public delegate bool Utf8PropertyParser<TState, TResult>(ReadOnlySpan<byte> name, ReadOnlySpan<byte> span, in TState state, [NotNullWhen(true)] out TResult? value);

/// <summary>
///   A delegate to a method that attempts to represent a JSON string as a given type.
/// </summary>
/// <typeparam name="TState">The type of the state for the parser.</typeparam>
/// <typeparam name="TResult">The type of the resulting value.</typeparam>
/// <param name="name">The JSON property name. This may be encoded or decoded depending on context.</param>
/// <param name="span">The JSON string. This will always be in its decoded form.</param>
/// <param name="state">The state for the parser.</param>
/// <param name="value">The resulting value.</param>
/// <remarks>
///   This method does not create a representation of values other than JSON strings.
/// </remarks>
/// <returns>
///   <see langword="true"/> if the string can be represented as the given type,
///   <see langword="false"/> otherwise.
/// </returns>
public delegate bool PropertyParser<TState, TResult>(ReadOnlySpan<char> name, ReadOnlySpan<char> span, in TState state, [NotNullWhen(true)] out TResult? value);