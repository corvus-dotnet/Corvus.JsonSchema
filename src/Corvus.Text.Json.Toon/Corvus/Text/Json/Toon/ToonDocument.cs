#if STJ && TOON

using System.Buffers;
using System.IO;
using System.Text;
using System.Text.Json;
using Corvus.Toon.Internal;

namespace Corvus.Toon;

/// <summary>
/// Provides methods for parsing TOON content and converting between TOON and JSON.
/// </summary>
public static class ToonDocument
{
    private static readonly JsonWriterOptions WriterOptions = new() { SkipValidation = true };

    /// <summary>
    /// Parses UTF-8 TOON bytes and returns a <see cref="JsonDocument"/> containing the equivalent JSON.
    /// </summary>
    /// <param name="utf8Toon">The UTF-8 TOON input.</param>
    /// <param name="options">The reader options.</param>
    /// <returns>A JSON document.</returns>
    public static JsonDocument Parse(ReadOnlyMemory<byte> utf8Toon, ToonReaderOptions options = default)
    {
        return ParseCore(utf8Toon.Span, options);
    }

    private static JsonDocument ParseCore(ReadOnlySpan<byte> utf8Toon, ToonReaderOptions options)
    {
        using ArrayPoolBufferWriter bufferWriter = new(Math.Max(utf8Toon.Length, 256));
        using Utf8JsonWriter writer = new(bufferWriter, WriterOptions);
        ToonToJsonConverter converter = new(utf8Toon, writer, options);
        converter.Convert();
        writer.Flush();

        var reader = new Utf8JsonReader(bufferWriter.WrittenSpan);
        return JsonDocument.ParseValue(ref reader);
    }

    /// <summary>
    /// Parses a TOON string and returns a <see cref="JsonDocument"/> containing the equivalent JSON.
    /// </summary>
    /// <param name="toon">The TOON input.</param>
    /// <param name="options">The reader options.</param>
    /// <returns>A JSON document.</returns>
    public static JsonDocument Parse(string toon, ToonReaderOptions options = default)
    {
        int utf8Length = Encoding.UTF8.GetByteCount(toon);
        byte[]? rentedArray = null;

        Span<byte> utf8Toon = utf8Length <= JsonConstants.StackallocByteThreshold
            ? stackalloc byte[JsonConstants.StackallocByteThreshold]
            : (rentedArray = ArrayPool<byte>.Shared.Rent(utf8Length));

        try
        {
            int bytesWritten = TranscodeToUtf8(toon, utf8Toon);
            return ParseCore(utf8Toon.Slice(0, bytesWritten), options);
        }
        finally
        {
            if (rentedArray != null)
            {
                ArrayPool<byte>.Shared.Return(rentedArray);
            }
        }
    }

    /// <summary>
    /// Converts UTF-8 TOON bytes to JSON.
    /// </summary>
    /// <param name="utf8Toon">The TOON input.</param>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="options">The reader options.</param>
    public static void Convert(ReadOnlySpan<byte> utf8Toon, Utf8JsonWriter writer, ToonReaderOptions options = default)
    {
        ToonToJsonConverter converter = new(utf8Toon, writer, options);
        converter.Convert();
    }

    /// <summary>
    /// Converts a TOON string to a JSON string.
    /// </summary>
    /// <param name="toon">The TOON input.</param>
    /// <param name="options">The reader options.</param>
    /// <returns>The JSON output.</returns>
    public static string ConvertToJsonString(string toon, ToonReaderOptions options = default)
    {
        int utf8Length = Encoding.UTF8.GetByteCount(toon);
        byte[]? rentedArray = null;

        Span<byte> utf8Toon = utf8Length <= JsonConstants.StackallocByteThreshold
            ? stackalloc byte[JsonConstants.StackallocByteThreshold]
            : (rentedArray = ArrayPool<byte>.Shared.Rent(utf8Length));

        try
        {
            int bytesWritten = TranscodeToUtf8(toon, utf8Toon);
            return ConvertToJsonString(utf8Toon.Slice(0, bytesWritten), options);
        }
        finally
        {
            if (rentedArray != null)
            {
                ArrayPool<byte>.Shared.Return(rentedArray);
            }
        }
    }

