// <copyright file="CorvusTextJsonPolyfills.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Buffers;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.Compatibility;

/// <summary>
/// Polyfills for interoperability with System.Text.Json and Corvus.Json.ExtendedTypes.
/// </summary>
public static class CorvusTextJsonPolyfills
{
    /// <summary>
    /// Gets a nullable instance of the value.
    /// </summary>
    /// <typeparam name="T">The type of the value for wich to get a nullable instance.</typeparam>
    /// <param name="value">The value to check.</param>
    /// <returns><c>null</c> if the value is null, or undefined. Otherwise an instance of the value.</returns>
    public static T? AsOptional<T>(this T value)
        where T : struct, IJsonElement<T>
    {
        return value.IsNullOrUndefined() ? null : value;
    }

    /// <summary>
    /// Gets the JSON element as a <see cref="Corvus.Json.JsonAny"/>.
    /// </summary>
    /// <typeparam name="T">The type of the instance for which to get the <see cref="Corvus.Json.JsonAny"/>.</typeparam>
    /// <param name="element">The instance for which to get the <see cref="Corvus.Json.JsonAny"/>.</param>
    /// <param name="options">The (optional) JSON reader options to use when transforming the element.</param>
    /// <returns>The transformed element.</returns>
    public static Corvus.Json.JsonAny AsCorvusJsonAny<T>(this T element, System.Text.Json.JsonReaderOptions options = default)
        where T : struct, IJsonElement<T>
    {
        using RawUtf8JsonString rawValue = element.ParentDocument.GetRawValue(element.ParentDocumentIndex, true);
        var reader = new System.Text.Json.Utf8JsonReader(rawValue.Span, options);
        return Corvus.Json.JsonAny.ParseValue(ref reader);
    }

    /// <summary>
    /// Gets the JSON element as a <see cref="System.Text.Json.Nodes.JsonNode"/>.
    /// </summary>
    /// <typeparam name="T">The type of the instance for which to get the <see cref="System.Text.Json.Nodes.JsonNode"/>.</typeparam>
    /// <param name="element">The instance for which to get the <see cref="System.Text.Json.Nodes.JsonNode"/>.</param>
    /// <param name="options">The (optional) JSON reader options to use when transforming the element.</param>
    /// <returns>The transformed element.</returns>
    public static System.Text.Json.Nodes.JsonNode? AsJsonNode<T>(this T element, System.Text.Json.Nodes.JsonNodeOptions nodeOptions = default, System.Text.Json.JsonDocumentOptions documentOptions = default)
        where T : struct, IJsonElement<T>
    {
        using RawUtf8JsonString rawValue = element.ParentDocument.GetRawValue(element.ParentDocumentIndex, true);
        return System.Text.Json.Nodes.JsonNode.Parse(rawValue.Span, nodeOptions, documentOptions);
    }

    /// <summary>
    /// Gets the JSON element as a <see cref="System.Text.Json.JsonElement"/>.
    /// </summary>
    /// <typeparam name="T">The type of the instance for which to get the <see cref="System.Text.Json.JsonElement"/>.</typeparam>
    /// <param name="element">The instance for which to get the <see cref="System.Text.Json.JsonElement"/>.</param>
    /// <param name="options">The (optional) JSON reader options to use when transforming the element.</param>
    /// <returns>The transformed element.</returns>
    public static System.Text.Json.JsonElement AsSTJsonElement<T>(this T element, System.Text.Json.JsonReaderOptions options = default)
        where T : struct, IJsonElement<T>
    {
        using RawUtf8JsonString rawValue = element.ParentDocument.GetRawValue(element.ParentDocumentIndex, true);
        var reader = new System.Text.Json.Utf8JsonReader(rawValue.Span, options);
        return System.Text.Json.JsonElement.ParseValue(ref reader);
    }

    extension<T>(ParsedJsonDocument<T> element)
        where T : struct, IJsonElement<T>
    {
        public static ParsedJsonDocument<T> FromSTJsonElement(System.Text.Json.JsonElement jsonElement)
        {
            Utf8JsonReader reader = new(System.Runtime.InteropServices.JsonMarshal.GetRawUtf8Value(jsonElement));
            return ParsedJsonDocument<T>.ParseValue(ref reader);
        }
    }

    extension<T>(T element)
        where T : struct, IJsonElement<T>
    {
        /// <summary>
        /// Create an instance from an arbitrary object, via System.Text.Json serialization.
        /// </summary>
        /// <typeparam name="T">The type of the object from which to create the instance.</typeparam>
        /// <param name="instance">The object from which to create the instance.</param>
        /// <param name="writerOptions">The (optional) <see cref="JsonWriterOptions"/>.</param>
        /// <param name="readerOptions">The (optional) <see cref="JsonReaderOptions"/>.</param>
        /// <returns>A <see cref="JsonAny"/> derived from serializing the object.</returns>
        public static T CreateFromSerializedInstance<TObject>(TObject instance, JsonWriterOptions writerOptions = default, JsonReaderOptions readerOptions = default)
        {
            var abw = new ArrayBufferWriter<byte>();
            using var writer = new System.Text.Json.Utf8JsonWriter(abw, ToSTJ(writerOptions));
            System.Text.Json.JsonSerializer.Serialize(writer, instance);
            writer.Flush();

            Utf8JsonReader reader = new(abw.WrittenMemory.Span, readerOptions);

            // You do not need to dispose the document produced by ParseValue()
            var document = ParsedJsonDocument<T>.ParseValue(ref reader);
            return document.RootElement;
        }

        /// <summary>
        /// Write the instance to a <see cref="System.Text.Json.Utf8JsonWriter"/>.
        /// </summary>
        /// <param name="jsonWriter">The JSON writer.</param>
        /// <remarks>
        /// Note that this has twice the memory demand of writing directly to a <see cref="Utf8JsonWriter"/>.
        /// </remarks>
        public void WriteTo(System.Text.Json.Utf8JsonWriter jsonWriter)
        {
            using var workspace = JsonWorkspace.Create();
            Utf8JsonWriter writer = workspace.RentWriterAndBuffer(defaultBufferSize: 1024, out IByteBufferWriter bufferWriter);
            try
            {
                element.WriteTo(writer);
                writer.Flush();
                jsonWriter.WriteRawValue(bufferWriter.WrittenMemory.Span, true);
            }
            finally
            {
                workspace.ReturnWriterAndBuffer(writer, bufferWriter);
            }
        }
    }

    private static System.Text.Json.JsonWriterOptions ToSTJ(JsonWriterOptions options)
    {
        return new System.Text.Json.JsonWriterOptions
        {
            Encoder = options.Encoder,
            Indented = options.Indented,
            IndentCharacter = options.IndentCharacter,
            MaxDepth = options.MaxDepth,
            IndentSize = options.IndentSize,
            NewLine = options.NewLine,
            SkipValidation = options.SkipValidation
        };
    }
}