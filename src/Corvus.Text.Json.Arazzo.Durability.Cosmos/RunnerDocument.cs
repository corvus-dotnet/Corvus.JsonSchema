// <copyright file="RunnerDocument.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Cosmos;

/// <summary>
/// The Azure Cosmos DB document shape for a runner registration, generated from <c>Schemas/RunnerDocument.json</c>.
/// It carries the canonical <see cref="RunnerRegistration"/> JSON (base64) plus a queryable last-seen timestamp and
/// a projected hosted-version list for the hosting index. Round-tripped through Corvus.Text.Json — never a
/// reflection serializer.
/// </summary>
[JsonSchemaTypeGenerator("Schemas/RunnerDocument.json")]
public readonly partial struct RunnerDocument
{
    private static readonly JsonWriterOptions WriterOptions = new() { Indented = false, SkipValidation = true };

    /// <summary>Gets the runner id.</summary>
    public string IdValue => (string)this.Id;

    /// <summary>Decodes the canonical registration JSON bytes.</summary>
    /// <returns>The registration JSON bytes.</returns>
    public byte[] DocBytes() => Convert.FromBase64String((string)this.Doc);

    /// <summary>Reconstructs the registration carried by this document.</summary>
    /// <returns>The runner registration.</returns>
    public RunnerRegistration ToRegistration() => RunnerRegistration.FromJson(this.DocBytes());

    /// <summary>Builds a runner document from a registration, detached and ready to persist.</summary>
    /// <param name="runnerId">The runner id.</param>
    /// <param name="registration">The registration.</param>
    /// <returns>The document.</returns>
    public static RunnerDocument From(string runnerId, RunnerRegistration registration)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteString(JsonPropertyNames.IdUtf8, runnerId);
            writer.WriteNumber(JsonPropertyNames.LastSeenAtUtf8, registration.LastSeenAtValue.ToUnixTimeMilliseconds());

            // Base64-encode the registration's JSON straight from a buffer into the doc field — no interim base64 string.
            var registrationBuffer = new ArrayBufferWriter<byte>();
            registration.WriteTo(registrationBuffer);
            writer.WriteBase64String(JsonPropertyNames.DocUtf8, registrationBuffer.WrittenSpan);
            writer.WriteStartArray(JsonPropertyNames.LoadedVersionsUtf8);
            foreach ((string baseWorkflowId, int versionNumber) in registration.LoadedHostedVersions())
            {
                writer.WriteStartObject();
                writer.WriteString(HostedKey.JsonPropertyNames.BaseWorkflowIdUtf8, baseWorkflowId);
                writer.WriteNumber(HostedKey.JsonPropertyNames.VersionNumberUtf8, versionNumber);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        using ParsedJsonDocument<RunnerDocument> doc = ParsedJsonDocument<RunnerDocument>.Parse(buffer.WrittenMemory);
        return doc.RootElement.Clone();
    }

    /// <summary>Parses a runner document from its persisted JSON, detached from the parse buffer.</summary>
    /// <param name="utf8">The UTF-8 JSON document.</param>
    /// <returns>The document.</returns>
    public static RunnerDocument FromJson(ReadOnlyMemory<byte> utf8)
    {
        using ParsedJsonDocument<RunnerDocument> doc = ParsedJsonDocument<RunnerDocument>.Parse(utf8);
        return doc.RootElement.Clone();
    }

    /// <summary>Serializes this runner document to its persisted JSON form.</summary>
    /// <returns>The UTF-8 JSON document.</returns>
    public byte[] ToJsonBytes()
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            this.WriteTo(writer);
        }

        return buffer.WrittenSpan.ToArray();
    }
}