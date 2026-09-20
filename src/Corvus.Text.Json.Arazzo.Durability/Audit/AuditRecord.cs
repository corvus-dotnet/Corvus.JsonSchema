// <copyright file="AuditRecord.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// One record of an audit chain (ADR 0069): the governance audit's record (ADR 0038) with a timestamp, a sequence and the
/// hash of the record before it. The shape is <c>Schemas/AuditRecord.json</c>, which the verifier validates every line
/// against.
/// </summary>
[JsonSchemaTypeGenerator("../Schemas/AuditRecord.json")]
public readonly partial struct AuditRecord
{
    /// <summary>The length of a chain id, in UTF-8 bytes (32 lowercase hex digits).</summary>
    public const int ChainIdLength = 32;

    /// <summary>The length of a record hash, in UTF-8 bytes (64 lowercase hex digits).</summary>
    public const int HashLength = 64;

    /// <summary>Writes one record, in the one property order the chain writer emits.</summary>
    /// <param name="writer">The writer to serialize into.</param>
    /// <param name="chainId">The chain's id, as UTF-8 hex.</param>
    /// <param name="sequence">The record's position in the chain.</param>
    /// <param name="at">When the record is appended.</param>
    /// <param name="previousHash">The hash of the record before it, as UTF-8 hex (64 zeros for a chain's first record).</param>
    /// <param name="continuesChain">The id of the writer's earlier chain, or empty where the record carries no continuation.</param>
    /// <param name="continuesHash">The hash of the last record the writer knows it appended to the earlier chain.</param>
    /// <param name="entry">What happened.</param>
    internal static void Write(Utf8JsonWriter writer, ReadOnlySpan<byte> chainId, long sequence, DateTimeOffset at, ReadOnlySpan<byte> previousHash, ReadOnlySpan<byte> continuesChain, ReadOnlySpan<byte> continuesHash, in AuditEntry entry)
    {
        writer.WriteStartObject();
        writer.WriteString(JsonPropertyNames.ChainUtf8, chainId);
        writer.WriteNumber(JsonPropertyNames.SeqUtf8, sequence);
        writer.WriteString(JsonPropertyNames.AtUtf8, at);
        writer.WriteString(JsonPropertyNames.PrevUtf8, previousHash);
        if (!continuesChain.IsEmpty)
        {
            writer.WriteStartObject(JsonPropertyNames.ContinuesUtf8);
            writer.WriteString(ChainContinuation.JsonPropertyNames.ChainUtf8, continuesChain);
            writer.WriteString(ChainContinuation.JsonPropertyNames.HashUtf8, continuesHash);
            writer.WriteEndObject();
        }

        writer.WriteString(JsonPropertyNames.KindUtf8, "mutation"u8);
        writer.WriteString(JsonPropertyNames.ActionUtf8, entry.Action);
        writer.WriteString(JsonPropertyNames.ActorUtf8, entry.Actor);
        if (entry.Tenant is not null)
        {
            writer.WriteString(JsonPropertyNames.TenantUtf8, entry.Tenant);
        }

        writer.WriteString(JsonPropertyNames.TargetKindUtf8, entry.TargetKind);
        writer.WriteString(JsonPropertyNames.TargetIdUtf8, entry.TargetId);
        writer.WriteString(JsonPropertyNames.OutcomeUtf8, entry.Outcome);
        if (entry.Environment is not null)
        {
            writer.WriteString(JsonPropertyNames.EnvironmentUtf8, entry.Environment);
        }

        writer.WriteEndObject();
    }
}