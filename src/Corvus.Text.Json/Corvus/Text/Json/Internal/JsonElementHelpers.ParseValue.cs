// <copyright file="JsonElementHelpers.ParseValue.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Helper methods for parsing JSON values from readers.
/// </summary>
public static partial class JsonElementHelpers
{
    /// <summary>
    /// Parses one JSON value (including objects or arrays) from the provided span.
    /// </summary>
    /// <param name="span">The span to read.</param>
    /// <param name="options">The <see cref="JsonDocumentOptions"/> for reading.</param>
    /// <returns>
    /// A <see cref="IJsonElement{T}"/> representing the value (and nested values) read from the span.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method makes a copy of the data the reader acted on, so there is no caller
    /// requirement to maintain data integrity beyond the return of this method.
    /// </para>
    /// </remarks>
    /// <exception cref="JsonException">
    /// A value could not be read from the span.
    /// </exception>
    [CLSCompliant(false)]
    public static T ParseValue<T>(ReadOnlySpan<byte> span, JsonDocumentOptions options = default)
        where T : struct, IJsonElement<T>
    {
        var document = ParsedJsonDocument<T>.ParseValue(span, options);
        return document.RootElement;
    }

    /// <summary>
    /// Parses one JSON value (including objects or arrays) from the provided span.
    /// </summary>
    /// <param name="span">The span to read.</param>
    /// <param name="options">The <see cref="JsonDocumentOptions"/> for reading.</param>
    /// <returns>
    /// A JsonElement representing the value (and nested values) read from the span.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method makes a copy of the data the reader acted on, so there is no caller
    /// requirement to maintain data integrity beyond the return of this method.
    /// </para>
    /// </remarks>
    /// <exception cref="JsonException">
    /// A value could not be read from the reader.
    /// </exception>
    [CLSCompliant(false)]
    public static T ParseValue<T>(ReadOnlySpan<char> span, JsonDocumentOptions options = default)
        where T : struct, IJsonElement<T>
    {
        var document = ParsedJsonDocument<T>.ParseValue(span, options);
        return document.RootElement;
    }

    /// <summary>
    /// Parses one JSON value (including objects or arrays) from the provided text.
    /// </summary>
    /// <param name="text">The text to read.</param>
    /// <param name="options">The <see cref="JsonDocumentOptions"/> for reading.</param>
    /// <returns>
    /// A JsonElement representing the value (and nested values) read from the text.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method makes a copy of the data, so there is no caller
    /// requirement to maintain data integrity beyond the return of this method.
    /// </para>
    /// </remarks>
    /// <exception cref="JsonException">
    /// A value could not be read from the text.
    /// </exception>
    [CLSCompliant(false)]
    public static T ParseValue<T>(string text, JsonDocumentOptions options = default)
        where T : struct, IJsonElement<T>
    {
        var document = ParsedJsonDocument<T>.ParseValue(text, options);
        return document.RootElement;
    }

    /// <summary>
    /// Parses one JSON value (including objects or arrays) from the provided reader.
    /// </summary>
    /// <param name="reader">The reader to read.</param>
    /// <returns>
    /// A JsonElement representing the value (and nested values) read from the reader.
    /// </returns>
    /// <remarks>
    /// <para>
    /// If the <see cref="Utf8JsonReader.TokenType"/> property of <paramref name="reader"/>
    /// is <see cref="JsonTokenType.PropertyName"/> or <see cref="JsonTokenType.None"/>, the
    /// reader will be advanced by one call to <see cref="Utf8JsonReader.Read"/> to determine
    /// the start of the value.
    /// </para>
    ///
    /// <para>
    /// Upon completion of this method, <paramref name="reader"/> will be positioned at the
    /// final token in the JSON value. If an exception is thrown, the reader is reset to
    /// the state it was in when the method was called.
    /// </para>
    ///
    /// <para>
    /// This method makes a copy of the data the reader acted on, so there is no caller
    /// requirement to maintain data integrity beyond the return of this method.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// <paramref name="reader"/> is using unsupported options.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The current <paramref name="reader"/> token does not start or represent a value.
    /// </exception>
    /// <exception cref="JsonException">
    /// A value could not be read from the reader.
    /// </exception>
    [CLSCompliant(false)]
    public static T ParseValue<T>(ref Utf8JsonReader reader)
        where T : struct, IJsonElement<T>
    {
        bool ret = ParsedJsonDocument<T>.TryParseValue(ref reader, out ParsedJsonDocument<T>? document, shouldThrow: true, useArrayPools: false);

        Debug.Assert(ret, "TryParseValue returned false with shouldThrow: true.");
        Debug.Assert(document != null, "null document returned with shouldThrow: true.");
        return document.RootElement;
    }

    /// <summary>
    /// Attempts to parse one JSON value (including objects or arrays) from the provided reader.
    /// </summary>
    /// <param name="reader">The reader to read.</param>
    /// <param name="element">Receives the parsed element.</param>
    /// <returns>
    /// <see langword="true"/> if a value was read and parsed into a JsonElement;
    /// <see langword="false"/> if the reader ran out of data while parsing.
    /// All other situations result in an exception being thrown.
    /// </returns>
    /// <remarks>
    /// <para>
    /// If the <see cref="Utf8JsonReader.TokenType"/> property of <paramref name="reader"/>
    /// is <see cref="JsonTokenType.PropertyName"/> or <see cref="JsonTokenType.None"/>, the
    /// reader will be advanced by one call to <see cref="Utf8JsonReader.Read"/> to determine
    /// the start of the value.
    /// </para>
    /// <para>
    /// Upon completion of this method, <paramref name="reader"/> will be positioned at the
    /// final token in the JSON value.  If an exception is thrown, or <see langword="false"/>
    /// is returned, the reader is reset to the state it was in when the method was called.
    /// </para>
    ///
    /// <para>
    /// This method makes a copy of the data the reader acted on, so there is no caller
    /// requirement to maintain data integrity beyond the return of this method.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// <paramref name="reader"/> is using unsupported options.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The current <paramref name="reader"/> token does not start or represent a value.
    /// </exception>
    /// <exception cref="JsonException">
    /// A value could not be read from the reader.
    /// </exception>
    [CLSCompliant(false)]
    public static bool TryParseValue<T>(ref Utf8JsonReader reader, [NotNullWhen(true)] out T? element)
        where T : struct, IJsonElement<T>
    {
        bool ret = ParsedJsonDocument<T>.TryParseValue(ref reader, out ParsedJsonDocument<T>? document, shouldThrow: false, useArrayPools: false);
        element = document?.RootElement;
        return ret;
    }
}