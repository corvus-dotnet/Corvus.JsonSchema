// <copyright file="JsonElement.Parse.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json;

public readonly partial struct JsonElement
{
    /// <summary>
    /// Parses UTF8-encoded text representing a single JSON value into a <see cref="JsonElement"/>.
    /// </summary>
    /// <param name="utf8Json">The JSON text to parse.</param>
    /// <param name="options">Options to control the reader behavior during parsing.</param>
    /// <returns>A <see cref="JsonElement"/> representation of the JSON value.</returns>
    /// <exception cref="JsonException"><paramref name="utf8Json"/> does not represent a valid single JSON value.</exception>
    /// <exception cref="ArgumentException"><paramref name="options"/> contains unsupported options.</exception>
    public static JsonElement ParseValue([StringSyntax(StringSyntaxAttribute.Json)] ReadOnlySpan<byte> utf8Json, JsonDocumentOptions options = default)
    {
        return JsonElementHelpers.ParseValue<JsonElement>(utf8Json, options);
    }

    /// <summary>
    /// Parses text representing a single JSON value into a <see cref="JsonElement"/>.
    /// </summary>
    /// <param name="json">The JSON text to parse.</param>
    /// <param name="options">Options to control the reader behavior during parsing.</param>
    /// <returns>A <see cref="JsonElement"/> representation of the JSON value.</returns>
    /// <exception cref="JsonException"><paramref name="json"/> does not represent a valid single JSON value.</exception>
    /// <exception cref="ArgumentException"><paramref name="options"/> contains unsupported options.</exception>
    public static JsonElement ParseValue([StringSyntax(StringSyntaxAttribute.Json)] ReadOnlySpan<char> json, JsonDocumentOptions options = default)
    {
        return JsonElementHelpers.ParseValue<JsonElement>(json, options);
    }

    /// <summary>
    /// Parses text representing a single JSON value into a <see cref="JsonElement"/>.
    /// </summary>
    /// <param name="json">The JSON text to parse.</param>
    /// <param name="options">Options to control the reader behavior during parsing.</param>
    /// <returns>A <see cref="JsonElement"/> representation of the JSON value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> is <see langword="null"/>.</exception>
    /// <exception cref="JsonException"><paramref name="json"/> does not represent a valid single JSON value.</exception>
    /// <exception cref="ArgumentException"><paramref name="options"/> contains unsupported options.</exception>
    public static JsonElement ParseValue([StringSyntax(StringSyntaxAttribute.Json)] string json, JsonDocumentOptions options = default)
    {
        ArgumentNullException.ThrowIfNull(json);

        return JsonElementHelpers.ParseValue<JsonElement>(json, options);
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonElement ParseValue(ref Utf8JsonReader reader)
    {
        return JsonElementHelpers.ParseValue<JsonElement>(ref reader);
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
    public static bool TryParseValue(ref Utf8JsonReader reader, [NotNullWhen(true)] out JsonElement? element)
    {
        return JsonElementHelpers.TryParseValue(ref reader, out element);
    }
}