    /// <summary>
    /// Converts UTF-8 TOON bytes to a JSON string.
    /// </summary>
    /// <param name="utf8Toon">The TOON input.</param>
    /// <param name="options">The reader options.</param>
    /// <returns>The JSON output.</returns>
    public static string ConvertToJsonString(ReadOnlySpan<byte> utf8Toon, ToonReaderOptions options = default)
    {
        using ArrayPoolBufferWriter bufferWriter = new(Math.Max(utf8Toon.Length, 256));
        using Utf8JsonWriter writer = new(bufferWriter, WriterOptions);
        ToonToJsonConverter converter = new(utf8Toon, writer, options);
        converter.Convert();
        writer.Flush();
        return FromUtf8(bufferWriter.WrittenSpan);
    }

    /// <summary>
    /// Converts UTF-8 TOON bytes to a JSON string.
    /// </summary>
    /// <param name="utf8Toon">The TOON input.</param>
    /// <param name="options">The reader options.</param>
    /// <returns>The JSON output.</returns>
    public static string ConvertToJsonString(ReadOnlyMemory<byte> utf8Toon, ToonReaderOptions options = default)
    {
        return ConvertToJsonString(utf8Toon.Span, options);
    }

    /// <summary>
    /// Converts a JSON element to a TOON string.
    /// </summary>
    /// <param name="element">The JSON element.</param>
    /// <param name="options">The writer options.</param>
    /// <returns>The TOON output.</returns>
    public static string ConvertToToonString(JsonElement element, ToonWriterOptions options = default)
    {
        using ArrayPoolBufferWriter bufferWriter = new(256);
        ConvertToToon(element, bufferWriter, options);
        return FromUtf8(bufferWriter.WrittenSpan);
    }

    /// <summary>
    /// Converts a JSON element to a TOON string.
    /// </summary>
    /// <param name="element">The JSON element.</param>
    /// <param name="options">The writer options.</param>
    /// <returns>The TOON output.</returns>
    public static string ConvertToToon(JsonElement element, ToonWriterOptions options = default)
    {
        return ConvertToToonString(element, options);
    }

    /// <summary>
    /// Converts a JSON string to TOON.
    /// </summary>
    /// <param name="json">The JSON input.</param>
    /// <param name="options">The writer options.</param>
    /// <returns>The TOON output.</returns>
    public static string ConvertToToonString(string json, ToonWriterOptions options = default)
    {
        int utf8Length = Encoding.UTF8.GetByteCount(json);
        byte[]? rentedArray = null;

        Span<byte> utf8Json = utf8Length <= JsonConstants.StackallocByteThreshold
            ? stackalloc byte[JsonConstants.StackallocByteThreshold]
            : (rentedArray = ArrayPool<byte>.Shared.Rent(utf8Length));

        try
        {
            int bytesWritten = TranscodeToUtf8(json, utf8Json);
            return ConvertToToonString(utf8Json.Slice(0, bytesWritten), options);
        }
        finally
        {
            if (rentedArray != null)
            {
                ArrayPool<byte>.Shared.Return(rentedArray);
            }
        }
    }

    /// <summary>
    /// Converts UTF-8 JSON bytes to TOON.
    /// </summary>
    /// <param name="utf8Json">The JSON input.</param>
    /// <param name="options">The writer options.</param>
    /// <returns>The TOON output.</returns>
    public static string ConvertToToonString(ReadOnlySpan<byte> utf8Json, ToonWriterOptions options = default)
    {
        using ArrayPoolBufferWriter bufferWriter = new(256);
        ConvertToToon(utf8Json, bufferWriter, options);
        return FromUtf8(bufferWriter.WrittenSpan);
    }

    /// <summary>
    /// Converts a JSON element to TOON.
    /// </summary>
    /// <param name="element">The JSON element.</param>
    /// <param name="writer">The output writer.</param>
    /// <param name="options">The writer options.</param>
    public static void ConvertToToon(JsonElement element, IBufferWriter<byte> writer, ToonWriterOptions options = default)
    {
        Utf8ToonWriter toonWriter = new(writer, options);
        try
        {
            JsonToToonConverter.Convert(ref toonWriter, element);
            toonWriter.Flush();
        }
        finally
        {
            toonWriter.Dispose();
        }
    }

    /// <summary>
    /// Converts UTF-8 JSON bytes to TOON.
    /// </summary>
    /// <param name="utf8Json">The JSON input.</param>
    /// <param name="writer">The output writer.</param>
    /// <param name="options">The writer options.</param>
    public static void ConvertToToon(ReadOnlySpan<byte> utf8Json, IBufferWriter<byte> writer, ToonWriterOptions options = default)
    {
        Utf8ToonWriter toonWriter = new(writer, options);
        try
        {
            JsonToToonConverter.Convert(ref toonWriter, utf8Json);
            toonWriter.Flush();
        }
        finally
        {
            toonWriter.Dispose();
        }
    }

