// <copyright file="YamlDocument.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Yaml.Internal;

namespace Corvus.Text.Json.Yaml;

/// <summary>
/// Provides methods for parsing YAML content and converting it to
/// <see cref="ParsedJsonDocument{T}"/> instances.
/// </summary>
public static class YamlDocument
{
    private static readonly JsonWriterOptions WriterOptions = new() { SkipValidation = true };

    /// <summary>
    /// Parses UTF-8 YAML bytes and returns a <see cref="ParsedJsonDocument{T}"/> containing
    /// the equivalent JSON representation.
    /// </summary>
    /// <param name="utf8Yaml">The UTF-8 encoded YAML bytes.</param>
    /// <param name="options">Optional YAML reader options.</param>
    /// <returns>A <see cref="ParsedJsonDocument{T}"/> that must be disposed when no longer needed.</returns>
    /// <exception cref="YamlException">The YAML content is invalid.</exception>
    public static ParsedJsonDocument<JsonElement> Parse(
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
            YamlToJsonConverter converter = new(utf8Yaml.Span, writer, options);
            converter.Convert();
            writer.Flush();

            return ParsedJsonDocument<JsonElement>.Parse(bufferWriter.WrittenMemory);
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
        YamlToJsonConverter converter = new(utf8Yaml, writer, options);
        converter.Convert();
    }
}