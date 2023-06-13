// <copyright file="LowAllocJsonUtils.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Encodings.Web;
using System.Text.Json;
using CommunityToolkit.HighPerformance.Buffers;
using Microsoft.Extensions.ObjectPool;

namespace Corvus.Json;

/// <summary>
/// Utilities for low allocation access to JSON values.
/// </summary>
/// <remarks>
/// These adapters give access to the underlying UTF8 bytes of JSON string
/// values, unless/until https://github.com/dotnet/runtime/issues/74028 lands.
/// </remarks>
internal static class LowAllocJsonUtils
{
    private static readonly ObjectPool<PooledWriter> WriterPool = new DefaultObjectPoolProvider().Create<PooledWriter>(new Utf8JsonWriterPooledObjectPolicy());

    /// <summary>
    /// Process raw JSON text.
    /// </summary>
    /// <typeparam name="TState">The type of the state for the processor.</typeparam>
    /// <typeparam name="TResult">The type of the result of processing.</typeparam>
    /// <param name="element">The json element to process.</param>
    /// <param name="state">The state passed to the processor.</param>
    /// <param name="callback">The processing callback.</param>
    /// <param name="result">The result of processing.</param>
    /// <returns><c>True</c> if the processing succeeded, otherwise false.</returns>
    public static bool ProcessRawText<TState, TResult>(
        this JsonElement element,
        in TState state,
        in Utf8Parser<TState, TResult> callback,
        [NotNullWhen(true)] out TResult? result)
    {
        PooledWriter? writerPair = null;
        try
        {
            writerPair = WriterPool.Get();
            (Utf8JsonWriter w, ArrayPoolBufferWriter<byte> writer) = writerPair.Get();
            element.WriteTo(w);
            w.Flush();
            return callback(writer.WrittenSpan[1..^1], state, out result);
        }
        finally
        {
            if (writerPair is not null)
            {
                WriterPool.Return(writerPair);
            }
        }
    }

    /// <summary>
    /// Process raw JSON text for a property name.
    /// </summary>
    /// <typeparam name="TState">The type of the state for the processor.</typeparam>
    /// <typeparam name="TResult">The type of the result of processing.</typeparam>
    /// <param name="property">The json property to process.</param>
    /// <param name="state">The state passed to the processor.</param>
    /// <param name="callback">The processing callback.</param>
    /// <param name="result">The result of processing.</param>
    /// <returns><c>True</c> if the processing succeeded, otherwise false.</returns>
    public static bool ProcessRawTextForName<TState, TResult>(
        this JsonProperty property,
        in TState state,
        in Utf8Parser<TState, TResult> callback,
        [NotNullWhen(true)] out TResult? result)
    {
        PooledWriter? writerPair = null;
        try
        {
            writerPair = WriterPool.Get();
            (Utf8JsonWriter w, ArrayPoolBufferWriter<byte> writer) = writerPair.Get();
            property.WriteTo(w);
            w.Flush();
            int endOfName = writer.WrittenSpan[1..].IndexOf(JsonConstants.Quote) + 1;
            return callback(writer.WrittenSpan[1..endOfName], state, out result);
        }
        finally
        {
            if (writerPair is not null)
            {
                WriterPool.Return(writerPair);
            }
        }
    }

