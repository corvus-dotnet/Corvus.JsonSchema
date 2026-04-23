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

        // Use ParseValue which copies the data internally, so the document
        // does not reference the pooled buffer after it is returned.
        var reader = new Utf8JsonReader(bufferWriter.WrittenSpan);
        return JsonDocument.ParseValue(ref reader);
    }

    /// <summary>
    /// Parses a YAML string and returns a <see cref="JsonDocument"/> containing
    /// the equivalent JSON representation.
    /// </summary>
    /// <param name="yaml">The YAML content as a string.</param>
    /// <param name="options">Optional YAML reader options.</param>
    /// <returns>A <see cref="JsonDocument"/> that must be disposed when no longer needed.</returns>
    /// <exception cref="YamlException">The YAML content is invalid.</exception>
    public static JsonDocument Parse(
        string yaml,
        YamlReaderOptions options = default)
    {
        int maxByteCount = Encoding.UTF8.GetMaxByteCount(yaml.Length);
        byte[]? rentedArray = null;

        Span<byte> utf8Buffer = maxByteCount <= JsonConstants.StackallocByteThreshold
            ? stackalloc byte[JsonConstants.StackallocByteThreshold]
            : (rentedArray = ArrayPool<byte>.Shared.Rent(maxByteCount));

        try
        {
            int bytesWritten;
#if NET
            bytesWritten = Encoding.UTF8.GetBytes(yaml, utf8Buffer);
#else
            unsafe
            {
                fixed (char* pChars = yaml)
                fixed (byte* pBytes = utf8Buffer)
                {
                    bytesWritten = Encoding.UTF8.GetBytes(pChars, yaml.Length, pBytes, utf8Buffer.Length);
                }
            }
#endif
            using ArrayPoolBufferWriter jsonBuffer = new(Math.Max(bytesWritten, 256));
            using Utf8JsonWriter writer = new(jsonBuffer, WriterOptions);

            YamlToJsonConverter converter = new(utf8Buffer.Slice(0, bytesWritten), writer, options, jsonBuffer);
            converter.Convert();
            writer.Flush();

            // Use ParseValue which copies the data internally, so the document
            // does not reference the pooled buffer after it is returned.
            var reader = new Utf8JsonReader(jsonBuffer.WrittenSpan);
            return JsonDocument.ParseValue(ref reader);
        }
        finally
        {
            if (rentedArray is not null)
            {
                ArrayPool<byte>.Shared.Return(rentedArray);
            }
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
    /// Converts a YAML string to a JSON string.
    /// </summary>
    /// <param name="yaml">The YAML content as a string.</param>
    /// <param name="options">Optional YAML reader options.</param>
    /// <returns>A string containing the JSON representation of the YAML content.</returns>
    /// <exception cref="YamlException">The YAML content is invalid.</exception>
    public static string ConvertToJsonString(
        string yaml,
        YamlReaderOptions options = default)
    {
        int maxByteCount = Encoding.UTF8.GetMaxByteCount(yaml.Length);
        byte[]? rentedArray = null;

        Span<byte> utf8Buffer = maxByteCount <= JsonConstants.StackallocByteThreshold
            ? stackalloc byte[JsonConstants.StackallocByteThreshold]
            : (rentedArray = ArrayPool<byte>.Shared.Rent(maxByteCount));

        try
        {
            int bytesWritten;
#if NET
            bytesWritten = Encoding.UTF8.GetBytes(yaml, utf8Buffer);
#else
            unsafe
            {
                fixed (char* pChars = yaml)
                fixed (byte* pBytes = utf8Buffer)
                {
                    bytesWritten = Encoding.UTF8.GetBytes(pChars, yaml.Length, pBytes, utf8Buffer.Length);
                }
            }
#endif
            return ConvertToJsonString(utf8Buffer.Slice(0, bytesWritten).ToArray(), options);
        }
        finally
        {
            if (rentedArray is not null)
            {
                ArrayPool<byte>.Shared.Return(rentedArray);
            }
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
        using ArrayPoolBufferWriter bufferWriter = new(Math.Max(utf8Yaml.Length, 256));
        using Utf8JsonWriter internalWriter = new(bufferWriter, WriterOptions);

        YamlToJsonConverter converter = new(utf8Yaml, internalWriter, options, bufferWriter);
        converter.Convert();
        internalWriter.Flush();

        writer.WriteRawValue(bufferWriter.WrittenSpan, skipInputValidation: true);
    }

#if !BUILDING_SOURCE_GENERATOR

    /// <summary>
    /// Converts a <see cref="JsonElement"/> to a YAML string.
    /// </summary>
    /// <param name="element">The JSON element to convert.</param>
    /// <param name="options">Optional YAML writer options.</param>
    /// <returns>A string containing the YAML representation of the JSON element.</returns>
    public static string ConvertToYamlString(
        JsonElement element,
        YamlWriterOptions options = default)
    {
        using ArrayPoolBufferWriter bufferWriter = new(256);
        Utf8YamlWriter yamlWriter = new(bufferWriter, options);

        try
        {
            JsonToYamlConverter.Convert(ref yamlWriter, element);
            yamlWriter.Flush();
        }
        finally
        {
            yamlWriter.Dispose();
        }

#if NET
        return Encoding.UTF8.GetString(bufferWriter.WrittenSpan);
#else
        return Encoding.UTF8.GetString(bufferWriter.WrittenSpan.ToArray());
#endif
    }

    /// <summary>
    /// Converts a JSON string to a YAML string.
    /// </summary>
    /// <param name="json">The JSON content as a string.</param>
    /// <param name="options">Optional YAML writer options.</param>
    /// <returns>A string containing the YAML representation of the JSON content.</returns>
    public static string ConvertToYamlString(
        string json,
        YamlWriterOptions options = default)
    {
        int maxByteCount = Encoding.UTF8.GetMaxByteCount(json.Length);
        byte[]? rentedArray = null;

        Span<byte> utf8Buffer = maxByteCount <= JsonConstants.StackallocByteThreshold
            ? stackalloc byte[JsonConstants.StackallocByteThreshold]
            : (rentedArray = ArrayPool<byte>.Shared.Rent(maxByteCount));

        try
        {
            int bytesWritten;
#if NET
            bytesWritten = Encoding.UTF8.GetBytes(json, utf8Buffer);
#else
            unsafe
            {
                fixed (char* pChars = json)
                fixed (byte* pBytes = utf8Buffer)
                {
                    bytesWritten = Encoding.UTF8.GetBytes(pChars, json.Length, pBytes, utf8Buffer.Length);
                }
            }
#endif
            return ConvertToYamlString(utf8Buffer.Slice(0, bytesWritten), options);
        }
        finally
        {
            if (rentedArray is not null)
            {
                ArrayPool<byte>.Shared.Return(rentedArray);
            }
        }
    }

    /// <summary>
    /// Converts UTF-8 JSON bytes to a YAML string.
    /// </summary>
    /// <param name="utf8Json">The UTF-8 encoded JSON bytes.</param>
    /// <param name="options">Optional YAML writer options.</param>
    /// <returns>A string containing the YAML representation of the JSON content.</returns>
    public static string ConvertToYamlString(
        ReadOnlySpan<byte> utf8Json,
        YamlWriterOptions options = default)
    {
        using ArrayPoolBufferWriter bufferWriter = new(256);
        Utf8YamlWriter yamlWriter = new(bufferWriter, options);

        try
        {
            JsonToYamlConverter.Convert(ref yamlWriter, utf8Json);
            yamlWriter.Flush();
        }
        finally
        {
            yamlWriter.Dispose();
        }

#if NET
        return Encoding.UTF8.GetString(bufferWriter.WrittenSpan);
#else
        return Encoding.UTF8.GetString(bufferWriter.WrittenSpan.ToArray());
#endif
    }

    /// <summary>
    /// Converts a <see cref="JsonElement"/> to YAML, writing the output to the
    /// specified <see cref="IBufferWriter{T}"/>.
    /// </summary>
    /// <param name="element">The JSON element to convert.</param>
    /// <param name="writer">The buffer writer to write the YAML output to.</param>
    /// <param name="options">Optional YAML writer options.</param>
    public static void ConvertToYaml(
        JsonElement element,
        IBufferWriter<byte> writer,
        YamlWriterOptions options = default)
    {
        Utf8YamlWriter yamlWriter = new(writer, options);

        try
        {
            JsonToYamlConverter.Convert(ref yamlWriter, element);
            yamlWriter.Flush();
        }
        finally
        {
            yamlWriter.Dispose();
        }
    }

    /// <summary>
    /// Converts a <see cref="JsonElement"/> to YAML, writing the output to the
    /// specified <see cref="System.IO.Stream"/>.
    /// </summary>
    /// <param name="element">The JSON element to convert.</param>
    /// <param name="utf8Stream">The stream to write the UTF-8 YAML output to.</param>
    /// <param name="options">Optional YAML writer options.</param>
    public static void ConvertToYaml(
        JsonElement element,
        System.IO.Stream utf8Stream,
        YamlWriterOptions options = default)
    {
        Utf8YamlWriter yamlWriter = new(utf8Stream, options);

        try
        {
            JsonToYamlConverter.Convert(ref yamlWriter, element);
            yamlWriter.Flush();
        }
        finally
        {
            yamlWriter.Dispose();
        }
    }

    /// <summary>
    /// Converts a JSON string to YAML, writing the output to the
    /// specified <see cref="IBufferWriter{T}"/>.
    /// </summary>
    /// <param name="json">The JSON content as a string.</param>
    /// <param name="writer">The buffer writer to write the YAML output to.</param>
    /// <param name="options">Optional YAML writer options.</param>
    public static void ConvertToYaml(
        string json,
        IBufferWriter<byte> writer,
        YamlWriterOptions options = default)
    {
        int maxByteCount = Encoding.UTF8.GetMaxByteCount(json.Length);
        byte[]? rentedArray = null;

        Span<byte> utf8Buffer = maxByteCount <= JsonConstants.StackallocByteThreshold
            ? stackalloc byte[JsonConstants.StackallocByteThreshold]
            : (rentedArray = ArrayPool<byte>.Shared.Rent(maxByteCount));

        try
        {
            int bytesWritten;
#if NET
            bytesWritten = Encoding.UTF8.GetBytes(json, utf8Buffer);
#else
            unsafe
            {
                fixed (char* pChars = json)
                fixed (byte* pBytes = utf8Buffer)
                {
                    bytesWritten = Encoding.UTF8.GetBytes(pChars, json.Length, pBytes, utf8Buffer.Length);
                }
            }
#endif
            ConvertToYaml(utf8Buffer.Slice(0, bytesWritten), writer, options);
        }
        finally
        {
            if (rentedArray is not null)
            {
                ArrayPool<byte>.Shared.Return(rentedArray);
            }
        }
    }

    /// <summary>
    /// Converts UTF-8 JSON bytes to YAML, writing the output to the
    /// specified <see cref="IBufferWriter{T}"/>.
    /// </summary>
    /// <param name="utf8Json">The UTF-8 encoded JSON bytes.</param>
    /// <param name="writer">The buffer writer to write the YAML output to.</param>
    /// <param name="options">Optional YAML writer options.</param>
    public static void ConvertToYaml(
        ReadOnlySpan<byte> utf8Json,
        IBufferWriter<byte> writer,
        YamlWriterOptions options = default)
    {
        Utf8YamlWriter yamlWriter = new(writer, options);

        try
        {
            JsonToYamlConverter.Convert(ref yamlWriter, utf8Json);
            yamlWriter.Flush();
        }
        finally
        {
            yamlWriter.Dispose();
        }
    }

    /// <summary>
    /// Converts a JSON string to YAML, writing the output to the
    /// specified <see cref="System.IO.Stream"/>.
    /// </summary>
    /// <param name="json">The JSON content as a string.</param>
    /// <param name="utf8Stream">The stream to write the UTF-8 YAML output to.</param>
    /// <param name="options">Optional YAML writer options.</param>
    public static void ConvertToYaml(
        string json,
        System.IO.Stream utf8Stream,
        YamlWriterOptions options = default)
    {
        int maxByteCount = Encoding.UTF8.GetMaxByteCount(json.Length);
        byte[]? rentedArray = null;

        Span<byte> utf8Buffer = maxByteCount <= JsonConstants.StackallocByteThreshold
            ? stackalloc byte[JsonConstants.StackallocByteThreshold]
            : (rentedArray = ArrayPool<byte>.Shared.Rent(maxByteCount));

        try
        {
            int bytesWritten;
#if NET
            bytesWritten = Encoding.UTF8.GetBytes(json, utf8Buffer);
#else
            unsafe
            {
                fixed (char* pChars = json)
                fixed (byte* pBytes = utf8Buffer)
                {
                    bytesWritten = Encoding.UTF8.GetBytes(pChars, json.Length, pBytes, utf8Buffer.Length);
                }
            }
#endif
            ConvertToYaml(utf8Buffer.Slice(0, bytesWritten), utf8Stream, options);
        }
        finally
        {
            if (rentedArray is not null)
            {
                ArrayPool<byte>.Shared.Return(rentedArray);
            }
        }
    }

    /// <summary>
    /// Converts UTF-8 JSON bytes to YAML, writing the output to the
    /// specified <see cref="System.IO.Stream"/>.
    /// </summary>
    /// <param name="utf8Json">The UTF-8 encoded JSON bytes.</param>
    /// <param name="utf8Stream">The stream to write the UTF-8 YAML output to.</param>
    /// <param name="options">Optional YAML writer options.</param>
    public static void ConvertToYaml(
        ReadOnlySpan<byte> utf8Json,
        System.IO.Stream utf8Stream,
        YamlWriterOptions options = default)
    {
        Utf8YamlWriter yamlWriter = new(utf8Stream, options);

        try
        {
            JsonToYamlConverter.Convert(ref yamlWriter, utf8Json);
            yamlWriter.Flush();
        }
        finally
        {
            yamlWriter.Dispose();
        }
    }

    /// <summary>
    /// Enumerates the YAML parse events from UTF-8 YAML bytes, invoking
    /// the specified callback for each event.
    /// </summary>
    /// <param name="utf8Yaml">The UTF-8 encoded YAML bytes.</param>
    /// <param name="callback">
    /// A callback invoked for each YAML event. Return <see langword="true"/> to continue
    /// parsing or <see langword="false"/> to stop early. The <see cref="YamlEvent"/>
    /// and its spans are only valid for the duration of the callback.
    /// </param>
    /// <param name="options">Optional YAML reader options.</param>
    /// <returns>
    /// <see langword="true"/> if parsing completed normally;
    /// <see langword="false"/> if the callback returned <see langword="false"/> to stop early.
    /// </returns>
    /// <exception cref="YamlException">The YAML content is invalid.</exception>
    public static bool EnumerateEvents(
        ReadOnlySpan<byte> utf8Yaml,
        YamlEventCallback callback,
        YamlReaderOptions options = default)
    {
        YamlEventParser parser = new(utf8Yaml, callback, options);
        return parser.Parse();
    }

    /// <summary>
    /// Enumerates the YAML parse events from a YAML string, invoking
    /// the specified callback for each event.
    /// </summary>
    /// <param name="yaml">The YAML content as a string.</param>
    /// <param name="callback">
    /// A callback invoked for each YAML event. Return <see langword="true"/> to continue
    /// parsing or <see langword="false"/> to stop early. The <see cref="YamlEvent"/>
    /// and its spans are only valid for the duration of the callback.
    /// </param>
    /// <param name="options">Optional YAML reader options.</param>
    /// <returns>
    /// <see langword="true"/> if parsing completed normally;
    /// <see langword="false"/> if the callback returned <see langword="false"/> to stop early.
    /// </returns>
    /// <exception cref="YamlException">The YAML content is invalid.</exception>
    public static bool EnumerateEvents(
        string yaml,
        YamlEventCallback callback,
        YamlReaderOptions options = default)
    {
        int maxByteCount = Encoding.UTF8.GetMaxByteCount(yaml.Length);
        byte[]? rentedArray = null;

        Span<byte> utf8Buffer = maxByteCount <= JsonConstants.StackallocByteThreshold
            ? stackalloc byte[JsonConstants.StackallocByteThreshold]
            : (rentedArray = ArrayPool<byte>.Shared.Rent(maxByteCount));

        try
        {
            int bytesWritten;
#if NET
            bytesWritten = Encoding.UTF8.GetBytes(yaml, utf8Buffer);
#else
            unsafe
            {
                fixed (char* pChars = yaml)
                fixed (byte* pBytes = utf8Buffer)
                {
                    bytesWritten = Encoding.UTF8.GetBytes(pChars, yaml.Length, pBytes, utf8Buffer.Length);
                }
            }
#endif
            YamlEventParser parser = new(utf8Buffer.Slice(0, bytesWritten), callback, options);
            return parser.Parse();
        }
        finally
        {
            if (rentedArray is not null)
            {
                ArrayPool<byte>.Shared.Return(rentedArray);
            }
        }
    }
#endif
}

#else

using System.Buffers;
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

            // Copy the JSON bytes to a rented array that the document will own.
            // The workspace's buffer will be returned to the cache, so we must not
            // let the document reference it directly.
            ReadOnlySpan<byte> written = bufferWriter.WrittenSpan;
            int length = written.Length;
            byte[] ownedBytes = ArrayPool<byte>.Shared.Rent(length);
            written.CopyTo(ownedBytes);

            try
            {
                return ParsedJsonDocument<TElement>.Parse(
                    ownedBytes.AsMemory(0, length), ownedBytes);
            }
            catch
            {
                ownedBytes.AsSpan(0, length).Clear();
                ArrayPool<byte>.Shared.Return(ownedBytes);
                throw;
            }
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(writer, bufferWriter);
        }
    }

    /// <summary>
    /// Parses a YAML string and returns a <see cref="ParsedJsonDocument{T}"/> containing
    /// the equivalent JSON representation.
    /// </summary>
    /// <typeparam name="TElement">The type of the JSON element.</typeparam>
    /// <param name="yaml">The YAML content as a string.</param>
    /// <param name="options">Optional YAML reader options.</param>
    /// <returns>A <see cref="ParsedJsonDocument{T}"/> that must be disposed when no longer needed.</returns>
    /// <exception cref="YamlException">The YAML content is invalid.</exception>
    public static ParsedJsonDocument<TElement> Parse<TElement>(
        string yaml,
        YamlReaderOptions options = default)
        where TElement : struct, IJsonElement<TElement>
    {
        int maxByteCount = Encoding.UTF8.GetMaxByteCount(yaml.Length);
        byte[]? rentedArray = null;

        Span<byte> utf8Buffer = maxByteCount <= JsonConstants.StackallocByteThreshold
            ? stackalloc byte[JsonConstants.StackallocByteThreshold]
            : (rentedArray = ArrayPool<byte>.Shared.Rent(maxByteCount));

        try
        {
            int bytesWritten;
#if NET
            bytesWritten = Encoding.UTF8.GetBytes(yaml, utf8Buffer);
#else
            unsafe
            {
                fixed (char* pChars = yaml)
                fixed (byte* pBytes = utf8Buffer)
                {
                    bytesWritten = Encoding.UTF8.GetBytes(pChars, yaml.Length, pBytes, utf8Buffer.Length);
                }
            }
#endif
            using JsonWorkspace workspace = JsonWorkspace.Create();
            Utf8JsonWriter writer = workspace.RentWriterAndBuffer(
                WriterOptions,
                Math.Max(bytesWritten, 256),
                out IByteBufferWriter jsonBuffer);

            try
            {
                YamlToJsonConverter converter = new(utf8Buffer.Slice(0, bytesWritten), writer, options, jsonBuffer);
                converter.Convert();
                writer.Flush();

                // Copy the JSON bytes to a rented array that the document will own.
                ReadOnlySpan<byte> written = jsonBuffer.WrittenSpan;
                int jsonLength = written.Length;
                byte[] ownedBytes = ArrayPool<byte>.Shared.Rent(jsonLength);
                written.CopyTo(ownedBytes);

                try
                {
                    return ParsedJsonDocument<TElement>.Parse(
                        ownedBytes.AsMemory(0, jsonLength), ownedBytes);
                }
                catch
                {
                    ownedBytes.AsSpan(0, jsonLength).Clear();
                    ArrayPool<byte>.Shared.Return(ownedBytes);
                    throw;
                }
            }
            finally
            {
                workspace.ReturnWriterAndBuffer(writer, jsonBuffer);
            }
        }
        finally
        {
            if (rentedArray is not null)
            {
                ArrayPool<byte>.Shared.Return(rentedArray);
            }
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
    /// Converts a YAML string to a JSON string.
    /// </summary>
    /// <param name="yaml">The YAML content as a string.</param>
    /// <param name="options">Optional YAML reader options.</param>
    /// <returns>A string containing the JSON representation of the YAML content.</returns>
    /// <exception cref="YamlException">The YAML content is invalid.</exception>
    public static string ConvertToJsonString(
        string yaml,
        YamlReaderOptions options = default)
    {
        int maxByteCount = Encoding.UTF8.GetMaxByteCount(yaml.Length);
        byte[]? rentedArray = null;

        Span<byte> utf8Buffer = maxByteCount <= JsonConstants.StackallocByteThreshold
            ? stackalloc byte[JsonConstants.StackallocByteThreshold]
            : (rentedArray = ArrayPool<byte>.Shared.Rent(maxByteCount));

        try
        {
            int bytesWritten;
#if NET
            bytesWritten = Encoding.UTF8.GetBytes(yaml, utf8Buffer);
#else
            unsafe
            {
                fixed (char* pChars = yaml)
                fixed (byte* pBytes = utf8Buffer)
                {
                    bytesWritten = Encoding.UTF8.GetBytes(pChars, yaml.Length, pBytes, utf8Buffer.Length);
                }
            }
#endif
            return ConvertToJsonString(utf8Buffer.Slice(0, bytesWritten).ToArray(), options);
        }
        finally
        {
            if (rentedArray is not null)
            {
                ArrayPool<byte>.Shared.Return(rentedArray);
            }
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

#if !BUILDING_SOURCE_GENERATOR

    /// <summary>
    /// Converts a JSON element to a YAML string.
    /// </summary>
    /// <typeparam name="TElement">The type of the JSON element.</typeparam>
    /// <param name="element">The JSON element to convert.</param>
    /// <param name="options">Optional YAML writer options.</param>
    /// <returns>A string containing the YAML representation of the JSON element.</returns>
    /// <remarks>
    /// The converter walks the element tree directly through the
    /// <see cref="IJsonDocument"/> APIs, using zero-allocation
    /// index-based enumeration for all <see cref="IJsonElement{T}"/>
    /// implementations.
    /// </remarks>
    public static string ConvertToYamlString<TElement>(
        in TElement element,
        YamlWriterOptions options = default)
        where TElement : struct, IJsonElement<TElement>
    {
        using ArrayPoolBufferWriter bufferWriter = new(256);
        Utf8YamlWriter yamlWriter = new(bufferWriter, options);

        try
        {
            ConvertToYamlCore(in element, ref yamlWriter);
            yamlWriter.Flush();
        }
        finally
        {
            yamlWriter.Dispose();
        }

#if NET
        return Encoding.UTF8.GetString(bufferWriter.WrittenSpan);
#else
        return Encoding.UTF8.GetString(bufferWriter.WrittenSpan.ToArray());
#endif
    }

    /// <summary>
    /// Converts a JSON string to a YAML string.
    /// </summary>
    /// <param name="json">The JSON content as a string.</param>
    /// <param name="options">Optional YAML writer options.</param>
    /// <returns>A string containing the YAML representation of the JSON content.</returns>
    public static string ConvertToYamlString(
        string json,
        YamlWriterOptions options = default)
    {
        int maxByteCount = Encoding.UTF8.GetMaxByteCount(json.Length);
        byte[]? rentedArray = null;

        Span<byte> utf8Buffer = maxByteCount <= JsonConstants.StackallocByteThreshold
            ? stackalloc byte[JsonConstants.StackallocByteThreshold]
            : (rentedArray = ArrayPool<byte>.Shared.Rent(maxByteCount));

        try
        {
            int bytesWritten;
#if NET
            bytesWritten = Encoding.UTF8.GetBytes(json, utf8Buffer);
#else
            unsafe
            {
                fixed (char* pChars = json)
                fixed (byte* pBytes = utf8Buffer)
                {
                    bytesWritten = Encoding.UTF8.GetBytes(pChars, json.Length, pBytes, utf8Buffer.Length);
                }
            }
#endif
            return ConvertToYamlString(utf8Buffer.Slice(0, bytesWritten), options);
        }
        finally
        {
            if (rentedArray is not null)
            {
                ArrayPool<byte>.Shared.Return(rentedArray);
            }
        }
    }

    /// <summary>
    /// Converts UTF-8 JSON bytes to a YAML string.
    /// </summary>
    /// <param name="utf8Json">The UTF-8 encoded JSON bytes.</param>
    /// <param name="options">Optional YAML writer options.</param>
    /// <returns>A string containing the YAML representation of the JSON content.</returns>
    public static string ConvertToYamlString(
        ReadOnlySpan<byte> utf8Json,
        YamlWriterOptions options = default)
    {
        using ArrayPoolBufferWriter bufferWriter = new(256);
        Utf8YamlWriter yamlWriter = new(bufferWriter, options);

        try
        {
            JsonToYamlConverter.Convert(ref yamlWriter, utf8Json);
            yamlWriter.Flush();
        }
        finally
        {
            yamlWriter.Dispose();
        }

#if NET
        return Encoding.UTF8.GetString(bufferWriter.WrittenSpan);
#else
        return Encoding.UTF8.GetString(bufferWriter.WrittenSpan.ToArray());
#endif
    }

    /// <summary>
    /// Converts a JSON element to YAML, writing the output to the
    /// specified <see cref="IBufferWriter{T}"/>.
    /// </summary>
    /// <typeparam name="TElement">The type of the JSON element.</typeparam>
    /// <param name="element">The JSON element to convert.</param>
    /// <param name="writer">The buffer writer to write the YAML output to.</param>
    /// <param name="options">Optional YAML writer options.</param>
    public static void ConvertToYaml<TElement>(
        in TElement element,
        IBufferWriter<byte> writer,
        YamlWriterOptions options = default)
        where TElement : struct, IJsonElement<TElement>
    {
        Utf8YamlWriter yamlWriter = new(writer, options);

        try
        {
            ConvertToYamlCore(in element, ref yamlWriter);
            yamlWriter.Flush();
        }
        finally
        {
            yamlWriter.Dispose();
        }
    }

    /// <summary>
    /// Converts a JSON element to YAML, writing the output to the
    /// specified <see cref="System.IO.Stream"/>.
    /// </summary>
    /// <typeparam name="TElement">The type of the JSON element.</typeparam>
    /// <param name="element">The JSON element to convert.</param>
    /// <param name="utf8Stream">The stream to write the UTF-8 YAML output to.</param>
    /// <param name="options">Optional YAML writer options.</param>
    public static void ConvertToYaml<TElement>(
        in TElement element,
        System.IO.Stream utf8Stream,
        YamlWriterOptions options = default)
        where TElement : struct, IJsonElement<TElement>
    {
        Utf8YamlWriter yamlWriter = new(utf8Stream, options);

        try
        {
            ConvertToYamlCore(in element, ref yamlWriter);
            yamlWriter.Flush();
        }
        finally
        {
            yamlWriter.Dispose();
        }
    }

    /// <summary>
    /// Converts a JSON string to YAML, writing the output to the
    /// specified <see cref="IBufferWriter{T}"/>.
    /// </summary>
    /// <param name="json">The JSON content as a string.</param>
    /// <param name="writer">The buffer writer to write the YAML output to.</param>
    /// <param name="options">Optional YAML writer options.</param>
    public static void ConvertToYaml(
        string json,
        IBufferWriter<byte> writer,
        YamlWriterOptions options = default)
    {
        int maxByteCount = Encoding.UTF8.GetMaxByteCount(json.Length);
        byte[]? rentedArray = null;

        Span<byte> utf8Buffer = maxByteCount <= JsonConstants.StackallocByteThreshold
            ? stackalloc byte[JsonConstants.StackallocByteThreshold]
            : (rentedArray = ArrayPool<byte>.Shared.Rent(maxByteCount));

        try
        {
            int bytesWritten;
#if NET
            bytesWritten = Encoding.UTF8.GetBytes(json, utf8Buffer);
#else
            unsafe
            {
                fixed (char* pChars = json)
                fixed (byte* pBytes = utf8Buffer)
                {
                    bytesWritten = Encoding.UTF8.GetBytes(pChars, json.Length, pBytes, utf8Buffer.Length);
                }
            }
#endif
            ConvertToYaml(utf8Buffer.Slice(0, bytesWritten), writer, options);
        }
        finally
        {
            if (rentedArray is not null)
            {
                ArrayPool<byte>.Shared.Return(rentedArray);
            }
        }
    }

    /// <summary>
    /// Converts UTF-8 JSON bytes to YAML, writing the output to the
    /// specified <see cref="IBufferWriter{T}"/>.
    /// </summary>
    /// <param name="utf8Json">The UTF-8 encoded JSON bytes.</param>
    /// <param name="writer">The buffer writer to write the YAML output to.</param>
    /// <param name="options">Optional YAML writer options.</param>
    public static void ConvertToYaml(
        ReadOnlySpan<byte> utf8Json,
        IBufferWriter<byte> writer,
        YamlWriterOptions options = default)
    {
        Utf8YamlWriter yamlWriter = new(writer, options);

        try
        {
            JsonToYamlConverter.Convert(ref yamlWriter, utf8Json);
            yamlWriter.Flush();
        }
        finally
        {
            yamlWriter.Dispose();
        }
    }

    /// <summary>
    /// Converts a JSON string to YAML, writing the output to the
    /// specified <see cref="System.IO.Stream"/>.
    /// </summary>
    /// <param name="json">The JSON content as a string.</param>
    /// <param name="utf8Stream">The stream to write the UTF-8 YAML output to.</param>
    /// <param name="options">Optional YAML writer options.</param>
    public static void ConvertToYaml(
        string json,
        System.IO.Stream utf8Stream,
        YamlWriterOptions options = default)
    {
        int maxByteCount = Encoding.UTF8.GetMaxByteCount(json.Length);
        byte[]? rentedArray = null;

        Span<byte> utf8Buffer = maxByteCount <= JsonConstants.StackallocByteThreshold
            ? stackalloc byte[JsonConstants.StackallocByteThreshold]
            : (rentedArray = ArrayPool<byte>.Shared.Rent(maxByteCount));

        try
        {
            int bytesWritten;
#if NET
            bytesWritten = Encoding.UTF8.GetBytes(json, utf8Buffer);
#else
            unsafe
            {
                fixed (char* pChars = json)
                fixed (byte* pBytes = utf8Buffer)
                {
                    bytesWritten = Encoding.UTF8.GetBytes(pChars, json.Length, pBytes, utf8Buffer.Length);
                }
            }
#endif
            ConvertToYaml(utf8Buffer.Slice(0, bytesWritten), utf8Stream, options);
        }
        finally
        {
            if (rentedArray is not null)
            {
                ArrayPool<byte>.Shared.Return(rentedArray);
            }
        }
    }

    /// <summary>
    /// Converts UTF-8 JSON bytes to YAML, writing the output to the
    /// specified <see cref="System.IO.Stream"/>.
    /// </summary>
    /// <param name="utf8Json">The UTF-8 encoded JSON bytes.</param>
    /// <param name="utf8Stream">The stream to write the UTF-8 YAML output to.</param>
    /// <param name="options">Optional YAML writer options.</param>
    public static void ConvertToYaml(
        ReadOnlySpan<byte> utf8Json,
        System.IO.Stream utf8Stream,
        YamlWriterOptions options = default)
    {
        Utf8YamlWriter yamlWriter = new(utf8Stream, options);

        try
        {
            JsonToYamlConverter.Convert(ref yamlWriter, utf8Json);
            yamlWriter.Flush();
        }
        finally
        {
            yamlWriter.Dispose();
        }
    }

    /// <summary>
    /// Dispatches element conversion through the generic
    /// <see cref="IJsonElement{T}"/>-based converter walk.
    /// </summary>
    private static void ConvertToYamlCore<TElement>(
        in TElement element,
        ref Utf8YamlWriter yamlWriter)
        where TElement : struct, IJsonElement<TElement>
    {
        JsonToYamlConverter.Convert(ref yamlWriter, in element);
    }

    /// <summary>
    /// Enumerates the YAML parse events from UTF-8 YAML bytes, invoking
    /// the specified callback for each event.
    /// </summary>
    /// <param name="utf8Yaml">The UTF-8 encoded YAML bytes.</param>
    /// <param name="callback">
    /// A callback invoked for each YAML event. Return <see langword="true"/> to continue
    /// parsing or <see langword="false"/> to stop early. The <see cref="YamlEvent"/>
    /// and its spans are only valid for the duration of the callback.
    /// </param>
    /// <param name="options">Optional YAML reader options.</param>
    /// <returns>
    /// <see langword="true"/> if parsing completed normally;
    /// <see langword="false"/> if the callback returned <see langword="false"/> to stop early.
    /// </returns>
    /// <exception cref="YamlException">The YAML content is invalid.</exception>
    public static bool EnumerateEvents(
        ReadOnlySpan<byte> utf8Yaml,
        YamlEventCallback callback,
        YamlReaderOptions options = default)
    {
        YamlEventParser parser = new(utf8Yaml, callback, options);
        return parser.Parse();
    }

    /// <summary>
    /// Enumerates the YAML parse events from a YAML string, invoking
    /// the specified callback for each event.
    /// </summary>
    /// <param name="yaml">The YAML content as a string.</param>
    /// <param name="callback">
    /// A callback invoked for each YAML event. Return <see langword="true"/> to continue
    /// parsing or <see langword="false"/> to stop early. The <see cref="YamlEvent"/>
    /// and its spans are only valid for the duration of the callback.
    /// </param>
    /// <param name="options">Optional YAML reader options.</param>
    /// <returns>
    /// <see langword="true"/> if parsing completed normally;
    /// <see langword="false"/> if the callback returned <see langword="false"/> to stop early.
    /// </returns>
    /// <exception cref="YamlException">The YAML content is invalid.</exception>
    public static bool EnumerateEvents(
        string yaml,
        YamlEventCallback callback,
        YamlReaderOptions options = default)
    {
        int maxByteCount = Encoding.UTF8.GetMaxByteCount(yaml.Length);
        byte[]? rentedArray = null;

        Span<byte> utf8Buffer = maxByteCount <= JsonConstants.StackallocByteThreshold
            ? stackalloc byte[JsonConstants.StackallocByteThreshold]
            : (rentedArray = ArrayPool<byte>.Shared.Rent(maxByteCount));

        try
        {
            int bytesWritten;
#if NET
            bytesWritten = Encoding.UTF8.GetBytes(yaml, utf8Buffer);
#else
            unsafe
            {
                fixed (char* pChars = yaml)
                fixed (byte* pBytes = utf8Buffer)
                {
                    bytesWritten = Encoding.UTF8.GetBytes(pChars, yaml.Length, pBytes, utf8Buffer.Length);
                }
            }
#endif
            YamlEventParser parser = new(utf8Buffer.Slice(0, bytesWritten), callback, options);
            return parser.Parse();
        }
        finally
        {
            if (rentedArray is not null)
            {
                ArrayPool<byte>.Shared.Return(rentedArray);
            }
        }
    }
#endif
}

#endif