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
/// Utilies for low allocation access to JSON values.
/// </summary>
/// <remarks>
/// These adapters give access to the underlying UTF8 bytes of JSON string
/// values, unless/until https://github.com/dotnet/runtime/issues/74028 lands.
/// </remarks>
public static class LowAllocJsonUtils
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