    /// <summary>
    /// Converts UTF-8 JSON bytes to TOON.
    /// </summary>
    /// <param name="utf8Json">The JSON input.</param>
    /// <param name="stream">The output stream.</param>
    /// <param name="options">The writer options.</param>
    public static void ConvertToToon(ReadOnlySpan<byte> utf8Json, Stream stream, ToonWriterOptions options = default)
    {
        Utf8ToonWriter toonWriter = new(stream, options);
        try
        {
            JsonToToonConverter.Convert(ref toonWriter, utf8Json);
            toonWriter.Flush();
        }
        finally
        {
            toonWriter.Dispose();
        }
    }

    private static int TranscodeToUtf8(string value, Span<byte> destination)
    {
#if NET
        return Encoding.UTF8.GetBytes(value, destination);
#else
        if (value.Length == 0)
        {
            return 0;
        }

        unsafe
        {
            fixed (char* pValue = value)
            fixed (byte* pDestination = destination)
            {
                return Encoding.UTF8.GetBytes(pValue, value.Length, pDestination, destination.Length);
            }
        }
#endif
    }

    private static string FromUtf8(ReadOnlySpan<byte> value)
    {
        if (value.Length == 0)
        {
            return string.Empty;
        }

#if NET
        return Encoding.UTF8.GetString(value);
#else
        unsafe
        {
            fixed (byte* pValue = value)
            {
                return Encoding.UTF8.GetString(pValue, value.Length);
            }
        }
#endif
    }
}

#else

using System.Buffers;
using System.IO;
using System.Text;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.Toon.Internal;

namespace Corvus.Text.Json.Toon;

/// <summary>
/// Provides methods for parsing TOON content and converting between TOON and JSON.
/// </summary>
public static class ToonDocument
{
    private static readonly JsonWriterOptions WriterOptions = new() { SkipValidation = true };

    /// <summary>
    /// Parses UTF-8 TOON bytes and returns a <see cref="ParsedJsonDocument{T}"/> containing the equivalent JSON.
    /// </summary>
    /// <typeparam name="TElement">The JSON element type.</typeparam>
    /// <param name="utf8Toon">The UTF-8 TOON input.</param>
    /// <param name="options">The reader options.</param>
    /// <returns>A parsed JSON document.</returns>
    public static ParsedJsonDocument<TElement> Parse<TElement>(ReadOnlyMemory<byte> utf8Toon, ToonReaderOptions options = default)
        where TElement : struct, IJsonElement<TElement>
    {
        return ParseCore<TElement>(utf8Toon.Span, options);
    }

    private static ParsedJsonDocument<TElement> ParseCore<TElement>(ReadOnlySpan<byte> utf8Toon, ToonReaderOptions options)
        where TElement : struct, IJsonElement<TElement>
    {
        using ArrayPoolBufferWriter bufferWriter = new(Math.Max(utf8Toon.Length, 256));
        using Utf8JsonWriter writer = new(bufferWriter, WriterOptions);
        ToonToJsonConverter converter = new(utf8Toon, writer, options);
        converter.Convert();
        writer.Flush();

        ReadOnlySpan<byte> written = bufferWriter.WrittenSpan;
        byte[] ownedBytes = ArrayPool<byte>.Shared.Rent(written.Length);
        written.CopyTo(ownedBytes);
        try
        {
            return ParsedJsonDocument<TElement>.Parse(ownedBytes.AsMemory(0, written.Length), ownedBytes);
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(ownedBytes);
            throw;
        }
    }

    /// <summary>
    /// Parses a TOON string and returns a <see cref="ParsedJsonDocument{T}"/> containing the equivalent JSON.
    /// </summary>
    /// <typeparam name="TElement">The JSON element type.</typeparam>
    /// <param name="toon">The TOON input.</param>
    /// <param name="options">The reader options.</param>
    /// <returns>A parsed JSON document.</returns>
    public static ParsedJsonDocument<TElement> Parse<TElement>(string toon, ToonReaderOptions options = default)
        where TElement : struct, IJsonElement<TElement>
    {
        int utf8Length = Encoding.UTF8.GetByteCount(toon);
        byte[]? rentedArray = null;

        Span<byte> utf8Toon = utf8Length <= JsonConstants.StackallocByteThreshold
            ? stackalloc byte[JsonConstants.StackallocByteThreshold]
            : (rentedArray = ArrayPool<byte>.Shared.Rent(utf8Length));

        try
        {
            int bytesWritten = TranscodeToUtf8(toon, utf8Toon);
            return ParseCore<TElement>(utf8Toon.Slice(0, bytesWritten), options);
        }
        finally
        {
            if (rentedArray != null)
            {
                ArrayPool<byte>.Shared.Return(rentedArray);
            }
        }
    }

