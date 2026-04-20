// <copyright file="YamlDocument.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if STJ

using System.Buffers;
using System.Text;
using System.Text.Json;
using Corvus.Yaml.Internal;

namespace Corvus.Yaml;

/// <summary>
/// Provides methods for parsing YAML content and converting it to
/// <see cref="JsonDocument"/> instances or JSON strings.
/// </summary>
public static class YamlDocument
{
    private static readonly JsonWriterOptions WriterOptions = new() { SkipValidation = true };

    /// <summary>
    /// Parses UTF-8 YAML bytes and returns a <see cref="JsonDocument"/> containing
    /// the equivalent JSON representation.
    /// </summary>
    /// <param name="utf8Yaml">The UTF-8 encoded YAML bytes.</param>
    /// <param name="options">Optional YAML reader options.</param>
    /// <returns>A <see cref="JsonDocument"/> that must be disposed when no longer needed.</returns>
    /// <exception cref="YamlException">The YAML content is invalid.</exception>
    public static JsonDocument Parse(
        ReadOnlyMemory<byte> utf8Yaml,
        YamlReaderOptions options = default)
    {
        using ArrayPoolBufferWriter bufferWriter = new(Math.Max(utf8Yaml.Length, 256));
        using Utf8JsonWriter writer = new(bufferWriter, WriterOptions);

        YamlToJsonConverter converter = new(utf8Yaml.Span, writer, options, bufferWriter);
        converter.Convert();
        writer.Flush();

        return JsonDocument.Parse(bufferWriter.WrittenMemory);
    }

    /// <summary>
    /// Converts UTF-8 YAML bytes to a JSON string.
    /// </summary>
    /// <param name="utf8Yaml">The UTF-8 encoded YAML bytes.</param>
    /// <param name="options">Optional YAML reader options.</param>
    /// <returns>A string containing the JSON representation of the YAML content.</returns>
    /// <exception cref="YamlException">The YAML content is invalid.</exception>
    public static string ConvertToJsonString(
        ReadOnlyMemory<byte> utf8Yaml,
        YamlReaderOptions options = default)
    {
        using ArrayPoolBufferWriter bufferWriter = new(Math.Max(utf8Yaml.Length, 256));
        using Utf8JsonWriter writer = new(bufferWriter, WriterOptions);

        YamlToJsonConverter converter = new(utf8Yaml.Span, writer, options, bufferWriter);
        converter.Convert();
        writer.Flush();

#if NET
        return Encoding.UTF8.GetString(bufferWriter.WrittenSpan);
#else
        return Encoding.UTF8.GetString(bufferWriter.WrittenSpan.ToArray());
#endif
    }

    /// <summary>
    /// Converts UTF-8 YAML bytes to JSON, writing the output to the specified <see cref="Utf8JsonWriter"/>.
    /// </summary>
    /// <param name="utf8Yaml">The UTF-8 encoded YAML bytes.</param>
    /// <param name="writer">The JSON writer to write the converted output to.</param>
    /// <param name="options">Optional YAML reader options.</param>
    /// <exception cref="YamlException">The YAML content is invalid.</exception>
    public static void Convert(
        ReadOnlySpan<byte> utf8Yaml,
        Utf8JsonWriter writer,
        YamlReaderOptions options = default)
    {
        using ArrayPoolBufferWriter bufferWriter = new(Math.Max(utf8Yaml.Length, 256));
        using Utf8JsonWriter internalWriter = new(bufferWriter, WriterOptions);

        YamlToJsonConverter converter = new(utf8Yaml, internalWriter, options, bufferWriter);
        converter.Convert();
        internalWriter.Flush();

        writer.WriteRawValue(bufferWriter.WrittenSpan, skipInputValidation: true);
    }
}

#else

using System.Text;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.Yaml.Internal;

namespace Corvus.Text.Json.Yaml;