    /// <summary>
    /// Process raw JSON text.
    /// </summary>
    /// <typeparam name="TState">The type of the state for the processor.</typeparam>
    /// <typeparam name="TResult">The type of the result of processing.</typeparam>
    /// <param name="element">The json element to process.</param>
    /// <param name="state">The state passed to the processor.</param>
    /// <param name="callback">The processing callback.</param>
    /// <param name="result">The result of processing.</param>
    /// <returns><c>True</c> if the processing succeeded, otherwise false.</returns>
    public static bool ProcessRawTextForNameAndString<TState, TResult>(
        this JsonProperty element,
        in TState state,
        in Utf8PropertyParser<TState, TResult> callback,
        [NotNullWhen(true)] out TResult? result)
    {
        PooledWriter? writerPair = null;
        try
        {
            writerPair = WriterPool.Get();
            (Utf8JsonWriter w, ArrayPoolBufferWriter<byte> writer) = writerPair.Get();
            element.WriteTo(w);
            w.Flush();

            // Find the name and the property value
            int endOfName = writer.WrittenSpan[1..].IndexOf(JsonConstants.Quote) + 1;
            int startOfSpan = writer.WrittenSpan[(endOfName + 1)..].IndexOf(JsonConstants.Quote) + endOfName + 1;
            return callback(writer.WrittenSpan[1..endOfName], writer.WrittenSpan[(startOfSpan + 1)..^1], state, out result);
        }
        finally
        {
            if (writerPair is not null)
            {
                WriterPool.Return(writerPair);
            }
        }
    }

    /// <summary>
    /// Concatenate two JSON values, as a UTF8 JSON string (including quotes).
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <param name="buffer">The buffer into which to concatenate the values.</param>
    /// <param name="firstValue">The first value.</param>
    /// <param name="secondValue">The second value.</param>
    /// <returns>A count of the characters written.</returns>
    public static int ConcatenateAsUtf8JsonString<T1, T2>(Span<byte> buffer, in T1 firstValue, in T2 secondValue)
        where T1 : struct, IJsonValue<T1>
        where T2 : struct, IJsonValue<T2>
    {
        int offset = 0;
        PooledWriter? writerPair = null;
        try
        {
            writerPair = WriterPool.Get();
            (Utf8JsonWriter w, ArrayPoolBufferWriter<byte> writer) = writerPair.Get();

            offset = CopyToBufferWithLeadingQuote(buffer, firstValue, offset, w, writer);

            // Reset the writer (which resets the buffer) for reuse
            w.Reset();
            writer.Clear();

            return CopyToBufferWithTrailingQuote(buffer, secondValue, offset, w, writer);
        }
        finally
        {
            if (writerPair is not null)
            {
                WriterPool.Return(writerPair);
            }
        }
    }

    /// <summary>
    /// Concatenate three JSON values, as a UTF8 JSON string (including quotes).
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <typeparam name="T3">The type of the third value.</typeparam>
    /// <param name="buffer">The buffer into which to concatenate the values.</param>
    /// <param name="firstValue">The first value.</param>
    /// <param name="secondValue">The second value.</param>
    /// <param name="thirdValue">The third value.</param>
    /// <returns>A count of the characters written.</returns>
    public static int ConcatenateAsUtf8JsonString<T1, T2, T3>(Span<byte> buffer, in T1 firstValue, in T2 secondValue, in T3 thirdValue)
    where T1 : struct, IJsonValue<T1>
    where T2 : struct, IJsonValue<T2>
    where T3 : struct, IJsonValue<T3>
    {
        int offset = 0;
        PooledWriter? writerPair = null;
        try
        {
            writerPair = WriterPool.Get();
            (Utf8JsonWriter w, ArrayPoolBufferWriter<byte> writer) = writerPair.Get();

            offset = CopyToBufferWithLeadingQuote(buffer, firstValue, offset, w, writer);

            // Reset the writer (which resets the buffer) for reuse
            w.Reset();
            writer.Clear();

            offset = CopyToBufferWithoutQuotes(buffer, secondValue, offset, w, writer);

            // Reset the writer (which resets the buffer) for reuse
            w.Reset();
            writer.Clear();

            return CopyToBufferWithTrailingQuote(buffer, thirdValue, offset, w, writer);
        }
        finally
        {
            if (writerPair is not null)
            {
                WriterPool.Return(writerPair);
            }
        }
    }

