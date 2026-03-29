// <copyright file="DynamicJsonType.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Buffers;
using System.Reflection;
using System.Text.Json;

using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.Validator;

/// <summary>
/// Represents a dynamically-compiled JSON Schema typeand provides methods to parse
/// JSON documents into instances of that type.
/// </summary>
public readonly struct DynamicJsonType
{
    private readonly Type documentType;
    private readonly MethodInfo parseString;
    private readonly MethodInfo parseMemoryByte;
    private readonly MethodInfo parseMemoryChar;
    private readonly MethodInfo parseStream;
    private readonly MethodInfo parseSequenceByte;
    private readonly MethodInfo fromJsonElement;
    private readonly PropertyInfo rootElementProperty;

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicJsonType"/> struct.
    /// </summary>
    /// <param name="type">The generated root type.</param>
    public DynamicJsonType(Type type)
    {
        this.Type = type;
        this.documentType = typeof(ParsedJsonDocument<>).MakeGenericType(type);

        this.parseString = GetParseMethod(this.documentType, typeof(string));
        this.parseMemoryByte = GetParseMethod(this.documentType, typeof(ReadOnlyMemory<byte>));
        this.parseMemoryChar = GetParseMethod(this.documentType, typeof(ReadOnlyMemory<char>));
        this.parseStream = GetParseMethod(this.documentType, typeof(Stream));
        this.parseSequenceByte = GetParseMethod(this.documentType, typeof(ReadOnlySequence<byte>));

        MethodInfo fromGeneric = type.GetMethod("From", BindingFlags.Static | BindingFlags.Public)
            ?? throw new InvalidOperationException($"Cannot find static From<T>() on {type.FullName}");
        this.fromJsonElement = fromGeneric.MakeGenericMethod(typeof(JsonElement));

        this.rootElementProperty = this.documentType.GetProperty(
            "RootElement",
            BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException($"Cannot find RootElement property on {this.documentType.FullName}");
    }

    /// <summary>
    /// Gets the generated root type.
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// Parse a JSON string into a <see cref="DynamicJsonElement"/>.
    /// </summary>
    /// <param name="json">The JSON string.</param>
    /// <param name="options">The JSON document options.</param>
    /// <returns>The parsed element. The caller must dispose this value.</returns>
    public DynamicJsonElement ParseInstance(string json, JsonDocumentOptions options = default)
    {
        return this.ParseCore(this.parseString, json, options);
    }

    /// <summary>
    /// Parse UTF-8 JSON bytes into a <see cref="DynamicJsonElement"/>.
    /// </summary>
    /// <param name="utf8Json">The UTF-8 encoded JSON bytes.</param>
    /// <param name="options">The JSON document options.</param>
    /// <returns>The parsed element. The caller must dispose this value.</returns>
    public DynamicJsonElement ParseInstance(ReadOnlyMemory<byte> utf8Json, JsonDocumentOptions options = default)
    {
        return this.ParseCore(this.parseMemoryByte, utf8Json, options);
    }

    /// <summary>
    /// Parse JSON characters into a <see cref="DynamicJsonElement"/>.
    /// </summary>
    /// <param name="json">The JSON characters.</param>
    /// <param name="options">The JSON document options.</param>
    /// <returns>The parsed element. The caller must dispose this value.</returns>
    public DynamicJsonElement ParseInstance(ReadOnlyMemory<char> json, JsonDocumentOptions options = default)
    {
        return this.ParseCore(this.parseMemoryChar, json, options);
    }

    /// <summary>
    /// Parse a UTF-8 JSON stream into a <see cref="DynamicJsonElement"/>.
    /// </summary>
    /// <param name="utf8Json">The UTF-8 encoded JSON stream.</param>
    /// <param name="options">The JSON document options.</param>
    /// <returns>The parsed element. The caller must dispose this value.</returns>
    public DynamicJsonElement ParseInstance(Stream utf8Json, JsonDocumentOptions options = default)
    {
        return this.ParseCore(this.parseStream, utf8Json, options);
    }

    /// <summary>
    /// Parse a UTF-8 JSON byte sequence into a <see cref="DynamicJsonElement"/>.
    /// </summary>
    /// <param name="utf8Json">The UTF-8 encoded JSON byte sequence.</param>
    /// <param name="options">The JSON document options.</param>
    /// <returns>The parsed element. The caller must dispose this value.</returns>
    public DynamicJsonElement ParseInstance(ReadOnlySequence<byte> utf8Json, JsonDocumentOptions options = default)
    {
        return this.ParseCore(this.parseSequenceByte, utf8Json, options);
    }

    /// <summary>
    /// Create an instance of the generated type from a <see cref="JsonElement"/>.
    /// </summary>
    /// <param name="element">The JSON element.</param>
    /// <returns>A <see cref="DynamicJsonElement"/> wrapping the generated type instance. The caller must not dispose this value
    /// as it does not own the underlying document.</returns>
    public DynamicJsonElement FromElement(in JsonElement element)
    {
        object result = this.fromJsonElement.Invoke(null, [element])
            ?? throw new InvalidOperationException("From<JsonElement> returned null.");
        return new DynamicJsonElement(this.Type, (IJsonElement)result);
    }

    private static MethodInfo GetParseMethod(Type documentType, Type inputType)
    {
        return documentType.GetMethod(
            "Parse",
            BindingFlags.Static | BindingFlags.Public,
            null,
            [inputType, typeof(JsonDocumentOptions)],
            null)
            ?? throw new InvalidOperationException(
                $"Cannot find Parse({inputType.Name}, JsonDocumentOptions) on {documentType.FullName}");
    }

    private DynamicJsonElement ParseCore(MethodInfo parseMethod, object data, JsonDocumentOptions options)
    {
        object document = parseMethod.Invoke(null, [data, options])
            ?? throw new InvalidOperationException("Parse returned null.");

        object rootElement = this.rootElementProperty.GetValue(document)
            ?? throw new InvalidOperationException("RootElement returned null.");

        return new DynamicJsonElement(this.Type, (IJsonElement)rootElement);
    }
}