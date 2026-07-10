// <copyright file="KycJson.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.Arazzo.Samples.Kyc;

/// <summary>
/// Pooled UTF-8 JSON composition helpers for the KYC service, following the durability-layer best practice
/// (<c>PersistedJson</c>): the scratch <see cref="Utf8JsonWriter"/> and its backing buffer are rented from a workspace
/// pool and returned — never a fresh <see cref="ArrayBufferWriter{T}"/> per call — and a composed response document is
/// materialised through the ownership model (the document adopts an <see cref="ArrayPool{T}"/>-rented buffer) rather
/// than being serialized to a <see cref="byte"/> array and re-parsed. The write callback takes its state by
/// <see langword="in"/> context so callers pass a <see langword="static"/> lambda and avoid a closure allocation.
/// </summary>
internal static class KycJson
{
    private const int DefaultBufferSize = 512;

    /// <summary>A callback that writes JSON into a rented writer, taking its state by <see langword="in"/> context.</summary>
    /// <typeparam name="TContext">The state type (may be a <see langword="ref"/> struct).</typeparam>
    /// <param name="writer">The rented writer to write into.</param>
    /// <param name="context">The caller's state.</param>
    internal delegate void WriteCallback<TContext>(Utf8JsonWriter writer, in TContext context)
        where TContext : allows ref struct;

    /// <summary>A stable, process-independent hash of a string, so demo-derived values are reproducible across restarts.</summary>
    /// <param name="value">The value to hash.</param>
    /// <returns>A 32-bit FNV-1a hash.</returns>
    internal static uint StableHash(string value)
    {
        uint hash = 2166136261u;
        foreach (char c in value)
        {
            hash = (hash ^ c) * 16777619u;
        }

        return hash;
    }

    /// <summary>
    /// Composes JSON into a pooled scratch buffer and materialises it as a pooled, owned document via the ownership
    /// model (the document adopts an <see cref="ArrayPool{T}"/>-rented copy — no fresh writer/buffer, no re-parse). The
    /// caller hands the result to <see cref="JsonWorkspace.TakeOwnership"/> so the response workspace disposes it.
    /// </summary>
    /// <typeparam name="T">The generated model type the body is validated as.</typeparam>
    /// <typeparam name="TContext">The write-callback state type.</typeparam>
    /// <param name="context">The state passed to <paramref name="write"/>.</param>
    /// <param name="write">Writes the JSON (pass a <see langword="static"/> lambda to avoid a closure).</param>
    /// <returns>The pooled, owned document.</returns>
    internal static ParsedJsonDocument<T> ToPooledDocument<T, TContext>(in TContext context, WriteCallback<TContext> write)
        where T : struct, IJsonElement<T>
        where TContext : allows ref struct
    {
        using JsonWorkspace scratch = JsonWorkspace.Create();
        Utf8JsonWriter writer = scratch.RentWriterAndBuffer(DefaultBufferSize, out IByteBufferWriter buffer);
        try
        {
            write(writer, in context);
            writer.Flush();
            ReadOnlySpan<byte> written = buffer.WrittenSpan;
            byte[] rented = ArrayPool<byte>.Shared.Rent(written.Length);
            try
            {
                written.CopyTo(rented);
                return ParsedJsonDocument<T>.Parse(rented.AsMemory(0, written.Length), rented);
            }
            catch
            {
                ArrayPool<byte>.Shared.Return(rented);
                throw;
            }
        }
        finally
        {
            scratch.ReturnWriterAndBuffer(writer, buffer);
        }
    }

    /// <summary>
    /// Composes JSON into a pooled scratch buffer and copies it into a single owned <see cref="byte"/> array — the one
    /// allocation, for a store column that requires a <see cref="byte"/> array. The scratch writer/buffer are pooled.
    /// </summary>
    /// <typeparam name="TContext">The write-callback state type.</typeparam>
    /// <param name="context">The state passed to <paramref name="write"/>.</param>
    /// <param name="write">Writes the JSON (pass a <see langword="static"/> lambda to avoid a closure).</param>
    /// <returns>The owned UTF-8 JSON bytes.</returns>
    internal static byte[] ToArray<TContext>(in TContext context, WriteCallback<TContext> write)
        where TContext : allows ref struct
    {
        using JsonWorkspace scratch = JsonWorkspace.Create();
        Utf8JsonWriter writer = scratch.RentWriterAndBuffer(DefaultBufferSize, out IByteBufferWriter buffer);
        try
        {
            write(writer, in context);
            writer.Flush();
            return buffer.WrittenSpan.ToArray();
        }
        finally
        {
            scratch.ReturnWriterAndBuffer(writer, buffer);
        }
    }

    /// <summary>Writes a stored JSON document (or nothing, if absent) as a named property, splicing its raw bytes.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="name">The property name.</param>
    /// <param name="document">The stored document bytes, or <see langword="null"/> to omit the property.</param>
    internal static void WriteDocumentProperty(Utf8JsonWriter writer, string name, byte[]? document)
    {
        if (document is not null)
        {
            writer.WritePropertyName(name);
            writer.WriteRawValue(document, skipInputValidation: true);
        }
    }
}
