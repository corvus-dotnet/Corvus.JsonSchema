// <copyright file="RunnerDocument.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;

namespace Corvus.Text.Json.Arazzo.Durability.Cosmos;

/// <summary>
/// The Azure Cosmos DB document shape for a runner registration, generated from <c>Schemas/RunnerDocument.json</c>.
/// It carries the canonical <see cref="RunnerRegistration"/> JSON (base64) plus a queryable last-seen timestamp and
/// a projected hosted-version list for the hosting index. The write path streams the envelope straight to the Cosmos
/// request (<see cref="WriteEnvelopeStream(string, RunnerRegistration)"/> / the heartbeat overload), base64-encoding
/// the registration from a pooled buffer — it never materializes a <see cref="RunnerDocument"/> instance or copies
/// the document out to a <see cref="byte"/> array. Reads go through <see cref="FromJson"/> / <see cref="ToRegistration"/>.
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

    /// <summary>Parses a runner document from its persisted JSON as a detached value (one owned copy).</summary>
    /// <param name="utf8">The UTF-8 JSON document.</param>
    /// <returns>The document.</returns>
    public static RunnerDocument FromJson(ReadOnlyMemory<byte> utf8) => ParseValue(utf8.Span);

    /// <summary>Streams the Cosmos envelope for a fresh registration straight to a stream (no materialized document).</summary>
    /// <param name="runnerId">The runner id.</param>
    /// <param name="registration">The registration to embed.</param>
    /// <returns>A readable stream over the envelope JSON, positioned at the start.</returns>
    public static MemoryStream WriteEnvelopeStream(string runnerId, RunnerRegistration registration)
        => BuildEnvelope(runnerId, registration, registration.LastSeenAtValue, advanceLastSeen: false);

    /// <summary>Streams the Cosmos envelope for a heartbeated registration (its last-seen advanced to <paramref name="at"/>).</summary>
    /// <param name="runnerId">The runner id.</param>
    /// <param name="registration">The existing registration to heartbeat.</param>
    /// <param name="at">The instant of the heartbeat.</param>
    /// <returns>A readable stream over the envelope JSON, positioned at the start.</returns>
    public static MemoryStream WriteEnvelopeStream(string runnerId, RunnerRegistration registration, DateTimeOffset at)
        => BuildEnvelope(runnerId, registration, at, advanceLastSeen: true);

    private static MemoryStream BuildEnvelope(string runnerId, RunnerRegistration registration, DateTimeOffset lastSeenAt, bool advanceLastSeen)
    {
        var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteString(JsonPropertyNames.IdUtf8, runnerId);
            writer.WriteNumber(JsonPropertyNames.LastSeenAtUtf8, lastSeenAt.ToUnixTimeMilliseconds());

            // Base64-encode the registration's JSON straight from a pooled buffer (no interim base64 string / byte[]).
            if (advanceLastSeen)
            {
                PersistedJson.WriteBase64(
                    writer,
                    JsonPropertyNames.DocUtf8,
                    (registration, lastSeenAt),
                    static (Utf8JsonWriter docWriter, in (RunnerRegistration Registration, DateTimeOffset At) ctx) => ctx.Registration.WriteWithLastSeenAt(docWriter, ctx.At));
            }
            else
            {
                PersistedJson.WriteBase64(
                    writer,
                    JsonPropertyNames.DocUtf8,
                    registration,
                    static (Utf8JsonWriter docWriter, in RunnerRegistration r) => r.WriteTo(docWriter));
            }

            writer.WriteStartArray(JsonPropertyNames.LoadedVersionsUtf8);
            foreach (RunnerRegistration.RunnerHostedVersion hosted in registration.HostedVersions.EnumerateArray())
            {
                if ((bool)hosted.Loaded)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName(HostedKey.JsonPropertyNames.BaseWorkflowIdUtf8);
                    hosted.BaseWorkflowId.WriteTo(writer);
                    writer.WriteNumber(HostedKey.JsonPropertyNames.VersionNumberUtf8, hosted.VersionNumber);
                    writer.WriteEndObject();
                }
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        stream.Position = 0;
        return stream;
    }
}