    /// <summary>
    /// Converts UTF-8 TOON bytes to JSON.
    /// </summary>
    /// <param name="utf8Toon">The TOON input.</param>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="options">The reader options.</param>
    public static void Convert(ReadOnlySpan<byte> utf8Toon, Utf8JsonWriter writer, ToonReaderOptions options = default)
    {
        ToonToJsonConverter converter = new(utf8Toon, writer, options);
        converter.Convert();
    }

    /// <summary>
    /// Converts a TOON string to a JSON string.
    /// </summary>
    /// <param name="toon">The TOON input.</param>
    /// <param name="options">The reader options.</param>
    /// <returns>The JSON output.</returns>
    public static string ConvertToJsonString(string toon, ToonReaderOptions options = default)
    {
        int utf8Length = Encoding.UTF8.GetByteCount(toon);
        byte[]? rentedArray = null;

        Span<byte> utf8Toon = utf8Length <= JsonConstants.StackallocByteThreshold
            ? stackalloc byte[JsonConstants.StackallocByteThreshold]
            : (rentedArray = ArrayPool<byte>.Shared.Rent(utf8Length));

        try
        {
            int bytesWritten = TranscodeToUtf8(toon, utf8Toon);
            return ConvertToJsonString(utf8Toon.Slice(0, bytesWritten), options);
        }
        finally
        {
            if (rentedArray != null)
            {
                ArrayPool<byte>.Shared.Return(rentedArray);
            }
        }
    }

    /// <summary>
    /// Converts UTF-8 TOON bytes to a JSON string.
    /// </summary>
    /// <param name="utf8Toon">The TOON input.</param>
    /// <param name="options">The reader options.</param>
    /// <returns>The JSON output.</returns>
    public static string ConvertToJsonString(ReadOnlySpan<byte> utf8Toon, ToonReaderOptions options = default)
    {
        using ArrayPoolBufferWriter bufferWriter = new(Math.Max(utf8Toon.Length, 256));
        using Utf8JsonWriter writer = new(bufferWriter, WriterOptions);
        ToonToJsonConverter converter = new(utf8Toon, writer, options);
        converter.Convert();
        writer.Flush();
        return FromUtf8(bufferWriter.WrittenSpan);
    }

    /// <summary>
    /// Converts UTF-8 TOON bytes to a JSON string.
    /// </summary>
    /// <param name="utf8Toon">The TOON input.</param>
    /// <param name="options">The reader options.</param>
    /// <returns>The JSON output.</returns>
    public static string ConvertToJsonString(ReadOnlyMemory<byte> utf8Toon, ToonReaderOptions options = default)
    {
        return ConvertToJsonString(utf8Toon.Span, options);
    }

    /// <summary>
    /// Converts a JSON element to a TOON string.
    /// </summary>
    /// <typeparam name="TElement">The JSON element type.</typeparam>
    /// <param name="element">The JSON element.</param>
    /// <param name="options">The writer options.</param>
    /// <returns>The TOON output.</returns>
    public static string ConvertToToonString<TElement>(in TElement element, ToonWriterOptions options = default)
        where TElement : struct, IJsonElement<TElement>
    {
        using ArrayPoolBufferWriter bufferWriter = new(256);
        ConvertToToon(element, bufferWriter, options);
        return FromUtf8(bufferWriter.WrittenSpan);
    }

    /// <summary>
    /// Converts a JSON element to a TOON string.
    /// </summary>
    /// <typeparam name="TElement">The JSON element type.</typeparam>
    /// <param name="element">The JSON element.</param>
    /// <param name="options">The writer options.</param>
    /// <returns>The TOON output.</returns>
    public static string ConvertToToon<TElement>(in TElement element, ToonWriterOptions options = default)
        where TElement : struct, IJsonElement<TElement>
    {
        return ConvertToToonString(element, options);
    }