    /// <summary>
    /// Concatenate four JSON values, as a UTF8 JSON string (including quotes).
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <typeparam name="T3">The type of the third value.</typeparam>
    /// <typeparam name="T4">The type of the fourth value.</typeparam>
    /// <param name="buffer">The buffer into which to concatenate the values.</param>
    /// <param name="firstValue">The first value.</param>
    /// <param name="secondValue">The second value.</param>
    /// <param name="thirdValue">The third value.</param>
    /// <param name="fourthValue">The fourth value.</param>
    /// <returns>A count of the characters written.</returns>
    public static int ConcatenateAsUtf8JsonString<T1, T2, T3, T4>(Span<byte> buffer, in T1 firstValue, in T2 secondValue, in T3 thirdValue, in T4 fourthValue)
        where T1 : struct, IJsonValue<T1>
        where T2 : struct, IJsonValue<T2>
        where T3 : struct, IJsonValue<T3>
        where T4 : struct, IJsonValue<T4>
    {
        int offset = 0;
        PooledWriter? writerPair = null;
        try
        {
            writerPair = WriterPool.Get();
            (Utf8JsonWriter w, ArrayPoolBufferWriter<byte> writer) = writerPair.Get();

            offset = CopyToBufferWithLeadingQuote(buffer, firstValue, offset, w, writer);

            // Reset the writer (which resets the buffer) for reuse
            w.Reset();
            writer.Clear();

            offset = CopyToBufferWithoutQuotes(buffer, secondValue, offset, w, writer);

            // Reset the writer (which resets the buffer) for reuse
            w.Reset();
            writer.Clear();

            offset = CopyToBufferWithoutQuotes(buffer, thirdValue, offset, w, writer);

            // Reset the writer (which resets the buffer) for reuse
            w.Reset();
            writer.Clear();

            return CopyToBufferWithTrailingQuote(buffer, fourthValue, offset, w, writer);
        }
        finally
        {
            if (writerPair is not null)
            {
                WriterPool.Return(writerPair);
            }
        }
    }

    /// <summary>
    /// Concatenate five JSON values, as a UTF8 JSON string (including quotes).
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <typeparam name="T3">The type of the third value.</typeparam>
    /// <typeparam name="T4">The type of the fourth value.</typeparam>
    /// <typeparam name="T5">The type of the fifth value.</typeparam>
    /// <param name="buffer">The buffer into which to concatenate the values.</param>
    /// <param name="firstValue">The first value.</param>
    /// <param name="secondValue">The second value.</param>
    /// <param name="thirdValue">The third value.</param>
    /// <param name="fourthValue">The fourth value.</param>
    /// <param name="fifthValue">The fifth value.</param>
    /// <returns>A count of the characters written.</returns>
    public static int ConcatenateAsUtf8JsonString<T1, T2, T3, T4, T5>(Span<byte> buffer, in T1 firstValue, in T2 secondValue, in T3 thirdValue, in T4 fourthValue, in T5 fifthValue)
        where T1 : struct, IJsonValue<T1>
        where T2 : struct, IJsonValue<T2>
        where T3 : struct, IJsonValue<T3>
        where T4 : struct, IJsonValue<T4>
        where T5 : struct, IJsonValue<T5>
    {
        int offset = 0;
        PooledWriter? writerPair = null;
        try
        {
            writerPair = WriterPool.Get();
            (Utf8JsonWriter w, ArrayPoolBufferWriter<byte> writer) = writerPair.Get();

            offset = CopyToBufferWithLeadingQuote(buffer, firstValue, offset, w, writer);

            // Reset the writer (which resets the buffer) for reuse
            w.Reset();
            writer.Clear();

            offset = CopyToBufferWithoutQuotes(buffer, secondValue, offset, w, writer);

            // Reset the writer (which resets the buffer) for reuse
            w.Reset();
            writer.Clear();

            offset = CopyToBufferWithoutQuotes(buffer, thirdValue, offset, w, writer);

            // Reset the writer (which resets the buffer) for reuse
            w.Reset();
            writer.Clear();

            offset = CopyToBufferWithoutQuotes(buffer, fourthValue, offset, w, writer);

            // Reset the writer (which resets the buffer) for reuse
            w.Reset();
            writer.Clear();

            return CopyToBufferWithTrailingQuote(buffer, fifthValue, offset, w, writer);
        }
        finally
        {
            if (writerPair is not null)
            {
                WriterPool.Return(writerPair);
            }
        }
    }

