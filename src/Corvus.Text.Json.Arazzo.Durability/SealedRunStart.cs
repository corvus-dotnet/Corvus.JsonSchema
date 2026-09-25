// <copyright file="SealedRunStart.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The wire form of a sealed start (ADR 0065 decision 9): what an initiator submits and the control plane's sealed
/// start endpoint takes. Generated from <c>Schemas/SealedRunStart.json</c>. It sits in the root namespace with the
/// other generated persisted forms, whose shared value types are emitted there.
/// </summary>
[JsonSchemaTypeGenerator("Schemas/SealedRunStart.json")]
public readonly partial struct SealedRunStart
{
    /// <summary>Writes a sealed start as the initiator submits it.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="runId">The run id the initiator chose.</param>
    /// <param name="sealedInputs">The sealed inputs.</param>
    public static void Write(Utf8JsonWriter writer, string runId, in SealedInputs sealedInputs)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStartObject();
        writer.WriteString(JsonPropertyNames.RunIdUtf8, runId);
        writer.WriteString(JsonPropertyNames.KeyIdUtf8, sealedInputs.KeyId);
        writer.WriteBase64String(JsonPropertyNames.EncUtf8, sealedInputs.Enc.Span);
        writer.WriteBase64String(JsonPropertyNames.CiphertextUtf8, sealedInputs.Ciphertext.Span);
        writer.WriteBase64String(JsonPropertyNames.SignatureUtf8, sealedInputs.Signature.Span);
        writer.WriteEndObject();
    }

    /// <summary>Serializes a sealed start to owned UTF-8 bytes.</summary>
    /// <param name="runId">The run id the initiator chose.</param>
    /// <param name="sealedInputs">The sealed inputs.</param>
    /// <returns>The UTF-8 JSON bytes.</returns>
    public static byte[] Serialize(string runId, in SealedInputs sealedInputs)
    {
        (string RunId, SealedInputs Sealed) start = (runId, sealedInputs);
        return PersistedJson.ToArray(start, static (Utf8JsonWriter writer, in (string RunId, SealedInputs Sealed) s) => Write(writer, s.RunId, s.Sealed));
    }

    /// <summary>Reads the sealed inputs this start carries, as owned bytes.</summary>
    /// <returns>The sealed inputs.</returns>
    /// <exception cref="FormatException">The start is malformed.</exception>
    public SealedInputs ToSealedInputs()
    {
        if (this.IsUndefined() || this.RunId.IsUndefined() || this.KeyId.IsUndefined() || this.Enc.IsUndefined() || this.Ciphertext.IsUndefined() || this.Signature.IsUndefined())
        {
            throw new FormatException("The sealed start is missing a required member.");
        }

        var sealedInputs = new SealedInputs(
            (string)this.KeyId,
            ((JsonElement)this.Enc).GetBytesFromBase64(),
            ((JsonElement)this.Ciphertext).GetBytesFromBase64(),
            ((JsonElement)this.Signature).GetBytesFromBase64());
        if (!sealedInputs.IsWellFormed)
        {
            throw new FormatException("The sealed start's parts do not have the shapes a seal produces.");
        }

        return sealedInputs;
    }
}