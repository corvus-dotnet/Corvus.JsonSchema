// <copyright file="AuditRecord.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// One record of an audit chain (ADR 0069): either the governance audit's record (ADR 0038) or a signed head over the
/// records before it, each with its chain's id, a sequence, a timestamp and the hash of the record before it. The shape
/// is <c>Schemas/AuditRecord.json</c>, which the verifier validates every line against.
/// </summary>
[JsonSchemaTypeGenerator("../Schemas/AuditRecord.json")]
public readonly partial struct AuditRecord
{
    /// <summary>The length of a chain id, in UTF-8 bytes (32 lowercase hex digits).</summary>
    public const int ChainIdLength = 32;

    /// <summary>The length of a record hash, in UTF-8 bytes (64 lowercase hex digits).</summary>
    public const int HashLength = 64;

    /// <summary>The most bytes a head statement takes: the prefix, the chain id, a 19-digit sequence, the hash and two line feeds.</summary>
    public const int MaxHeadStatementLength = 28 + ChainIdLength + 19 + HashLength + 2;

    /// <summary>The first line of the statement a head's signature covers, which keeps the audit's key from signing anything else.</summary>
    public static ReadOnlySpan<byte> HeadStatementPrefix => "corvus-arazzo-audit-head-v1\n"u8;

    /// <summary>Writes one mutation record, in the one property order the chain writer emits.</summary>
    /// <param name="writer">The writer to serialize into.</param>
    /// <param name="chainId">The chain's id, as UTF-8 hex.</param>
    /// <param name="sequence">The record's position in the chain.</param>
    /// <param name="at">When the record is appended.</param>
    /// <param name="previousHash">The hash of the record before it, as UTF-8 hex (64 zeros for a chain's first record).</param>
    /// <param name="continuesChain">The id of the writer's earlier chain, or empty where the record carries no continuation.</param>
    /// <param name="continuesHash">The hash of the last record the writer knows it appended to the earlier chain.</param>
    /// <param name="entry">What happened.</param>
    internal static void WriteMutation(Utf8JsonWriter writer, ReadOnlySpan<byte> chainId, long sequence, DateTimeOffset at, ReadOnlySpan<byte> previousHash, ReadOnlySpan<byte> continuesChain, ReadOnlySpan<byte> continuesHash, in AuditEntry entry)
    {
        writer.WriteStartObject();
        writer.WriteString(MutationRecord.JsonPropertyNames.ChainUtf8, chainId);
        writer.WriteNumber(MutationRecord.JsonPropertyNames.SeqUtf8, sequence);
        writer.WriteString(MutationRecord.JsonPropertyNames.AtUtf8, at);
        writer.WriteString(MutationRecord.JsonPropertyNames.PrevUtf8, previousHash);
        if (!continuesChain.IsEmpty)
        {
            writer.WriteStartObject(MutationRecord.JsonPropertyNames.ContinuesUtf8);
            writer.WriteString(ChainContinuation.JsonPropertyNames.ChainUtf8, continuesChain);
            writer.WriteString(ChainContinuation.JsonPropertyNames.HashUtf8, continuesHash);
            writer.WriteEndObject();
        }

        writer.WriteString(MutationRecord.JsonPropertyNames.KindUtf8, "mutation"u8);
        writer.WriteString(MutationRecord.JsonPropertyNames.ActionUtf8, entry.Action);
        writer.WriteString(MutationRecord.JsonPropertyNames.ActorUtf8, entry.Actor);
        if (entry.Tenant is not null)
        {
            writer.WriteString(MutationRecord.JsonPropertyNames.TenantUtf8, entry.Tenant);
        }

        writer.WriteString(MutationRecord.JsonPropertyNames.TargetKindUtf8, entry.TargetKind);
        writer.WriteString(MutationRecord.JsonPropertyNames.TargetIdUtf8, entry.TargetId);
        writer.WriteString(MutationRecord.JsonPropertyNames.OutcomeUtf8, entry.Outcome);
        if (entry.Environment is not null)
        {
            writer.WriteString(MutationRecord.JsonPropertyNames.EnvironmentUtf8, entry.Environment);
        }

        writer.WriteEndObject();
    }

    /// <summary>Writes one head record.</summary>
    /// <param name="writer">The writer to serialize into.</param>
    /// <param name="chainId">The chain's id, as UTF-8 hex.</param>
    /// <param name="sequence">The head's position in the chain.</param>
    /// <param name="at">When the head is appended.</param>
    /// <param name="previousHash">The hash of the record before the head, as UTF-8 hex: the chain's tail, which the signature vouches for.</param>
    /// <param name="signature">The signature over <see cref="WriteHeadStatement"/>.</param>
    internal static void WriteHead(Utf8JsonWriter writer, ReadOnlySpan<byte> chainId, long sequence, DateTimeOffset at, ReadOnlySpan<byte> previousHash, in Execution.ExecutorPackageSignature signature)
    {
        writer.WriteStartObject();
        writer.WriteString(HeadRecord.JsonPropertyNames.ChainUtf8, chainId);
        writer.WriteNumber(HeadRecord.JsonPropertyNames.SeqUtf8, sequence);
        writer.WriteString(HeadRecord.JsonPropertyNames.AtUtf8, at);
        writer.WriteString(HeadRecord.JsonPropertyNames.PrevUtf8, previousHash);
        writer.WriteString(HeadRecord.JsonPropertyNames.KindUtf8, "head"u8);
        writer.WriteStartObject(HeadRecord.JsonPropertyNames.SignatureUtf8);
        writer.WriteString(HeadSignature.JsonPropertyNames.AlgorithmUtf8, signature.Algorithm);
        writer.WriteString(HeadSignature.JsonPropertyNames.KeyIdUtf8, signature.KeyId);
        writer.WriteBase64String(HeadSignature.JsonPropertyNames.ValueUtf8, signature.Value.Span);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    /// <summary>
    /// Writes the statement a head's signature covers: the prefix, then the chain's id, the head's sequence and the
    /// head's previous-hash, one to a line. The three are what place the head in its chain, so a head cannot be moved
    /// to another chain or another position, and the previous-hash vouches for every record before it.
    /// </summary>
    /// <param name="destination">The buffer to write into, at least <see cref="MaxHeadStatementLength"/> bytes.</param>
    /// <param name="chainId">The chain's id, as UTF-8 hex.</param>
    /// <param name="sequence">The head's position in the chain.</param>
    /// <param name="previousHash">The head's previous-hash, as UTF-8 hex.</param>
    /// <returns>The number of bytes written.</returns>
    internal static int WriteHeadStatement(Span<byte> destination, ReadOnlySpan<byte> chainId, long sequence, ReadOnlySpan<byte> previousHash)
    {
        int written = 0;
        HeadStatementPrefix.CopyTo(destination);
        written += HeadStatementPrefix.Length;
        chainId.CopyTo(destination[written..]);
        written += chainId.Length;
        destination[written++] = (byte)'\n';
        System.Buffers.Text.Utf8Formatter.TryFormat(sequence, destination[written..], out int digits);
        written += digits;
        destination[written++] = (byte)'\n';
        previousHash.CopyTo(destination[written..]);
        return written + previousHash.Length;
    }
}