// <copyright file="LedgerJson.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;

namespace Corvus.Text.Json.Arazzo.Samples.Ledger;

/// <summary>
/// Small helpers for composing the ledger service's UTF-8 JSON wire documents at the leaf. The composed bytes are
/// always parsed back into the generated, schema-validated model before they leave the service (and the generated
/// endpoint middleware re-validates every response body), so the wire contract stays type-checked end to end.
/// </summary>
internal static class LedgerJson
{
    /// <summary>A callback that writes a JSON value to a pooled writer.</summary>
    /// <param name="writer">The writer to write to.</param>
    internal delegate void WriteValue(Utf8JsonWriter writer);

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

    /// <summary>Serializes a JSON value to a fresh byte array via a pooled writer.</summary>
    /// <param name="write">The callback that writes the value.</param>
    /// <returns>The UTF-8 JSON bytes.</returns>
    internal static byte[] Serialize(WriteValue write)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            write(writer);
            writer.Flush();
        }

        return buffer.WrittenSpan.ToArray();
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