/// <summary>
/// Provides methods for parsing YAML content and converting it to
/// <see cref="ParsedJsonDocument{T}"/> instances or JSON strings.
/// </summary>
public static class YamlDocument
{
    private static readonly JsonWriterOptions WriterOptions = new() { SkipValidation = true };

    /// <summary>
    /// Parses UTF-8 YAML bytes and returns a <see cref="ParsedJsonDocument{T}"/> containing
    /// the equivalent JSON representation.
    /// </summary>
    /// <typeparam name="TElement">The type of the JSON element.</typeparam>
    /// <param name="utf8Yaml">The UTF-8 encoded YAML bytes.</param>
    /// <param name="options">Optional YAML reader options.</param>
    /// <returns>A <see cref="ParsedJsonDocument{T}"/> that must be disposed when no longer needed.</returns>
    /// <exception cref="YamlException">The YAML content is invalid.</exception>
    public static ParsedJsonDocument<TElement> Parse<TElement>(
        ReadOnlyMemory<byte> utf8Yaml,
        YamlReaderOptions options = default)
        where TElement : struct, IJsonElement<TElement>
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Utf8JsonWriter writer = workspace.RentWriterAndBuffer(
            WriterOptions,
            Math.Max(utf8Yaml.Length, 256),
            out IByteBufferWriter bufferWriter);

        try
        {
            YamlToJsonConverter converter = new(utf8Yaml.Span, writer, options, bufferWriter);
            converter.Convert();
            writer.Flush();

            return ParsedJsonDocument<TElement>.Parse(bufferWriter.WrittenMemory);
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(writer, bufferWriter);
        }
    }

    /// <summary>
    /// Converts UTF-8 YAML bytes to a JSON string.
    /// </summary>
    /// <param name="utf8Yaml">The UTF-8 encoded YAML bytes.</param>
    /// <param name="options">Optional YAML reader options.</param>
    /// <returns>A string containing the JSON representation of the YAML content.</returns>
    /// <exception cref="YamlException">The YAML content is invalid.</exception>
    public static string ConvertToJsonString(
        ReadOnlyMemory<byte> utf8Yaml,
        YamlReaderOptions options = default)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Utf8JsonWriter writer = workspace.RentWriterAndBuffer(
            WriterOptions,
            Math.Max(utf8Yaml.Length, 256),
            out IByteBufferWriter bufferWriter);

        try
        {
            YamlToJsonConverter converter = new(utf8Yaml.Span, writer, options, bufferWriter);
            converter.Convert();
            writer.Flush();

#if NET
            return Encoding.UTF8.GetString(bufferWriter.WrittenSpan);
#else
            return Encoding.UTF8.GetString(bufferWriter.WrittenSpan.ToArray());
#endif
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(writer, bufferWriter);
        }
    }

    /// <summary>
    /// Converts UTF-8 YAML bytes to JSON, writing the output to the specified <see cref="Utf8JsonWriter"/>.
    /// </summary>
    /// <param name="utf8Yaml">The UTF-8 encoded YAML bytes.</param>
    /// <param name="writer">The JSON writer to write the converted output to.</param>
    /// <param name="options">Optional YAML reader options.</param>
    /// <exception cref="YamlException">The YAML content is invalid.</exception>
    public static void Convert(
        ReadOnlySpan<byte> utf8Yaml,
        Utf8JsonWriter writer,
        YamlReaderOptions options = default)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Utf8JsonWriter internalWriter = workspace.RentWriterAndBuffer(
            WriterOptions,
            Math.Max(utf8Yaml.Length, 256),
            out IByteBufferWriter bufferWriter);

        try
        {
            YamlToJsonConverter converter = new(utf8Yaml, internalWriter, options, bufferWriter);
            converter.Convert();
            internalWriter.Flush();

            writer.WriteRawValue(bufferWriter.WrittenSpan, skipInputValidation: true);
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(internalWriter, bufferWriter);
        }
    }
}

#endif