    /// <summary>
    /// Concatenate six JSON values, as a UTF8 JSON string (including quotes).
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <typeparam name="T3">The type of the third value.</typeparam>
    /// <typeparam name="T4">The type of the fourth value.</typeparam>
    /// <typeparam name="T5">The type of the fifth value.</typeparam>
    /// <typeparam name="T6">The type of the sixth value.</typeparam>
    /// <param name="buffer">The buffer into which to concatenate the values.</param>
    /// <param name="firstValue">The first value.</param>
    /// <param name="secondValue">The second value.</param>
    /// <param name="thirdValue">The third value.</param>
    /// <param name="fourthValue">The fourth value.</param>
    /// <param name="fifthValue">The fifth value.</param>
    /// <param name="sixthValue">The sixth value.</param>
    /// <returns>A count of the characters written.</returns>
    public static int ConcatenateAsUtf8JsonString<T1, T2, T3, T4, T5, T6>(Span<byte> buffer, in T1 firstValue, in T2 secondValue, in T3 thirdValue, in T4 fourthValue, in T5 fifthValue, in T6 sixthValue)
        where T1 : struct, IJsonValue<T1>
        where T2 : struct, IJsonValue<T2>
        where T3 : struct, IJsonValue<T3>
        where T4 : struct, IJsonValue<T4>
        where T5 : struct, IJsonValue<T5>
        where T6 : struct, IJsonValue<T6>
    {
        int offset = 0;
        PooledWriter? writerPair = null;
        try
        {
            writerPair = WriterPool.Get();
            (Utf8JsonWriter w, ArrayPoolBufferWriter<byte> writer) = writerPair.Get();

            offset = CopyToBufferWithLeadingQuote(buffer, firstValue, offset, w, writer);

            // Reset the writer (which resets the buffer) for reuse
            w.Reset();
            writer.Clear();

            offset = CopyToBufferWithoutQuotes(buffer, secondValue, offset, w, writer);

            // Reset the writer (which resets the buffer) for reuse
            w.Reset();
            writer.Clear();

            offset = CopyToBufferWithoutQuotes(buffer, thirdValue, offset, w, writer);

            // Reset the writer (which resets the buffer) for reuse
            w.Reset();
            writer.Clear();

            offset = CopyToBufferWithoutQuotes(buffer, fourthValue, offset, w, writer);

            // Reset the writer (which resets the buffer) for reuse
            w.Reset();
            writer.Clear();

            offset = CopyToBufferWithoutQuotes(buffer, fifthValue, offset, w, writer);

            // Reset the writer (which resets the buffer) for reuse
            w.Reset();
            writer.Clear();

            return CopyToBufferWithTrailingQuote(buffer, sixthValue, offset, w, writer);
        }
        finally
        {
            if (writerPair is not null)
            {
                WriterPool.Return(writerPair);
            }
        }
    }

