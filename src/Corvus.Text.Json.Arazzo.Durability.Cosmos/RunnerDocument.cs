// <copyright file="RunnerDocument.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;

namespace Corvus.Text.Json.Arazzo.Durability.Cosmos;

/// <summary>
/// The Azure Cosmos DB document shape for a runner registration, generated from <c>Schemas/RunnerDocument.json</c>.
/// It carries the canonical <see cref="RunnerRegistration"/> JSON as a nested <c>doc</c> object (embedded verbatim,
/// not base64) plus a queryable last-seen timestamp and a projected hosted-version list for the hosting index. The
/// write path streams the envelope straight to the Cosmos request
/// (<see cref="WriteEnvelopeStream(string, RunnerRegistration)"/> on register), writing the registration's bytes
/// verbatim as a raw nested value from a pooled buffer — it never materializes a <see cref="RunnerDocument"/>
/// instance or copies the document out to a <see cref="byte"/> array. A heartbeat instead patches the mirrored
/// timestamps in place. Reads go through <see cref="FromJson"/> / <see cref="ToRegistration"/>.
/// </summary>
[JsonSchemaTypeGenerator("Schemas/RunnerDocument.json")]
public readonly partial struct RunnerDocument
{
    /// <summary>Gets the runner id.</summary>
    public string IdValue => (string)this.Id;

    /// <summary>Parses a runner document from its persisted JSON as a detached value (one owned copy).</summary>
    /// <param name="utf8">The UTF-8 JSON document.</param>
    /// <returns>The document.</returns>
    public static RunnerDocument FromJson(ReadOnlyMemory<byte> utf8) => ParseValue(utf8.Span);

    /// <summary>Streams the Cosmos envelope for a registration straight to a stream (no materialized document).</summary>
    /// <param name="runnerId">The runner id.</param>
    /// <param name="registration">The registration to embed.</param>
    /// <returns>A readable stream over the envelope JSON, positioned at the start.</returns>
    /// <remarks>A heartbeat does not use this — it patches the two mirrored timestamps in place (no read, no rewrite).</remarks>
    public static Stream WriteEnvelopeStream(string runnerId, RunnerRegistration registration)
    {
        // Serialize the registration JSON once into a pooled buffer, then write the envelope — id, queryable
        // last-seen, the registration as a raw nested value, and the loaded-version projection — into a pooled
        // stream, embedding the same bytes verbatim. No owned byte[], no nested writer rent.
        using CosmosJson.RentedJson docJson = CosmosJson.RentJson(
            registration,
            static (Utf8JsonWriter docWriter, in RunnerRegistration r) => r.WriteTo(docWriter));

        return CosmosJson.WriteToStream(
            (RunnerId: runnerId, LastSeenAt: registration.LastSeenAtValue, Doc: docJson, Registration: registration),
            static (Utf8JsonWriter writer, in (string RunnerId, DateTimeOffset LastSeenAt, CosmosJson.RentedJson Doc, RunnerRegistration Registration) c) =>
            {
                writer.WriteStartObject();
                writer.WriteString(JsonPropertyNames.IdUtf8, c.RunnerId);
                writer.WriteNumber(JsonPropertyNames.LastSeenAtUtf8, c.LastSeenAt.ToUnixTimeMilliseconds());

                // The registration is itself JSON — embed it verbatim as a nested value, not base64 (which would be a
                // spurious encode here + decode on read). It is valid JSON we produced, so skip validation.
                writer.WritePropertyName(JsonPropertyNames.DocUtf8);
                writer.WriteRawValue(c.Doc.Span, skipInputValidation: true);

                writer.WriteStartArray(JsonPropertyNames.LoadedVersionsUtf8);
                foreach (RunnerRegistration.RunnerHostedVersion hosted in c.Registration.HostedVersions.EnumerateArray())
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
            });
    }
}