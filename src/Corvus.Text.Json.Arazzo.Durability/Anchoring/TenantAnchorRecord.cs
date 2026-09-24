// <copyright file="TenantAnchorRecord.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Anchoring;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The persisted form of an <see cref="AnchorRecord"/> (ADR 0065, the normative tenant-anchor specification).
/// Generated from <c>Schemas/TenantAnchorRecord.json</c>; every durable <see cref="ITenantAnchorStore"/> stores
/// exactly these bytes, so the record a Postgres tenant reads is the record any other backend would. It sits in
/// the root namespace with the other generated persisted forms, whose shared value types are emitted there.
/// </summary>
[JsonSchemaTypeGenerator("../Schemas/TenantAnchorRecord.json")]
public readonly partial struct TenantAnchorRecord
{
    /// <summary>Serializes an anchor record to the owned UTF-8 bytes a store persists (pooled scratch, no detached clone).</summary>
    /// <param name="record">The record.</param>
    /// <returns>The UTF-8 JSON bytes.</returns>
    public static byte[] Serialize(in AnchorRecord record)
        => PersistedJson.ToArray(record, static (Utf8JsonWriter writer, in AnchorRecord r) => Write(writer, r));

    /// <summary>Reads an anchor record back from its persisted bytes.</summary>
    /// <param name="document">The stored UTF-8 JSON bytes.</param>
    /// <returns>The record.</returns>
    /// <exception cref="FormatException">The bytes are not a well-formed anchor record.</exception>
    public static AnchorRecord Deserialize(ReadOnlySpan<byte> document)
    {
        using ParsedJsonDocument<TenantAnchorRecord> parsed = PersistedJson.ToPooledDocument<TenantAnchorRecord>(document);
        return parsed.RootElement.ToAnchorRecord();
    }

    /// <summary>Writes an anchor record in the persisted property order.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="record">The record.</param>
    public static void Write(Utf8JsonWriter writer, in AnchorRecord record)
    {
        writer.WriteStartObject();
        writer.WriteString(JsonPropertyNames.RunIdUtf8, record.RunId);
        writer.WriteString(JsonPropertyNames.EnvironmentIdUtf8, record.EnvironmentId);
        writer.WriteString(JsonPropertyNames.StateUtf8, record.State == AnchorState.Terminal ? "Terminal"u8 : "Live"u8);
        writer.WriteString(JsonPropertyNames.DispositionUtf8, record.Disposition switch
        {
            AnchorDisposition.Completed => "Completed"u8,
            AnchorDisposition.Abandoned => "Abandoned"u8,
            _ => "None"u8,
        });
        WriteKey(writer, JsonPropertyNames.EpochHighWaterUtf8, record.EpochHighWater);
        WriteMark(writer, JsonPropertyNames.CommittedUtf8, record.Committed);
        if (record.Pending is { } pending)
        {
            WriteMark(writer, JsonPropertyNames.PendingUtf8, pending);
        }

        writer.WriteNumber(JsonPropertyNames.ReanchorCounterUtf8, record.ReanchorCounter);
        writer.WriteEndObject();
    }

    /// <summary>Converts this persisted record to the domain value the pure functions take.</summary>
    /// <returns>The record.</returns>
    /// <exception cref="FormatException">The persisted record is malformed.</exception>
    public AnchorRecord ToAnchorRecord()
    {
        if (this.IsUndefined()
            || this.RunId.IsUndefined()
            || this.EnvironmentId.IsUndefined()
            || this.State.IsUndefined()
            || this.Disposition.IsUndefined()
            || this.EpochHighWater.IsUndefined()
            || this.Committed.IsUndefined()
            || this.ReanchorCounter.IsUndefined())
        {
            throw new FormatException("The stored bytes are not a well-formed tenant anchor record.");
        }

        AnchorState state = this.State.ValueEquals("Terminal"u8) ? AnchorState.Terminal : AnchorState.Live;
        AnchorDisposition disposition = this.Disposition.ValueEquals("Completed"u8) ? AnchorDisposition.Completed
            : this.Disposition.ValueEquals("Abandoned"u8) ? AnchorDisposition.Abandoned
            : AnchorDisposition.None;

        return new AnchorRecord(
            (string)this.RunId,
            (string)this.EnvironmentId,
            state,
            ReadKey(this.EpochHighWater),
            ReadMark(this.Committed),
            this.Pending.IsNotUndefined() ? ReadMark(this.Pending) : null,
            (ulong)this.ReanchorCounter,
            disposition);
    }

    private static void WriteKey(Utf8JsonWriter writer, ReadOnlySpan<byte> name, in AnchorOrderingKey key)
    {
        writer.WriteStartObject(name);
        writer.WriteNumber(OrderingKey.JsonPropertyNames.IncarnationUtf8, key.Incarnation);
        writer.WriteNumber(OrderingKey.JsonPropertyNames.EpochUtf8, key.Epoch);
        writer.WriteEndObject();
    }

    private static void WriteMark(Utf8JsonWriter writer, ReadOnlySpan<byte> name, in AnchorMark mark)
    {
        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        mark.Digest.CopyTo(digest);
        writer.WriteStartObject(name);
        WriteKey(writer, Mark.JsonPropertyNames.KeyUtf8, mark.Key);
        writer.WriteNumber(Mark.JsonPropertyNames.SequenceUtf8, mark.Sequence);
        writer.WriteBase64String(Mark.JsonPropertyNames.DigestUtf8, digest);
        writer.WriteEndObject();
    }

    private static AnchorOrderingKey ReadKey(in OrderingKey key)
        => new((ulong)key.Incarnation, (ulong)key.Epoch);

    private static AnchorMark ReadMark(in Mark mark)
    {
        if (!((JsonElement)mark.Digest).TryGetBytesFromBase64(out byte[]? digest) || digest.Length != SHA256.HashSizeInBytes)
        {
            throw new FormatException("A stored anchor mark carries a digest that is not 32 base64 bytes.");
        }

        return new AnchorMark(ReadKey(mark.Key), (ulong)mark.Sequence, new AnchorDigest(digest));
    }
}