    /// <summary>
    /// Concatenate seven JSON values, as a UTF8 JSON string (including quotes).
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <typeparam name="T3">The type of the third value.</typeparam>
    /// <typeparam name="T4">The type of the fourth value.</typeparam>
    /// <typeparam name="T5">The type of the fifth value.</typeparam>
    /// <typeparam name="T6">The type of the sixth value.</typeparam>
    /// <typeparam name="T7">The type of the seventh value.</typeparam>
    /// <param name="buffer">The buffer into which to concatenate the values.</param>
    /// <param name="firstValue">The first value.</param>
    /// <param name="secondValue">The second value.</param>
    /// <param name="thirdValue">The third value.</param>
    /// <param name="fourthValue">The fourth value.</param>
    /// <param name="fifthValue">The fifth value.</param>
    /// <param name="sixthValue">The sixth value.</param>
    /// <param name="seventhValue">The seventh value.</param>
    /// <returns>A count of the characters written.</returns>
    public static int ConcatenateAsUtf8JsonString<T1, T2, T3, T4, T5, T6, T7>(Span<byte> buffer, in T1 firstValue, in T2 secondValue, in T3 thirdValue, in T4 fourthValue, in T5 fifthValue, in T6 sixthValue, in T7 seventhValue)
        where T1 : struct, IJsonValue<T1>
        where T2 : struct, IJsonValue<T2>
        where T3 : struct, IJsonValue<T3>
        where T4 : struct, IJsonValue<T4>
        where T5 : struct, IJsonValue<T5>
        where T6 : struct, IJsonValue<T6>
        where T7 : struct, IJsonValue<T7>
    {
        int offset = 0;
        PooledWriter? writerPair = null;
        try
        {
            writerPair = WriterPool.Get();
            (Utf8JsonWriter w, ArrayPoolBufferWriter<byte> writer) = writerPair.Get();

            offset = CopyToBufferWithLeadingQuote(buffer, firstValue, offset, w, writer);

            // Reset the writer (which resets the buffer) for reuse
            w.Reset();
            writer.Clear();

            offset = CopyToBufferWithoutQuotes(buffer, secondValue, offset, w, writer);

            // Reset the writer (which resets the buffer) for reuse
            w.Reset();
            writer.Clear();

            offset = CopyToBufferWithoutQuotes(buffer, thirdValue, offset, w, writer);

            // Reset the writer (which resets the buffer) for reuse
            w.Reset();
            writer.Clear();

            offset = CopyToBufferWithoutQuotes(buffer, fourthValue, offset, w, writer);

            // Reset the writer (which resets the buffer) for reuse
            w.Reset();
            writer.Clear();

            offset = CopyToBufferWithoutQuotes(buffer, fifthValue, offset, w, writer);

            // Reset the writer (which resets the buffer) for reuse
            w.Reset();
            writer.Clear();

            offset = CopyToBufferWithoutQuotes(buffer, sixthValue, offset, w, writer);

            // Reset the writer (which resets the buffer) for reuse
            w.Reset();
            writer.Clear();

            return CopyToBufferWithTrailingQuote(buffer, seventhValue, offset, w, writer);
        }
        finally
        {
            if (writerPair is not null)
            {
                WriterPool.Return(writerPair);
            }
        }
    }