    /// <summary>
    /// Converts UTF-8 JSON bytes to a TOON string.
    /// </summary>
    /// <param name="utf8Json">The JSON input.</param>
    /// <param name="options">The writer options.</param>
    /// <returns>The TOON output.</returns>
    public static string ConvertToToonString(ReadOnlySpan<byte> utf8Json, ToonWriterOptions options = default)
    {
        using ArrayPoolBufferWriter bufferWriter = new(256);
        ConvertToToon(utf8Json, bufferWriter, options);
        return FromUtf8(bufferWriter.WrittenSpan);
    }

    /// <summary>
    /// Converts a JSON string to a TOON string.
    /// </summary>
    /// <param name="json">The JSON input.</param>
    /// <param name="options">The writer options.</param>
    /// <returns>The TOON output.</returns>
    public static string ConvertToToonString(string json, ToonWriterOptions options = default)
    {
        int utf8Length = Encoding.UTF8.GetByteCount(json);
        byte[]? rentedArray = null;

        Span<byte> utf8Json = utf8Length <= JsonConstants.StackallocByteThreshold
            ? stackalloc byte[JsonConstants.StackallocByteThreshold]
            : (rentedArray = ArrayPool<byte>.Shared.Rent(utf8Length));

        try
        {
            int bytesWritten = TranscodeToUtf8(json, utf8Json);
            return ConvertToToonString(utf8Json.Slice(0, bytesWritten), options);
        }
        finally
        {
            if (rentedArray != null)
            {
                ArrayPool<byte>.Shared.Return(rentedArray);
            }
        }
    }

    /// <summary>
    /// Converts a JSON element to TOON.
    /// </summary>
    /// <typeparam name="TElement">The JSON element type.</typeparam>
    /// <param name="element">The JSON element.</param>
    /// <param name="writer">The output writer.</param>
    /// <param name="options">The writer options.</param>
    public static void ConvertToToon<TElement>(in TElement element, IBufferWriter<byte> writer, ToonWriterOptions options = default)
        where TElement : struct, IJsonElement<TElement>
    {
        Utf8ToonWriter toonWriter = new(writer, options);
        try
        {
            JsonToToonConverter.Convert(ref toonWriter, element);
            toonWriter.Flush();
        }
        finally
        {
            toonWriter.Dispose();
        }
    }

    /// <summary>
    /// Converts UTF-8 JSON bytes to TOON.
    /// </summary>
    /// <param name="utf8Json">The JSON input.</param>
    /// <param name="writer">The output writer.</param>
    /// <param name="options">The writer options.</param>
    public static void ConvertToToon(ReadOnlySpan<byte> utf8Json, IBufferWriter<byte> writer, ToonWriterOptions options = default)
    {
        Utf8ToonWriter toonWriter = new(writer, options);
        try
        {
            JsonToToonConverter.Convert(ref toonWriter, utf8Json);
            toonWriter.Flush();
        }
        finally
        {
            toonWriter.Dispose();
        }
    }

    /// <summary>
    /// Converts UTF-8 JSON bytes to TOON.
    /// </summary>
    /// <param name="utf8Json">The JSON input.</param>
    /// <param name="stream">The output stream.</param>
    /// <param name="options">The writer options.</param>
    public static void ConvertToToon(ReadOnlySpan<byte> utf8Json, Stream stream, ToonWriterOptions options = default)
    {
        Utf8ToonWriter toonWriter = new(stream, options);
        try
        {
            JsonToToonConverter.Convert(ref toonWriter, utf8Json);
            toonWriter.Flush();
        }
        finally
        {
            toonWriter.Dispose();
        }
    }

    private static int TranscodeToUtf8(string value, Span<byte> destination)
    {
#if NET
        return Encoding.UTF8.GetBytes(value, destination);
#else
        if (value.Length == 0)
        {
            return 0;
        }

        unsafe
        {
            fixed (char* pValue = value)
            fixed (byte* pDestination = destination)
            {
                return Encoding.UTF8.GetBytes(pValue, value.Length, pDestination, destination.Length);
            }
        }
#endif
    }

    private static string FromUtf8(ReadOnlySpan<byte> value)
    {
        if (value.Length == 0)
        {
            return string.Empty;
        }

#if NET
        return Encoding.UTF8.GetString(value);
#else
        unsafe
        {
            fixed (byte* pValue = value)
            {
                return Encoding.UTF8.GetString(pValue, value.Length);
            }
        }
#endif
    }
}

#endif