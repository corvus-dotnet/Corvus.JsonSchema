// <copyright file="PersistedJson.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

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
    /// <typeparam name="TContext">The state type.</typeparam>
    /// <param name="writer">The rented writer to write into.</param>
    /// <param name="context">The caller's state.</param>
    public delegate void WriteCallback<TContext>(Utf8JsonWriter writer, in TContext context);

    /// <summary>
    /// Serializes JSON into a pooled buffer and copies it into a single owned <see cref="byte"/> array — the one
    /// allocation, being the form most persistence drivers require. The scratch writer and buffer are pooled.
    /// </summary>
    /// <typeparam name="TContext">The write-callback state type.</typeparam>
    /// <param name="context">The state passed to <paramref name="write"/>.</param>
    /// <param name="write">Writes the JSON (pass a <see langword="static"/> lambda to avoid a closure).</param>
    /// <returns>The owned UTF-8 JSON bytes.</returns>
    public static byte[] ToArray<TContext>(in TContext context, WriteCallback<TContext> write)
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
}