    /// <summary>
    /// Concatenate eight JSON values, as a UTF8 JSON string (including quotes).
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <typeparam name="T3">The type of the third value.</typeparam>
    /// <typeparam name="T4">The type of the fourth value.</typeparam>
    /// <typeparam name="T5">The type of the fifth value.</typeparam>
    /// <typeparam name="T6">The type of the sixth value.</typeparam>
    /// <typeparam name="T7">The type of the seventh value.</typeparam>
    /// <typeparam name="T8">The type of the eighth value.</typeparam>
    /// <param name="buffer">The buffer into which to concatenate the values.</param>
    /// <param name="firstValue">The first value.</param>
    /// <param name="secondValue">The second value.</param>
    /// <param name="thirdValue">The third value.</param>
    /// <param name="fourthValue">The fourth value.</param>
    /// <param name="fifthValue">The fifth value.</param>
    /// <param name="sixthValue">The sixth value.</param>
    /// <param name="seventhValue">The seventh value.</param>
    /// <param name="eighthValue">The eighth value.</param>
    /// <returns>A count of the characters written.</returns>
    public static int ConcatenateAsUtf8JsonString<T1, T2, T3, T4, T5, T6, T7, T8>(Span<byte> buffer, in T1 firstValue, in T2 secondValue, in T3 thirdValue, in T4 fourthValue, in T5 fifthValue, in T6 sixthValue, in T7 seventhValue, in T8 eighthValue)
        where T1 : struct, IJsonValue<T1>
        where T2 : struct, IJsonValue<T2>
        where T3 : struct, IJsonValue<T3>
        where T4 : struct, IJsonValue<T4>
        where T5 : struct, IJsonValue<T5>
        where T6 : struct, IJsonValue<T6>
        where T7 : struct, IJsonValue<T7>
        where T8 : struct, IJsonValue<T8>
    {
        int offset = 0;
        PooledWriter? writerPair = null;
        try
        {
            writerPair = WriterPool.Get();
            (Utf8JsonWriter w, ArrayPoolBufferWriter<byte> writer) = writerPair.Get();

            offset = CopyToBufferWithLeadingQuote(buffer, firstValue, offset, w, writer);

            // Reset the writer (which resets the buffer) for reuse
            w.Reset();
            writer.Clear();

            offset = CopyToBufferWithoutQuotes(buffer, secondValue, offset, w, writer);

            // Reset the writer (which resets the buffer) for reuse
            w.Reset();
            writer.Clear();

            offset = CopyToBufferWithoutQuotes(buffer, thirdValue, offset, w, writer);

            // Reset the writer (which resets the buffer) for reuse
            w.Reset();
            writer.Clear();

            offset = CopyToBufferWithoutQuotes(buffer, fourthValue, offset, w, writer);

            // Reset the writer (which resets the buffer) for reuse
            w.Reset();
            writer.Clear();

            offset = CopyToBufferWithoutQuotes(buffer, fifthValue, offset, w, writer);

            // Reset the writer (which resets the buffer) for reuse
            w.Reset();
            writer.Clear();

            offset = CopyToBufferWithoutQuotes(buffer, sixthValue, offset, w, writer);

            // Reset the writer (which resets the buffer) for reuse
            w.Reset();
            writer.Clear();

            offset = CopyToBufferWithoutQuotes(buffer, seventhValue, offset, w, writer);

            // Reset the writer (which resets the buffer) for reuse
            w.Reset();
            writer.Clear();

            return CopyToBufferWithTrailingQuote(buffer, eighthValue, offset, w, writer);
        }
        finally
        {
            if (writerPair is not null)
            {
                WriterPool.Return(writerPair);
            }
        }
    }

    private static int CopyToBufferWithTrailingQuote<T>(Span<byte> buffer, in T value, int offset, Utf8JsonWriter jsonWriter, ArrayPoolBufferWriter<byte> writer)
        where T : struct, IJsonValue<T>
    {
        value.WriteTo(jsonWriter);
        jsonWriter.Flush();

        if (value.ValueKind == JsonValueKind.String)
        {
            // Ignore the open quote, but write the close quote
            writer.WrittenSpan[1..].CopyTo(buffer[offset..]);
            offset += writer.WrittenCount - 1;
        }
        else
        {
            int firstToEscape = JsonWriterHelper.NeedsEscaping(writer.WrittenSpan, null);
            if (firstToEscape >= 0)
            {
                JsonWriterHelper.EscapeString(writer.WrittenSpan, buffer[offset..], firstToEscape, null, out int escapedWritten);
                offset += escapedWritten;
            }
            else
            {
                writer.WrittenSpan.CopyTo(buffer[offset..]);
                offset += writer.WrittenCount;
            }

            buffer[offset++] = 0x22; // '"'
        }

        return offset;
    }

