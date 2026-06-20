// <copyright file="PersistedJson.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Pooled serialization helpers for the durability stores. Every method rents its <see cref="Utf8JsonWriter"/> and
/// backing buffer from the workspace pool (<see cref="JsonWorkspace.RentWriterAndBuffer(int, out IByteBufferWriter)"/>)
/// and returns them — so the scratch buffer is never a GC allocation. The only allocation is the one a backend driver
/// genuinely requires (an owned <see cref="byte"/> array), or none at all where the destination accepts the written
/// span directly. The write callback takes its state by <see langword="in"/> context so callers can pass a
/// <see langword="static"/> lambda and avoid a closure allocation.
/// </summary>
public static class PersistedJson
{
    private const int DefaultBufferSize = 512;

    /// <summary>A callback that writes JSON into a rented writer, taking its state by <see langword="in"/> context.</summary>
    /// <typeparam name="TContext">The state type (may be a <see langword="ref"/> struct, so the state can carry <see cref="ReadOnlySpan{T}"/>s).</typeparam>
    /// <param name="writer">The rented writer to write into.</param>
    /// <param name="context">The caller's state.</param>
    public delegate void WriteCallback<TContext>(Utf8JsonWriter writer, in TContext context)
        where TContext : allows ref struct;

    /// <summary>
    /// Serializes JSON into a pooled buffer and copies it into a single owned <see cref="byte"/> array — the one
    /// allocation, being the form most persistence drivers require. The scratch writer and buffer are pooled.
    /// </summary>
    /// <typeparam name="TContext">The write-callback state type.</typeparam>
    /// <param name="context">The state passed to <paramref name="write"/>.</param>
    /// <param name="write">Writes the JSON (pass a <see langword="static"/> lambda to avoid a closure).</param>
    /// <returns>The owned UTF-8 JSON bytes.</returns>
    public static byte[] ToArray<TContext>(in TContext context, WriteCallback<TContext> write)
        where TContext : allows ref struct
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Utf8JsonWriter writer = workspace.RentWriterAndBuffer(DefaultBufferSize, out IByteBufferWriter buffer);
        try
        {
            write(writer, in context);
            writer.Flush();
            return buffer.WrittenSpan.ToArray();
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(writer, buffer);
        }
    }

    /// <summary>
    /// Serializes JSON into a pooled buffer and writes it as a base64 string property on <paramref name="destination"/> —
    /// straight from the span, with no intermediate base64 string or owned byte array. The scratch is pooled.
    /// </summary>
    /// <typeparam name="TContext">The write-callback state type.</typeparam>
    /// <param name="destination">The writer receiving the base64 property.</param>
    /// <param name="propertyName">The UTF-8 property name.</param>
    /// <param name="context">The state passed to <paramref name="write"/>.</param>
    /// <param name="write">Writes the JSON to be base64-encoded (pass a <see langword="static"/> lambda).</param>
    public static void WriteBase64<TContext>(Utf8JsonWriter destination, ReadOnlySpan<byte> propertyName, in TContext context, WriteCallback<TContext> write)
        where TContext : allows ref struct
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Utf8JsonWriter writer = workspace.RentWriterAndBuffer(DefaultBufferSize, out IByteBufferWriter buffer);
        try
        {
            write(writer, in context);
            writer.Flush();
            destination.WriteBase64String(propertyName, buffer.WrittenSpan);
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(writer, buffer);
        }
    }

    /// <summary>
    /// Serializes JSON into a pooled scratch buffer and materializes it as a disposable, pooled document — the
    /// callback-driven counterpart of <see cref="ToPooledDocument{T}(ReadOnlySpan{byte})"/> with no intermediate owned
    /// <see cref="byte"/> array. The scratch writer/buffer and the document's backing buffer are all pooled; the only
    /// allocation is the small document wrapper, returned to the pool on <see cref="IDisposable.Dispose"/>. Use this to
    /// build a draft document from in-memory values (e.g. a programmatic store caller) that the caller disposes.
    /// </summary>
    /// <typeparam name="T">The Corvus.Text.Json document type.</typeparam>
    /// <typeparam name="TContext">The write-callback state type.</typeparam>
    /// <param name="context">The state passed to <paramref name="write"/>.</param>
    /// <param name="write">Writes the JSON (pass a <see langword="static"/> lambda to avoid a closure).</param>
    /// <returns>The pooled, disposable document.</returns>
    public static ParsedJsonDocument<T> ToPooledDocument<T, TContext>(in TContext context, WriteCallback<TContext> write)
        where T : struct, IJsonElement<T>
        where TContext : allows ref struct
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Utf8JsonWriter writer = workspace.RentWriterAndBuffer(DefaultBufferSize, out IByteBufferWriter buffer);
        try
        {
            write(writer, in context);
            writer.Flush();
            return ToPooledDocument<T>(buffer.WrittenSpan);
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(writer, buffer);
        }
    }

    /// <summary>
    /// Materializes a disposable document over an <see cref="ArrayPool{T}"/>-rented copy of <paramref name="utf8"/> —
    /// the only allocation is the small document wrapper; its backing buffer and metadata return to the pool on
    /// <see cref="IDisposable.Dispose"/>. The caller owns the returned document's lifetime: dispose it when done, or
    /// <see cref="JsonDocument"/>-clone the value out first if it must outlive the dispose.
    /// </summary>
    /// <typeparam name="T">The Corvus.Text.Json document type.</typeparam>
    /// <param name="utf8">The UTF-8 JSON to copy into the pooled document.</param>
    /// <returns>The pooled, disposable document.</returns>
    public static ParsedJsonDocument<T> ToPooledDocument<T>(ReadOnlySpan<byte> utf8)
        where T : struct, IJsonElement<T>
    {
        byte[] rented = ArrayPool<byte>.Shared.Rent(utf8.Length);
        try
        {
            utf8.CopyTo(rented);
            return ParsedJsonDocument<T>.Parse(rented.AsMemory(0, utf8.Length), rented);
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(rented);
            throw;
        }
    }
}