    private static int CopyToBufferWithLeadingQuote<T>(Span<byte> buffer, in T value, int offset, Utf8JsonWriter jsonWriter, ArrayPoolBufferWriter<byte> writer)
        where T : struct, IJsonValue<T>
    {
        value.WriteTo(jsonWriter);
        jsonWriter.Flush();

        if (value.ValueKind == JsonValueKind.String)
        {
            // Don't copy the close quote
            writer.WrittenSpan[..^1].CopyTo(buffer[offset..]);

            // Ignore the closing quote
            offset += writer.WrittenCount - 1;
        }
        else
        {
            buffer[offset++] = 0x22; // '"'

            int firstToEscape = JsonWriterHelper.NeedsEscaping(writer.WrittenSpan, null);
            if (firstToEscape >= 0)
            {
                JsonWriterHelper.EscapeString(writer.WrittenSpan, buffer[offset..], firstToEscape, null, out int escapedWritten);
                offset += escapedWritten;
            }
            else
            {
                // Copy the value in after the open quote
                writer.WrittenSpan.CopyTo(buffer[offset..]);
                offset += writer.WrittenCount;
            }
        }

        return offset;
    }

    private static int CopyToBufferWithoutQuotes<T>(Span<byte> buffer, in T value, int offset, Utf8JsonWriter jsonWriter, ArrayPoolBufferWriter<byte> writer)
        where T : struct, IJsonValue<T>
    {
        value.WriteTo(jsonWriter);
        jsonWriter.Flush();

        if (value.ValueKind == JsonValueKind.String)
        {
            // Ignore the open and close quotes
            writer.WrittenSpan[1..^1].CopyTo(buffer[offset..]);
            offset += writer.WrittenCount - 2;
        }
        else
        {
            int firstToEscape = JsonWriterHelper.NeedsEscaping(writer.WrittenSpan, null);
            if (firstToEscape >= 0)
            {
                JsonWriterHelper.EscapeString(writer.WrittenSpan, buffer[offset..], firstToEscape, null, out int escapedWritten);
                offset += escapedWritten;
            }
            else
            {
                // Copy the value in after the open quote
                writer.WrittenSpan.CopyTo(buffer[offset..]);
                offset += writer.WrittenCount;
            }
        }

        return offset;
    }

    private class PooledWriter : IDisposable
    {
        private static readonly ObjectPool<ArrayPoolBufferWriter<byte>> ArrayPoolWriterPool =
            new DefaultObjectPoolProvider().Create<ArrayPoolBufferWriter<byte>>();

        private static readonly JsonWriterOptions Options = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, Indented = false, SkipValidation = true };

        private Utf8JsonWriter? writer;
        private (Utf8JsonWriter JsonWriter, ArrayPoolBufferWriter<byte> BufferWriter)? value;

        public void Dispose()
        {
            if (this.value.HasValue)
            {
                ArrayPoolWriterPool.Return(this.value.Value.BufferWriter);
                this.value.Value.JsonWriter.Dispose();
                this.value = null;
                this.writer?.Dispose();
                this.writer = null;
            }
        }

        public (Utf8JsonWriter JsonWriter, ArrayPoolBufferWriter<byte> BufferWriter) Get()
        {
            if (this.value.HasValue)
            {
                return this.value.Value;
            }

            ArrayPoolBufferWriter<byte> bufferWriter = ArrayPoolWriterPool.Get();
            bufferWriter.Clear();
            if (this.writer is null)
            {
                this.writer = new(bufferWriter, Options);
            }
            else
            {
                this.writer.Reset(bufferWriter);
            }

            (Utf8JsonWriter Writer, ArrayPoolBufferWriter<byte> BufferWriter) result = (this.writer, bufferWriter);
            this.value = result;
            return result;
        }

        internal void Reset()
        {
            if (this.value.HasValue)
            {
                this.value.Value.BufferWriter.Clear();
                ArrayPoolWriterPool.Return(this.value.Value.BufferWriter);
                this.writer!.Reset();
                this.value = null;
            }
        }
    }

    private class Utf8JsonWriterPooledObjectPolicy : PooledObjectPolicy<PooledWriter>
    {
        public override PooledWriter Create()
        {
            return new PooledWriter();
        }

        public override bool Return(PooledWriter obj)
        {
            obj.Reset();
            return true;
        }
    }
}